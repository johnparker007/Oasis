using System.Linq;
using EditorCommands = OasisEditor.Commands;
using Xunit;

namespace OasisEditor.Tests;

public sealed class InspectorAggregateValueTests
{
    [Fact]
    public void From_CommonAndMixedValues_ReportsExplicitState()
    {
        Assert.True(InspectorAggregateValue.From(new[] { 10d, 10d }).IsCommon);
        Assert.True(InspectorAggregateValue.From(new[] { 10d, 20d }).IsMixed);
        Assert.True(InspectorAggregateValue.From(new[] { "Lamp", "Lamp" }).IsCommon);
        Assert.True(InspectorAggregateValue.From(new[] { "Lamp", "Other" }).IsMixed);
        Assert.True(InspectorAggregateValue.From(new[] { true, false }).IsMixed);
        Assert.False(InspectorAggregateValue.From(Array.Empty<int>()).IsAvailable);
    }

    [Fact]
    public void SameTypePanelSelection_ExposesTypeSpecificRowsAndMixedState()
    {
        var document = CreatePanelDocument(
            new PanelElementModel { ObjectId = "lamp-1", Name = "A", Kind = PanelElementKind.Lamp, X = 1, Y = 2, Width = 10, Height = 10, DisplayNumber = 7 },
            new PanelElementModel { ObjectId = "lamp-2", Name = "B", Kind = PanelElementKind.Lamp, X = 1, Y = 3, Width = 10, Height = 10, DisplayNumber = 7 });
        document.SelectionState.Replace(
            [new EditorSelectionItem(EditorSelectionDomain.PanelElement, "lamp-1"), new EditorSelectionItem(EditorSelectionDomain.PanelElement, "lamp-2")]);
        var viewModel = CreateInspectorViewModel(document);

        viewModel.NotifyContextChanged();

        Assert.Equal("2 Lamps", viewModel.InspectorTitle);
        Assert.Contains(viewModel.InspectorPropertyRows, row => row.DisplayName == "Name");
        Assert.Contains(viewModel.InspectorPropertyRows, row => row.DisplayName == "Display Number");
        var xRow = Assert.IsType<InspectorDoublePropertyViewModel>(viewModel.InspectorPropertyRows.Single(row => row.DisplayName == "X"));
        var yRow = Assert.IsType<InspectorDoublePropertyViewModel>(viewModel.InspectorPropertyRows.Single(row => row.DisplayName == "Y"));
        Assert.False(xRow.IsMixed);
        Assert.True(yRow.IsMixed);
    }

    [Fact]
    public void MixedTypePanelSelection_ExposesBaseRowsOnly()
    {
        var document = CreatePanelDocument(
            new PanelElementModel { ObjectId = "lamp-1", Name = "A", Kind = PanelElementKind.Lamp, X = 1, Y = 2, Width = 10, Height = 10 },
            new PanelElementModel { ObjectId = "reel-1", Name = "B", Kind = PanelElementKind.Reel, X = 1, Y = 3, Width = 10, Height = 10 });
        document.SelectionState.Replace(
            [new EditorSelectionItem(EditorSelectionDomain.PanelElement, "lamp-1"), new EditorSelectionItem(EditorSelectionDomain.PanelElement, "reel-1")]);
        var viewModel = CreateInspectorViewModel(document);

        viewModel.NotifyContextChanged();

        Assert.Contains(viewModel.InspectorPropertyRows, row => row.DisplayName == "X");
        Assert.Contains(viewModel.InspectorPropertyRows, row => row.DisplayName == "Visible");
        Assert.DoesNotContain(viewModel.InspectorPropertyRows, row => row.DisplayName == "Name");
        Assert.DoesNotContain(viewModel.InspectorPropertyRows, row => row.DisplayName == "Display Number");
    }

    [Fact]
    public void PanelMultiEdit_AssignsAbsoluteValueAndSkipsLockedTransforms()
    {
        var document = CreatePanelDocument(
            new PanelElementModel { ObjectId = "lamp-1", Name = "A", Kind = PanelElementKind.Lamp, X = 1, Y = 2, Width = 10, Height = 10 },
            new PanelElementModel { ObjectId = "lamp-2", Name = "B", Kind = PanelElementKind.Lamp, X = 1, Y = 3, Width = 10, Height = 10, IsTransformLocked = true });
        document.SelectionState.Replace(
            [new EditorSelectionItem(EditorSelectionDomain.PanelElement, "lamp-1"), new EditorSelectionItem(EditorSelectionDomain.PanelElement, "lamp-2")]);
        var viewModel = CreateInspectorViewModel(document, ExecuteImmediately);
        viewModel.NotifyContextChanged();

        var yRow = Assert.IsType<InspectorDoublePropertyViewModel>(viewModel.InspectorPropertyRows.Single(row => row.DisplayName == "Y"));
        yRow.Value = "42";
        yRow.Commit();

        Assert.Equal(42, document.GetPanelElements().Single(element => element.ObjectId == "lamp-1").Y);
        Assert.Equal(3, document.GetPanelElements().Single(element => element.ObjectId == "lamp-2").Y);
    }

    [Fact]
    public void PanelMultiEdit_NonTransformEditAppliesToLockedMembers()
    {
        var document = CreatePanelDocument(
            new PanelElementModel { ObjectId = "lamp-1", Name = "A", Kind = PanelElementKind.Lamp, X = 1, Y = 2, Width = 10, Height = 10 },
            new PanelElementModel { ObjectId = "lamp-2", Name = "B", Kind = PanelElementKind.Lamp, X = 1, Y = 3, Width = 10, Height = 10, IsTransformLocked = true });
        document.SelectionState.Replace(
            [new EditorSelectionItem(EditorSelectionDomain.PanelElement, "lamp-1"), new EditorSelectionItem(EditorSelectionDomain.PanelElement, "lamp-2")]);
        var viewModel = CreateInspectorViewModel(document, ExecuteImmediately);
        viewModel.NotifyContextChanged();

        var visibleRow = Assert.IsType<InspectorBoolPropertyViewModel>(viewModel.InspectorPropertyRows.Single(row => row.DisplayName == "Visible"));
        visibleRow.Value = false;

        Assert.All(document.GetPanelElements(), element => Assert.False(element.IsVisible));
    }

    private static DocumentTabViewModel CreatePanelDocument(params PanelElementModel[] elements)
    {
        var document = new DocumentTabViewModel(EditorDocument.CreatePanel2DStub("Panel"));
        document.SetPanelElements(elements);
        return document;
    }

    private static InspectorViewModel CreateInspectorViewModel(DocumentTabViewModel document, Func<Guid, EditorCommands.ICommand, bool>? executeCanvasCommand = null)
    {
        var context = new ActiveDocumentContextService();
        context.SetActiveDocument(document);
        return new InspectorViewModel(
            selectedAssetAccessor: () => null,
            selectedDocumentAccessor: () => document,
            loadedProjectAccessor: () => null,
            activeDocumentContext: context,
            executeCanvasCommand: executeCanvasCommand ?? ((_, _) => true),
            applySummary: (selectedDocument, _) => selectedDocument);
    }

    private static bool ExecuteImmediately(Guid _, EditorCommands.ICommand command)
    {
        command.Execute();
        return command is not EditorCommands.IExecutionTrackedCommand tracked || tracked.WasExecuted;
    }
}
