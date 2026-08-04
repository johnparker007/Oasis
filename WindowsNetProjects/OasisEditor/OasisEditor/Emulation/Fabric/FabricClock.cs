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

        var nanoseconds = TicksToNanoseconds(delta, _frequency, _remainder, out var remainder);
        _remainder = remainder;
        return nanoseconds;
    }

    internal static ulong TicksToNanoseconds(long ticks, long frequency) =>
        TicksToNanoseconds(ticks, frequency, 0, out _);

    private static ulong TicksToNanoseconds(long ticks, long frequency, long remainder, out long nextRemainder)
    {
        if (ticks < 0)
            throw new ArgumentOutOfRangeException(nameof(ticks));
        if (frequency <= 0)
            throw new ArgumentOutOfRangeException(nameof(frequency));
        var seconds = ticks / frequency;
        var fractionalTicks = ticks % frequency;
        var wholeNanoseconds = checked((ulong)seconds * 1_000_000_000UL);
        var numerator = checked((ulong)fractionalTicks * 1_000_000_000UL + (ulong)remainder);
        var fractionalNanoseconds = numerator / (ulong)frequency;
        nextRemainder = checked((long)(numerator % (ulong)frequency));
        return checked(wholeNanoseconds + fractionalNanoseconds);
    }
}
