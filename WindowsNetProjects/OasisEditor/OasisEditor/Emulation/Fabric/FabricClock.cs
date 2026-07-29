namespace OasisEditor;

internal interface IFabricClock
{
    long Frequency { get; }
    long GetTimestamp();
}

internal sealed class StopwatchFabricClock : IFabricClock
{
    public long Frequency => System.Diagnostics.Stopwatch.Frequency;
    public long GetTimestamp() => System.Diagnostics.Stopwatch.GetTimestamp();
}

internal sealed class FabricElapsedTime
{
    private readonly long _frequency;
    private long _baseline;
    private long _remainder;

    internal FabricElapsedTime(long frequency)
    {
        if (frequency <= 0)
            throw new ArgumentOutOfRangeException(nameof(frequency));
        _frequency = frequency;
    }

    internal void Reset(long timestamp)
    {
        _baseline = timestamp;
        _remainder = 0;
    }

    internal ulong AdvanceTo(long timestamp)
    {
        var delta = timestamp - _baseline;
        if (delta < 0)
            throw new InvalidOperationException("The Fabric monotonic clock moved backwards.");
        _baseline = timestamp;

        // Divide before multiplying. This cannot overflow and carries the fractional numerator.
        var seconds = delta / _frequency;
        var ticks = delta % _frequency;
        var wholeNanoseconds = checked((ulong)seconds * 1_000_000_000UL);
        var numerator = checked((ulong)ticks * 1_000_000_000UL + (ulong)_remainder);
        var fractionalNanoseconds = numerator / (ulong)_frequency;
        _remainder = checked((long)(numerator % (ulong)_frequency));
        return checked(wholeNanoseconds + fractionalNanoseconds);
    }
}
