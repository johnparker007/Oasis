using System.Diagnostics;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace OasisEditor;

public sealed class NAudioEmulationAudioSink : IEmulationAudioSink
{
    private const int DiagnosticPushLimit = 32;
    private readonly int _bufferLengthMilliseconds;

    private BufferedWaveProvider? _buffer;
    private WasapiOut? _output;
    private EmulationAudioFormat? _format;
    private AudioPrebufferPolicy? _prebuffer;
    private long _droppedBlocks;
    private long _droppedBytes;
    private long _runtimeStarvationEvents;
    private long _pushCount;
    private int _minimumBufferedBytes = int.MaxValue;
    private bool _playbackStarted;

    internal int BufferLengthMilliseconds => _bufferLengthMilliseconds;
    internal int PrebufferThresholdMilliseconds => AudioPrebufferPolicy.CalculateThresholdMilliseconds(_bufferLengthMilliseconds);
    internal int PrebufferThresholdBytes => _prebuffer?.ThresholdBytes ?? 0;
    internal long DroppedBlocks => Interlocked.Read(ref _droppedBlocks);
    internal long DroppedBytes => Interlocked.Read(ref _droppedBytes);
    internal long RuntimeStarvationEvents => Interlocked.Read(ref _runtimeStarvationEvents);
    internal int MinimumObservedBufferedBytes => _minimumBufferedBytes == int.MaxValue ? 0 : _minimumBufferedBytes;
    internal bool PlaybackStarted => _playbackStarted;

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
        _buffer = new BufferedWaveProvider(waveFormat)
        {
            BufferDuration = TimeSpan.FromMilliseconds(_bufferLengthMilliseconds),
            DiscardOnBufferOverflow = true
        };
        _output = new WasapiOut(AudioClientShareMode.Shared, _bufferLengthMilliseconds);
        _output.Init(_buffer);
        _format = format;
        _prebuffer = new AudioPrebufferPolicy(format, _bufferLengthMilliseconds);
        ResetDiagnostics();
        // Play is deliberately deferred until enough PCM (including digital silence) is queued.
    }

    public void PushPcm(ReadOnlySpan<byte> pcmBytes)
    {
        if (pcmBytes.IsEmpty || _buffer is null)
            return;

        var format = _format ?? throw new InvalidOperationException("Audio sink has not been started.");
        var before = _buffer.BufferedBytes;
        var capacity = _buffer.BufferLength;
        var push = Interlocked.Increment(ref _pushCount);
        ObserveMinimum(before);

        if (_playbackStarted && before < format.BytesPerMillisecond())
        {
            var starvation = Interlocked.Increment(ref _runtimeStarvationEvents);
            if (starvation == 1 || IsPowerOfTwo(starvation))
                WriteDepthDiagnostic("runtime starvation risk", pcmBytes.Length, before, before, capacity);
        }

        if (ShouldDropIncomingBlock(before, capacity, pcmBytes.Length))
        {
            var droppedBlocks = Interlocked.Increment(ref _droppedBlocks);
            Interlocked.Add(ref _droppedBytes, pcmBytes.Length);
            if (droppedBlocks == 1 || IsPowerOfTwo(droppedBlocks))
                WriteDepthDiagnostic("dropped incoming block", pcmBytes.Length, before, before, capacity);
            return;
        }

        var bytes = pcmBytes.ToArray();
        _buffer.AddSamples(bytes, 0, bytes.Length);
        var after = _buffer.BufferedBytes;
        if (push <= DiagnosticPushLimit)
            WriteDepthDiagnostic("push", bytes.Length, before, after, capacity);

        if (!_playbackStarted && _prebuffer!.ObserveQueuedBytes(after))
        {
            _output!.Play();
            _playbackStarted = true;
            Debug.WriteLine($"NAudio emulation buffer: startup prebuffer complete; thresholdBytes={_prebuffer.ThresholdBytes}, bufferedBytes={after}.");
        }
    }

    public void Clear()
    {
        if (_output is not null && _playbackStarted)
            _output.Stop();
        _buffer?.ClearBuffer();
        _playbackStarted = false;
        _prebuffer?.Reset();
        _minimumBufferedBytes = int.MaxValue;
    }

    public void Stop()
    {
        if (_format is not null)
            Debug.WriteLine($"NAudio emulation buffer: stop summary; pushes={_pushCount}, droppedBlocks={DroppedBlocks}, droppedBytes={DroppedBytes}, starvationRiskEvents={RuntimeStarvationEvents}, minimumBufferedBytes={MinimumObservedBufferedBytes}.");
        _output?.Stop();
        _output?.Dispose();
        _output = null;
        _buffer = null;
        _format = null;
        _prebuffer = null;
        _playbackStarted = false;
    }

    public void Dispose() => Stop();

    internal static bool ShouldDropIncomingBlock(int bufferedBytes, int bufferCapacityBytes, int incomingBytes)
        => incomingBytes > bufferCapacityBytes - bufferedBytes;

    private void ResetDiagnostics()
    {
        Interlocked.Exchange(ref _droppedBlocks, 0);
        Interlocked.Exchange(ref _droppedBytes, 0);
        Interlocked.Exchange(ref _runtimeStarvationEvents, 0);
        Interlocked.Exchange(ref _pushCount, 0);
        _minimumBufferedBytes = int.MaxValue;
        _playbackStarted = false;
    }

    private void ObserveMinimum(int bufferedBytes) => _minimumBufferedBytes = Math.Min(_minimumBufferedBytes, bufferedBytes);

    private void WriteDepthDiagnostic(string reason, int incoming, int before, int after, int capacity)
    {
        var bytesPerMillisecond = _format!.Value.BytesPerMillisecond();
        Debug.WriteLine($"NAudio emulation buffer: {reason}; incomingBytes={incoming}, bufferedBefore={before}, bufferedAfter={after}, capacity={capacity}, millisecondsBefore={(double)before / bytesPerMillisecond:F2}, millisecondsAfter={(double)after / bytesPerMillisecond:F2}, droppedBlocks={DroppedBlocks}, droppedBytes={DroppedBytes}, minimumBufferedBytes={MinimumObservedBufferedBytes}.");
    }

    private static bool IsPowerOfTwo(long value) => (value & (value - 1)) == 0;

    private static void ValidateFormat(EmulationAudioFormat format)
    {
        if (format.SampleRate <= 0) throw new ArgumentOutOfRangeException(nameof(format), "Audio sample rate must be greater than zero.");
        if (format.Channels <= 0) throw new ArgumentOutOfRangeException(nameof(format), "Audio channel count must be greater than zero.");
        if (format.BitsPerSample != 16) throw new NotSupportedException($"Only 16-bit PCM audio is supported; native core reported {format.BitsPerSample} bits per sample.");
    }
}

internal sealed class AudioPrebufferPolicy
{
    internal AudioPrebufferPolicy(EmulationAudioFormat format, int bufferLengthMilliseconds)
    {
        ThresholdMilliseconds = CalculateThresholdMilliseconds(bufferLengthMilliseconds);
        var numerator = checked((long)format.SampleRate * format.Channels * (format.BitsPerSample / 8) * ThresholdMilliseconds);
        ThresholdBytes = checked((int)((numerator + 999) / 1000));
    }

    internal int ThresholdMilliseconds { get; }
    internal int ThresholdBytes { get; }
    internal bool PlaybackStarted { get; private set; }

    internal bool ObserveQueuedBytes(int bufferedBytes)
    {
        if (!PlaybackStarted && bufferedBytes >= ThresholdBytes)
            PlaybackStarted = true;
        return PlaybackStarted;
    }

    internal void Reset() => PlaybackStarted = false;

    internal static int CalculateThresholdMilliseconds(int bufferLengthMilliseconds) =>
        Math.Max(1, Math.Min(10, bufferLengthMilliseconds / 2));
}

internal static class EmulationAudioFormatExtensions
{
    internal static int BytesPerMillisecond(this EmulationAudioFormat format) =>
        Math.Max(1, checked((format.SampleRate * format.Channels * (format.BitsPerSample / 8)) / 1000));
}
