using System.Collections.ObjectModel;
using System.Linq;
using Xunit;

namespace OasisEditor.Tests;

public sealed class HierarchyPanelCommandServiceTests
{
    [Fact]
    public void SmokeLikeFlow_HierarchyCommandsUndoRedoAndSaveReopen_PreservesPanelElements()
    {
        var document = CreatePanelDocument(
            new PanelElementModel
            {
                ObjectId = "source-id",
                Name = "Rect One",
                Kind = PanelElementKind.Rectangle,
                X = 10,
                Y = 20,
                Width = 30,
                Height = 40
            });
        var workspace = CreateWorkspace(document, document);
        var service = CreateService(document, workspace);

        document.HierarchySelectedPanelSelection = new PanelSelectionInfo("source-id", "rectangle", 10, 20, 30, 40);
        service.ExecuteCopySelected();
        service.ExecutePasteSelected();
        service.ExecuteDuplicateSelected();
        service.ExecuteCutSelected();

        Assert.Equal(2, document.GetPanelElements().Count);
        Assert.Equal(2, document.GetPanelElements().Select(element => element.ObjectId).Distinct(StringComparer.Ordinal).Count());

        Assert.True(workspace.UndoActiveDocument());
        Assert.Equal(3, document.GetPanelElements().Count);

        Assert.True(workspace.RedoActiveDocument());
        Assert.Equal(2, document.GetPanelElements().Count);

        var savedContent = DocumentWorkspaceViewModel.BuildDocumentContent(document);
        var openData = DocumentWorkspaceViewModel.BuildOpenDocumentData("C:/Repo/TestProject/Assets/panel.panel2d", savedContent);
        var reopened = new DocumentTabViewModel(
            EditorDocument.CreateFromFile("C:/Repo/TestProject/Assets/panel.panel2d", openData.Summary, openData.PanelTitle),
            openData.PanelLayoutJson);

        Assert.Equal(2, reopened.GetPanelElements().Count);
        Assert.Equal(2, reopened.GetPanelElements().Select(element => element.ObjectId).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void ExecutePasteSelected_AfterCopy_CreatesNewObjectIdAndRecordsCommand()
    {
        var document = CreatePanelDocument(
            new PanelElementModel
            {
                ObjectId = "source-id",
                Name = "Rect One",
                Kind = PanelElementKind.Rectangle,
                X = 10,
                Y = 20,
                Width = 30,
                Height = 40
            });

        var workspace = CreateWorkspace(document, document);
        document.HierarchySelectedPanelSelection = new PanelSelectionInfo("source-id", "rectangle", 10, 20, 30, 40);

        var service = CreateService(document, workspace);

        service.ExecuteCopySelected();
        Assert.True(service.CanPasteSelected());

        service.ExecutePasteSelected();

        Assert.Equal(2, document.GetPanelElements().Count);
        var pasted = document.GetPanelElements().Single(element => element.ObjectId != "source-id");
        Assert.NotEqual("source-id", pasted.ObjectId);
        Assert.Single(document.CommandService.History.Entries);
    }

    [Fact]
    public void ExecuteDuplicateSelected_WithSelection_CreatesNewObjectIdAndRecordsCommand()
    {
        var document = CreatePanelDocument(
            new PanelElementModel
            {
                ObjectId = "dup-source",
                Name = "Rect Two",
                Kind = PanelElementKind.Rectangle,
                X = 5,
                Y = 6,
                Width = 7,
                Height = 8
            });

        var workspace = CreateWorkspace(document, document);
        document.HierarchySelectedPanelSelection = new PanelSelectionInfo("dup-source", "rectangle", 5, 6, 7, 8);
        var service = CreateService(document, workspace);

        service.ExecuteDuplicateSelected();

        Assert.Equal(2, document.GetPanelElements().Count);
        Assert.Contains(document.GetPanelElements(), element => element.ObjectId == "dup-source");
        Assert.Contains(document.GetPanelElements(), element => element.ObjectId != "dup-source");
        Assert.Single(document.CommandService.History.Entries);
    }

    [Fact]
    public void ExecutePasteSelected_WithoutClipboardPayload_DoesNotMutateOrRecordHistory()
    {
        var document = CreatePanelDocument(
            new PanelElementModel
            {
                ObjectId = "source-id",
                Name = "Rect One",
                Kind = PanelElementKind.Rectangle,
                X = 10,
                Y = 20,
                Width = 30,
                Height = 40
            });

        var workspace = CreateWorkspace(document, document);
        document.HierarchySelectedPanelSelection = new PanelSelectionInfo("source-id", "rectangle", 10, 20, 30, 40);
        var service = CreateService(document, workspace);

        Assert.False(service.CanPasteSelected());

        service.ExecutePasteSelected();

        Assert.Single(document.GetPanelElements());
        Assert.Empty(document.CommandService.History.Entries);
    }

    [Fact]
    public void CanDeleteItem_ReturnsFalse_ForHierarchyGroupItems()
    {
        var document = CreatePanelDocument(
            new PanelElementModel
            {
                ObjectId = "source-id",
                Name = "Rect One",
                Kind = PanelElementKind.Rectangle,
                X = 1,
                Y = 2,
                Width = 3,
                Height = 4
            });
        var workspace = CreateWorkspace(document, document);
        var service = CreateService(document, workspace);
        var groupItem = new HierarchyItemViewModel("Rectangles (1)", "group:rectangle", isGroup: true);

        var canDelete = service.CanDeleteItem(groupItem);

        Assert.False(canDelete);
    }

    [Fact]
    public void DeleteItem_ForPanelEntity_DeletesElementAndClearsSelection()
    {
        var document = CreatePanelDocument(
            new PanelElementModel
            {
                ObjectId = "source-id",
                Name = "Rect One",
                Kind = PanelElementKind.Rectangle,
                X = 10,
                Y = 20,
                Width = 30,
                Height = 40
            });

        var workspace = CreateWorkspace(document, document);
        var selection = new PanelSelectionInfo("source-id", "rectangle", 10, 20, 30, 40);
        var entityItem = new HierarchyItemViewModel("Rect One", "rectangle:source-id", panelSelection: selection);
        var service = CreateService(document, workspace);

        var deleted = service.DeleteItem(entityItem);

        Assert.True(deleted);
        Assert.Empty(document.GetPanelElements());
        Assert.Null(document.HierarchySelectedPanelSelection);
        Assert.Single(document.CommandService.History.Entries);
    }

    private static HierarchyPanelCommandService CreateService(
        DocumentTabViewModel selectedDocument,
        DocumentWorkspaceViewModel workspace)
    {
        return new HierarchyPanelCommandService(
            () => selectedDocument,
            workspace.ExecuteDocumentCanvasCommand,
            (documentId, selection) =>
            {
                if (selectedDocument.DocumentId == documentId)
                {
                    selectedDocument.HierarchySelectedPanelSelection = selection;
                }
            },
            () => { });
    }

    private static DocumentTabViewModel CreatePanelDocument(params PanelElementModel[] elements)
    {
        var document = new DocumentTabViewModel(EditorDocument.CreatePanel2DStub("Panel"));
        document.SetPanelElements(elements);
        return document;
    }

    private static DocumentWorkspaceViewModel CreateWorkspace(
        DocumentTabViewModel selectedDocument,
        params DocumentTabViewModel[] openDocuments)
    {
        var loadedProject = new EditorProject
        {
            Name = "TestProject",
            ProjectFilePath = "C:/Repo/TestProject/TestProject.oasisproj",
            ProjectDirectory = "C:/Repo/TestProject",
            AssetsDirectory = "C:/Repo/TestProject/Assets",
            MachinesDirectory = "C:/Repo/TestProject/Machines",
            GeneratedDirectory = "C:/Repo/TestProject/Generated"
        };
        var documents = new ObservableCollection<DocumentTabViewModel>(openDocuments);
        var currentSelection = selectedDocument;

        return new DocumentWorkspaceViewModel(
            () => loadedProject,
            project => loadedProject = project,
            documents,
            () => currentSelection,
            document => currentSelection = document,
            () => { },
            _ => { },
            (_, _) => { });
    }
}
