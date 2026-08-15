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
        Assert.All(changes, change => Assert.Equal(ReelPositionConvention.Amber, change.Convention));
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
        backend.ReelChanged += (_, change) => adapter.ApplyReelState(change.ReelId, change.Position, change.Convention);

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
    [InlineData(ReelPositionConvention.Amber, 0, 96, 0d)]
    [InlineData(ReelPositionConvention.Amber, 1, 96, 95d)]
    [InlineData(ReelPositionConvention.Amber, 95, 96, 1d)]
    [InlineData(ReelPositionConvention.Amber, 96, 96, 0d)]
    [InlineData(ReelPositionConvention.Amber, 97, 96, 95d)]
    [InlineData(ReelPositionConvention.Amber, -1, 96, 1d)]
    [InlineData(ReelPositionConvention.Oasis, 10, 96, 10d)]
    [InlineData(ReelPositionConvention.Amber, 10, 48, 76d)]
    public void BackendNormalization_UsesConventionAndConfiguredMotorStepRange(
        ReelPositionConvention convention, int position, int positionCount, double expected)
    {
        Assert.Equal(expected, MachineReelRuntimeAdapter.NormalizeBackendReelPosition(convention, position, positionCount));
    }

    [Theory]
    [InlineData(FruitMachinePlatformType.Impact, false, 86d)]
    [InlineData(FruitMachinePlatformType.Impact, true, 10d)]
    [InlineData(FruitMachinePlatformType.MPU5, false, 86d)]
    [InlineData(FruitMachinePlatformType.MPU5, true, 10d)]
    [InlineData(FruitMachinePlatformType.Epoch, false, 10d)]
    [InlineData(FruitMachinePlatformType.Epoch, true, 86d)]
    public void FabricAmberNormalization_AppliesOnceBeforeComponentReversal(FruitMachinePlatformType platform, bool isReversed, double expected)
    {
        var document = new DocumentTabViewModel(EditorDocument.CreateFromFile("panel.panel2d", "panel", "panel"));
        document.SetPanelElements([new PanelElementModel
        {
            ObjectId = "reel-0", Kind = PanelElementKind.Reel, DisplayNumber = 0,
            Stops = 24, IsReversed = isReversed
        }]);
        var dispatches = new List<Action>();
        var adapter = new MachineReelRuntimeAdapter(() => [document], () => platform,
            () => false, _ => { }, dispatches.Add);

        adapter.ApplyReelState(0, 10, ReelPositionConvention.Amber);
        Assert.Single(dispatches)();

        Assert.Equal(expected, document.RuntimeState.GetReelPosition("reel-0"));
        Assert.Equal(86d, document.RuntimeState.GetReelPosition(MachineObjectReference.Reel(0)));
    }

    [Fact]
    public void ConventionNeutralBackend_RemainsUnreversed()
    {
        var document = new DocumentTabViewModel(EditorDocument.CreateFromFile("panel.panel2d", "panel", "panel"));
        document.SetPanelElements([new PanelElementModel
        {
            ObjectId = "reel-0", Kind = PanelElementKind.Reel, DisplayNumber = 0, Stops = 24
        }]);
        var dispatches = new List<Action>();
        var adapter = new MachineReelRuntimeAdapter(() => [document], () => FruitMachinePlatformType.Impact,
            () => false, _ => { }, dispatches.Add);

        adapter.ApplyReelState(0, 10);
        Assert.Single(dispatches)();

        Assert.Equal(10d, document.RuntimeState.GetReelPosition("reel-0"));
        Assert.Equal(10d, document.RuntimeState.GetReelPosition(MachineObjectReference.Reel(0)));
    }

    [Fact]
    public void ImpactCorrection_AppliesTwelveAndSixteenStopBandOffsets()
    {
        Assert.Equal(0.025d, MachineReelRuntimeAdapter.ResolvePlatformBandOffsetNormalized(FruitMachinePlatformType.Impact, 12), 6);
        Assert.Equal(-0.018d, MachineReelRuntimeAdapter.ResolvePlatformBandOffsetNormalized(FruitMachinePlatformType.Impact, 16), 6);
    }

    [Fact]
    public void EpochTwelveStopCorrection_UsesSameEffectivePositionPipelineForPanelAndFaceReels()
    {
        var document = new DocumentTabViewModel(EditorDocument.CreateFromFile("panel.panel2d", "panel", "panel"));
        document.SetPanelElements([new PanelElementModel
        {
            ObjectId = "reel-0", Kind = PanelElementKind.Reel, DisplayNumber = 0,
            Stops = 12, BandOffset = 0.05d
        }]);
        var dispatches = new List<Action>();
        var adapter = new MachineReelRuntimeAdapter(() => [document], () => FruitMachinePlatformType.Epoch,
            () => false, _ => { }, dispatches.Add);

        adapter.ApplyReelState(0, 10, ReelPositionConvention.Oasis);
        Assert.Single(dispatches)();

        var faceRuntimeState = new MachineRuntimeState
        {
            FruitMachinePlatform = FruitMachinePlatformType.Epoch
        };
        faceRuntimeState.SetReelPositionIfChanged(MachineObjectReference.Reel(0), 10d);
        var faceReel = new FaceReelDisplayElement
        {
            LinkedMachineObjectReference = MachineObjectReference.Reel(0),
            Stops = 12,
            BandOffset = 0.05d
        };

        var panelPosition = document.RuntimeState.GetReelPosition("reel-0");
        var facePosition = FaceRuntimeStateResolver.Instance.GetReelPosition(faceReel, faceRuntimeState);

        Assert.Equal(75.44d, panelPosition, 6);
        Assert.Equal(panelPosition, facePosition, 6);
    }

    [Theory]
    [InlineData(12, 10.48d)]
    [InlineData(16, 5.968d)]
    public void MaygayM1Correction_UsesSameEffectivePositionPipelineForPanelAndFaceReels(int stops, double expected)
    {
        var document = new DocumentTabViewModel(EditorDocument.CreateFromFile("panel.panel2d", "panel", "panel"));
        document.SetPanelElements([new PanelElementModel
        {
            ObjectId = "reel-0", Kind = PanelElementKind.Reel, DisplayNumber = 0,
            Stops = stops, BandOffset = 0.05d
        }]);
        var dispatches = new List<Action>();
        var adapter = new MachineReelRuntimeAdapter(() => [document], () => FruitMachinePlatformType.MaygayM1,
            () => false, _ => { }, dispatches.Add);

        adapter.ApplyReelState(0, 10, ReelPositionConvention.Oasis);
        Assert.Single(dispatches)();

        var faceRuntimeState = new MachineRuntimeState
        {
            FruitMachinePlatform = FruitMachinePlatformType.MaygayM1
        };
        faceRuntimeState.SetReelPositionIfChanged(MachineObjectReference.Reel(0), 10d);
        var faceReel = new FaceReelDisplayElement
        {
            LinkedMachineObjectReference = MachineObjectReference.Reel(0),
            Stops = stops,
            BandOffset = 0.05d
        };

        var panelPosition = document.RuntimeState.GetReelPosition("reel-0");
        var facePosition = FaceRuntimeStateResolver.Instance.GetReelPosition(faceReel, faceRuntimeState);

        Assert.Equal(expected, panelPosition, 6);
        Assert.Equal(panelPosition, facePosition, 6);
    }

    [Theory]
    [InlineData(12, 1.024d, 12.304d)]
    [InlineData(16, 0.961d, 6.256d)]
    public void Scorpion4Correction_AddsUserBandOffsetForPanelAndFaceReels(
        int stops,
        double expectedNormalizedOffset,
        double expectedPosition)
    {
        var document = new DocumentTabViewModel(EditorDocument.CreateFromFile("panel.panel2d", "panel", "panel"));
        document.SetPanelElements([new PanelElementModel
        {
            ObjectId = "reel-0", Kind = PanelElementKind.Reel, DisplayNumber = 0,
            Stops = stops, BandOffset = 0.05d
        }]);
        var dispatches = new List<Action>();
        var adapter = new MachineReelRuntimeAdapter(() => [document], () => FruitMachinePlatformType.Scorpion4,
            () => false, _ => { }, dispatches.Add);

        adapter.ApplyReelState(0, 10, ReelPositionConvention.Oasis);
        Assert.Single(dispatches)();

        var faceRuntimeState = new MachineRuntimeState
        {
            FruitMachinePlatform = FruitMachinePlatformType.Scorpion4
        };
        faceRuntimeState.SetReelPositionIfChanged(MachineObjectReference.Reel(0), 10d);
        var faceReel = new FaceReelDisplayElement
        {
            LinkedMachineObjectReference = MachineObjectReference.Reel(0),
            Stops = stops,
            BandOffset = 0.05d
        };

        var panelPosition = document.RuntimeState.GetReelPosition("reel-0");
        var facePosition = FaceRuntimeStateResolver.Instance.GetReelPosition(faceReel, faceRuntimeState);

        var effectiveNormalizedOffset = MachineReelRuntimeAdapter.ResolvePlatformBandOffsetNormalized(
            FruitMachinePlatformType.Scorpion4, stops) + 0.05d;
        Assert.Equal(expectedNormalizedOffset, effectiveNormalizedOffset, 6);
        Assert.Equal(expectedPosition, panelPosition, 6);
        Assert.Equal(panelPosition, facePosition, 6);
    }

    [Fact]
    public void AmberAdapter_AllowsEachReelToUseItsConfiguredMotorStepCount()
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

        adapter.ApplyReelState(0, 10, ReelPositionConvention.Amber);
        adapter.ApplyReelState(1, 10, ReelPositionConvention.Amber);
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
