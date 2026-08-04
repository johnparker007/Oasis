using Xunit;

namespace OasisEditor.Tests;

public sealed class FabricPumpSchedulerTests
{
    [Fact]
    public void DoesNotRunSliceBeforeOneMillisecondElapsed()
    {
        var scheduler = CreateScheduler();
        scheduler.Reset(0);
        Assert.Equal(0, scheduler.AdvanceTo(999).SlicesToRun);
        var step = scheduler.AdvanceTo(1000);
        Assert.Equal(1, step.SlicesToRun);
        Assert.Equal(0, step.AccumulatedTicks);
    }

    [Fact]
    public void TenMillisecondsRunsTenSlicesAcrossBatchLimit()
    {
        var scheduler = CreateScheduler();
        scheduler.Reset(0);
        var first = scheduler.AdvanceTo(10_000);
        var second = scheduler.AdvanceTo(10_000);
        Assert.Equal(8, first.SlicesToRun);
        Assert.Equal(2, second.SlicesToRun);
        Assert.Equal(0, second.AccumulatedTicks);
    }

    [Fact]
    public void FortyMillisecondStallProducesExactlyFortySlicesAcrossFiveBatches()
    {
        var scheduler = CreateScheduler();
        scheduler.Reset(0);
        var total = 0;
        for (var i = 0; i < 5; i++)
            total += scheduler.AdvanceTo(40_000).SlicesToRun;
        Assert.Equal(40, total);
        Assert.Equal(0, scheduler.AccumulatedTicks);
    }

    [Fact]
    public void SubMillisecondRemainderIsRetainedWithoutDuplication()
    {
        var scheduler = CreateScheduler();
        scheduler.Reset(0);
        Assert.Equal(0, scheduler.AdvanceTo(500).SlicesToRun);
        Assert.Equal(1, scheduler.AdvanceTo(1_100).SlicesToRun);
        Assert.Equal(100, scheduler.AccumulatedTicks);
    }

    [Fact]
    public void RandomAdvancesProduceFloorOfTotalElapsedMilliseconds()
    {
        var scheduler = CreateScheduler();
        scheduler.Reset(0);
        var random = new Random(1234);
        var timestamp = 0L;
        var total = 0;
        for (var i = 0; i < 100; i++)
        {
            timestamp += random.Next(0, 5_000);
            FabricPumpSchedulerStep step;
            do
            {
                step = scheduler.AdvanceTo(timestamp);
                total += step.SlicesToRun;
            } while (step.SlicesToRun > 0);
        }
        Assert.Equal(timestamp / 1_000, total);
    }

    [Fact]
    public void ExcessiveDebtIsClampedAndReportedOncePerAdvance()
    {
        var scheduler = CreateScheduler(maxDebtTicks: 20_000);
        scheduler.Reset(0);
        var step = scheduler.AdvanceTo(100_000);
        Assert.True(step.DiscardedExcessiveDebt);
        Assert.Equal(80_000, step.DiscardedTicks);
        Assert.Equal(8, step.SlicesToRun);
    }

    private static FabricPumpScheduler CreateScheduler(long maxDebtTicks = 125_000) =>
        new(sliceTicks: 1_000, maximumSlicesPerBatch: 8, maximumDebtTicks: maxDebtTicks);
}
