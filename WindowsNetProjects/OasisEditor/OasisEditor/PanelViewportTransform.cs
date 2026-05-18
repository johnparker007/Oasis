using System.Windows;

namespace OasisEditor;

public readonly record struct PanelViewportTransform(double Zoom, double PanX, double PanY)
{
    public const double MinZoom = 0.25d;
    public const double MaxZoom = 4.0d;
    public const double ZoomStep = 1.1d;

    public static PanelViewportTransform Identity => new(1d, 0d, 0d);

    public double NormalizedZoom => Math.Clamp(Zoom, MinZoom, MaxZoom);

    public Point DocumentToScreen(Point documentPoint)
    {
        var normalizedZoom = NormalizedZoom;
        return new Point(
            (documentPoint.X * normalizedZoom) + PanX,
            (documentPoint.Y * normalizedZoom) + PanY);
    }

    public Point ScreenToDocument(Point screenPoint)
    {
        var normalizedZoom = NormalizedZoom;
        return new Point(
            (screenPoint.X - PanX) / normalizedZoom,
            (screenPoint.Y - PanY) / normalizedZoom);
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
}
