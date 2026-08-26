using System.Windows;

namespace OasisEditor;

/// <summary>Maps Geometry's normalized coordinates through the shared actual-pixel viewport.</summary>
public readonly record struct ArtworkRegistrationViewportTransform(double Zoom, double PanX, double PanY)
{
    public const double MinZoom = EditorViewportTransform.MinZoom;
    public const double MaxZoom = EditorViewportTransform.MaxZoom;
    public const double ZoomStep = EditorViewportTransform.ZoomStep;
    private EditorViewportTransform Core => new(Zoom, PanX, PanY);

    public static ArtworkRegistrationViewportTransform FitTo(Rect viewport, double width, double height, double dpiX, double dpiY)
    {
        var fit = EditorViewportTransform.Fit(viewport, width, height, dpiX, dpiY);
        return new(fit.Zoom, 0, 0);
    }

    public Rect ImageRect(Rect viewport, double width, double height, double dpiX, double dpiY) =>
        Core.ContentRect(viewport, width, height, dpiX, dpiY);

    public Point NormalizedToScreen(Point point, Rect viewport, double width, double height, double dpiX, double dpiY) =>
        Core.ContentToScreen(new Point(point.X * width, point.Y * height), viewport, width, height, dpiX, dpiY);

    public Point ScreenToNormalized(Point point, Rect viewport, double width, double height, double dpiX, double dpiY)
    {
        var content = Core.ScreenToContent(point, viewport, width, height, dpiX, dpiY);
        return new Point(content.X / width, content.Y / height);
    }

    public ArtworkRegistrationViewportTransform WithPannedBy(Vector delta) => this with
    {
        PanX = PanX + delta.X,
        PanY = PanY + delta.Y
    };

    public ArtworkRegistrationViewportTransform WithZoomAt(Point pivot, double newZoom, Rect viewport, double width, double height, double dpiX, double dpiY)
    {
        var next = Core.WithZoomAt(pivot, newZoom, viewport, width, height, dpiX, dpiY);
        return new(next.Zoom, next.PanX, next.PanY);
    }
}
