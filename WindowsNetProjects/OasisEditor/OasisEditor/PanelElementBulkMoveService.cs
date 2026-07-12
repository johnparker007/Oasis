using System.Windows;

namespace OasisEditor;

internal sealed record PanelElementMoveSnapshot(string ObjectId, PanelElementModel OriginalElement);

internal static class PanelElementBulkMoveService
{
    public static IReadOnlyList<PanelElementMoveSnapshot> CaptureMovableSelection(DocumentTabViewModel document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var snapshots = new List<PanelElementMoveSnapshot>();
        foreach (var item in document.SelectionState.Items)
        {
            if (item.Domain != EditorSelectionDomain.PanelElement || !document.TryGetPanelElementByObjectId(item.ObjectId, out var element))
            {
                continue;
            }

            if (!TransformLockInteractionService.CanMoveOrResize(element))
            {
                continue;
            }

            snapshots.Add(new PanelElementMoveSnapshot(item.ObjectId, PanelElementModelCloner.Clone(element)));
        }

        return snapshots;
    }

    public static IReadOnlyDictionary<string, PanelElementModel> ComputeMovedElements(IEnumerable<PanelElementMoveSnapshot> snapshots, Point start, Point end)
    {
        ArgumentNullException.ThrowIfNull(snapshots);
        return snapshots.ToDictionary(snapshot => snapshot.ObjectId, snapshot => Panel2DMoveComputationService.ComputeMovedElement(snapshot.OriginalElement, start, end));
    }
}
