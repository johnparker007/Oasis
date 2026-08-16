using System.Linq;
using EditorCommands = OasisEditor.Commands;
using Xunit;

namespace OasisEditor.Tests;

public sealed class FaceEditViewModelTests
{
    [Fact]
    public void ArtworkPrimarySelection_SuppressesOnlyLampWindowsFromViewportPresentation()
    {
        var artwork = new FaceArtworkElement { ObjectId = "art", Name = "Artwork", Width = 100, Height = 100, IsVisible = true };
        var lamp = new FaceLampWindowElement { ObjectId = "lamp", Name = "Lamp", Width = 20, Height = 20, IsVisible = true };
        var reel = new FaceReelDisplayElement { ObjectId = "reel", Name = "Reel", Width = 20, Height = 30, IsVisible = true };
        var document = new DocumentTabViewModel(EditorDocument.CreateFaceStub("Face"));
        document.SetFaceElements([artwork, lamp, reel]);
        var faceBefore = document.GetFaceDocument();

        document.SelectionState.Replace(new EditorSelectionItem(EditorSelectionDomain.FaceElement, artwork.ObjectId));
        var artworkViewport = FaceArtworkEditingPresentation.GetViewportElements(document).ToArray();

        Assert.True(FaceArtworkEditingPresentation.IsArtworkPrimarySelection(document));
        Assert.Contains(artwork, artworkViewport);
        Assert.Contains(reel, artworkViewport);
        Assert.DoesNotContain(lamp, artworkViewport);
        Assert.True(lamp.IsVisible);
        Assert.Same(faceBefore, document.GetFaceDocument());
        Assert.Equal(["art", "lamp", "reel"], document.GetFaceDocument().Elements.Select(element => element.ObjectId));

        document.SelectionState.Replace(new EditorSelectionItem(EditorSelectionDomain.FaceElement, lamp.ObjectId));
        Assert.False(FaceArtworkEditingPresentation.IsArtworkPrimarySelection(document));
        Assert.Contains(lamp, FaceArtworkEditingPresentation.GetViewportElements(document));
        Assert.Equal(lamp.ObjectId, document.SelectionState.PrimaryItem?.ObjectId);
    }

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

    [Fact]
    public void FaceHierarchyProvider_BuildsArtworkAndLampWindowGroupsWhenArtworkExists()
    {
        var document = new DocumentTabViewModel(EditorDocument.CreateFaceStub("Face"));
        document.SetFaceElements(
            [
                new FaceArtworkElement
                {
                    ObjectId = "face-artwork-1",
                    Name = "Glass Artwork",
                    X = 0,
                    Y = 0,
                    Width = 200,
                    Height = 100
                },
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

        var provider = new FaceHierarchyProvider();
        var roots = provider.Build(document);

        Assert.Equal(2, roots.Count);
        Assert.Equal("Artwork (1)", roots[0].DisplayName);
        Assert.Equal("face-artwork-1", Assert.Single(roots[0].Children).PanelSelection?.ObjectId);
        Assert.Equal("Lamp Windows (1)", roots[1].DisplayName);
        Assert.Equal("face-lamp-1", Assert.Single(roots[1].Children).PanelSelection?.ObjectId);
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
