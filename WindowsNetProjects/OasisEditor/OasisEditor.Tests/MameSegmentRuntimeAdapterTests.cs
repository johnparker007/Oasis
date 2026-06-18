using OasisEditor;
using Xunit;

namespace OasisEditor.Tests;

public sealed class MameSegmentRuntimeAdapterTests
{
    [Fact]
    public void ApplySegmentState_PreservesPreviouslyReceivedCells()
    {
        var document = CreateDocument();
        var dispatches = new List<Action>();
        var adapter = new MameSegmentRuntimeAdapter(() => [document], action => dispatches.Add(action));

        adapter.ApplySegmentState(0, 1, MameSegmentOutputType.Vfd);
        var first = Assert.Single(dispatches);
        first();

        adapter.ApplySegmentState(1, 2, MameSegmentOutputType.Vfd);
        var second = Assert.Single(dispatches.Skip(1));
        second();

        var masks = document.RuntimeState.GetSegmentCellMasks("alpha-0", 16);
        Assert.Equal(3, masks[0]);
        Assert.Equal(4, masks[1]);
    }

    [Fact]
    public void ApplySegmentState_CoalescesPendingUpdatesIntoSingleUiDispatch()
    {
        var document = CreateDocument();
        var dispatches = new List<Action>();
        var adapter = new MameSegmentRuntimeAdapter(() => [document], action => dispatches.Add(action));

        adapter.ApplySegmentState(0, 1, MameSegmentOutputType.Vfd);
        adapter.ApplySegmentState(0, 3, MameSegmentOutputType.Vfd);
        adapter.ApplySegmentState(1, 2, MameSegmentOutputType.Vfd);

        var dispatch = Assert.Single(dispatches);
        dispatch();

        var masks = document.RuntimeState.GetSegmentCellMasks("alpha-0", 16);
        Assert.Equal(7, masks[0]);
        Assert.Equal(4, masks[1]);
    }

    [Fact]
    public void ApplySegmentState_UpdatesSevenSegmentFromDigitCell()
    {
        var document = CreateDocument();
        var dispatches = new List<Action>();
        var adapter = new MameSegmentRuntimeAdapter(() => [document], action => dispatches.Add(action));

        adapter.ApplySegmentState(3, 16, MameSegmentOutputType.Digit);

        var dispatch = Assert.Single(dispatches);
        dispatch();

        var masks = document.RuntimeState.GetSegmentCellMasks("seven-3", 1);
        Assert.Single(masks);
        Assert.Equal(16, masks[0]);
    }

    [Fact]
    public void ApplySegmentState_DoesNotApplyVfdMasksToSevenSegment()
    {
        var document = CreateDocument();
        var dispatches = new List<Action>();
        var adapter = new MameSegmentRuntimeAdapter(() => [document], action => dispatches.Add(action));

        adapter.ApplySegmentState(3, 255, MameSegmentOutputType.Vfd);

        var dispatch = Assert.Single(dispatches);
        dispatch();

        var masks = document.RuntimeState.GetSegmentCellMasks("seven-3", 1);
        Assert.Single(masks);
        Assert.Equal(0, masks[0]);
    }

    [Fact]
    public void ApplyVfdBrightness_AppliesPerDisplayBrightnessAcrossAllAlphaCells()
    {
        var document = CreateDocument();
        var dispatches = new List<Action>();
        var adapter = new MameSegmentRuntimeAdapter(() => [document], action => dispatches.Add(action));

        adapter.ApplySegmentState(0, 1, MameSegmentOutputType.Vfd);
        adapter.ApplySegmentState(1, 2, MameSegmentOutputType.Vfd);
        adapter.ApplyVfdBrightness(0, 0.25d);

        var dispatch = Assert.Single(dispatches);
        dispatch();

        var brightness = document.RuntimeState.GetSegmentCellBrightness("alpha-0", 16);
        Assert.Equal(16, brightness.Length);
        Assert.All(brightness, value => Assert.Equal(0.25d, value));
    }

    [Fact]
    public void ApplySegmentState_ReversesImpactAlphaCellOrderBeforeRendering()
    {
        var document = CreateDocument();
        var dispatches = new List<Action>();
        var adapter = new MameSegmentRuntimeAdapter(
            () => [document],
            action => dispatches.Add(action),
            () => FruitMachinePlatformType.Impact);

        adapter.ApplySegmentState(0, 2, MameSegmentOutputType.Vfd);
        adapter.ApplySegmentState(15, 1, MameSegmentOutputType.Vfd);

        var dispatch = Assert.Single(dispatches);
        dispatch();

        var masks = document.RuntimeState.GetSegmentCellMasks("alpha-0", 16);
        Assert.Equal(3, masks[0]);
        Assert.Equal(4, masks[15]);
    }

    [Fact]
    public void ApplySegmentState_UpdatesFaceSevenSegmentThroughMachineReference()
    {
        var document = CreateDocument();
        document.SetFaceElements([
            new FaceSevenSegmentDisplayElement
            {
                ObjectId = "face-seven-3",
                LinkedMachineObjectReference = MachineObjectReference.SevenSegmentDisplay(3),
                LinkedPanel2DElementId = "seven-ignored"
            }
        ]);
        var changedFaceIds = new List<string>();
        document.FaceVisualStateChanged += changed => changedFaceIds.AddRange(changed.ObjectIds);
        var dispatches = new List<Action>();
        var adapter = new MameSegmentRuntimeAdapter(() => [document], action => dispatches.Add(action));

        adapter.ApplySegmentState(3, 0x5B, MameSegmentOutputType.Digit);

        var dispatch = Assert.Single(dispatches);
        dispatch();

        var masks = document.RuntimeState.GetSegmentCellMasks(MachineObjectReference.SevenSegmentDisplay(3), 1);
        Assert.Single(masks);
        Assert.Equal(0x5B, masks[0]);
        Assert.Contains("face-seven-3", changedFaceIds);
    }


    [Fact]
    public void ApplySegmentState_NativeAlphaPublishesRawOasisMaskToMachineReference()
    {
        var document = CreateDocument();
        document.SetFaceElements([
            new FaceAlphaDisplayElement
            {
                ObjectId = "face-alpha-0",
                LinkedMachineObjectReference = MachineObjectReference.AlphaDisplay(0)
            }
        ]);
        var changedFaceIds = new List<string>();
        document.FaceVisualStateChanged += changed => changedFaceIds.AddRange(changed.ObjectIds);
        var dispatches = new List<Action>();
        var adapter = new MameSegmentRuntimeAdapter(() => [document], action => dispatches.Add(action));

        adapter.ApplySegmentState(0, 0x8002, MameSegmentOutputType.NativeAlpha);

        var dispatch = Assert.Single(dispatches);
        dispatch();

        var masks = document.RuntimeState.GetSegmentCellMasks(MachineObjectReference.AlphaDisplay(0), 16);
        Assert.Equal(0x8002, masks[0]);
        Assert.Contains("face-alpha-0", changedFaceIds);
    }

    [Fact]
    public void ApplySegmentState_UnchangedNativeAlphaMaskDoesNotNotifyFaceAgain()
    {
        var document = CreateDocument();
        document.SetFaceElements([
            new FaceAlphaDisplayElement
            {
                ObjectId = "face-alpha-0",
                LinkedMachineObjectReference = MachineObjectReference.AlphaDisplay(0)
            }
        ]);
        var notifyCount = 0;
        document.FaceVisualStateChanged += _ => notifyCount++;
        var dispatches = new List<Action>();
        var adapter = new MameSegmentRuntimeAdapter(() => [document], action => dispatches.Add(action));

        adapter.ApplySegmentState(0, 0x1234, MameSegmentOutputType.NativeAlpha);
        Assert.Single(dispatches)();
        adapter.ApplySegmentState(0, 0x1234, MameSegmentOutputType.NativeAlpha);
        Assert.Single(dispatches.Skip(1))();

        Assert.Equal(1, notifyCount);
    }

    private static DocumentTabViewModel CreateDocument()
    {
        var panelDocument = EditorDocument.CreateFromFile("panel.panel2d", "panel", "panel");
        var tab = new DocumentTabViewModel(panelDocument);
        tab.SetPanelElements([
            new PanelElementModel { ObjectId = "alpha-0", Kind = PanelElementKind.Alpha, DisplayNumber = 0 },
            new PanelElementModel { ObjectId = "seven-3", Kind = PanelElementKind.SevenSegment, DisplayNumber = 3 }
        ]);
        return tab;
    }
}
