using System.Windows;

namespace OasisEditor;

/// <summary>Authoritative logical extents used by navigation, status and hit testing.</summary>
public static class EditorLogicalViewportBounds
{
    public static Rect Panel2D(DocumentTabViewModel document)
    {
        var elements = document.GetPanelElements();
        var background = elements.FirstOrDefault(e => e.Kind == PanelElementKind.Background && e.Width > 0 && e.Height > 0);
        if (background is not null) return new Rect(background.X, background.Y, background.Width, background.Height);

        Rect? result = null;
        foreach (var element in elements.Where(e => e.Width > 0 && e.Height > 0))
            result = Union(result, new Rect(element.X, element.Y, element.Width, element.Height));
        foreach (var shape in document.GetPanelFaceSourceShapes())
        {
            var points = new[] { shape.TopLeft, shape.TopRight, shape.BottomRight, shape.BottomLeft };
            var left = points.Min(p => p.X); var top = points.Min(p => p.Y);
            result = Union(result, new Rect(left, top, points.Max(p => p.X) - left, points.Max(p => p.Y) - top));
        }
        return result is { Width: > 0, Height: > 0 } bounds ? bounds : new Rect(0, 0, 1, 1);
    }

    public static Rect Face(DocumentTabViewModel document)
    {
        var source = document.GetFaceDocument().SourceRegion;
        return source is { Width: > 0, Height: > 0 }
            ? new Rect(0, 0, source.Width, source.Height)
            : new Rect(0, 0, FaceDocumentStorage.DefaultNativeLogicalWidth, FaceDocumentStorage.DefaultNativeLogicalHeight);
    }

    private static Rect Union(Rect? current, Rect next) => current is null ? next : Rect.Union(current.Value, next);
}
