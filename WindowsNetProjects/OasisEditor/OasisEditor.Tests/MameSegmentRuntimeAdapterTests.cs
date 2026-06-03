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
    public void ApplyVfdDotMatrixDotState_UpdatesDotMatrixElements()
    {
        var document = CreateDocument();
        var dispatches = new List<Action>();
        var adapter = new MameSegmentRuntimeAdapter(() => [document], action => dispatches.Add(action));

        adapter.ApplyVfdDotMatrixDotState(0, true);
        adapter.ApplyVfdDotMatrixDotState(1, false);
        adapter.ApplyVfdDotMatrixDotState(767, true);

        var dispatch = Assert.Single(dispatches);
        dispatch();

        var dots = document.RuntimeState.GetVfdDotMatrixDots("vfd-dot-0", MameVfdDotMatrixStateParser.DotCount);
        Assert.Equal(MameVfdDotMatrixStateParser.DotCount, dots.Length);
        Assert.True(dots[0]);
        Assert.False(dots[1]);
        Assert.True(dots[767]);
    }

    private static DocumentTabViewModel CreateDocument()
    {
        var panelDocument = EditorDocument.CreateFromFile("panel.panel2d", "panel", "panel");
        var tab = new DocumentTabViewModel(panelDocument);
        tab.SetPanelElements([
            new PanelElementModel { ObjectId = "alpha-0", Kind = PanelElementKind.Alpha, DisplayNumber = 0 },
            new PanelElementModel { ObjectId = "seven-3", Kind = PanelElementKind.SevenSegment, DisplayNumber = 3 },
            new PanelElementModel { ObjectId = "vfd-dot-0", Kind = PanelElementKind.VfdDotMatrix }
        ]);
        return tab;
    }
}
