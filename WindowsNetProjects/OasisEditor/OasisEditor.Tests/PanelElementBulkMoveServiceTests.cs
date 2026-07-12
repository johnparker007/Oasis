using System.Windows;
using Xunit;

namespace OasisEditor.Tests;

public sealed class PanelElementBulkMoveServiceTests
{
    [Fact]
    public void ComputeMovedElements_UsesOriginalSnapshotsAndSharedDelta()
    {
        var document = CreateDocument(
            new PanelElementModel { ObjectId = "a", Kind = PanelElementKind.Lamp, X = 10, Y = 20, Width = 5, Height = 5, IsVisible = true },
            new PanelElementModel { ObjectId = "b", Kind = PanelElementKind.Reel, X = 30, Y = 40, Width = 5, Height = 5, IsVisible = true });
        document.SelectionState.Replace(new EditorSelectionItem(EditorSelectionDomain.PanelElement, "a"));
        document.SelectionState.Add(new EditorSelectionItem(EditorSelectionDomain.PanelElement, "b"));

        var snapshots = PanelElementBulkMoveService.CaptureMovableSelection(document);
        Assert.True(PanelElementPreviewMutationService.TryApplyPreview(document, "a", PanelElementModelCloner.Clone(document.GetPanelElements()[0], x: 999, y: 999)));

        var moved = PanelElementBulkMoveService.ComputeMovedElements(snapshots, new Point(0, 0), new Point(7, 9));

        Assert.Equal(17, moved["a"].X);
        Assert.Equal(29, moved["a"].Y);
        Assert.Equal(37, moved["b"].X);
        Assert.Equal(49, moved["b"].Y);
    }

    [Fact]
    public void CaptureMovableSelection_ExcludesSelectedTransformLockedMembers()
    {
        var document = CreateDocument(
            new PanelElementModel { ObjectId = "a", Kind = PanelElementKind.Lamp, X = 0, Y = 0, Width = 5, Height = 5, IsVisible = true },
            new PanelElementModel { ObjectId = "locked", Kind = PanelElementKind.Background, X = 10, Y = 10, Width = 5, Height = 5, IsVisible = true, IsTransformLocked = true });
        document.SelectionState.Replace(new EditorSelectionItem(EditorSelectionDomain.PanelElement, "a"));
        document.SelectionState.Add(new EditorSelectionItem(EditorSelectionDomain.PanelElement, "locked"));

        var snapshots = PanelElementBulkMoveService.CaptureMovableSelection(document);

        Assert.Equal(new[] { "a" }, snapshots.Select(snapshot => snapshot.ObjectId));
    }

    [Fact]
    public void BulkPreviewCancellation_RestoresAllPreviewedItemsWithoutUndoHistory()
    {
        var document = CreateDocument(
            new PanelElementModel { ObjectId = "a", Kind = PanelElementKind.Lamp, X = 0, Y = 0, Width = 5, Height = 5, IsVisible = true },
            new PanelElementModel { ObjectId = "b", Kind = PanelElementKind.Reel, X = 10, Y = 10, Width = 5, Height = 5, IsVisible = true });
        var originals = document.GetPanelElements().ToDictionary(element => element.ObjectId, element => PanelElementModelCloner.Clone(element));
        var preview = originals.ToDictionary(pair => pair.Key, pair => PanelElementModelCloner.Clone(pair.Value, x: pair.Value.X + 5, y: pair.Value.Y + 6));

        Assert.True(PanelElementPreviewMutationService.TryApplyPreviews(document, preview));
        Assert.True(PanelElementPreviewMutationService.TryApplyPreviews(document, originals));

        Assert.Equal(0, document.GetPanelElements()[0].X);
        Assert.Equal(10, document.GetPanelElements()[1].X);
        Assert.Empty(document.CommandService.History.Entries);
        Assert.False(document.IsDirty);
    }

    [Fact]
    public void BulkMoveCommand_IsOneAtomicUndoRedoEntryForCompleteMove()
    {
        var document = CreateDocument(
            new PanelElementModel { ObjectId = "a", Kind = PanelElementKind.Lamp, X = 0, Y = 0, Width = 5, Height = 5, IsVisible = true },
            new PanelElementModel { ObjectId = "b", Kind = PanelElementKind.Reel, X = 10, Y = 10, Width = 5, Height = 5, IsVisible = true });
        var originals = document.GetPanelElements().ToDictionary(element => element.ObjectId, element => PanelElementModelCloner.Clone(element));
        var moved = originals.ToDictionary(pair => pair.Key, pair => PanelElementModelCloner.Clone(pair.Value, x: pair.Value.X + 3, y: pair.Value.Y + 4));

        document.CommandService.Execute(CanvasMutationCommands.CreateBulkUpdateElementsCommand(document.DocumentId, document, moved, originals, "Move elements"));

        Assert.Single(document.CommandService.History.Entries);
        Assert.Equal(3, document.GetPanelElements()[0].X);
        Assert.Equal(13, document.GetPanelElements()[1].X);

        Assert.True(document.CommandService.TryUndo());
        Assert.Equal(0, document.GetPanelElements()[0].X);
        Assert.Equal(10, document.GetPanelElements()[1].X);

        Assert.True(document.CommandService.TryRedo());
        Assert.Equal(3, document.GetPanelElements()[0].X);
        Assert.Equal(13, document.GetPanelElements()[1].X);
    }

    private static DocumentTabViewModel CreateDocument(params PanelElementModel[] elements)
    {
        var document = new DocumentTabViewModel(EditorDocument.CreatePanel2DStub("Panel"));
        document.SetPanelElements(elements);
        return document;
    }
}
