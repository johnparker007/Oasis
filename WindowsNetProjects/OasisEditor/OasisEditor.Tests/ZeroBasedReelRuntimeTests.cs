using OasisEditor;
using Xunit;

namespace OasisEditor.Tests;

public sealed class ZeroBasedReelRuntimeTests
{
    [Fact]
    public void FabricSnapshot_UsesExactZeroBasedReelIds()
    {
        var backend = new FabricEmulationBackend("fabric", "amber");
        var changes = new List<MachineReelChangedEventArgs>();
        backend.ReelChanged += (_, change) => changes.Add(change);

        backend.PublishSnapshot(CreateSnapshot());

        Assert.Equal([0, 1, 2, 3], changes.Select(change => change.ReelId));
    }

    [Fact]
    public void FabricSnapshot_ThroughRuntimeAdapter_UpdatesEveryZeroBasedReel()
    {
        var document = new DocumentTabViewModel(EditorDocument.CreateFromFile("panel.panel2d", "panel", "panel"));
        document.SetPanelElements(Enumerable.Range(0, 4).Select(reelId => new PanelElementModel
        {
            ObjectId = $"reel-{reelId}",
            Name = $"Reel {reelId}",
            Kind = PanelElementKind.Reel,
            DisplayNumber = reelId,
            Stops = 24
        }).ToArray());
        var dispatches = new List<Action>();
        var adapter = new MachineReelRuntimeAdapter(() => [document], () => FruitMachinePlatformType.Impact,
            () => false, _ => { }, dispatches.Add);
        var backend = new FabricEmulationBackend("fabric", "amber");
        backend.ReelChanged += (_, change) => adapter.ApplyReelState(change.ReelId, change.Position);

        backend.PublishSnapshot(CreateSnapshot());
        Assert.Single(dispatches)();

        Assert.Equal(86, document.RuntimeState.GetReelPosition("reel-0"));
        Assert.Equal(76, document.RuntimeState.GetReelPosition("reel-1"));
        Assert.Equal(66, document.RuntimeState.GetReelPosition("reel-2"));
        Assert.Equal(56, document.RuntimeState.GetReelPosition("reel-3"));
        Assert.Equal(86, document.RuntimeState.GetReelPosition(MachineObjectReference.Reel(0)));
        Assert.Equal(56, document.RuntimeState.GetReelPosition(MachineObjectReference.Reel(3)));
    }

    [Theory]
    [InlineData(FruitMachinePlatformType.Impact, 0, 96, 0d)]
    [InlineData(FruitMachinePlatformType.Impact, 1, 96, 95d)]
    [InlineData(FruitMachinePlatformType.Impact, 95, 96, 1d)]
    [InlineData(FruitMachinePlatformType.Impact, 96, 96, 0d)]
    [InlineData(FruitMachinePlatformType.Impact, 97, 96, 95d)]
    [InlineData(FruitMachinePlatformType.Impact, -1, 96, 1d)]
    [InlineData(FruitMachinePlatformType.Scorpion4, 10, 96, 10d)]
    [InlineData(FruitMachinePlatformType.Impact, 10, 48, 76d)]
    public void PlatformNormalization_UsesConfiguredBackendMotorStepRange(
        FruitMachinePlatformType platform, int position, int positionCount, double expected)
    {
        Assert.Equal(expected, MachineReelRuntimeAdapter.NormalizePlatformReelPosition(platform, position, positionCount));
    }

    [Theory]
    [InlineData(false, 86d)]
    [InlineData(true, 10d)]
    public void ImpactPlatformCorrection_PrecedesComponentReversal(bool isReversed, double expected)
    {
        var document = new DocumentTabViewModel(EditorDocument.CreateFromFile("panel.panel2d", "panel", "panel"));
        document.SetPanelElements([new PanelElementModel
        {
            ObjectId = "reel-0", Kind = PanelElementKind.Reel, DisplayNumber = 0,
            Stops = 24, IsReversed = isReversed
        }]);
        var dispatches = new List<Action>();
        var adapter = new MachineReelRuntimeAdapter(() => [document], () => FruitMachinePlatformType.Impact,
            () => false, _ => { }, dispatches.Add);

        adapter.ApplyReelState(0, 10);
        Assert.Single(dispatches)();

        Assert.Equal(expected, document.RuntimeState.GetReelPosition("reel-0"));
    }

    [Fact]
    public void ImpactCorrection_AppliesTwelveAndSixteenStopBandOffsets()
    {
        Assert.Equal(0.025d, MachineReelRuntimeAdapter.ResolvePlatformBandOffsetNormalized(FruitMachinePlatformType.Impact, 12), 6);
        Assert.Equal(-0.018d, MachineReelRuntimeAdapter.ResolvePlatformBandOffsetNormalized(FruitMachinePlatformType.Impact, 16), 6);
    }

    [Fact]
    public void ImpactAdapter_AllowsEachReelToUseItsConfiguredMotorStepCount()
    {
        var document = new DocumentTabViewModel(EditorDocument.CreateFromFile("panel.panel2d", "panel", "panel"));
        document.SetPanelElements(Enumerable.Range(0, 2).Select(reelId => new PanelElementModel
        {
            ObjectId = $"reel-{reelId}", Kind = PanelElementKind.Reel,
            DisplayNumber = reelId, Stops = 24
        }).ToArray());
        var dispatches = new List<Action>();
        var adapter = new MachineReelRuntimeAdapter(() => [document], () => FruitMachinePlatformType.Impact,
            () => false, _ => { }, dispatches.Add, reelId => reelId == 0 ? 48 : 96);

        adapter.ApplyReelState(0, 10);
        adapter.ApplyReelState(1, 10);
        Assert.Single(dispatches)();

        Assert.Equal(76d, document.RuntimeState.GetReelPosition("reel-0"));
        Assert.Equal(86d, document.RuntimeState.GetReelPosition("reel-1"));
    }

    private static FabricMachineSnapshot CreateSnapshot() => new(1, [],
    [
        new FabricReel("amber.reel.0", 0, 10),
        new FabricReel("amber.reel.1", 1, 20),
        new FabricReel("amber.reel.2", 2, 30),
        new FabricReel("amber.reel.3", 3, 40)
    ], [], []);
}
