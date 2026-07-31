using OasisEditor;
using Xunit;

namespace OasisEditor.Tests;

public sealed class FabricSevenSegmentRuntimeTests
{
    [Fact]
    public void Snapshot_UsesDenseDigitCellsAndUpdatesPanelAndLinkedFaceDisplays()
    {
        var document = CreateDocument();
        var panelNotifications = new List<IReadOnlyDictionary<string, object>>();
        var faceNotifications = new List<IReadOnlyCollection<string>>();
        document.PanelVisualStateChanged += change => panelNotifications.Add(change.ValuesByObjectId);
        document.FaceVisualStateChanged += change => faceNotifications.Add(change.ObjectIds);
        var dispatches = new List<Action>();
        var diagnostics = new List<string>();
        var adapter = new MachineSegmentRuntimeAdapter(
            () => [document], dispatches.Add, diagnosticLogger: diagnostics.Add);
        var backend = new FabricEmulationBackend("fabric", "amber");
        backend.SegmentChanged += (_, change) =>
            adapter.ApplySegmentState(change.CellId, change.SegmentMask, change.OutputType);

        backend.PublishSnapshot(new FabricMachineSnapshot(1, [], [], [],
        [
            new FabricSegmentDisplay("amber.display.0", [0x3FUL, 0x06UL]),
            new FabricSegmentDisplay("amber.display.1", [0x5BUL])
        ]));
        Assert.Single(dispatches)();

        Assert.Equal([0x7E], document.RuntimeState.GetSegmentCellMasks("seven-0", 1));
        Assert.Equal([0x30], document.RuntimeState.GetSegmentCellMasks("seven-1", 1));
        Assert.Equal([0x6D], document.RuntimeState.GetSegmentCellMasks("seven-2", 1));
        var reference = MachineObjectReference.SevenSegmentDisplay(1);
        Assert.Equal([0x30, 0x6D], document.RuntimeState.GetSegmentCellMasks(reference, 2));
        Assert.Contains(panelNotifications.SelectMany(values => values.Keys), id => id == "seven-0");
        Assert.Contains(panelNotifications.SelectMany(values => values.Keys), id => id == "seven-2");
        Assert.Contains(faceNotifications.SelectMany(ids => ids), id => id == "face-seven-1");
        Assert.Contains(diagnostics, message => message.Contains("cellId=0") && message.Contains("mask=0x7E"));
        Assert.Contains(diagnostics, message => message.Contains("face=face-seven-1") && message.Contains("0x6D"));
    }

    [Fact]
    public void Snapshot_ZeroTurnsDigitOffAndDuplicateSnapshotDoesNotDispatchAgain()
    {
        var document = CreateDocument();
        var dispatches = new List<Action>();
        var adapter = new MachineSegmentRuntimeAdapter(() => [document], dispatches.Add);
        var backend = new FabricEmulationBackend("fabric", "amber");
        backend.SegmentChanged += (_, change) =>
            adapter.ApplySegmentState(change.CellId, change.SegmentMask, change.OutputType);

        backend.PublishSnapshot(new FabricMachineSnapshot(1, [], [], [],
            [new FabricSegmentDisplay("amber.display.0", [0x7FUL])]));
        Assert.Single(dispatches)();
        Assert.Equal([0x7F], document.RuntimeState.GetSegmentCellMasks("seven-0", 1));

        backend.PublishSnapshot(new FabricMachineSnapshot(2, [], [], [],
            [new FabricSegmentDisplay("amber.display.0", [0UL])]));
        Assert.Single(dispatches.Skip(1))();
        Assert.Equal([0], document.RuntimeState.GetSegmentCellMasks("seven-0", 1));

        backend.PublishSnapshot(new FabricMachineSnapshot(3, [], [], [],
            [new FabricSegmentDisplay("amber.display.0", [0UL])]));
        Assert.Equal(2, dispatches.Count);
    }

    [Fact]
    public void Snapshot_AlphaAddressingAndMasksRemainUnchanged()
    {
        var backend = new FabricEmulationBackend("fabric", "amber");
        var changes = new List<MachineSegmentChangedEventArgs>();
        backend.SegmentChanged += (_, change) => changes.Add(change);

        backend.PublishSnapshot(new FabricMachineSnapshot(1, [], [],
        [
            new FabricCharacterDisplay("alpha.0", [1u], [0]),
            new FabricCharacterDisplay("alpha.1", [2u], [0])
        ], []));

        Assert.Collection(changes,
            change =>
            {
                Assert.Equal(0, change.CellId);
                Assert.Equal(1, change.SegmentMask);
                Assert.Equal(SegmentOutputType.NativeAlpha, change.OutputType);
            },
            change =>
            {
                Assert.Equal(FabricAbi.CharacterCapacity, change.CellId);
                Assert.Equal(2, change.SegmentMask);
                Assert.Equal(SegmentOutputType.NativeAlpha, change.OutputType);
            });
    }

    private static DocumentTabViewModel CreateDocument()
    {
        var document = new DocumentTabViewModel(
            EditorDocument.CreateFromFile("panel.panel2d", "panel", "panel"));
        document.SetPanelElements([
            new PanelElementModel { ObjectId = "seven-0", Kind = PanelElementKind.SevenSegment, DisplayNumber = 0 },
            new PanelElementModel { ObjectId = "seven-1", Kind = PanelElementKind.SevenSegment, DisplayNumber = 1 },
            new PanelElementModel { ObjectId = "seven-2", Kind = PanelElementKind.SevenSegment, DisplayNumber = 2 }
        ]);
        document.SetFaceElements([
            new FaceSevenSegmentDisplayElement
            {
                ObjectId = "face-seven-1",
                DigitCount = 2,
                LinkedMachineObjectReference = MachineObjectReference.SevenSegmentDisplay(1)
            }
        ]);
        return document;
    }
}
