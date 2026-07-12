using System.Linq;
using System.Windows.Media;
using EditorCommands = OasisEditor.Commands;
using Xunit;

namespace OasisEditor.Tests;

public sealed class InspectorViewModelTests
{
    [Fact]
    public void InspectorSummary_SelectedLamp_IncludesLampNumber()
    {
        var selectedDocument = new DocumentTabViewModel(EditorDocument.CreatePanel2DStub("Panel"));
        selectedDocument.SetPanelElements(
            [
                new PanelElementModel
                {
                    ObjectId = "lamp-1",
                    Name = "Lamp 7",
                    Kind = PanelElementKind.Lamp,
                    X = 10,
                    Y = 20,
                    Width = 30,
                    Height = 40,
                    DisplayNumber = 7
                }
            ]);

        var context = new ActiveDocumentContextService();
        context.SetActiveDocument(selectedDocument);
        context.SetPanelSelection(selectedDocument.DocumentId, new PanelSelectionInfo("lamp-1", "lamp", 10, 20, 30, 40));

        var viewModel = CreateInspectorViewModel(selectedDocument, context);

        Assert.Contains("Selected lamp 'Lamp 7'", viewModel.InspectorSummary);
        Assert.Contains("Lamp number: 7.", viewModel.InspectorSummary);
    }

    [Fact]
    public void InspectorSummary_SelectedReel_IncludesReelNumberAndStops()
    {
        var selectedDocument = new DocumentTabViewModel(EditorDocument.CreatePanel2DStub("Panel"));
        selectedDocument.SetPanelElements(
            [
                new PanelElementModel
                {
                    ObjectId = "reel-1",
                    Name = "Reel 3",
                    Kind = PanelElementKind.Reel,
                    X = 100,
                    Y = 120,
                    Width = 80,
                    Height = 120,
                    DisplayNumber = 3,
                    Stops = 24
                }
            ]);

        var context = new ActiveDocumentContextService();
        context.SetActiveDocument(selectedDocument);
        context.SetPanelSelection(selectedDocument.DocumentId, new PanelSelectionInfo("reel-1", "reel", 100, 120, 80, 120));

        var viewModel = CreateInspectorViewModel(selectedDocument, context);

        Assert.Contains("Selected reel 'Reel 3'", viewModel.InspectorSummary);
        Assert.Contains("Reel number: 3, stops: 24.", viewModel.InspectorSummary);
    }

    [Fact]
    public void InspectorPropertyRows_SelectedLamp_IncludesCommonAndLampFields()
    {
        var selectedDocument = new DocumentTabViewModel(EditorDocument.CreatePanel2DStub("Panel"));
        selectedDocument.SetPanelElements(
            [
                new PanelElementModel
                {
                    ObjectId = "lamp-1",
                    Name = "Lamp 7",
                    Kind = PanelElementKind.Lamp,
                    X = 10,
                    Y = 20,
                    Width = 30,
                    Height = 40,
                    DisplayNumber = 7,
                    OnColorHex = "#FFFFFF",
                    IsLocked = true,
                    IsVisible = false
                }
            ]);

        var context = new ActiveDocumentContextService();
        context.SetActiveDocument(selectedDocument);
        context.SetPanelSelection(selectedDocument.DocumentId, new PanelSelectionInfo("lamp-1", "lamp", 10, 20, 30, 40));

        var viewModel = CreateInspectorViewModel(selectedDocument, context);
        viewModel.NotifyContextChanged();

        Assert.Contains(viewModel.InspectorPropertyRows, row => row.DisplayName == "Name");
        Assert.Contains(viewModel.InspectorPropertyRows, row => row.DisplayName == "Lock Transform");
        Assert.Contains(viewModel.InspectorPropertyRows, row => row.DisplayName == "Visible");
        Assert.Contains(viewModel.InspectorPropertyRows, row => row.DisplayName == "Display Number");
        Assert.Contains(viewModel.InspectorPropertyRows, row => row.DisplayName == "On Color");
        Assert.Contains(viewModel.InspectorPropertyRows, row => row.DisplayName == "Border");
    }



    [Fact]
    public void InspectorPropertyRows_TogglingLampBorder_PersistsThroughCommandAndRefresh()
    {
        var selectedDocument = new DocumentTabViewModel(EditorDocument.CreatePanel2DStub("Panel"));
        selectedDocument.SetPanelElements(
            [
                new PanelElementModel
                {
                    ObjectId = "lamp-1",
                    Name = "Lamp",
                    Kind = PanelElementKind.Lamp,
                    X = 10,
                    Y = 20,
                    Width = 30,
                    Height = 40,
                    HasBorder = true
                }
            ]);

        var context = new ActiveDocumentContextService();
        context.SetActiveDocument(selectedDocument);
        context.SetPanelSelection(selectedDocument.DocumentId, new PanelSelectionInfo("lamp-1", "lamp", 10, 20, 30, 40));

        var viewModel = CreateInspectorViewModel(selectedDocument, context, ExecuteImmediately);
        viewModel.NotifyContextChanged();
        var row = Assert.IsType<InspectorBoolPropertyViewModel>(viewModel.InspectorPropertyRows.Single(x => x.DisplayName == "Border"));

        row.Value = false;
        viewModel.NotifyContextChanged();

        Assert.False(selectedDocument.GetPanelElements().Single().HasBorder);
        Assert.False(row.Value);
    }

    [Fact]
    public void InspectorPropertyRows_SelectedAlpha_IncludesEditableFieldsEvenWhenUnset()
    {
        var selectedDocument = new DocumentTabViewModel(EditorDocument.CreatePanel2DStub("Panel"));
        selectedDocument.SetPanelElements(
            [
                new PanelElementModel
                {
                    ObjectId = "alpha-1",
                    Name = "Alpha",
                    Kind = PanelElementKind.Alpha,
                    X = 10,
                    Y = 20,
                    Width = 30,
                    Height = 40
                }
            ]);

        var context = new ActiveDocumentContextService();
        context.SetActiveDocument(selectedDocument);
        context.SetPanelSelection(selectedDocument.DocumentId, new PanelSelectionInfo("alpha-1", "alpha", 10, 20, 30, 40));

        var viewModel = CreateInspectorViewModel(selectedDocument, context, ExecuteImmediately);
        viewModel.NotifyContextChanged();

        Assert.Contains(viewModel.InspectorPropertyRows, row => row.DisplayName == "On Color");
        Assert.Contains(viewModel.InspectorPropertyRows, row => row.DisplayName == "Text Color");
        Assert.Contains(viewModel.InspectorPropertyRows, row => row.DisplayName == "Display Text");
        Assert.Contains(viewModel.InspectorPropertyRows, row => row.DisplayName == "Reversed");
    }

    [Fact]
    public void InspectorPropertyRows_SelectedLamp_IncludesDisplayNumberWhenUnset()
    {
        var selectedDocument = new DocumentTabViewModel(EditorDocument.CreatePanel2DStub("Panel"));
        selectedDocument.SetPanelElements(
            [
                new PanelElementModel
                {
                    ObjectId = "lamp-1",
                    Name = "Lamp",
                    Kind = PanelElementKind.Lamp,
                    X = 10,
                    Y = 20,
                    Width = 30,
                    Height = 40
                }
            ]);

        var context = new ActiveDocumentContextService();
        context.SetActiveDocument(selectedDocument);
        context.SetPanelSelection(selectedDocument.DocumentId, new PanelSelectionInfo("lamp-1", "lamp", 10, 20, 30, 40));

        var viewModel = CreateInspectorViewModel(selectedDocument, context, ExecuteImmediately);
        viewModel.NotifyContextChanged();

        Assert.Contains(viewModel.InspectorPropertyRows, row => row.DisplayName == "Display Number");
    }


    [Fact]
    public void InspectorPropertyRows_SelectedLabel_IncludesAndEditsTextColorFontAndLampNumber()
    {
        var selectedDocument = new DocumentTabViewModel(EditorDocument.CreatePanel2DStub("Panel"));
        selectedDocument.SetPanelElements(
            [
                new PanelElementModel
                {
                    ObjectId = "label-1",
                    Name = "Label",
                    Kind = PanelElementKind.Label,
                    X = 10,
                    Y = 20,
                    Width = 30,
                    Height = 40,
                    DisplayText = "OLD",
                    TextColorHex = "#FFFFFFFF",
                    TextBoxFontName = "Tahoma",
                    TextBoxFontStyle = "Regular",
                    TextBoxFontSize = "8",
                    LampNumber = 4
                }
            ]);

        var context = new ActiveDocumentContextService();
        context.SetActiveDocument(selectedDocument);
        context.SetPanelSelection(selectedDocument.DocumentId, new PanelSelectionInfo("label-1", "label", 10, 20, 30, 40));
        var viewModel = CreateInspectorViewModel(selectedDocument, context, ExecuteImmediately);
        viewModel.NotifyContextChanged();

        Assert.Contains(viewModel.InspectorPropertyRows, row => row.DisplayName == "Display Text");
        Assert.Contains(viewModel.InspectorPropertyRows, row => row.DisplayName == "Text Color");
        Assert.Contains(viewModel.InspectorPropertyRows, row => row.DisplayName == "Lamp Number");
        Assert.Contains(viewModel.InspectorPropertyRows, row => row.DisplayName == "Text Font Name");
        Assert.Contains(viewModel.InspectorPropertyRows, row => row.DisplayName == "Text Font Style");
        Assert.Contains(viewModel.InspectorPropertyRows, row => row.DisplayName == "Text Font Size");

        var textRow = Assert.IsType<InspectorTextPropertyViewModel>(viewModel.InspectorPropertyRows.Single(x => x.DisplayName == "Display Text"));
        textRow.Value = "COLLECT";
        textRow.Commit();

        var lampRow = Assert.IsType<InspectorIntPropertyViewModel>(viewModel.InspectorPropertyRows.Single(x => x.DisplayName == "Lamp Number"));
        lampRow.Value = "0";
        lampRow.Commit();
        Assert.Equal("COLLECT", selectedDocument.GetPanelElements().Single().DisplayText);
        Assert.Equal(0, selectedDocument.GetPanelElements().Single().LampNumber);

        lampRow.Value = string.Empty;
        lampRow.Commit();
        Assert.Null(selectedDocument.GetPanelElements().Single().LampNumber);
    }

    [Fact]
    public void InspectorPropertyRows_NoSelection_AreEmpty()
    {
        var selectedDocument = new DocumentTabViewModel(EditorDocument.CreatePanel2DStub("Panel"));
        var context = new ActiveDocumentContextService();
        context.SetActiveDocument(selectedDocument);

        var viewModel = CreateInspectorViewModel(selectedDocument, context);
        viewModel.NotifyContextChanged();

        Assert.Empty(viewModel.InspectorPropertyRows);
    }

    [Fact]
    public void InspectorPropertyRows_EditName_CommitsThroughCanvasCommand()
    {
        var selectedDocument = new DocumentTabViewModel(EditorDocument.CreatePanel2DStub("Panel"));
        selectedDocument.SetPanelElements(
            [
                new PanelElementModel
                {
                    ObjectId = "rect-1",
                    Name = "Original",
                    Kind = PanelElementKind.Rectangle,
                    X = 10,
                    Y = 20,
                    Width = 30,
                    Height = 40
                }
            ]);

        var context = new ActiveDocumentContextService();
        context.SetActiveDocument(selectedDocument);
        context.SetPanelSelection(selectedDocument.DocumentId, new PanelSelectionInfo("rect-1", "rectangle", 10, 20, 30, 40));
        var viewModel = CreateInspectorViewModel(selectedDocument, context, ExecuteImmediately);
        viewModel.NotifyContextChanged();

        var row = Assert.IsType<InspectorTextPropertyViewModel>(viewModel.InspectorPropertyRows.Single(x => x.DisplayName == "Name"));
        row.Value = "Renamed";
        row.Commit();

        Assert.Equal("Renamed", selectedDocument.GetPanelElements().Single().Name);
    }


    [Fact]
    public void InspectorPropertyRows_EditOnColor_CommitsThroughCanvasCommand()
    {
        var selectedDocument = new DocumentTabViewModel(EditorDocument.CreatePanel2DStub("Panel"));
        selectedDocument.SetPanelElements(
            [
                new PanelElementModel
                {
                    ObjectId = "lamp-1",
                    Name = "Lamp 7",
                    Kind = PanelElementKind.Lamp,
                    X = 10,
                    Y = 20,
                    Width = 30,
                    Height = 40,
                    OnColorHex = "#FFFFFF"
                }
            ]);

        var context = new ActiveDocumentContextService();
        context.SetActiveDocument(selectedDocument);
        context.SetPanelSelection(selectedDocument.DocumentId, new PanelSelectionInfo("lamp-1", "lamp", 10, 20, 30, 40));
        var viewModel = CreateInspectorViewModel(selectedDocument, context, ExecuteImmediately);
        viewModel.NotifyContextChanged();

        var row = Assert.IsType<InspectorColorPropertyViewModel>(viewModel.InspectorPropertyRows.Single(x => x.DisplayName == "On Color"));
        row.HexValue = "#00FF00";
        row.Commit();

        Assert.Equal("#00FF00", selectedDocument.GetPanelElements().Single().OnColorHex);
    }

    [Fact]
    public void InspectorPropertyRows_EditReversed_CommitsThroughCanvasCommand()
    {
        var selectedDocument = new DocumentTabViewModel(EditorDocument.CreatePanel2DStub("Panel"));
        selectedDocument.SetPanelElements(
            [
                new PanelElementModel
                {
                    ObjectId = "alpha-1",
                    Name = "Alpha",
                    Kind = PanelElementKind.Alpha,
                    X = 10,
                    Y = 20,
                    Width = 30,
                    Height = 40,
                    IsReversed = false
                }
            ]);

        var context = new ActiveDocumentContextService();
        context.SetActiveDocument(selectedDocument);
        context.SetPanelSelection(selectedDocument.DocumentId, new PanelSelectionInfo("alpha-1", "alpha", 10, 20, 30, 40));
        var viewModel = CreateInspectorViewModel(selectedDocument, context, ExecuteImmediately);
        viewModel.NotifyContextChanged();

        var row = Assert.IsType<InspectorBoolPropertyViewModel>(viewModel.InspectorPropertyRows.Single(x => x.DisplayName == "Reversed"));
        row.Value = true;

        Assert.True(selectedDocument.GetPanelElements().Single().IsReversed);
    }

    [Fact]
    public void InspectorPropertyRows_WidthRejectsNonPositiveValue()
    {
        var selectedDocument = new DocumentTabViewModel(EditorDocument.CreatePanel2DStub("Panel"));
        selectedDocument.SetPanelElements(
            [
                new PanelElementModel
                {
                    ObjectId = "rect-1",
                    Name = "Original",
                    Kind = PanelElementKind.Rectangle,
                    X = 10,
                    Y = 20,
                    Width = 30,
                    Height = 40
                }
            ]);

        var context = new ActiveDocumentContextService();
        context.SetActiveDocument(selectedDocument);
        context.SetPanelSelection(selectedDocument.DocumentId, new PanelSelectionInfo("rect-1", "rectangle", 10, 20, 30, 40));
        var viewModel = CreateInspectorViewModel(selectedDocument, context, ExecuteImmediately);
        viewModel.NotifyContextChanged();

        var row = Assert.IsType<InspectorDoublePropertyViewModel>(viewModel.InspectorPropertyRows.Single(x => x.DisplayName == "Width"));
        row.Value = "0";
        row.Commit();

        Assert.Equal("Width must be greater than zero.", row.ErrorText);
        Assert.Equal(30, selectedDocument.GetPanelElements().Single().Width);
    }


    [Fact]
    public void InspectorPropertyRows_ColorFields_UseColorRows()
    {
        var selectedDocument = new DocumentTabViewModel(EditorDocument.CreatePanel2DStub("Panel"));
        selectedDocument.SetPanelElements(
            [
                new PanelElementModel
                {
                    ObjectId = "lamp-1",
                    Name = "Lamp",
                    Kind = PanelElementKind.Lamp,
                    X = 10,
                    Y = 20,
                    Width = 30,
                    Height = 40,
                    OnColorHex = "#00FF00",
                    OffColorHex = "#000000",
                    TextColorHex = "#FFFFFF"
                }
            ]);

        var context = new ActiveDocumentContextService();
        context.SetActiveDocument(selectedDocument);
        context.SetPanelSelection(selectedDocument.DocumentId, new PanelSelectionInfo("lamp-1", "lamp", 10, 20, 30, 40));
        var viewModel = CreateInspectorViewModel(selectedDocument, context, ExecuteImmediately);
        viewModel.NotifyContextChanged();

        Assert.IsType<InspectorColorPropertyViewModel>(viewModel.InspectorPropertyRows.Single(x => x.DisplayName == "On Color"));
        Assert.IsType<InspectorColorPropertyViewModel>(viewModel.InspectorPropertyRows.Single(x => x.DisplayName == "Off Color"));
        Assert.IsType<InspectorColorPropertyViewModel>(viewModel.InspectorPropertyRows.Single(x => x.DisplayName == "Text Color"));
    }


    [Fact]
    public void InspectorPropertyRows_SelectedLamp_InitializesOffColorStringAndPicker()
    {
        var selectedDocument = new DocumentTabViewModel(EditorDocument.CreatePanel2DStub("Panel"));
        selectedDocument.SetPanelElements(
            [
                new PanelElementModel
                {
                    ObjectId = "lamp-1",
                    Name = "Lamp",
                    Kind = PanelElementKind.Lamp,
                    X = 10,
                    Y = 20,
                    Width = 30,
                    Height = 40,
                    OffColorHex = "#FF204060"
                }
            ]);

        var context = new ActiveDocumentContextService();
        context.SetActiveDocument(selectedDocument);
        context.SetPanelSelection(selectedDocument.DocumentId, new PanelSelectionInfo("lamp-1", "lamp", 10, 20, 30, 40));
        var viewModel = CreateInspectorViewModel(selectedDocument, context, ExecuteImmediately);
        viewModel.NotifyContextChanged();

        var row = Assert.IsType<InspectorColorPropertyViewModel>(viewModel.InspectorPropertyRows.Single(x => x.DisplayName == "Off Color"));
        Assert.Equal("#204060", row.HexValue);
        Assert.Equal(255, row.SelectedColor.A);
        Assert.Equal(32, row.SelectedColor.R);
        Assert.Equal(64, row.SelectedColor.G);
        Assert.Equal(96, row.SelectedColor.B);

        row.SelectedColor = Color.FromArgb(255, 1, 2, 3);

        Assert.Equal("#010203", row.HexValue);
        Assert.Equal("#010203", selectedDocument.GetPanelElements().Single().OffColorHex);
    }

    [Fact]
    public void InspectorPropertyRows_SelectedLamp_WithUnsetOffColor_DocumentsBlankOptionalColor()
    {
        var selectedDocument = new DocumentTabViewModel(EditorDocument.CreatePanel2DStub("Panel"));
        selectedDocument.SetPanelElements(
            [
                new PanelElementModel
                {
                    ObjectId = "lamp-1",
                    Name = "Lamp",
                    Kind = PanelElementKind.Lamp,
                    X = 10,
                    Y = 20,
                    Width = 30,
                    Height = 40
                }
            ]);

        var context = new ActiveDocumentContextService();
        context.SetActiveDocument(selectedDocument);
        context.SetPanelSelection(selectedDocument.DocumentId, new PanelSelectionInfo("lamp-1", "lamp", 10, 20, 30, 40));
        var viewModel = CreateInspectorViewModel(selectedDocument, context, ExecuteImmediately);
        viewModel.NotifyContextChanged();

        var row = Assert.IsType<InspectorColorPropertyViewModel>(viewModel.InspectorPropertyRows.Single(x => x.DisplayName == "Off Color"));
        Assert.Equal(string.Empty, row.HexValue);
        Assert.Equal(Colors.White, row.SelectedColor);
    }

    private static InspectorViewModel CreateInspectorViewModel(
        DocumentTabViewModel selectedDocument,
        ActiveDocumentContextService context,
        Func<Guid, EditorCommands.ICommand, bool>? executeCanvasCommand = null)
    {
        return new InspectorViewModel(
            selectedAssetAccessor: () => null,
            selectedDocumentAccessor: () => selectedDocument,
            loadedProjectAccessor: () => null,
            activeDocumentContext: context,
            executeCanvasCommand: executeCanvasCommand ?? ((_, _) => true),
            applySummary: (document, summary) => document);
    }

    private static bool ExecuteImmediately(Guid _, EditorCommands.ICommand command)
    {
        command.Execute();
        return command is not EditorCommands.IExecutionTrackedCommand tracked || tracked.WasExecuted;
    }
}
