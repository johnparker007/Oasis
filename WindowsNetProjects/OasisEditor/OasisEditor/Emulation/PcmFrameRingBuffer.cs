namespace OasisEditor;

internal sealed class PcmFrameRingBuffer
{
    private readonly object _gate = new();
    private readonly short[] _samples;
    private int _readFrame;
    private int _writeFrame;
    private int _readableFrames;

    public PcmFrameRingBuffer(int channels, int capacityFrames)
    {
        if (channels <= 0) throw new ArgumentOutOfRangeException(nameof(channels));
        if (capacityFrames <= 0) throw new ArgumentOutOfRangeException(nameof(capacityFrames));
        Channels = channels;
        CapacityFrames = capacityFrames;
        _samples = new short[checked(channels * capacityFrames)];
    }

    public int Channels { get; }
    public int CapacityFrames { get; }
    public int ReadableFrames { get { lock (_gate) return _readableFrames; } }
    public int WritableFrames { get { lock (_gate) return CapacityFrames - _readableFrames; } }

    public int Write(ReadOnlySpan<short> interleavedSamples, int frameCount)
    {
        if (frameCount < 0) throw new ArgumentOutOfRangeException(nameof(frameCount));
        var sampleCount = checked(frameCount * Channels);
        if (interleavedSamples.Length < sampleCount) throw new ArgumentException("Source does not contain the requested complete frames.", nameof(interleavedSamples));
        lock (_gate)
        {
            var framesToWrite = Math.Min(frameCount, CapacityFrames - _readableFrames);
            CopyIn(interleavedSamples[..checked(framesToWrite * Channels)], _writeFrame);
            _writeFrame = (_writeFrame + framesToWrite) % CapacityFrames;
            _readableFrames += framesToWrite;
            return framesToWrite;
        }
    }

    public int Read(Span<short> destination, int frameCount)
    {
        if (frameCount < 0) throw new ArgumentOutOfRangeException(nameof(frameCount));
        var sampleCount = checked(frameCount * Channels);
        if (destination.Length < sampleCount) throw new ArgumentException("Destination cannot contain the requested complete frames.", nameof(destination));
        lock (_gate)
        {
            var framesToRead = Math.Min(frameCount, _readableFrames);
            CopyOut(destination[..checked(framesToRead * Channels)], _readFrame);
            _readFrame = (_readFrame + framesToRead) % CapacityFrames;
            _readableFrames -= framesToRead;
            return framesToRead;
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            Array.Clear(_samples);
            _readFrame = 0;
            _writeFrame = 0;
            _readableFrames = 0;
        }
    }

    private void CopyIn(ReadOnlySpan<short> source, int startFrame)
    {
        var firstFrames = Math.Min(source.Length / Channels, CapacityFrames - startFrame);
        source[..checked(firstFrames * Channels)].CopyTo(_samples.AsSpan(checked(startFrame * Channels)));
        var remainingSamples = source.Length - checked(firstFrames * Channels);
        if (remainingSamples > 0) source[^remainingSamples..].CopyTo(_samples);
    }

    private void CopyOut(Span<short> destination, int startFrame)
    {
        var firstFrames = Math.Min(destination.Length / Channels, CapacityFrames - startFrame);
        _samples.AsSpan(checked(startFrame * Channels), checked(firstFrames * Channels)).CopyTo(destination);
        var remainingSamples = destination.Length - checked(firstFrames * Channels);
        if (remainingSamples > 0) _samples.AsSpan(0, remainingSamples).CopyTo(destination[^remainingSamples..]);
    }
}
