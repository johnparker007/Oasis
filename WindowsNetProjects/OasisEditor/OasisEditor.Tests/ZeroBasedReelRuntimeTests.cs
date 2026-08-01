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

        Assert.Equal(10, document.RuntimeState.GetReelPosition("reel-0"));
        Assert.Equal(20, document.RuntimeState.GetReelPosition("reel-1"));
        Assert.Equal(30, document.RuntimeState.GetReelPosition("reel-2"));
        Assert.Equal(40, document.RuntimeState.GetReelPosition("reel-3"));
        Assert.Equal(10, document.RuntimeState.GetReelPosition(MachineObjectReference.Reel(0)));
        Assert.Equal(40, document.RuntimeState.GetReelPosition(MachineObjectReference.Reel(3)));
    }

    private static FabricMachineSnapshot CreateSnapshot() => new(1, [],
    [
        new FabricReel("amber.reel.0", 0, 10),
        new FabricReel("amber.reel.1", 1, 20),
        new FabricReel("amber.reel.2", 2, 30),
        new FabricReel("amber.reel.3", 3, 40)
    ], [], []);
}
