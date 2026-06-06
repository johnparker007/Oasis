using System.Linq;
using EditorCommands = OasisEditor.Commands;
using Xunit;

namespace OasisEditor.Tests;

public sealed class FaceEditViewModelTests
{
    [Fact]
    public void AddLampWindowCommand_AddsSelectsAndPersistsFaceElement()
    {
        var document = new DocumentTabViewModel(EditorDocument.CreateFaceStub("Face"));
        var element = new FaceLampWindowElement
        {
            ObjectId = "face-lamp-1",
            Name = "Start Lamp Window",
            X = 10,
            Y = 20,
            Width = 80,
            Height = 40,
            LinkedMachineObjectReference = MachineObjectReference.Lamp(17)
        };

        var command = FaceMutationCommands.CreateAddLampWindowCommand(document.DocumentId, document, element);
        command.Execute();

        var added = Assert.Single(document.GetFaceElements());
        Assert.Equal("face-lamp-1", added.ObjectId);
        Assert.Equal("lamp:17", added.LinkedMachineObjectReference?.ToString());
        Assert.Equal("face-lamp-1", document.HierarchySelectedPanelSelection?.ObjectId);
        Assert.True(document.IsDirty);

        var savedJson = document.GetFaceDocumentJson();
        Assert.True(FaceDocumentStorage.TryRead(savedJson, out var saved));
        var savedElement = Assert.Single(saved.Elements!);
        Assert.Equal("lampWindow", savedElement.Kind);
        Assert.Equal("lamp:17", savedElement.LinkedMachineObjectReference);
    }

    [Fact]
    public void InspectorRows_SelectedFaceLampWindow_EditMachineReferencePersists()
    {
        var selectedDocument = new DocumentTabViewModel(EditorDocument.CreateFaceStub("Face"));
        selectedDocument.SetFaceElements(
            [
                new FaceLampWindowElement
                {
                    ObjectId = "face-lamp-1",
                    Name = "Lamp Window",
                    X = 10,
                    Y = 20,
                    Width = 80,
                    Height = 40
                }
            ]);

        var context = new ActiveDocumentContextService();
        context.SetActiveDocument(selectedDocument);
        context.SetPanelSelection(selectedDocument.DocumentId, new PanelSelectionInfo("face-lamp-1", "lampWindow", 10, 20, 80, 40));
        var viewModel = CreateInspectorViewModel(selectedDocument, context, ExecuteImmediately);
        viewModel.NotifyContextChanged();

        Assert.Contains("Selected face lamp window", viewModel.InspectorSummary);
        Assert.Contains(viewModel.InspectorPropertyRows, row => row.DisplayName == "Machine Reference");
        var row = Assert.IsType<InspectorTextPropertyViewModel>(viewModel.InspectorPropertyRows.Single(x => x.DisplayName == "Machine Reference"));
        row.Value = "lamp:42";
        row.Commit();

        var updated = Assert.Single(selectedDocument.GetFaceElements());
        Assert.Equal("lamp:42", updated.LinkedMachineObjectReference?.ToString());
        Assert.True(FaceDocumentStorage.TryRead(selectedDocument.GetFaceDocumentJson(), out var saved));
        Assert.Equal("lamp:42", Assert.Single(saved.Elements!).LinkedMachineObjectReference);
    }

    [Fact]
    public void FaceHierarchyProvider_BuildsLampWindowGroup()
    {
        var document = new DocumentTabViewModel(EditorDocument.CreateFaceStub("Face"));
        document.SetFaceElements(
            [
                new FaceLampWindowElement
                {
                    ObjectId = "face-lamp-1",
                    Name = "Lamp Window",
                    X = 10,
                    Y = 20,
                    Width = 80,
                    Height = 40,
                    LinkedMachineObjectReference = MachineObjectReference.Lamp(1)
                }
            ]);

        var provider = new FaceHierarchyProvider();
        var root = Assert.Single(provider.Build(document));
        Assert.Equal("Lamp Windows (1)", root.DisplayName);
        var child = Assert.Single(root.Children);
        Assert.Equal("face-lamp-1", child.PanelSelection?.ObjectId);
        Assert.Contains("lamp:1", child.DisplayName);
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
