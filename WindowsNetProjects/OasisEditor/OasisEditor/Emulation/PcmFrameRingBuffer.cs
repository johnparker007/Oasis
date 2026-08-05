namespace OasisEditor;

internal sealed class PcmFrameRingBuffer
{
    private readonly short[] _samples;
    private readonly object _gate = new();
    private int _readFrame;
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

    public int Write(ReadOnlySpan<short> samples, int frameCount)
    {
        ValidateFrameSpan(samples.Length, frameCount);
        lock (_gate)
        {
            var frames = Math.Min(frameCount, CapacityFrames - _readableFrames);
            if (frames <= 0) return 0;
            var writeFrame = (_readFrame + _readableFrames) % CapacityFrames;
            CopyFrames(samples, _samples, writeFrame, frames);
            _readableFrames += frames;
            return frames;
        }
    }

    public int Read(Span<short> destination, int frameCount)
    {
        ValidateFrameSpan(destination.Length, frameCount);
        lock (_gate)
        {
            var frames = Math.Min(frameCount, _readableFrames);
            if (frames <= 0) return 0;
            CopyFrames(_samples, _readFrame, destination, frames);
            _readFrame = (_readFrame + frames) % CapacityFrames;
            _readableFrames -= frames;
            return frames;
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _readFrame = 0;
            _readableFrames = 0;
            Array.Clear(_samples);
        }
    }

    private void ValidateFrameSpan(int sampleLength, int frameCount)
    {
        if (frameCount < 0) throw new ArgumentOutOfRangeException(nameof(frameCount));
        if (sampleLength < checked(frameCount * Channels))
            throw new ArgumentException("The sample span is shorter than the requested complete frame count.");
    }

    private void CopyFrames(ReadOnlySpan<short> source, short[] destination, int destinationFrame, int frameCount)
    {
        var firstFrames = Math.Min(frameCount, CapacityFrames - destinationFrame);
        source[..(firstFrames * Channels)].CopyTo(destination.AsSpan(destinationFrame * Channels));
        var remaining = frameCount - firstFrames;
        if (remaining > 0)
            source.Slice(firstFrames * Channels, remaining * Channels).CopyTo(destination);
    }

    private void CopyFrames(short[] source, int sourceFrame, Span<short> destination, int frameCount)
    {
        var firstFrames = Math.Min(frameCount, CapacityFrames - sourceFrame);
        source.AsSpan(sourceFrame * Channels, firstFrames * Channels).CopyTo(destination);
        var remaining = frameCount - firstFrames;
        if (remaining > 0)
            source.AsSpan(0, remaining * Channels).CopyTo(destination[(firstFrames * Channels)..]);
    }
}
