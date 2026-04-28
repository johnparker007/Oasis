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

    private static InspectorViewModel CreateInspectorViewModel(
        DocumentTabViewModel selectedDocument,
        ActiveDocumentContextService context)
    {
        return new InspectorViewModel(
            selectedAssetAccessor: () => null,
            selectedDocumentAccessor: () => selectedDocument,
            loadedProjectAccessor: () => null,
            activeDocumentContext: context,
            applySummary: (document, summary) => document);
    }
}
