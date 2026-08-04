namespace OasisEditor;

public enum EmulationAudioOutputBackend
{
    WasapiOut,
    WaveOutEvent
}

internal readonly record struct EmulationAudioFifoWriteResult(int OfferedFrames, int AcceptedFrames, int RejectedFrames)
{
    internal bool Rejected => RejectedFrames > 0;
}

internal sealed class EmulationPcmFrameFifo
{
    private readonly short[] _buffer;
    private readonly object _gate = new();
    private int _readFrame;
    private int _writeFrame;
    private int _queuedFrames;

    internal EmulationPcmFrameFifo(int capacityFrames, int channels)
    {
        if (capacityFrames <= 0) throw new ArgumentOutOfRangeException(nameof(capacityFrames));
        if (channels <= 0) throw new ArgumentOutOfRangeException(nameof(channels));
        CapacityFrames = capacityFrames;
        Channels = channels;
        _buffer = new short[checked(capacityFrames * channels)];
        LowWaterFrames = capacityFrames;
    }

    internal int CapacityFrames { get; }
    internal int Channels { get; }
    internal int HighWaterFrames { get; private set; }
    internal int LowWaterFrames { get; private set; }
    internal int QueuedFrames { get { lock (_gate) return _queuedFrames; } }

    internal EmulationAudioFifoWriteResult Write(ReadOnlySpan<short> interleavedSamples)
    {
        if (interleavedSamples.Length % Channels != 0)
            throw new ArgumentException("PCM sample count must align to complete frames.", nameof(interleavedSamples));
        var offeredFrames = interleavedSamples.Length / Channels;
        if (offeredFrames == 0) return new(0, 0, 0);
        lock (_gate)
        {
            var acceptedFrames = Math.Min(offeredFrames, CapacityFrames - _queuedFrames);
            CopyFramesIntoRing(interleavedSamples[..checked(acceptedFrames * Channels)], acceptedFrames);
            _queuedFrames += acceptedFrames;
            HighWaterFrames = Math.Max(HighWaterFrames, _queuedFrames);
            LowWaterFrames = Math.Min(LowWaterFrames, _queuedFrames);
            return new(offeredFrames, acceptedFrames, offeredFrames - acceptedFrames);
        }
    }

    internal int Read(Span<short> destinationSamples, int maxFrames)
    {
        if (maxFrames < 0) throw new ArgumentOutOfRangeException(nameof(maxFrames));
        if (destinationSamples.Length < checked(maxFrames * Channels))
            throw new ArgumentException("Destination is too small for the requested frame count.", nameof(destinationSamples));
        if (maxFrames == 0) return 0;
        lock (_gate)
        {
            var frames = Math.Min(maxFrames, _queuedFrames);
            CopyFramesFromRing(destinationSamples[..checked(frames * Channels)], frames);
            _queuedFrames -= frames;
            LowWaterFrames = Math.Min(LowWaterFrames, _queuedFrames);
            return frames;
        }
    }

    internal void Clear()
    {
        lock (_gate)
        {
            _readFrame = 0;
            _writeFrame = 0;
            _queuedFrames = 0;
            HighWaterFrames = 0;
            LowWaterFrames = CapacityFrames;
        }
    }

    private void CopyFramesIntoRing(ReadOnlySpan<short> samples, int frames)
    {
        var remaining = frames;
        var sourceOffsetSamples = 0;
        while (remaining > 0)
        {
            var contiguousFrames = Math.Min(remaining, CapacityFrames - _writeFrame);
            var samplesToCopy = checked(contiguousFrames * Channels);
            samples.Slice(sourceOffsetSamples, samplesToCopy).CopyTo(_buffer.AsSpan(checked(_writeFrame * Channels), samplesToCopy));
            _writeFrame = (_writeFrame + contiguousFrames) % CapacityFrames;
            sourceOffsetSamples += samplesToCopy;
            remaining -= contiguousFrames;
        }
    }

    private void CopyFramesFromRing(Span<short> destination, int frames)
    {
        var remaining = frames;
        var destinationOffsetSamples = 0;
        while (remaining > 0)
        {
            var contiguousFrames = Math.Min(remaining, CapacityFrames - _readFrame);
            var samplesToCopy = checked(contiguousFrames * Channels);
            _buffer.AsSpan(checked(_readFrame * Channels), samplesToCopy).CopyTo(destination.Slice(destinationOffsetSamples, samplesToCopy));
            _readFrame = (_readFrame + contiguousFrames) % CapacityFrames;
            destinationOffsetSamples += samplesToCopy;
            remaining -= contiguousFrames;
        }
    }
}

internal readonly record struct EmulationAudioReservePolicy(
    int CapacityFrames,
    int TargetFrames,
    int LowWaterFrames,
    int HighWaterFrames,
    int FeedBlockFrames)
{
    private const int TargetPercent = 75;
    private const int HighWaterPercent = 90;
    private const int MinimumTargetMilliseconds = 20;
    private const int SafetyMarginMilliseconds = 5;
    private const int FeedBlockMilliseconds = 5;

    internal static EmulationAudioReservePolicy Create(EmulationAudioFormat format, int capacityMilliseconds, int targetPercent = TargetPercent)
    {
        if (capacityMilliseconds <= 0) throw new ArgumentOutOfRangeException(nameof(capacityMilliseconds));
        if (format.SampleRate <= 0) throw new ArgumentOutOfRangeException(nameof(format));
        var capacityFrames = MillisecondsToFrames(format.SampleRate, capacityMilliseconds);
        var minimumTargetFrames = MillisecondsToFrames(format.SampleRate, Math.Min(MinimumTargetMilliseconds, capacityMilliseconds));
        var maximumTargetFrames = Math.Max(1, capacityFrames - MillisecondsToFrames(format.SampleRate, Math.Min(SafetyMarginMilliseconds, Math.Max(0, capacityMilliseconds - 1))));
        var requestedTargetFrames = checked((capacityFrames * Math.Clamp(targetPercent, 1, 100) + 99) / 100);
        var targetFrames = Math.Clamp(requestedTargetFrames, Math.Min(minimumTargetFrames, maximumTargetFrames), maximumTargetFrames);
        var lowWaterFrames = Math.Max(1, targetFrames / 2);
        var highWaterFrames = Math.Max(targetFrames, checked((capacityFrames * HighWaterPercent + 99) / 100));
        var feedBlockFrames = MillisecondsToFrames(format.SampleRate, FeedBlockMilliseconds);
        return new(capacityFrames, targetFrames, lowWaterFrames, Math.Min(capacityFrames, highWaterFrames), feedBlockFrames);
    }

    internal static int MillisecondsToFrames(int sampleRate, int milliseconds) =>
        checked((sampleRate * milliseconds + 999) / 1000);
}
