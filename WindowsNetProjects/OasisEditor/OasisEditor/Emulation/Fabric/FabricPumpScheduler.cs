namespace OasisEditor;

internal readonly record struct FabricPumpSchedulerStep(
    int SlicesToRun,
    long ElapsedTicks,
    long AccumulatedTicks,
    long DiscardedTicks,
    bool DiscardedExcessiveDebt);

internal sealed class FabricPumpScheduler
{
    private readonly long _sliceTicks;
    private readonly int _maximumSlicesPerBatch;
    private readonly long _maximumDebtTicks;
    private long _lastTimestamp;
    private long _accumulatedTicks;
    private bool _hasBaseline;

    internal FabricPumpScheduler(long sliceTicks, int maximumSlicesPerBatch, long maximumDebtTicks)
    {
        if (sliceTicks <= 0) throw new ArgumentOutOfRangeException(nameof(sliceTicks));
        if (maximumSlicesPerBatch <= 0) throw new ArgumentOutOfRangeException(nameof(maximumSlicesPerBatch));
        if (maximumDebtTicks < sliceTicks) throw new ArgumentOutOfRangeException(nameof(maximumDebtTicks));
        _sliceTicks = sliceTicks;
        _maximumSlicesPerBatch = maximumSlicesPerBatch;
        _maximumDebtTicks = maximumDebtTicks;
    }

    internal long AccumulatedTicks => _accumulatedTicks;

    internal void Reset(long timestamp)
    {
        _lastTimestamp = timestamp;
        _accumulatedTicks = 0;
        _hasBaseline = true;
    }

    internal FabricPumpSchedulerStep AdvanceTo(long timestamp)
    {
        if (!_hasBaseline)
            Reset(timestamp);

        var elapsedTicks = Math.Max(0, timestamp - _lastTimestamp);
        _lastTimestamp = timestamp;
        _accumulatedTicks += elapsedTicks;

        var discardedTicks = 0L;
        if (_accumulatedTicks > _maximumDebtTicks)
        {
            discardedTicks = _accumulatedTicks - _maximumDebtTicks;
            _accumulatedTicks = _maximumDebtTicks;
        }

        var slices = (int)Math.Min(_maximumSlicesPerBatch, _accumulatedTicks / _sliceTicks);
        _accumulatedTicks -= slices * _sliceTicks;
        return new(slices, elapsedTicks, _accumulatedTicks, discardedTicks, discardedTicks > 0);
    }

    internal TimeSpan TimeUntilNextSlice(long ticksPerSecond)
    {
        var missingTicks = Math.Max(0, _sliceTicks - _accumulatedTicks);
        return TimeSpan.FromSeconds((double)missingTicks / ticksPerSecond);
    }
}
