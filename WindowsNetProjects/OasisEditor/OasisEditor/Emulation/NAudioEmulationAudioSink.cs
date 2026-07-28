using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace OasisEditor;

public sealed class NAudioEmulationAudioSink : IEmulationAudioSink
{
    private readonly int _bufferLengthMilliseconds;

    private BufferedWaveProvider? _buffer;
    private WasapiOut? _output;
    private EmulationAudioFormat? _format;
    private long _droppedBlocks;
    private long _droppedBytes;

    internal long DroppedBlocks => Interlocked.Read(ref _droppedBlocks);
    internal long DroppedBytes => Interlocked.Read(ref _droppedBytes);

    public NAudioEmulationAudioSink(int bufferLengthMilliseconds = NativeEmulationPreferences.DefaultAudioBufferLengthMilliseconds)
    {
        if (bufferLengthMilliseconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bufferLengthMilliseconds), bufferLengthMilliseconds, "Audio buffer length must be greater than zero.");
        }

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
        _output.Play();
        _format = format;
        Interlocked.Exchange(ref _droppedBlocks, 0);
        Interlocked.Exchange(ref _droppedBytes, 0);
    }

    public void PushPcm(ReadOnlySpan<byte> pcmBytes)
    {
        if (pcmBytes.IsEmpty || _buffer is null)
        {
            return;
        }

        _ = _format ?? throw new InvalidOperationException("Audio sink has not been started.");
        if (ShouldDropIncomingBlock(_buffer.BufferedBytes, _buffer.BufferLength, pcmBytes.Length))
        {
            var droppedBlocks = Interlocked.Increment(ref _droppedBlocks);
            Interlocked.Add(ref _droppedBytes, pcmBytes.Length);
            if (droppedBlocks == 1 || (droppedBlocks & (droppedBlocks - 1)) == 0)
                System.Diagnostics.Debug.WriteLine($"NAudio emulation buffer: dropped incoming audio block; blocks={droppedBlocks}, bytes={DroppedBytes}, buffered={_buffer.BufferedBytes}, capacity={_buffer.BufferLength}.");
            return;
        }

        var bytes = pcmBytes.ToArray();
        _buffer.AddSamples(bytes, 0, bytes.Length);
    }

    public void Clear() => _buffer?.ClearBuffer();

    public void Stop()
    {
        _output?.Stop();
        _output?.Dispose();
        _output = null;
        _buffer = null;
        _format = null;
    }

    public void Dispose() => Stop();

    internal static bool ShouldDropIncomingBlock(int bufferedBytes, int bufferCapacityBytes, int incomingBytes)
        => incomingBytes > bufferCapacityBytes - bufferedBytes;

    private static void ValidateFormat(EmulationAudioFormat format)
    {
        if (format.SampleRate <= 0) throw new ArgumentOutOfRangeException(nameof(format), "Audio sample rate must be greater than zero.");
        if (format.Channels <= 0) throw new ArgumentOutOfRangeException(nameof(format), "Audio channel count must be greater than zero.");
        if (format.BitsPerSample != 16) throw new NotSupportedException($"Only 16-bit PCM audio is supported; native core reported {format.BitsPerSample} bits per sample.");
    }
}
