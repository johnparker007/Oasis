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
