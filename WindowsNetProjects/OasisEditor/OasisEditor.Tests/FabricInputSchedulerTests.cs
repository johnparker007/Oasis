using Xunit;

namespace OasisEditor.Tests;

public sealed class FabricInputSchedulerTests
{
    [Fact]
    public void RapidClick_IsAssertedForOneAdvance()
    {
        var scheduler = new FabricInputScheduler();
        var session = new RecordingSession();
        scheduler.Request(34, true);
        scheduler.Request(34, false);

        Pump(scheduler, session, 1);
        Pump(scheduler, session, 2);

        Assert.Equal(["Submit 34 True", "Advance", "Submit 34 False", "Advance"], session.Operations);
    }

    [Fact]
    public void HeldAndRepeatedPress_RemainsAssertedWithoutRedundantSubmissions()
    {
        var scheduler = new FabricInputScheduler();
        var session = new RecordingSession();
        scheduler.Request(7, true);
        scheduler.Request(7, true);
        Pump(scheduler, session, 1);
        Pump(scheduler, session, 2);
        scheduler.Request(7, false);
        Pump(scheduler, session, 3);

        Assert.Equal(["Submit 7 True", "Advance", "Advance", "Submit 7 False", "Advance"], session.Operations);
    }

    [Fact]
    public void MultipleSwitchesAndZero_AreScheduledIndependentlyAndUnchanged()
    {
        var scheduler = new FabricInputScheduler();
        var session = new RecordingSession();
        scheduler.Request(0, true);
        scheduler.Request(9, true);
        Pump(scheduler, session, 1);
        scheduler.Request(0, false);
        Pump(scheduler, session, 2);
        scheduler.Request(9, false);
        Pump(scheduler, session, 3);

        Assert.Equal(["Submit 0 True", "Submit 9 True", "Advance", "Submit 0 False", "Advance", "Submit 9 False", "Advance"], session.Operations);
    }

    [Fact]
    public void ResetOrShutdown_ReleasesEveryAppliedSwitch()
    {
        var scheduler = new FabricInputScheduler();
        var session = new RecordingSession();
        scheduler.Request(2, true);
        scheduler.Request(3, true);
        Pump(scheduler, session, 1);

        scheduler.ReleaseAll(session);

        Assert.Equal(["Submit 2 True", "Submit 3 True", "Advance", "Submit 2 False", "Submit 3 False"], session.Operations);
    }

    private static void Pump(FabricInputScheduler scheduler, RecordingSession session, long sequence)
    {
        scheduler.ApplyBeforeAdvance(session, sequence, null);
        session.Advance(1_000_000);
        scheduler.MarkAdvanced();
    }

    private sealed class RecordingSession : IFabricMachineSession
    {
        public List<string> Operations { get; } = [];
        public FabricCapabilities Capabilities => new((ulong)FabricCapability.DigitalInput);
        public void Initialise() { }
        public void Reset() { }
        public void Advance(ulong elapsedNanoseconds) => Operations.Add("Advance");
        public void SubmitInput(FabricInput input) => Operations.Add($"Submit {input.NumericalIndex} {input.Active}");
        public FabricMachineSnapshot GetSnapshot() => new(0, [], [], [], []);
        public FabricAudioFormat GetAudioFormat() => default;
        public int ReadAudio(Span<short> samples, int frameCapacity) => 0;
        public void Shutdown() { }
        public void Dispose() { }
    }
}
