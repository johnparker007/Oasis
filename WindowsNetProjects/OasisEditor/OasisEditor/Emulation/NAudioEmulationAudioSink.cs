using System.Diagnostics;
using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace OasisEditor;

public sealed class NAudioEmulationAudioSink : IEmulationAudioSink
{
    private readonly object _lifecycleGate = new();
    private readonly int _bufferLengthMilliseconds;
    private PlaybackState? _state;
    private long _ringFramesWritten;
    private long _ringFramesRejected;
    private long _firstOverflowWarningWritten;

    internal int BufferLengthMilliseconds => _bufferLengthMilliseconds;
    internal int PrebufferThresholdMilliseconds => AudioPrebufferPolicy.CalculateThresholdMilliseconds(_bufferLengthMilliseconds);
    internal int PrebufferThresholdFrames => _state?.Prebuffer.ThresholdFrames ?? 0;
    internal long RingFramesWritten => Interlocked.Read(ref _ringFramesWritten);
    internal long RingFramesRejected => Interlocked.Read(ref _ringFramesRejected);
    internal long DeviceFramesDelivered => _state?.Provider.FramesDelivered ?? 0;
    internal long SilenceFrames => _state?.Provider.SilenceFrames ?? 0;
    internal long UnderrunEpisodes => _state?.Provider.UnderrunEpisodes ?? 0;
    internal int MinimumObservedRingFrames => _state?.MinimumObservedRingFramesValue ?? 0;
    internal bool PlaybackStarted => _state?.Prebuffer.PlaybackStarted ?? false;

    public NAudioEmulationAudioSink(int bufferLengthMilliseconds = NativeEmulationPreferences.DefaultAudioBufferLengthMilliseconds)
    {
        if (bufferLengthMilliseconds <= 0)
            throw new ArgumentOutOfRangeException(nameof(bufferLengthMilliseconds), bufferLengthMilliseconds, "Audio buffer length must be greater than zero.");
        _bufferLengthMilliseconds = bufferLengthMilliseconds;
    }

    public void Start(EmulationAudioFormat format)
    {
        ValidateFormat(format);
        Stop();
        var waveFormat = new WaveFormat(format.SampleRate, format.BitsPerSample, format.Channels);
        var capacityFrames = AudioPrebufferPolicy.CalculateCapacityFrames(format, _bufferLengthMilliseconds);
        var ring = new PcmFrameRingBuffer(format.Channels, capacityFrames);
        var provider = new PcmRingWaveProvider(ring, waveFormat);
        var output = new WasapiOut(AudioClientShareMode.Shared, _bufferLengthMilliseconds);
        output.Init(provider);
        var state = new PlaybackState(format, ring, provider, output, new AudioPrebufferPolicy(format, _bufferLengthMilliseconds));
        ResetDiagnostics();
        lock (_lifecycleGate)
            _state = state;
        Debug.WriteLine($"NAudio emulation buffer: started; capacityFrames={capacityFrames}, prebufferFrames={state.Prebuffer.ThresholdFrames}.");
    }

    public void PushPcm(ReadOnlySpan<byte> pcmBytes)
    {
        var state = Volatile.Read(ref _state);
        if (state is null || state.IsStopping || pcmBytes.IsEmpty)
            return;
        var samples = MemoryMarshal.Cast<byte, short>(pcmBytes);
        var frameCount = samples.Length / state.Format.Channels;
        if (frameCount <= 0) return;
        state.ObserveDepthBeforeWrite();
        var written = state.Ring.Write(samples[..(frameCount * state.Format.Channels)], frameCount);
        Interlocked.Add(ref _ringFramesWritten, written);
        var rejected = frameCount - written;
        if (rejected > 0)
        {
            Interlocked.Add(ref _ringFramesRejected, rejected);
            if (Interlocked.Exchange(ref _firstOverflowWarningWritten, 1) == 0)
                Debug.WriteLine($"NAudio emulation buffer: ring overflow; rejectedFrames={rejected}.");
        }
        if (!state.Prebuffer.PlaybackStarted && state.Prebuffer.ObserveQueuedFrames(state.Ring.ReadableFrames))
            state.PlayOnce();
    }

    public void Clear()
    {
        PlaybackState? state;
        lock (_lifecycleGate) state = _state;
        state?.ClearForFreshPrebuffer();
    }

    public void Stop()
    {
        PlaybackState? state;
        lock (_lifecycleGate)
        {
            state = _state;
            if (state is not null) state.IsStopping = true;
            _state = null;
        }
        if (state is null) return;
        Debug.WriteLine($"NAudio emulation buffer: stop summary; ringFramesWritten={RingFramesWritten}, ringFramesRejected={RingFramesRejected}, deviceFramesDelivered={state.Provider.FramesDelivered}, silenceFrames={state.Provider.SilenceFrames}, underrunEpisodes={state.Provider.UnderrunEpisodes}, minimumRingFrames={state.MinimumObservedRingFramesValue}.");
        state.StopDisposeAndClear();
    }

    public void Dispose() => Stop();

    internal static bool ShouldDropIncomingBlock(int bufferedBytes, int bufferCapacityBytes, int incomingBytes)
        => incomingBytes > bufferCapacityBytes - bufferedBytes;

    private void ResetDiagnostics()
    {
        Interlocked.Exchange(ref _ringFramesWritten, 0);
        Interlocked.Exchange(ref _ringFramesRejected, 0);
        Interlocked.Exchange(ref _firstOverflowWarningWritten, 0);
    }

    private static void ValidateFormat(EmulationAudioFormat format)
    {
        if (format.SampleRate <= 0) throw new ArgumentOutOfRangeException(nameof(format), "Audio sample rate must be greater than zero.");
        if (format.Channels <= 0) throw new ArgumentOutOfRangeException(nameof(format), "Audio channel count must be greater than zero.");
        if (format.BitsPerSample != 16) throw new NotSupportedException($"Only 16-bit PCM audio is supported; native core reported {format.BitsPerSample} bits per sample.");
    }

    private sealed class PlaybackState
    {
        private int _playStarted;
        private int _minimumObservedRingFrames = int.MaxValue;
        public PlaybackState(EmulationAudioFormat format, PcmFrameRingBuffer ring, PcmRingWaveProvider provider, WasapiOut output, AudioPrebufferPolicy prebuffer)
        { Format = format; Ring = ring; Provider = provider; Output = output; Prebuffer = prebuffer; }
        public EmulationAudioFormat Format { get; }
        public PcmFrameRingBuffer Ring { get; }
        public PcmRingWaveProvider Provider { get; }
        public WasapiOut Output { get; }
        public AudioPrebufferPolicy Prebuffer { get; }
        public volatile bool IsStopping;
        public int MinimumObservedRingFramesValue => _minimumObservedRingFrames == int.MaxValue ? 0 : _minimumObservedRingFrames;
        public void ObserveDepthBeforeWrite() => _minimumObservedRingFrames = Math.Min(_minimumObservedRingFrames, Ring.ReadableFrames);
        public void PlayOnce()
        {
            if (IsStopping || Interlocked.Exchange(ref _playStarted, 1) != 0) return;
            Output.Play();
        }
        public void ClearForFreshPrebuffer()
        {
            IsStopping = false;
            Output.Stop();
            Ring.Clear();
            Prebuffer.Reset();
            Interlocked.Exchange(ref _playStarted, 0);
            _minimumObservedRingFrames = int.MaxValue;
        }
        public void StopDisposeAndClear()
        {
            try { Output.Stop(); } catch { }
            try { Output.Dispose(); } catch { }
            Ring.Clear();
        }
    }
}

internal sealed class AudioPrebufferPolicy
{
    internal AudioPrebufferPolicy(EmulationAudioFormat format, int bufferLengthMilliseconds)
    {
        ThresholdMilliseconds = CalculateThresholdMilliseconds(bufferLengthMilliseconds);
        BytesPerFrame = checked(format.Channels * (format.BitsPerSample / 8));
        ThresholdFrames = Math.Max(1, checked((int)(((long)format.SampleRate * ThresholdMilliseconds + 999) / 1000)));
        ThresholdBytes = checked(ThresholdFrames * BytesPerFrame);
    }
    internal int ThresholdMilliseconds { get; }
    internal int ThresholdFrames { get; }
    internal int ThresholdBytes { get; }
    private int BytesPerFrame { get; }
    internal bool PlaybackStarted { get; private set; }
    internal bool ObserveQueuedFrames(int queuedFrames) { if (!PlaybackStarted && queuedFrames >= ThresholdFrames) PlaybackStarted = true; return PlaybackStarted; }
    internal bool ObserveQueuedBytes(int bufferedBytes) => ObserveQueuedFrames(bufferedBytes / BytesPerFrame);
    internal void Reset() => PlaybackStarted = false;
    internal static int CalculateThresholdMilliseconds(int bufferLengthMilliseconds) => Math.Max(1, (bufferLengthMilliseconds * 3 + 3) / 4);
    internal static int CalculateCapacityFrames(EmulationAudioFormat format, int bufferLengthMilliseconds) => Math.Max(1, checked((int)(((long)format.SampleRate * bufferLengthMilliseconds + 999) / 1000)));
}

internal static class EmulationAudioFormatExtensions
{
    internal static int BytesPerMillisecond(this EmulationAudioFormat format) =>
        Math.Max(1, checked((format.SampleRate * format.Channels * (format.BitsPerSample / 8)) / 1000));
}
