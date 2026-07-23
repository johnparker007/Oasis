using System.Linq;
using System.Windows.Media;
using OasisEditor.Features.CabinetEditor.Models;
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
    public void InspectorPropertyRows_SelectedCabinetDocument_ShowsReachableReelSpecificationEditor()
    {
        var selectedDocument = new DocumentTabViewModel(
            EditorDocument.CreateCabinet3DStub("Cabinet"),
            cabinetDocumentJson: CabinetDocumentStorage.Serialize(new CabinetDocument(2, new CabinetModelReference("cabinet.glb", 1, "Y"), [], CabinetPreviewSettings.Default, [], null)));
        var context = new ActiveDocumentContextService();
        context.SetActiveDocument(selectedDocument);
        var viewModel = CreateInspectorViewModel(selectedDocument, context, ExecuteImmediately);

        viewModel.NotifyContextChanged();

        var addRow = Assert.IsType<InspectorActionPropertyViewModel>(viewModel.InspectorPropertyRows.Single(row => row.DisplayName == "Add Reel Specification"));
        Assert.Contains(viewModel.InspectorPropertyRows, row => row.DisplayName == "Default Reel Specification" && row.GroupName == "Reel Specifications");

        addRow.Command.Execute(null);
        viewModel.NotifyContextChanged();

        Assert.Contains(viewModel.InspectorPropertyRows, row => row.DisplayName == "Name" && row.GroupName.StartsWith("Reel:", StringComparison.Ordinal));
        Assert.Contains(viewModel.InspectorPropertyRows, row => row.DisplayName == "Diameter mm" && row.GroupName.StartsWith("Reel:", StringComparison.Ordinal));
        Assert.Contains(viewModel.InspectorPropertyRows, row => row.DisplayName == "Width mm" && row.GroupName.StartsWith("Reel:", StringComparison.Ordinal));
        Assert.Contains(viewModel.InspectorPropertyRows, row => row.DisplayName == "Delete" && row.GroupName.StartsWith("Reel:", StringComparison.Ordinal));
    }


    [Fact]
    public void InspectorPropertyRows_CabinetReelSpecificationActions_UseProductionStyleCommandHistoryAndAutoRefresh()
    {
        var selectedDocument = new DocumentTabViewModel(
            EditorDocument.CreateCabinet3DStub("Cabinet"),
            cabinetDocumentJson: CabinetDocumentStorage.Serialize(new CabinetDocument(2, new CabinetModelReference("cabinet.glb", 1, "Y"), [], CabinetPreviewSettings.Default, [], null)));
        var context = new ActiveDocumentContextService();
        context.SetActiveDocument(selectedDocument);
        InspectorViewModel? viewModel = null;
        bool ExecuteViaDocumentCommandHistory(Guid documentId, EditorCommands.ICommand command)
        {
            if (selectedDocument.DocumentId != documentId)
            {
                return false;
            }

            selectedDocument.CommandService.Execute(command);
            var executed = command is not EditorCommands.IExecutionTrackedCommand tracked || tracked.WasExecuted;
            if (executed)
            {
                viewModel!.NotifyContextChanged();
            }

            return executed;
        }

        viewModel = CreateInspectorViewModel(selectedDocument, context, ExecuteViaDocumentCommandHistory);
        viewModel.NotifyContextChanged();

        Assert.IsType<InspectorActionPropertyViewModel>(viewModel.InspectorPropertyRows.Single(row => row.DisplayName == "Add Reel Specification")).Command.Execute(null);

        var specification = Assert.Single(selectedDocument.GetCabinetDocument().ReelSpecifications);
        Assert.Equal(specification.Id, selectedDocument.GetCabinetDocument().DefaultReelSpecificationId);
        Assert.Contains(viewModel.InspectorPropertyRows, row => row.DisplayName == "Name" && row.GroupName.StartsWith("Reel:", StringComparison.Ordinal));
        Assert.Contains(viewModel.InspectorPropertyRows, row => row.DisplayName == "Diameter mm" && row.GroupName.StartsWith("Reel:", StringComparison.Ordinal));
        Assert.Contains(viewModel.InspectorPropertyRows, row => row.DisplayName == "Width mm" && row.GroupName.StartsWith("Reel:", StringComparison.Ordinal));
        Assert.Contains(viewModel.InspectorPropertyRows, row => row.DisplayName == "Delete" && row.GroupName.StartsWith("Reel:", StringComparison.Ordinal));

        var nameRow = Assert.IsType<InspectorTextPropertyViewModel>(viewModel.InspectorPropertyRows.Single(row => row.DisplayName == "Name" && row.GroupName.StartsWith("Reel:", StringComparison.Ordinal)));
        nameRow.Value = "Standard 3 Reel";
        Assert.Equal("Standard 3 Reel", Assert.Single(selectedDocument.GetCabinetDocument().ReelSpecifications).Name);

        Assert.True(selectedDocument.CommandService.TryUndo());
        viewModel.NotifyContextChanged();
        Assert.Equal("Reel Specification 1", Assert.Single(selectedDocument.GetCabinetDocument().ReelSpecifications).Name);

        Assert.True(selectedDocument.CommandService.TryUndo());
        viewModel.NotifyContextChanged();
        Assert.Empty(selectedDocument.GetCabinetDocument().ReelSpecifications);
        Assert.DoesNotContain(viewModel.InspectorPropertyRows, row => row.DisplayName == "Name" && row.GroupName.StartsWith("Reel:", StringComparison.Ordinal));

        Assert.True(selectedDocument.CommandService.TryRedo());
        viewModel.NotifyContextChanged();
        Assert.Single(selectedDocument.GetCabinetDocument().ReelSpecifications);
        Assert.Contains(viewModel.InspectorPropertyRows, row => row.DisplayName == "Name" && row.GroupName.StartsWith("Reel:", StringComparison.Ordinal));

        Assert.IsType<InspectorActionPropertyViewModel>(viewModel.InspectorPropertyRows.Single(row => row.DisplayName == "Delete" && row.GroupName.StartsWith("Reel:", StringComparison.Ordinal))).Command.Execute(null);
        Assert.Empty(selectedDocument.GetCabinetDocument().ReelSpecifications);
        Assert.DoesNotContain(viewModel.InspectorPropertyRows, row => row.DisplayName == "Delete" && row.GroupName.StartsWith("Reel:", StringComparison.Ordinal));
    }

    [Fact]
    public void InspectorPropertyRows_SelectedFaceReelWithoutCabinet_ShowsExplanationAndRetainsStoredId()
    {
        var reel = new FaceReelDisplayElement { ObjectId = "reel-1", Name = "Reel", X = 1, Y = 2, Width = 3, Height = 4, IsVisible = true, ReelSpecificationId = "stored-id" };
        var selectedDocument = new DocumentTabViewModel(
            EditorDocument.CreateFaceStub("Face"),
            faceDocumentJson: FaceDocumentStorage.Serialize(new FaceDocumentModel { Title = "Face", Elements = [reel] }));
        var context = new ActiveDocumentContextService();
        context.SetActiveDocument(selectedDocument);
        context.SetPanelSelection(selectedDocument.DocumentId, FaceSelectionService.ToSelectionInfo(reel)!);
        var viewModel = CreateInspectorViewModel(selectedDocument, context, ExecuteImmediately);

        viewModel.NotifyContextChanged();

        Assert.Contains(viewModel.InspectorPropertyRows, row => row.DisplayName == "Reel Specification" && row.GroupName == "Reel Size" && row is InspectorInfoPropertyViewModel info && info.Value == "stored-id");
        Assert.Contains(viewModel.InspectorPropertyRows, row => row.DisplayName == "Cabinet" && row.GroupName == "Reel Size" && row is InspectorInfoPropertyViewModel info && info.Value.Contains("Face is not assigned", StringComparison.Ordinal));
    }

    [Fact]
    public void InspectorPropertyRows_SelectedFaceReelWithCabinet_PopulatesSpecificationChoices()
    {
        var root = Path.Combine(Path.GetTempPath(), $"oasis-inspector-reel-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var cabinetPath = Path.Combine(root, "Assets", "Cabinets", "main.cabinet3d");
            Directory.CreateDirectory(Path.GetDirectoryName(cabinetPath)!);
            var cabinetDocument = new DocumentTabViewModel(
                EditorDocument.CreateFromFile(cabinetPath, "Cabinet"),
                cabinetDocumentJson: CabinetDocumentStorage.Serialize(new CabinetDocument(2, new CabinetModelReference("cabinet.glb", 1, "Y"), [], CabinetPreviewSettings.Default, [new CabinetReelSpecification("standard", "Standard Reel", 210, 50)], "standard")));
            var reel = new FaceReelDisplayElement { ObjectId = "reel-1", Name = "Reel", X = 1, Y = 2, Width = 3, Height = 4, IsVisible = true, ReelSpecificationId = "missing" };
            var faceDocument = new DocumentTabViewModel(
                EditorDocument.CreateFaceStub("Face"),
                faceDocumentJson: FaceDocumentStorage.Serialize(new FaceDocumentModel { Title = "Face", AssignedCabinetAssetPath = "Assets/Cabinets/main.cabinet3d", Elements = [reel] }));
            var project = new EditorProject { Name = "Project", ProjectDirectory = root, ProjectFilePath = Path.Combine(root, "project.oasis"), AssetsDirectory = Path.Combine(root, "Assets"), MachinesDirectory = Path.Combine(root, "Machines"), GeneratedDirectory = Path.Combine(root, "Generated") };
            var context = new ActiveDocumentContextService();
            context.SetActiveDocument(faceDocument);
            context.SetPanelSelection(faceDocument.DocumentId, FaceSelectionService.ToSelectionInfo(reel)!);
            var viewModel = CreateInspectorViewModel(faceDocument, context, ExecuteImmediately, openDocuments: [cabinetDocument, faceDocument], loadedProject: project);

            viewModel.NotifyContextChanged();

            var row = Assert.IsType<InspectorChoicePropertyViewModel>(viewModel.InspectorPropertyRows.Single(row => row.DisplayName == "Reel Specification" && row.GroupName == "Reel Size"));
            Assert.Contains("missing (unresolved)", row.Choices);
            Assert.Contains(row.Choices, choice => choice.Contains("Standard Reel", StringComparison.Ordinal) && choice.Contains("210", StringComparison.Ordinal) && choice.Contains("50", StringComparison.Ordinal));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
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
                    IsTransformLocked = true,
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
        Func<Guid, EditorCommands.ICommand, bool>? executeCanvasCommand = null,
        IReadOnlyList<DocumentTabViewModel>? openDocuments = null,
        EditorProject? loadedProject = null)
    {
        return new InspectorViewModel(
            selectedAssetAccessor: () => null,
            selectedAssetDirectoryAccessor: () => null,
            selectedDocumentAccessor: () => selectedDocument,
            openDocumentsAccessor: () => openDocuments ?? [selectedDocument],
            loadedProjectAccessor: () => loadedProject,
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
