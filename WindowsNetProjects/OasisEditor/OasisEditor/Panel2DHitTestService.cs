using System.Windows;

namespace OasisEditor;

internal static class Panel2DHitTestService
{
    public static PanelElementModel? HitTopmostAtPoint(IReadOnlyList<PanelElementModel> elements, Point documentPoint)
    {
        ArgumentNullException.ThrowIfNull(elements);

        for (var i = elements.Count - 1; i >= 0; i--)
        {
            var element = elements[i];
            if (!element.IsVisible || element.IsLocked)
            {
                continue;
            }

            if (documentPoint.X >= element.X
                && documentPoint.X <= element.X + element.Width
                && documentPoint.Y >= element.Y
                && documentPoint.Y <= element.Y + element.Height)
            {
                return element;
            }
        }

        return null;
    }

    public static IReadOnlyList<PanelElementModel> HitAllAtPoint(IReadOnlyList<PanelElementModel> elements, Point documentPoint)
    {
        ArgumentNullException.ThrowIfNull(elements);

        var hits = new List<PanelElementModel>();
        for (var i = elements.Count - 1; i >= 0; i--)
        {
            var element = elements[i];
            if (!element.IsVisible || element.IsLocked)
            {
                continue;
            }

            if (documentPoint.X >= element.X
                && documentPoint.X <= element.X + element.Width
                && documentPoint.Y >= element.Y
                && documentPoint.Y <= element.Y + element.Height)
            {
                hits.Add(element);
            }
        }

        return hits;
    }

    public static PanelElementModel? HitTopmostIntersectingRect(
        IReadOnlyList<PanelElementModel> elements,
        double minX,
        double minY,
        double maxX,
        double maxY)
    {
        ArgumentNullException.ThrowIfNull(elements);

        for (var i = elements.Count - 1; i >= 0; i--)
        {
            var element = elements[i];
            if (!element.IsVisible || element.IsLocked)
            {
                continue;
            }

            if (element.X <= maxX
                && (element.X + element.Width) >= minX
                && element.Y <= maxY
                && (element.Y + element.Height) >= minY)
            {
                return element;
            }
        }

        return null;
    }
}
