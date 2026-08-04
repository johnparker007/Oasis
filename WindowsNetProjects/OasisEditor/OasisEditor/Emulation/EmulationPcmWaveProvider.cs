using System.Buffers.Binary;
using NAudio.Wave;

namespace OasisEditor;

internal sealed class EmulationPcmWaveProvider : IWaveProvider
{
    private readonly PcmFrameRingBuffer _ringBuffer;
    private readonly short[] _scratch;
    private bool _underrunActive;
    private long _framesDelivered;
    private long _silenceFrames;
    private long _underrunEpisodes;

    public EmulationPcmWaveProvider(PcmFrameRingBuffer ringBuffer, EmulationAudioFormat format, int maxReadFrames)
    {
        _ringBuffer = ringBuffer ?? throw new ArgumentNullException(nameof(ringBuffer));
        if (format.BitsPerSample != 16) throw new NotSupportedException("Only 16-bit PCM is supported.");
        if (format.Channels != ringBuffer.Channels) throw new ArgumentException("Format channel count must match the ring buffer.", nameof(format));
        if (maxReadFrames <= 0) throw new ArgumentOutOfRangeException(nameof(maxReadFrames));
        Format = format;
        WaveFormat = new WaveFormat(format.SampleRate, format.BitsPerSample, format.Channels);
        _scratch = new short[checked(maxReadFrames * format.Channels)];
    }

    public WaveFormat WaveFormat { get; }
    internal EmulationAudioFormat Format { get; }
    internal long FramesDelivered => Interlocked.Read(ref _framesDelivered);
    internal long SilenceFrames => Interlocked.Read(ref _silenceFrames);
    internal long UnderrunEpisodes => Interlocked.Read(ref _underrunEpisodes);

    public int Read(byte[] buffer, int offset, int count)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        if ((uint)offset > (uint)buffer.Length || count < 0 || count > buffer.Length - offset) throw new ArgumentOutOfRangeException(nameof(count));
        var bytesPerFrame = checked(Format.Channels * sizeof(short));
        var requestedFrames = count / bytesPerFrame;
        var requestedSamples = checked(requestedFrames * Format.Channels);
        var requestedFrameBytes = checked(requestedFrames * bytesPerFrame);
        var framesRead = requestedFrames == 0 ? 0 : _ringBuffer.Read(_scratch.AsSpan(0, requestedSamples), requestedFrames);
        var samplesRead = checked(framesRead * Format.Channels);
        var span = buffer.AsSpan(offset, count);
        for (var i = 0; i < samplesRead; i++)
            BinaryPrimitives.WriteInt16LittleEndian(span.Slice(i * sizeof(short), sizeof(short)), _scratch[i]);
        var silenceBytesStart = checked(samplesRead * sizeof(short));
        span[silenceBytesStart..requestedFrameBytes].Clear();
        span[requestedFrameBytes..].Clear();
        Interlocked.Add(ref _framesDelivered, framesRead);
        var silenceFrames = requestedFrames - framesRead;
        if (silenceFrames > 0)
        {
            Interlocked.Add(ref _silenceFrames, silenceFrames);
            if (!_underrunActive) { Interlocked.Increment(ref _underrunEpisodes); _underrunActive = true; }
        }
        else _underrunActive = false;
        return count;
    }
}
