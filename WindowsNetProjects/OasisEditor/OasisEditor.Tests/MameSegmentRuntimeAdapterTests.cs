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
    public void ApplySegmentState_ForReversedAlpha_MapsDescendingCellIdsToAscendingVisualCells()
    {
        var panelDocument = EditorDocument.CreateFromFile("panel.panel2d", "panel", "panel");
        var document = new DocumentTabViewModel(panelDocument);
        document.SetPanelElements([
            new PanelElementModel { ObjectId = "alpha-rev", Kind = PanelElementKind.Alpha, DisplayNumber = 10, IsReversed = true }
        ]);

        var dispatches = new List<Action>();
        var adapter = new MameSegmentRuntimeAdapter(() => [document], action => dispatches.Add(action));

        adapter.ApplySegmentState(25, 0xAA);
        adapter.ApplySegmentState(24, 0xBB);
        adapter.ApplySegmentState(10, 0xCC);

        var dispatch = Assert.Single(dispatches);
        dispatch();

        var masks = document.RuntimeState.GetSegmentCellMasks("alpha-rev", 16);
        Assert.Equal(0xAA, masks[0]);
        Assert.Equal(0xBB, masks[1]);
        Assert.Equal(0xCC, masks[15]);
    }

    [Theory]
    [InlineData(1 << 0, 1 << 2)]
    [InlineData(1 << 1, 1 << 3)]
    [InlineData(1 << 2, 1 << 1)]
    [InlineData(1 << 3, 1 << 4)]
    [InlineData(1 << 4, 1 << 7)]
    [InlineData(1 << 5, 1 << 6)]
    [InlineData(1 << 6, 1 << 5)]
    [InlineData(1 << 7, 1 << 0)]
    public void ApplySegmentState_RemapAlphaBitsToGeometryIndices(int sourceMask, int expectedMask)
    {
        var document = CreateDocument();
        var dispatches = new List<Action>();
        var adapter = new MameSegmentRuntimeAdapter(() => [document], action => dispatches.Add(action));

        adapter.ApplySegmentState(0, sourceMask);
        var dispatch = Assert.Single(dispatches);
        dispatch();

        var masks = document.RuntimeState.GetSegmentCellMasks("alpha-0", 16);
        Assert.Equal(expectedMask, masks[0]);
    }

    private static DocumentTabViewModel CreateDocument()
    {
        var panelDocument = EditorDocument.CreateFromFile("panel.panel2d", "panel", "panel");
        var tab = new DocumentTabViewModel(panelDocument);
        tab.SetPanelElements([
            new PanelElementModel { ObjectId = "alpha-0", Kind = PanelElementKind.Alpha, DisplayNumber = 0 }
        ]);
        return tab;
    }
}
