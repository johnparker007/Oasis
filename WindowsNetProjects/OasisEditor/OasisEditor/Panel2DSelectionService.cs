using System.Windows;

namespace OasisEditor;

internal static class Panel2DSelectionService
{
    public static PanelSelectionInfo? SelectFromPoint(
        IReadOnlyList<PanelElementModel> elements,
        Point documentPoint)
    {
        var hit = Panel2DHitTestService.HitTopmostAtPoint(elements, documentPoint);
        return hit is null ? null : ToSelectionInfo(hit);
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
}
