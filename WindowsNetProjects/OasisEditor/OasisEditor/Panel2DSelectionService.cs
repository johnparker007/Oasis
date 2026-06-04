using System.Windows;

namespace OasisEditor;

internal static class Panel2DSelectionService
{
    public static PanelSelectionInfo? SelectFromPoint(
        IReadOnlyList<PanelElementModel> elements,
        Point documentPoint)
    {
        return SelectFromPoint(elements, documentPoint, currentSelection: null);
    }

    public static PanelSelectionInfo? SelectFromPoint(
        IReadOnlyList<PanelElementModel> elements,
        Point documentPoint,
        PanelSelectionInfo? currentSelection)
    {
        var hits = Panel2DHitTestService.HitAllAtPoint(elements, documentPoint);
        if (hits.Count == 0)
        {
            return null;
        }

        if (currentSelection is not { } selected)
        {
            return ToSelectionInfo(hits[0]);
        }

        for (var i = 0; i < hits.Count; i++)
        {
            if (IsSelectionMatch(hits[i], selected))
            {
                return ToSelectionInfo(hits[(i + 1) % hits.Count]);
            }
        }

        return ToSelectionInfo(hits[0]);
    }

    public static PanelSelectionInfo? SelectFromRect(
        IReadOnlyList<PanelElementModel> elements,
        double minX,
        double minY,
        double maxX,
        double maxY)
    {
        var hit = Panel2DHitTestService.HitTopmostIntersectingRect(elements, minX, minY, maxX, maxY);
        return hit is null ? null : ToSelectionInfo(hit);
    }

    private static PanelSelectionInfo ToSelectionInfo(PanelElementModel element)
    {
        return new PanelSelectionInfo(
            element.ObjectId,
            Panel2DDocumentStorage.SerializeElementKind(element.Kind),
            element.X,
            element.Y,
            element.Width,
            element.Height);
    }

    private static bool IsSelectionMatch(PanelElementModel element, PanelSelectionInfo selection)
    {
        if (!string.IsNullOrWhiteSpace(element.ObjectId)
            && !string.IsNullOrWhiteSpace(selection.ObjectId))
        {
            return string.Equals(element.ObjectId, selection.ObjectId, StringComparison.Ordinal);
        }

        return element.Kind == Panel2DDocumentStorage.ParseElementKind(selection.Kind)
            && AreClose(element.X, selection.X)
            && AreClose(element.Y, selection.Y)
            && AreClose(element.Width, selection.Width)
            && AreClose(element.Height, selection.Height);
    }

    private static bool AreClose(double left, double right)
    {
        return Math.Abs(left - right) < 0.01d;
    }
}
