using System.Windows;

namespace OasisEditor;

public readonly record struct PanelViewportTransform(double Zoom, double PanX, double PanY, double ScaleY = double.NaN,
    double RasterMagnification = double.NaN, double ScaleX = double.NaN)
{
    public const double MinZoom = EditorViewportTransform.MinZoom;
    public const double MaxZoom = EditorViewportTransform.MaxZoom;
    public const double ZoomStep = EditorViewportTransform.ZoomStep;

    public static PanelViewportTransform Identity => new(1d, 0d, 0d);

    public double NormalizedZoom => double.IsFinite(ScaleX) && ScaleX > 0d ? ScaleX : Math.Clamp(Zoom, MinZoom, MaxZoom);
    public double NormalizedScaleY => double.IsFinite(ScaleY) && ScaleY > 0d ? ScaleY : NormalizedZoom;
    public double NormalizedRasterMagnification => double.IsFinite(RasterMagnification) && RasterMagnification > 0d
        ? RasterMagnification : NormalizedZoom;

    /// <summary>Creates the legacy renderer-shaped adapter from the shared, centred viewport policy.</summary>
    public static PanelViewportTransform FromEditor(EditorViewportTransform transform, Rect viewport, Rect contentBounds,
        double dpiScaleX, double dpiScaleY, EditorViewportContentScale contentScale = default)
    {
        if (contentScale == default) contentScale = EditorViewportContentScale.Identity;
        var dpiX = ValidDpi(dpiScaleX); var dpiY = ValidDpi(dpiScaleY);
        var originDip = transform.ContentToScreen(new Point(contentBounds.X, contentBounds.Y), viewport, contentBounds,
            dpiX, dpiY, contentScale);
        // SKElement's surface/canvas uses device pixels, while WPF layout and mouse input use DIPs.
        var scaleX = transform.ClampedZoom * contentScale.X;
        var scaleY = transform.ClampedZoom * contentScale.Y;
        return new PanelViewportTransform(transform.ClampedZoom, originDip.X * dpiX - contentBounds.X * scaleX,
            originDip.Y * dpiY - contentBounds.Y * scaleY, scaleY, transform.ClampedZoom, scaleX);
    }

    /// <summary>Creates the WPF-DIP adapter used with mouse positions and other navigation input.</summary>
    public static PanelViewportTransform FromEditorNavigation(EditorViewportTransform transform, Rect viewport,
        Rect contentBounds, double dpiScaleX, double dpiScaleY, EditorViewportContentScale contentScale = default)
    {
        if (contentScale == default) contentScale = EditorViewportContentScale.Identity;
        var dpiX = ValidDpi(dpiScaleX); var dpiY = ValidDpi(dpiScaleY);
        var origin = transform.ContentToScreen(new Point(contentBounds.X, contentBounds.Y), viewport, contentBounds,
            dpiX, dpiY, contentScale);
        var scaleX = transform.ClampedZoom * contentScale.X / dpiX;
        var scaleY = transform.ClampedZoom * contentScale.Y / dpiY;
        return new PanelViewportTransform(transform.ClampedZoom, origin.X - contentBounds.X * scaleX,
            origin.Y - contentBounds.Y * scaleY, scaleY, transform.ClampedZoom, scaleX);
    }

    public Point DocumentToScreen(Point documentPoint)
    {
        var normalizedZoom = NormalizedZoom;
        return new Point(
            (documentPoint.X * normalizedZoom) + PanX,
            (documentPoint.Y * NormalizedScaleY) + PanY);
    }

    public Point ScreenToDocument(Point screenPoint)
    {
        var normalizedZoom = NormalizedZoom;
        return new Point(
            (screenPoint.X - PanX) / normalizedZoom,
            (screenPoint.Y - PanY) / NormalizedScaleY);
    }

    public PanelViewportTransform WithPannedBy(Vector delta)
    {
        return this with
        {
            PanX = PanX + delta.X,
            PanY = PanY + delta.Y
        };
    }

    public PanelViewportTransform WithZoomAt(Point pivotScreenPoint, double wheelDelta)
    {
        var previousZoom = NormalizedZoom;
        var zoomFactor = wheelDelta > 0 ? ZoomStep : 1d / ZoomStep;
        var newZoom = Math.Clamp(previousZoom * zoomFactor, MinZoom, MaxZoom);
        if (Math.Abs(previousZoom - newZoom) < 0.0001d)
        {
            return this with { Zoom = newZoom };
        }

        var worldX = (pivotScreenPoint.X - PanX) / previousZoom;
        var worldY = (pivotScreenPoint.Y - PanY) / previousZoom;

        return this with
        {
            Zoom = newZoom,
            PanX = pivotScreenPoint.X - (worldX * newZoom),
            PanY = pivotScreenPoint.Y - (worldY * newZoom)
        };
    }

    private static double ValidDpi(double value) => double.IsFinite(value) && value > 0d ? value : 1d;
}
