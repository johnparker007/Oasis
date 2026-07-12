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


    [Fact]
    public void PanelMultiEdit_GeometryOnlyPanelChange_RefreshesAggregateRowsInPlace()
    {
        var document = CreatePanelDocument(
            new PanelElementModel { ObjectId = "lamp-1", Name = "A", Kind = PanelElementKind.Lamp, X = 1, Y = 2, Width = 10, Height = 10 },
            new PanelElementModel { ObjectId = "lamp-2", Name = "B", Kind = PanelElementKind.Lamp, X = 1, Y = 2, Width = 10, Height = 10 });
        document.SelectionState.Replace(
            [new EditorSelectionItem(EditorSelectionDomain.PanelElement, "lamp-1"), new EditorSelectionItem(EditorSelectionDomain.PanelElement, "lamp-2")]);
        var viewModel = CreateInspectorViewModel(document);
        viewModel.NotifyContextChanged();
        var originalRows = viewModel.InspectorPropertyRows.ToArray();

        document.SetPanelElements(
            [
                new PanelElementModel { ObjectId = "lamp-1", Name = "A", Kind = PanelElementKind.Lamp, X = 5, Y = 7, Width = 10, Height = 10 },
                new PanelElementModel { ObjectId = "lamp-2", Name = "B", Kind = PanelElementKind.Lamp, X = 5, Y = 7, Width = 10, Height = 10 }
            ]);
        viewModel.NotifyPanelChanged(CreatePanelChange(document, PanelChangeProperties.Geometry));

        Assert.Same(originalRows[0], viewModel.InspectorPropertyRows[0]);
        Assert.True(originalRows.SequenceEqual(viewModel.InspectorPropertyRows));
        Assert.Equal("5", Assert.IsType<InspectorDoublePropertyViewModel>(viewModel.InspectorPropertyRows.Single(row => row.DisplayName == "X")).Value);
        Assert.Equal("7", Assert.IsType<InspectorDoublePropertyViewModel>(viewModel.InspectorPropertyRows.Single(row => row.DisplayName == "Y")).Value);
        Assert.Contains("2 Panel2D components selected", viewModel.InspectorSummary);
    }

    [Fact]
    public void PanelMultiEdit_GeometryOnlyPanelChange_UpdatesMixedStatesInPlace()
    {
        var document = CreatePanelDocument(
            new PanelElementModel { ObjectId = "lamp-1", Name = "A", Kind = PanelElementKind.Lamp, X = 1, Y = 2, Width = 10, Height = 10 },
            new PanelElementModel { ObjectId = "lamp-2", Name = "B", Kind = PanelElementKind.Lamp, X = 2, Y = 2, Width = 10, Height = 10 });
        document.SelectionState.Replace(
            [new EditorSelectionItem(EditorSelectionDomain.PanelElement, "lamp-1"), new EditorSelectionItem(EditorSelectionDomain.PanelElement, "lamp-2")]);
        var viewModel = CreateInspectorViewModel(document);
        viewModel.NotifyContextChanged();
        var originalRows = viewModel.InspectorPropertyRows.ToArray();
        var xRow = Assert.IsType<InspectorDoublePropertyViewModel>(viewModel.InspectorPropertyRows.Single(row => row.DisplayName == "X"));
        var yRow = Assert.IsType<InspectorDoublePropertyViewModel>(viewModel.InspectorPropertyRows.Single(row => row.DisplayName == "Y"));
        Assert.True(xRow.IsMixed);
        Assert.False(yRow.IsMixed);

        document.SetPanelElements(
            [
                new PanelElementModel { ObjectId = "lamp-1", Name = "A", Kind = PanelElementKind.Lamp, X = 4, Y = 2, Width = 10, Height = 10 },
                new PanelElementModel { ObjectId = "lamp-2", Name = "B", Kind = PanelElementKind.Lamp, X = 4, Y = 3, Width = 10, Height = 10 }
            ]);
        viewModel.NotifyPanelChanged(CreatePanelChange(document, PanelChangeProperties.Geometry));

        Assert.True(originalRows.SequenceEqual(viewModel.InspectorPropertyRows));
        Assert.False(xRow.IsMixed);
        Assert.Equal("4", xRow.Value);
        Assert.True(yRow.IsMixed);
    }

    [Fact]
    public void PanelMultiEdit_RepeatedGeometryOnlyPanelChanges_RetainAggregateRowInstances()
    {
        var elements = Enumerable.Range(0, 50)
            .Select(index => new PanelElementModel { ObjectId = $"lamp-{index}", Name = $"Lamp {index}", Kind = PanelElementKind.Lamp, X = 0, Y = 0, Width = 10, Height = 10 })
            .ToArray();
        var document = CreatePanelDocument(elements);
        document.SelectionState.Replace(elements.Select(element => new EditorSelectionItem(EditorSelectionDomain.PanelElement, element.ObjectId)).ToArray());
        var viewModel = CreateInspectorViewModel(document);
        viewModel.NotifyContextChanged();
        var originalRows = viewModel.InspectorPropertyRows.ToArray();

        for (var step = 1; step <= 5; step++)
        {
            document.SetPanelElements(elements.Select(element => new PanelElementModel { ObjectId = element.ObjectId, Name = element.Name, Kind = element.Kind, X = step, Y = step, Width = 10, Height = 10 }).ToArray());
            viewModel.NotifyPanelChanged(CreatePanelChange(document, PanelChangeProperties.Geometry));
        }

        Assert.True(originalRows.SequenceEqual(viewModel.InspectorPropertyRows));
        Assert.Equal("5", Assert.IsType<InspectorDoublePropertyViewModel>(viewModel.InspectorPropertyRows.Single(row => row.DisplayName == "X")).Value);
    }

    [Fact]
    public void PanelMultiEdit_StructuralPanelChange_RebuildsAggregateRows()
    {
        var document = CreatePanelDocument(
            new PanelElementModel { ObjectId = "lamp-1", Name = "A", Kind = PanelElementKind.Lamp, X = 1, Y = 2, Width = 10, Height = 10 },
            new PanelElementModel { ObjectId = "lamp-2", Name = "B", Kind = PanelElementKind.Lamp, X = 1, Y = 2, Width = 10, Height = 10 });
        document.SelectionState.Replace(
            [new EditorSelectionItem(EditorSelectionDomain.PanelElement, "lamp-1"), new EditorSelectionItem(EditorSelectionDomain.PanelElement, "lamp-2")]);
        var viewModel = CreateInspectorViewModel(document);
        viewModel.NotifyContextChanged();
        var firstRow = viewModel.InspectorPropertyRows[0];

        viewModel.NotifyPanelChanged(CreatePanelChange(document, PanelChangeProperties.Structure));

        Assert.NotSame(firstRow, viewModel.InspectorPropertyRows[0]);
    }

    [Fact]
    public void PanelMultiEdit_SelectionMembershipChange_RebuildsAggregateRows()
    {
        var document = CreatePanelDocument(
            new PanelElementModel { ObjectId = "lamp-1", Name = "A", Kind = PanelElementKind.Lamp, X = 1, Y = 2, Width = 10, Height = 10 },
            new PanelElementModel { ObjectId = "lamp-2", Name = "B", Kind = PanelElementKind.Lamp, X = 1, Y = 2, Width = 10, Height = 10 },
            new PanelElementModel { ObjectId = "lamp-3", Name = "C", Kind = PanelElementKind.Lamp, X = 1, Y = 2, Width = 10, Height = 10 });
        document.SelectionState.Replace(
            [new EditorSelectionItem(EditorSelectionDomain.PanelElement, "lamp-1"), new EditorSelectionItem(EditorSelectionDomain.PanelElement, "lamp-2")]);
        var viewModel = CreateInspectorViewModel(document);
        viewModel.NotifyContextChanged();
        var firstRow = viewModel.InspectorPropertyRows[0];

        document.SelectionState.Replace(
            [new EditorSelectionItem(EditorSelectionDomain.PanelElement, "lamp-1"), new EditorSelectionItem(EditorSelectionDomain.PanelElement, "lamp-2"), new EditorSelectionItem(EditorSelectionDomain.PanelElement, "lamp-3")]);
        viewModel.NotifyContextChanged();

        Assert.NotSame(firstRow, viewModel.InspectorPropertyRows[0]);
        Assert.Contains("3 Panel2D components selected", viewModel.InspectorSummary);
    }

    [Fact]
    public void PanelMultiEdit_UnsupportedMixedSelection_RemainsReadOnlyAfterPanelChange()
    {
        var document = CreatePanelDocument(
            new PanelElementModel { ObjectId = "lamp-1", Name = "A", Kind = PanelElementKind.Lamp, X = 1, Y = 2, Width = 10, Height = 10 });
        document.SelectionState.Replace(
            [new EditorSelectionItem(EditorSelectionDomain.PanelElement, "lamp-1"), new EditorSelectionItem(EditorSelectionDomain.FaceElement, "face-1")]);
        var viewModel = CreateInspectorViewModel(document);
        viewModel.NotifyContextChanged();

        viewModel.NotifyPanelChanged(CreatePanelChange(document, PanelChangeProperties.Geometry));

        Assert.All(viewModel.InspectorPropertyRows, row => Assert.True(row.IsReadOnly));
        Assert.Contains("cannot be edited together", Assert.IsType<InspectorInfoPropertyViewModel>(viewModel.InspectorPropertyRows[0]).Value);
    }

    private static PanelChangeEvent CreatePanelChange(DocumentTabViewModel document, PanelChangeProperties properties)
    {
        return new PanelChangeEvent(document.DocumentId, null, properties, AffectsCanvas: true, AffectsHierarchy: false, AffectsInspectorRows: true, AffectsPersistence: false);
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
