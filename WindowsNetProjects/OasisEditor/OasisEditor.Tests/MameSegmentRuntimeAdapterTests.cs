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

        adapter.ApplySegmentState(0, 1);
        var first = Assert.Single(dispatches);
        first();

        adapter.ApplySegmentState(1, 2);
        var second = Assert.Single(dispatches.Skip(1));
        second();

        var masks = document.RuntimeState.GetSegmentCellMasks("alpha-0", 16);
        Assert.Equal(1, masks[0]);
        Assert.Equal(2, masks[1]);
    }

    [Fact]
    public void ApplySegmentState_CoalescesPendingUpdatesIntoSingleUiDispatch()
    {
        var document = CreateDocument();
        var dispatches = new List<Action>();
        var adapter = new MameSegmentRuntimeAdapter(() => [document], action => dispatches.Add(action));

        adapter.ApplySegmentState(0, 1);
        adapter.ApplySegmentState(0, 3);
        adapter.ApplySegmentState(1, 2);

        var dispatch = Assert.Single(dispatches);
        dispatch();

        var masks = document.RuntimeState.GetSegmentCellMasks("alpha-0", 16);
        Assert.Equal(3, masks[0]);
        Assert.Equal(2, masks[1]);
    }

    [Fact]
    public void ApplySegmentState_UpdatesSevenSegmentFromDigitCell()
    {
        var document = CreateDocument();
        var dispatches = new List<Action>();
        var adapter = new MameSegmentRuntimeAdapter(() => [document], action => dispatches.Add(action));

        adapter.ApplySegmentState(3, 16);

        var dispatch = Assert.Single(dispatches);
        dispatch();

        var masks = document.RuntimeState.GetSegmentCellMasks("seven-3", 1);
        Assert.Single(masks);
        Assert.Equal(16, masks[0]);
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
