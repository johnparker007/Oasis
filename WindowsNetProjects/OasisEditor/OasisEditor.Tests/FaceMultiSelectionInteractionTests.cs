using System.Windows;
using Xunit;

namespace OasisEditor.Tests;

public sealed class FaceMultiSelectionInteractionTests
{
    private static readonly EditorSelectionItem A = new(EditorSelectionDomain.FaceElement, "a");
    private static readonly EditorSelectionItem B = new(EditorSelectionDomain.FaceElement, "b");

    [Fact]
    public void DocumentSelection_ClickReplacePreserveCtrlAndEmptySpaceBehaviors_ForFaceItems()
    {
        var state = new DocumentSelectionState();
        state.Replace(A);
        state.Add(B);

        state.SetPrimary(A);
        Assert.Equal(new[] { A, B }, state.Items);
        Assert.Equal(A, state.PrimaryItem);

        var c = new EditorSelectionItem(EditorSelectionDomain.FaceElement, "c");
        state.Replace(c);
        Assert.Equal(new[] { c }, state.Items);

        state.Add(A);
        Assert.Equal(new[] { c, A }, state.Items);
        state.Toggle(A);
        Assert.Equal(new[] { c }, state.Items);

        state.Clear();
        Assert.Empty(state.Items);
    }

    [Fact]
    public void SelectItemsFromRect_UsesFullEnclosureAndKeepsLockedArtworkSelectable()
    {
        var elements = new FaceElementModel[]
        {
            new FaceArtworkElement { ObjectId = "art", X = 0, Y = 0, Width = 10, Height = 10, IsVisible = true, IsTransformLocked = true },
            new FaceLampWindowElement { ObjectId = "partial", X = 8, Y = 8, Width = 10, Height = 10, IsVisible = true },
            new FaceLampWindowElement { ObjectId = "hidden", X = 0, Y = 0, Width = 10, Height = 10, IsVisible = false },
            new FaceReelDisplayElement { ObjectId = "reel", X = 20, Y = 20, Width = 5, Height = 5, IsVisible = true }
        };

        var selected = FaceSelectionInteractionService.SelectItemsFromRect(elements, new Rect(0, 0, 25, 25));

        Assert.Equal(new[] { "art", "reel" }, selected.Select(item => item.ObjectId));
    }

    [Fact]
    public void CaptureMovableSelection_ExcludesLockedMembersAndRejectsLockedDragStart()
    {
        var unlocked = new FaceLampWindowElement { ObjectId = "a", X = 0, Y = 0, Width = 5, Height = 5, IsVisible = true };
        var locked = new FaceArtworkElement { ObjectId = "locked", X = 10, Y = 10, Width = 5, Height = 5, IsVisible = true, IsTransformLocked = true };
        var document = CreateDocument(unlocked, locked);
        document.SelectionState.Replace(FaceSelectionInteractionService.ToSelectionItem(unlocked));
        document.SelectionState.Add(FaceSelectionInteractionService.ToSelectionItem(locked));

        var snapshots = FaceElementBulkMoveService.CaptureMovableSelection(document);

        Assert.Equal(new[] { "a" }, snapshots.Select(snapshot => snapshot.ObjectId));
        Assert.True(FaceSelectionInteractionService.CanStartGroupMoveFrom(unlocked, document.SelectionState));
        Assert.False(FaceSelectionInteractionService.CanStartGroupMoveFrom(locked, document.SelectionState));
    }

    [Fact]
    public void ComputeMovedElements_UsesImmutableOriginalsAndCommonDocumentDelta()
    {
        var document = CreateDocument(
            new FaceLampWindowElement { ObjectId = "a", X = 10, Y = 20, Width = 5, Height = 5, IsVisible = true },
            new FaceReelDisplayElement { ObjectId = "b", X = 30, Y = 40, Width = 5, Height = 5, IsVisible = true });
        document.SelectionState.Replace(A);
        document.SelectionState.Add(B);

        var snapshots = FaceElementBulkMoveService.CaptureMovableSelection(document);
        Assert.True(FaceElementPreviewMutationService.TryApplyPreview(document, "a", FaceElementModelCloner.Clone(document.GetFaceElements()[0], x: 999, y: 999)));

        var moved = FaceElementBulkMoveService.ComputeMovedElements(snapshots, new Point(0, 0), new Point(7, 9));

        Assert.Equal(17, moved["a"].X);
        Assert.Equal(29, moved["a"].Y);
        Assert.Equal(37, moved["b"].X);
        Assert.Equal(49, moved["b"].Y);
    }

    [Fact]
    public void BulkPreviewRestoreAndBulkMoveCommand_AreAtomicUndoRedo()
    {
        var document = CreateDocument(
            new FaceLampWindowElement { ObjectId = "a", X = 0, Y = 0, Width = 5, Height = 5, IsVisible = true },
            new FaceReelDisplayElement { ObjectId = "b", X = 10, Y = 10, Width = 5, Height = 5, IsVisible = true });
        var originals = document.GetFaceElements().ToDictionary(element => element.ObjectId, element => FaceElementModelCloner.Clone(element));
        var moved = originals.ToDictionary(pair => pair.Key, pair => FaceElementModelCloner.Clone(pair.Value, x: pair.Value.X + 3, y: pair.Value.Y + 4));

        Assert.True(FaceElementPreviewMutationService.TryApplyPreviews(document, moved));
        Assert.True(FaceElementPreviewMutationService.TryApplyPreviews(document, originals));
        Assert.Empty(document.CommandService.History.Entries);

        document.CommandService.Execute(FaceMutationCommands.CreateBulkUpdateElementsCommand(document.DocumentId, document, moved, originals, "Move face elements"));

        Assert.Single(document.CommandService.History.Entries);
        Assert.Equal(3, document.GetFaceElements()[0].X);
        Assert.Equal(13, document.GetFaceElements()[1].X);

        Assert.True(document.CommandService.TryUndo());
        Assert.Equal(0, document.GetFaceElements()[0].X);
        Assert.Equal(10, document.GetFaceElements()[1].X);

        Assert.True(document.CommandService.TryRedo());
        Assert.Equal(3, document.GetFaceElements()[0].X);
        Assert.Equal(13, document.GetFaceElements()[1].X);
    }

    private static DocumentTabViewModel CreateDocument(params FaceElementModel[] elements)
    {
        var document = new DocumentTabViewModel(EditorDocument.CreateFaceStub("Face"));
        document.SetFaceElements(elements);
        return document;
    }
}
