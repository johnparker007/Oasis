using System.Windows;

namespace OasisEditor;

internal sealed record FaceElementMoveSnapshot(string ObjectId, FaceElementModel OriginalElement);

internal static class FaceElementBulkMoveService
{
    public static IReadOnlyList<FaceElementMoveSnapshot> CaptureMovableSelection(DocumentTabViewModel document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var snapshots = new List<FaceElementMoveSnapshot>();
        foreach (var item in document.SelectionState.Items)
        {
            if (item.Domain != EditorSelectionDomain.FaceElement || !document.TryGetFaceElementByObjectId(item.ObjectId, out var element)) continue;
            if (!TransformLockInteractionService.CanMoveOrResize(element)) continue;
            snapshots.Add(new FaceElementMoveSnapshot(item.ObjectId, FaceElementModelCloner.Clone(element)));
        }
        return snapshots;
    }

    public static IReadOnlyDictionary<string, FaceElementModel> ComputeMovedElements(IEnumerable<FaceElementMoveSnapshot> snapshots, Point start, Point end)
    {
        ArgumentNullException.ThrowIfNull(snapshots);
        var delta = end - start;
        return snapshots.ToDictionary(snapshot => snapshot.ObjectId, snapshot => FaceElementModelCloner.Clone(snapshot.OriginalElement, x: snapshot.OriginalElement.X + delta.X, y: snapshot.OriginalElement.Y + delta.Y));
    }
}
