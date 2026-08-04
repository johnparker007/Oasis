using System.Diagnostics;
using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace OasisEditor;

public sealed class NAudioEmulationAudioSink : IEmulationAudioSink, IEmulationAudioDiagnosticSink
{
    private readonly int _bufferLengthMilliseconds;
    private readonly EmulationAudioOutputBackend _outputBackend;
    private readonly object _gate = new();
    private readonly AutoResetEvent _feederWake = new(false);

    private BufferedWaveProvider? _buffer;
    private IWavePlayer? _output;
    private EmulationAudioFormat? _format;
    private EmulationAudioReservePolicy _reservePolicy;
    private EmulationPcmFrameFifo? _fifo;
    private Thread? _feederThread;
    private EmulationAudioDiagnostics? _audioDiagnostics;
    private byte[] _feedBytes = [];
    private short[] _feedSamples = [];
    private bool _stopFeeder;
    private bool _playbackStarted;
    private long _acceptedStartFrame;
    private long _fifoRejectedFrames;
    private long _feederWakeups;
    private long _feederUnderruns;
    private long _lowWaterEvents;
    private long _zeroDepthEvents;
    private long _minimumReserveFrames = long.MaxValue;
    private long _playbackStartReserveFrames;

    public NAudioEmulationAudioSink(
        int bufferLengthMilliseconds = NativeEmulationPreferences.DefaultAudioBufferLengthMilliseconds,
        EmulationAudioOutputBackend outputBackend = EmulationAudioOutputBackend.WasapiOut)
    {
        if (bufferLengthMilliseconds <= 0)
            throw new ArgumentOutOfRangeException(nameof(bufferLengthMilliseconds), bufferLengthMilliseconds, "Audio buffer length must be greater than zero.");
        _bufferLengthMilliseconds = bufferLengthMilliseconds;
        _outputBackend = outputBackend;
    }

    internal int BufferLengthMilliseconds => _bufferLengthMilliseconds;
    internal EmulationAudioOutputBackend OutputBackend => _outputBackend;
    internal int StartupTargetMilliseconds => _format is { } format ? FramesToMilliseconds(_reservePolicy.TargetFrames, format.SampleRate) : 0;
    internal long FeederWakeups => Interlocked.Read(ref _feederWakeups);
    internal long FeederUnderruns => Interlocked.Read(ref _feederUnderruns);
    internal long LowWaterEvents => Interlocked.Read(ref _lowWaterEvents);
    internal long ZeroDepthEvents => Interlocked.Read(ref _zeroDepthEvents);
    internal long FifoRejectedFrames => Interlocked.Read(ref _fifoRejectedFrames);
    internal long MinimumReserveFrames => Interlocked.Read(ref _minimumReserveFrames) == long.MaxValue ? 0 : Interlocked.Read(ref _minimumReserveFrames);
    internal bool PlaybackStarted => _playbackStarted;

    public void Start(EmulationAudioFormat format)
    {
        ValidateFormat(format);
        Stop();

        _reservePolicy = EmulationAudioReservePolicy.Create(format, _bufferLengthMilliseconds);
        _fifo = new EmulationPcmFrameFifo(_reservePolicy.CapacityFrames, format.Channels);
        var waveFormat = new WaveFormat(format.SampleRate, format.BitsPerSample, format.Channels);
        _buffer = new BufferedWaveProvider(waveFormat)
        {
            BufferDuration = TimeSpan.FromMilliseconds(_bufferLengthMilliseconds),
            DiscardOnBufferOverflow = false
        };
        _output = CreateOutputDevice();
        _output.Init(_buffer);
        _format = format;
        ResetDiagnostics();
        _feedSamples = new short[checked(_reservePolicy.FeedBlockFrames * format.Channels)];
        _feedBytes = new byte[checked(_feedSamples.Length * sizeof(short))];
        _stopFeeder = false;
        _feederThread = new Thread(FeederLoop)
        {
            IsBackground = true,
            Name = "Oasis Amber audio feeder",
            Priority = ThreadPriority.Normal
        };
        _feederThread.Start();
    }

    public EmulationAudioPushResult PushPcm(ReadOnlySpan<byte> pcmBytes, EmulationAudioPushContext context = default)
    {
        if (pcmBytes.IsEmpty)
            return new(0, 0, 0, null);
        var format = _format ?? throw new InvalidOperationException("Audio sink has not been started.");
        var fifo = _fifo ?? throw new InvalidOperationException("Audio FIFO has not been started.");
        var bytesPerFrame = checked(format.Channels * sizeof(short));
        if (pcmBytes.Length % bytesPerFrame != 0)
            throw new ArgumentException("PCM byte count must align to complete frames.", nameof(pcmBytes));
        var offeredFrames = pcmBytes.Length / bytesPerFrame;
        var samples = MemoryMarshal.Cast<byte, short>(pcmBytes);
        var result = fifo.Write(samples);
        var acceptedBytes = checked(result.AcceptedFrames * bytesPerFrame);
        var droppedBytes = checked(result.RejectedFrames * bytesPerFrame);
        if (result.Rejected)
        {
            Interlocked.Add(ref _fifoRejectedFrames, result.RejectedFrames);
            _audioDiagnostics?.RecordSinkDrop(context, droppedBytes, "Application audio FIFO full");
        }
        _feederWake.Set();
        RecordDepthTimeline(context, offeredFrames, false, false);
        return new(pcmBytes.Length, acceptedBytes, droppedBytes, result.Rejected ? "Application audio FIFO full" : null);
    }

    public void Clear()
    {
        lock (_gate)
        {
            _fifo?.Clear();
            _buffer?.ClearBuffer();
            _playbackStarted = false;
            Interlocked.Exchange(ref _acceptedStartFrame, 0);
            Interlocked.Exchange(ref _minimumReserveFrames, long.MaxValue);
        }
        _feederWake.Set();
    }

    public void Stop()
    {
        Thread? feeder;
        lock (_gate)
        {
            _stopFeeder = true;
            feeder = _feederThread;
        }
        _feederWake.Set();
        if (feeder is not null && feeder.IsAlive)
            feeder.Join(TimeSpan.FromSeconds(2));

        lock (_gate)
        {
            _output?.Stop();
            _output?.Dispose();
            _output = null;
            _buffer = null;
            _fifo = null;
            _format = null;
            _feederThread = null;
            _playbackStarted = false;
        }
    }

    public void Dispose()
    {
        Stop();
        _feederWake.Dispose();
    }

    void IEmulationAudioDiagnosticSink.ConfigureDiagnostics(EmulationAudioDiagnostics? diagnostics) => _audioDiagnostics = diagnostics;

    private IWavePlayer CreateOutputDevice() => _outputBackend switch
    {
        EmulationAudioOutputBackend.WaveOutEvent => new WaveOutEvent { DesiredLatency = _bufferLengthMilliseconds },
        _ => new WasapiOut(AudioClientShareMode.Shared, _bufferLengthMilliseconds)
    };

    private void FeederLoop()
    {
        while (true)
        {
            if (Volatile.Read(ref _stopFeeder)) return;
            var fedAny = FeedAvailablePcm();
            if (!fedAny)
            {
                Interlocked.Increment(ref _feederUnderruns);
                RecordDepthTimeline(default, 0, false, true);
                _feederWake.WaitOne(5);
            }
            else
            {
                Interlocked.Increment(ref _feederWakeups);
                _feederWake.WaitOne(1);
            }
        }
    }

    private bool FeedAvailablePcm()
    {
        var format = _format;
        var buffer = _buffer;
        var fifo = _fifo;
        if (format is null || buffer is null || fifo is null)
            return false;

        var fedAny = false;
        while (!Volatile.Read(ref _stopFeeder))
        {
            var providerFreeBytes = buffer.BufferLength - buffer.BufferedBytes;
            var bytesPerFrame = format.Value.Channels * sizeof(short);
            var providerFreeFrames = providerFreeBytes / bytesPerFrame;
            if (providerFreeFrames <= 0)
                break;

            var framesToRead = Math.Min(_reservePolicy.FeedBlockFrames, providerFreeFrames);
            var framesRead = fifo.Read(_feedSamples, framesToRead);
            if (framesRead <= 0)
                break;

            var samplesRead = checked(framesRead * format.Value.Channels);
            Buffer.BlockCopy(_feedSamples, 0, _feedBytes, 0, checked(samplesRead * sizeof(short)));
            buffer.AddSamples(_feedBytes, 0, checked(samplesRead * sizeof(short)));
            fedAny = true;

            var startFrame = Interlocked.Read(ref _acceptedStartFrame);
            _audioDiagnostics?.Capture(EmulationAudioCaptureBoundary.NAudioAccepted, format.Value, 0, startFrame, _feedSamples.AsSpan(0, samplesRead), framesRead);
            Interlocked.Add(ref _acceptedStartFrame, framesRead);
            MaybeStartPlayback(format.Value, fifo, buffer);
            RecordDepthTimeline(default, framesRead, false, false);
        }
        return fedAny;
    }

    private void MaybeStartPlayback(EmulationAudioFormat format, EmulationPcmFrameFifo fifo, BufferedWaveProvider buffer)
    {
        if (_playbackStarted || _output is null)
            return;
        var reserveFrames = CombinedReserveFrames(format, fifo, buffer);
        if (reserveFrames < _reservePolicy.TargetFrames)
            return;
        _playbackStartReserveFrames = reserveFrames;
        ObserveMinimumReserve(reserveFrames);
        _output.Play();
        _playbackStarted = true;
    }

    private void RecordDepthTimeline(EmulationAudioPushContext context, int incomingFrames, bool droppedBlock, bool feederUnderrun)
    {
        var format = _format;
        var fifo = _fifo;
        var buffer = _buffer;
        if (format is null || fifo is null || buffer is null)
            return;
        var fifoFrames = fifo.QueuedFrames;
        var nAudioFrames = buffer.BufferedBytes / checked(format.Value.Channels * sizeof(short));
        var reserveFrames = fifoFrames + nAudioFrames;
        if (_playbackStarted)
        {
            ObserveMinimumReserve(reserveFrames);
            if (reserveFrames == 0)
                Interlocked.Increment(ref _zeroDepthEvents);
            if (reserveFrames < _reservePolicy.LowWaterFrames)
                Interlocked.Increment(ref _lowWaterEvents);
        }
        _audioDiagnostics?.RecordTimeline(new(
            incomingFrames,
            buffer.BufferedBytes,
            buffer.BufferedBytes,
            buffer.BufferLength,
            _playbackStarted,
            droppedBlock,
            FifoRejectedFrames,
            context.AdvanceLatenessTicks,
            context.ZeroFrameRead), fifoFrames, FramesToMilliseconds(fifoFrames, format.Value.SampleRate), nAudioFrames,
            FramesToMilliseconds(nAudioFrames, format.Value.SampleRate), reserveFrames, FramesToMilliseconds(reserveFrames, format.Value.SampleRate),
            LowWaterEvents, ZeroDepthEvents, FeederWakeups, FeederUnderruns, _playbackStartReserveFrames,
            MinimumReserveFrames == 0 ? 0 : FramesToMilliseconds((int)Math.Min(int.MaxValue, MinimumReserveFrames), format.Value.SampleRate));
    }

    private int CombinedReserveFrames(EmulationAudioFormat format, EmulationPcmFrameFifo fifo, BufferedWaveProvider buffer) =>
        fifo.QueuedFrames + buffer.BufferedBytes / checked(format.Channels * sizeof(short));

    private void ObserveMinimumReserve(long reserveFrames)
    {
        long current;
        do
        {
            current = Interlocked.Read(ref _minimumReserveFrames);
            if (reserveFrames >= current) return;
        } while (Interlocked.CompareExchange(ref _minimumReserveFrames, reserveFrames, current) != current);
    }

    private void ResetDiagnostics()
    {
        Interlocked.Exchange(ref _acceptedStartFrame, 0);
        Interlocked.Exchange(ref _fifoRejectedFrames, 0);
        Interlocked.Exchange(ref _feederWakeups, 0);
        Interlocked.Exchange(ref _feederUnderruns, 0);
        Interlocked.Exchange(ref _lowWaterEvents, 0);
        Interlocked.Exchange(ref _zeroDepthEvents, 0);
        Interlocked.Exchange(ref _minimumReserveFrames, long.MaxValue);
        _playbackStartReserveFrames = 0;
        _playbackStarted = false;
    }

    private static int FramesToMilliseconds(long frames, int sampleRate) =>
        sampleRate <= 0 ? 0 : checked((int)Math.Min(int.MaxValue, (frames * 1000 + sampleRate - 1) / sampleRate));

    private static void ValidateFormat(EmulationAudioFormat format)
    {
        if (format.SampleRate <= 0) throw new ArgumentOutOfRangeException(nameof(format), "Audio sample rate must be greater than zero.");
        if (format.Channels <= 0) throw new ArgumentOutOfRangeException(nameof(format), "Audio channel count must be greater than zero.");
        if (format.BitsPerSample != 16) throw new NotSupportedException($"Only 16-bit PCM audio is supported; native core reported {format.BitsPerSample} bits per sample.");
    }
}
