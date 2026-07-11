using System.Collections.ObjectModel;
using System.Linq;
using Xunit;

namespace OasisEditor.Tests;

public sealed class HierarchyPanelCommandServiceTests
{
    [Theory]
    [InlineData((int)PanelElementKind.Rectangle, "rectangle", "Rect One")]
    [InlineData((int)PanelElementKind.Background, "background", "Background")]
    [InlineData((int)PanelElementKind.Lamp, "lamp", "Lamp 7")]
    [InlineData((int)PanelElementKind.Reel, "reel", "Reel 3")]
    [InlineData((int)PanelElementKind.SevenSegment, "sevenSegment", "7 Segment 4")]
    [InlineData((int)PanelElementKind.Alpha, "alpha", "Alpha")]
    [InlineData((int)PanelElementKind.Label, "label", "Label")]
    public void SmokeLikeFlow_HierarchyCommandsUndoRedoAndSaveReopen_PreservesPanelElements(
        int kindValue,
        string kindToken,
        string name)
    {
        var kind = (PanelElementKind)kindValue;
        var document = CreatePanelDocument(
            new PanelElementModel
            {
                ObjectId = "source-id",
                Name = name,
                Kind = kind,
                X = 10,
                Y = 20,
                Width = 30,
                Height = 40
            });
        var workspace = CreateWorkspace(document, document);
        var service = CreateService(document, workspace);

        document.HierarchySelectedPanelSelection = new PanelSelectionInfo("source-id", kindToken, 10, 20, 30, 40);
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
        var openData = DocumentWorkspaceViewModel.BuildOpenDocumentData("C:/Repo/TestProject/Assets/Panel2D/Panel/asset.panel2d", savedContent);
        var reopened = new DocumentTabViewModel(
            EditorDocument.CreateFromFile("C:/Repo/TestProject/Assets/Panel2D/Panel/asset.panel2d", openData.Summary, openData.PanelTitle),
            openData.PanelLayoutJson);

        Assert.Equal(2, reopened.GetPanelElements().Count);
        Assert.Equal(2, reopened.GetPanelElements().Select(element => element.ObjectId).Distinct(StringComparer.Ordinal).Count());
    }

    [Theory]
    [InlineData((int)PanelElementKind.Background, "group:background", "background")]
    [InlineData((int)PanelElementKind.Lamp, "group:lamp", "lamp")]
    [InlineData((int)PanelElementKind.Reel, "group:reel", "reel")]
    [InlineData((int)PanelElementKind.SevenSegment, "group:sevenSegment", "sevenSegment")]
    [InlineData((int)PanelElementKind.Alpha, "group:alpha", "alpha")]
    [InlineData((int)PanelElementKind.Label, "group:label", "label")]
    public void HierarchyProvider_NativeGroups_ExposeNativeSelectionTokens(
        int kindValue,
        string expectedGroupKey,
        string expectedKindToken)
    {
        var document = CreatePanelDocument(
            new PanelElementModel
            {
                ObjectId = "source-id",
                Name = "Native",
                Kind = (PanelElementKind)kindValue,
                X = 10,
                Y = 20,
                Width = 30,
                Height = 40
            });

        var provider = new Panel2DHierarchyProvider();
        var groups = provider.Build(document);
        var group = groups.Single(item => item.NodeKey == expectedGroupKey);
        var child = Assert.Single(group.Children);

        var selection = Assert.IsType<PanelSelectionInfo>(child.PanelSelection);
        Assert.Equal("source-id", selection.ObjectId);
        Assert.Equal(expectedKindToken, selection.Kind);
        Assert.Equal(10, selection.X);
        Assert.Equal(20, selection.Y);
        Assert.Equal(30, selection.Width);
        Assert.Equal(40, selection.Height);
    }

    [Fact]
    public void HierarchyProvider_AppendsLockAndHiddenFlagsToDisplayName()
    {
        var document = CreatePanelDocument(
            new PanelElementModel
            {
                ObjectId = "flagged",
                Name = "Lamp 1",
                Kind = PanelElementKind.Lamp,
                X = 1,
                Y = 2,
                Width = 3,
                Height = 4,
                IsLocked = true,
                IsVisible = false
            });

        var provider = new Panel2DHierarchyProvider();
        var group = provider.Build(document).Single(item => item.NodeKey == "group:lamp");
        var child = Assert.Single(group.Children);
        Assert.Equal("Lamp 1 [Locked] [Hidden]", child.DisplayName);
    }

    [Fact]
    public void ExecutePasteSelected_AfterCopy_CreatesNewObjectIdAndRecordsCommand()
    {
        var document = CreatePanelDocument(
            new PanelElementModel
            {
                ObjectId = "source-id",
                Name = "Rect One",
                Kind = PanelElementKind.Lamp,
                X = 10,
                Y = 20,
                Width = 30,
                Height = 40,
                AssetPath = "Assets/FmlImport/layout/Lamps/lamp1.png",
                DisplayNumber = 7,
                OnColorHex = "#FFFFCC00",
                OffColorHex = "#FF111111",
                TextColorHex = "#FFFFFFFF",
                DisplayText = "HOLD",
                ImportSource = new PanelElementImportSourceModel
                {
                    Format = "LegacyImport",
                    Reference = "layout:lamp:7"
                }
            });

        var workspace = CreateWorkspace(document, document);
        document.HierarchySelectedPanelSelection = new PanelSelectionInfo("source-id", "lamp", 10, 20, 30, 40);

        var service = CreateService(document, workspace);

        service.ExecuteCopySelected();
        Assert.True(service.CanPasteSelected());

        service.ExecutePasteSelected();

        Assert.Equal(2, document.GetPanelElements().Count);
        var pasted = document.GetPanelElements().Single(element => element.ObjectId != "source-id");
        Assert.NotEqual("source-id", pasted.ObjectId);
        Assert.Equal(PanelElementKind.Lamp, pasted.Kind);
        Assert.Equal("Assets/FmlImport/layout/Lamps/lamp1.png", pasted.AssetPath);
        Assert.Equal(7, pasted.DisplayNumber);
        Assert.Equal("#FFFFCC00", pasted.OnColorHex);
        Assert.Equal("#FF111111", pasted.OffColorHex);
        Assert.Equal("#FFFFFFFF", pasted.TextColorHex);
        Assert.Equal("HOLD", pasted.DisplayText);
        Assert.NotNull(pasted.ImportSource);
        Assert.Equal("LegacyImport", pasted.ImportSource!.Format);
        Assert.Equal("layout:lamp:7", pasted.ImportSource.Reference);
        Assert.Single(document.CommandService.History.Entries);
    }

    [Fact]
    public void ExecuteDuplicateSelected_WithSelection_CreatesNewObjectIdAndRecordsCommand()
    {
        var document = CreatePanelDocument(
            new PanelElementModel
            {
                ObjectId = "dup-source",
                Name = "Reel Two",
                Kind = PanelElementKind.Reel,
                X = 5,
                Y = 6,
                Width = 7,
                Height = 8,
                AssetPath = "Assets/FmlImport/layout/Reels/reel2.png",
                SecondaryAssetPath = "Assets/FmlImport/layout/Reels/reel2-overlay.png",
                DisplayNumber = 3,
                IsReversed = true,
                Stops = 24,
                VisibleScale = 0.125
            });

        var workspace = CreateWorkspace(document, document);
        document.HierarchySelectedPanelSelection = new PanelSelectionInfo("dup-source", "reel", 5, 6, 7, 8);
        var service = CreateService(document, workspace);

        service.ExecuteDuplicateSelected();

        Assert.Equal(2, document.GetPanelElements().Count);
        Assert.Contains(document.GetPanelElements(), element => element.ObjectId == "dup-source");
        var duplicate = Assert.Single(document.GetPanelElements(), element => element.ObjectId != "dup-source");
        Assert.Equal(PanelElementKind.Reel, duplicate.Kind);
        Assert.Equal("Assets/FmlImport/layout/Reels/reel2.png", duplicate.AssetPath);
        Assert.Equal("Assets/FmlImport/layout/Reels/reel2-overlay.png", duplicate.SecondaryAssetPath);
        Assert.Equal(3, duplicate.DisplayNumber);
        Assert.True(duplicate.IsReversed);
        Assert.Equal(24, duplicate.Stops);
        Assert.Equal(0.125, duplicate.VisibleScale);
        Assert.Single(document.CommandService.History.Entries);
    }

    [Fact]
    public void RenameSelected_PreservesNativeProperties()
    {
        var document = CreatePanelDocument(
            new PanelElementModel
            {
                ObjectId = "rename-source",
                Name = "Alpha",
                Kind = PanelElementKind.Alpha,
                X = 11,
                Y = 12,
                Width = 13,
                Height = 14,
                DisplayText = "READY",
                TextColorHex = "#FF00FF00",
                IsReversed = true,
                ImportSource = new PanelElementImportSourceModel
                {
                    Format = "LegacyImport",
                    Reference = "layout:alpha:1"
                }
            });

        var workspace = CreateWorkspace(document, document);
        document.HierarchySelectedPanelSelection = new PanelSelectionInfo("rename-source", "alpha", 11, 12, 13, 14);
        var service = CreateService(document, workspace);

        var renamed = service.RenameSelected("Alpha Renamed");

        Assert.True(renamed);
        var updated = Assert.Single(document.GetPanelElements());
        Assert.Equal("Alpha Renamed", updated.Name);
        Assert.Equal(PanelElementKind.Alpha, updated.Kind);
        Assert.Equal("READY", updated.DisplayText);
        Assert.Equal("#FF00FF00", updated.TextColorHex);
        Assert.True(updated.IsReversed);
        Assert.NotNull(updated.ImportSource);
        Assert.Equal("LegacyImport", updated.ImportSource!.Format);
        Assert.Equal("layout:alpha:1", updated.ImportSource.Reference);
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

    [Fact]
    public void ReorderCanExecute_ReflectsSelectedElementPosition()
    {
        var document = CreatePanelDocument(
            new PanelElementModel { ObjectId = "back", Name = "Back", Kind = PanelElementKind.Rectangle, X = 0, Y = 0, Width = 10, Height = 10 },
            new PanelElementModel { ObjectId = "middle", Name = "Middle", Kind = PanelElementKind.Rectangle, X = 20, Y = 0, Width = 10, Height = 10 },
            new PanelElementModel { ObjectId = "front", Name = "Front", Kind = PanelElementKind.Rectangle, X = 40, Y = 0, Width = 10, Height = 10 });
        var workspace = CreateWorkspace(document, document);
        var service = CreateService(document, workspace);

        document.HierarchySelectedPanelSelection = new PanelSelectionInfo("middle", "rectangle", 20, 0, 10, 10);
        Assert.True(service.CanBringToFrontSelected());
        Assert.True(service.CanBringForwardSelected());
        Assert.True(service.CanSendToBackSelected());
        Assert.True(service.CanSendBackwardSelected());

        document.HierarchySelectedPanelSelection = new PanelSelectionInfo("front", "rectangle", 40, 0, 10, 10);
        Assert.False(service.CanBringToFrontSelected());
        Assert.False(service.CanBringForwardSelected());
        Assert.True(service.CanSendToBackSelected());
        Assert.True(service.CanSendBackwardSelected());
    }

    [Fact]
    public void ExecuteBringToFrontSelected_ReordersElement_AndUndoRestoresOrder()
    {
        var document = CreatePanelDocument(
            new PanelElementModel { ObjectId = "first", Name = "First", Kind = PanelElementKind.Rectangle, X = 0, Y = 0, Width = 10, Height = 10 },
            new PanelElementModel { ObjectId = "second", Name = "Second", Kind = PanelElementKind.Rectangle, X = 20, Y = 0, Width = 10, Height = 10 },
            new PanelElementModel { ObjectId = "third", Name = "Third", Kind = PanelElementKind.Rectangle, X = 40, Y = 0, Width = 10, Height = 10 });
        var workspace = CreateWorkspace(document, document);
        var service = CreateService(document, workspace);
        document.HierarchySelectedPanelSelection = new PanelSelectionInfo("first", "rectangle", 0, 0, 10, 10);

        service.ExecuteBringToFrontSelected();

        Assert.Equal(["second", "third", "first"], document.GetPanelElements().Select(element => element.ObjectId));
        Assert.Single(document.CommandService.History.Entries);

        Assert.True(workspace.UndoActiveDocument());
        Assert.Equal(["first", "second", "third"], document.GetPanelElements().Select(element => element.ObjectId));
    }

    [Fact]
    public void ExecuteLockSelected_UpdatesState_AndUndoRestoresUnlocked()
    {
        var document = CreatePanelDocument(
            new PanelElementModel { ObjectId = "one", Name = "One", Kind = PanelElementKind.Rectangle, X = 0, Y = 0, Width = 10, Height = 10, IsVisible = true });
        var workspace = CreateWorkspace(document, document);
        var service = CreateService(document, workspace);
        document.HierarchySelectedPanelSelection = new PanelSelectionInfo("one", "rectangle", 0, 0, 10, 10);

        Assert.True(service.CanLockSelected());
        service.ExecuteLockSelected();

        var updated = Assert.Single(document.GetPanelElements());
        Assert.True(updated.IsLocked);
        Assert.False(service.CanLockSelected());
        Assert.True(service.CanUnlockSelected());

        Assert.True(workspace.UndoActiveDocument());
        updated = Assert.Single(document.GetPanelElements());
        Assert.False(updated.IsLocked);
    }

    [Fact]
    public void ExecuteHideSelected_HidesElement_AndUndoRestoresVisibility()
    {
        var document = CreatePanelDocument(
            new PanelElementModel { ObjectId = "one", Name = "One", Kind = PanelElementKind.Rectangle, X = 0, Y = 0, Width = 10, Height = 10, IsVisible = true });
        var workspace = CreateWorkspace(document, document);
        var service = CreateService(document, workspace);
        document.HierarchySelectedPanelSelection = new PanelSelectionInfo("one", "rectangle", 0, 0, 10, 10);

        Assert.True(service.CanHideSelected());
        service.ExecuteHideSelected();

        var updated = Assert.Single(document.GetPanelElements());
        Assert.False(updated.IsVisible);
        Assert.NotNull(document.HierarchySelectedPanelSelection);
        Assert.False(service.CanHideSelected());
        Assert.True(service.CanShowSelected());

        Assert.True(workspace.UndoActiveDocument());
        updated = Assert.Single(document.GetPanelElements());
        Assert.True(updated.IsVisible);
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
