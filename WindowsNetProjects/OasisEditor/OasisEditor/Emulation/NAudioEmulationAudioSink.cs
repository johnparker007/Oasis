using System.Diagnostics;
using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;

namespace OasisEditor;

public sealed class NAudioEmulationAudioSink : IEmulationAudioSink
{
    private readonly int _bufferLengthMilliseconds;
    private readonly object _gate = new();
    private PcmFrameRingBuffer? _ringBuffer;
    private EmulationPcmWaveProvider? _provider;
    private WasapiOut? _output;
    private EmulationAudioFormat? _format;
    private AudioPrebufferPolicy? _prebuffer;
    private long _framesOffered;
    private long _framesWritten;
    private long _framesRejected;
    private bool _overflowWarned;

    internal int BufferLengthMilliseconds => _bufferLengthMilliseconds;
    internal int PrebufferThresholdFrames => _prebuffer?.ThresholdFrames ?? 0;
    internal int CapacityFrames => _ringBuffer?.CapacityFrames ?? 0;
    internal long FramesOffered => Interlocked.Read(ref _framesOffered);
    internal long FramesWritten => Interlocked.Read(ref _framesWritten);
    internal long FramesRejected => Interlocked.Read(ref _framesRejected);
    internal long FramesDelivered => _provider?.FramesDelivered ?? 0;
    internal long SilenceFrames => _provider?.SilenceFrames ?? 0;
    internal long UnderrunEpisodes => _provider?.UnderrunEpisodes ?? 0;
    internal bool PlaybackStarted => _prebuffer?.PlaybackStarted ?? false;

    public NAudioEmulationAudioSink(int bufferLengthMilliseconds = NativeEmulationPreferences.DefaultAudioBufferLengthMilliseconds)
    {
        if (bufferLengthMilliseconds <= 0) throw new ArgumentOutOfRangeException(nameof(bufferLengthMilliseconds));
        _bufferLengthMilliseconds = bufferLengthMilliseconds;
    }

    public void Start(EmulationAudioFormat format)
    {
        ValidateFormat(format);
        Stop();
        var capacityFrames = AudioPrebufferPolicy.CalculateCapacityFrames(format.SampleRate, _bufferLengthMilliseconds);
        var ringBuffer = new PcmFrameRingBuffer(format.Channels, capacityFrames);
        var provider = new EmulationPcmWaveProvider(ringBuffer, format, capacityFrames);
        var output = new WasapiOut(AudioClientShareMode.Shared, _bufferLengthMilliseconds);
        output.Init(provider);
        lock (_gate)
        {
            _format = format;
            _ringBuffer = ringBuffer;
            _provider = provider;
            _output = output;
            _prebuffer = new AudioPrebufferPolicy(format, capacityFrames);
            _overflowWarned = false;
            ResetCounters();
        }
        Debug.WriteLine($"NAudio emulation ring: format={format.SampleRate}Hz/{format.Channels}ch/{format.BitsPerSample}bit capacityFrames={capacityFrames} thresholdFrames={_prebuffer.ThresholdFrames}.");
    }

    public void PushPcm(ReadOnlySpan<byte> pcmBytes)
    {
        if (pcmBytes.IsEmpty) return;
        var format = _format ?? throw new InvalidOperationException("Audio sink has not been started.");
        var bytesPerFrame = checked(format.Channels * sizeof(short));
        var frameCount = pcmBytes.Length / bytesPerFrame;
        if (frameCount == 0) return;
        var samples = MemoryMarshal.Cast<byte, short>(pcmBytes[..checked(frameCount * bytesPerFrame)]);
        var written = _ringBuffer?.Write(samples, frameCount) ?? 0;
        Interlocked.Add(ref _framesOffered, frameCount);
        Interlocked.Add(ref _framesWritten, written);
        var rejected = frameCount - written;
        if (rejected > 0)
        {
            Interlocked.Add(ref _framesRejected, rejected);
            if (!_overflowWarned)
            {
                _overflowWarned = true;
                Debug.WriteLine($"NAudio emulation ring: first overflow; rejectedFrames={rejected} totalRejectedFrames={FramesRejected}.");
            }
        }
        var prebuffer = _prebuffer;
        if (prebuffer is not null && !prebuffer.PlaybackStarted && prebuffer.ObserveQueuedFrames(_ringBuffer!.ReadableFrames))
            _output?.Play();
    }

    public void Clear()
    {
        _output?.Stop();
        _ringBuffer?.Clear();
        _prebuffer?.Reset();
    }

    public void Stop()
    {
        _output?.Stop();
        _output?.Dispose();
        _output = null;
        _ringBuffer?.Clear();
        _ringBuffer = null;
        _provider = null;
        _format = null;
        _prebuffer = null;
        Debug.WriteLine($"NAudio emulation ring: stop summary; offered={FramesOffered}, written={FramesWritten}, rejected={FramesRejected}, delivered={FramesDelivered}, silence={SilenceFrames}, underruns={UnderrunEpisodes}.");
    }

    public void Dispose() => Stop();
    internal static bool ShouldDropIncomingBlock(int bufferedBytes, int bufferCapacityBytes, int incomingBytes) => incomingBytes > bufferCapacityBytes - bufferedBytes;
    private void ResetCounters() { Interlocked.Exchange(ref _framesOffered, 0); Interlocked.Exchange(ref _framesWritten, 0); Interlocked.Exchange(ref _framesRejected, 0); }
    private static void ValidateFormat(EmulationAudioFormat format) { if (format.SampleRate <= 0) throw new ArgumentOutOfRangeException(nameof(format)); if (format.Channels <= 0) throw new ArgumentOutOfRangeException(nameof(format)); if (format.BitsPerSample != 16) throw new NotSupportedException("Only 16-bit PCM audio is supported."); }
}

internal sealed class AudioPrebufferPolicy
{
    internal AudioPrebufferPolicy(EmulationAudioFormat format, int capacityFrames) => ThresholdFrames = CalculateThresholdFrames(format.SampleRate, capacityFrames);
    internal int ThresholdFrames { get; }
    internal bool PlaybackStarted { get; private set; }
    internal bool ObserveQueuedFrames(int queuedFrames) { if (!PlaybackStarted && queuedFrames >= ThresholdFrames) PlaybackStarted = true; return PlaybackStarted; }
    internal void Reset() => PlaybackStarted = false;
    internal static int CalculateCapacityFrames(int sampleRate, int bufferLengthMilliseconds) => checked((int)(((long)sampleRate * bufferLengthMilliseconds + 999) / 1000));
    internal static int CalculateThresholdFrames(int sampleRate, int capacityFrames)
    {
        if (sampleRate <= 0) throw new ArgumentOutOfRangeException(nameof(sampleRate));
        if (capacityFrames <= 1) return 1;
        var seventyFivePercent = checked((capacityFrames * 3 + 3) / 4);
        var minimumTwentyMs = CalculateCapacityFrames(sampleRate, 20);
        var threshold = capacityFrames > minimumTwentyMs ? Math.Max(seventyFivePercent, minimumTwentyMs) : seventyFivePercent;
        return Math.Min(capacityFrames - 1, Math.Max(1, threshold));
    }
}

internal static class EmulationAudioFormatExtensions
{
    internal static int BytesPerMillisecond(this EmulationAudioFormat format) => Math.Max(1, checked((format.SampleRate * format.Channels * (format.BitsPerSample / 8)) / 1000));
}
