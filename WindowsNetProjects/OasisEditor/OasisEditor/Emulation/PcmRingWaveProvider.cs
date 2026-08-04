using System.Runtime.InteropServices;
using NAudio.Wave;

namespace OasisEditor;

internal sealed class PcmRingWaveProvider : IWaveProvider
{
    private readonly PcmFrameRingBuffer _ring;
    private readonly short[] _scratch;
    private bool _inUnderrun;
    private long _framesDelivered;
    private long _silenceFrames;
    private long _underrunEpisodes;

    public PcmRingWaveProvider(PcmFrameRingBuffer ring, WaveFormat waveFormat)
    {
        _ring = ring ?? throw new ArgumentNullException(nameof(ring));
        WaveFormat = waveFormat ?? throw new ArgumentNullException(nameof(waveFormat));
        if (waveFormat.BitsPerSample != 16 || waveFormat.Channels != ring.Channels)
            throw new ArgumentException("Wave format must be 16-bit PCM and match the ring channel count.");
        _scratch = new short[Math.Max(ring.Channels, (waveFormat.AverageBytesPerSecond / 10) / sizeof(short))];
    }

    public WaveFormat WaveFormat { get; }
    public long FramesDelivered => Interlocked.Read(ref _framesDelivered);
    public long SilenceFrames => Interlocked.Read(ref _silenceFrames);
    public long UnderrunEpisodes => Interlocked.Read(ref _underrunEpisodes);

    public int Read(byte[] buffer, int offset, int count)
    {
        var bytesPerFrame = WaveFormat.BlockAlign;
        var requestedFrames = count / bytesPerFrame;
        var requestedBytes = requestedFrames * bytesPerFrame;
        var destinationBytes = buffer.AsSpan(offset, requestedBytes);
        var destinationSamples = MemoryMarshal.Cast<byte, short>(destinationBytes);
        var framesRead = 0;
        while (framesRead < requestedFrames)
        {
            var framesThisRead = Math.Min(requestedFrames - framesRead, _scratch.Length / _ring.Channels);
            var read = _ring.Read(_scratch.AsSpan(0, framesThisRead * _ring.Channels), framesThisRead);
            if (read == 0) break;
            _scratch.AsSpan(0, read * _ring.Channels).CopyTo(destinationSamples[(framesRead * _ring.Channels)..]);
            framesRead += read;
        }
        var silenceFrames = requestedFrames - framesRead;
        if (silenceFrames > 0)
        {
            destinationSamples[(framesRead * _ring.Channels)..(requestedFrames * _ring.Channels)].Clear();
            Interlocked.Add(ref _silenceFrames, silenceFrames);
            if (!_inUnderrun)
            {
                _inUnderrun = true;
                Interlocked.Increment(ref _underrunEpisodes);
            }
        }
        else
        {
            _inUnderrun = false;
        }
        Interlocked.Add(ref _framesDelivered, requestedFrames);
        if (count > requestedBytes)
            buffer.AsSpan(offset + requestedBytes, count - requestedBytes).Clear();
        return count;
    }
}
