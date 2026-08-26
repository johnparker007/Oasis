using System.Windows;

namespace OasisEditor;

/// <summary>Maps normalized raw-artwork coordinates through a fitted image rectangle and an editor-only viewport.</summary>
public readonly record struct ArtworkRegistrationViewportTransform(double Zoom, double PanX, double PanY)
{
    public const double MinZoom = PanelViewportTransform.MinZoom;
    public const double MaxZoom = PanelViewportTransform.MaxZoom;
    public const double ZoomStep = PanelViewportTransform.ZoomStep;

    public static ArtworkRegistrationViewportTransform Fit => new(1d, 0d, 0d);

    public double NormalizedZoom => Math.Clamp(Zoom, MinZoom, MaxZoom);

    public Rect ImageRect(Rect fittedImageRect)
    {
        var center = new Point(fittedImageRect.X + (fittedImageRect.Width / 2d), fittedImageRect.Y + (fittedImageRect.Height / 2d));
        var width = fittedImageRect.Width * NormalizedZoom;
        var height = fittedImageRect.Height * NormalizedZoom;
        return new Rect(center.X - (width / 2d) + PanX, center.Y - (height / 2d) + PanY, width, height);
    }

    public Point NormalizedToScreen(Point normalizedPoint, Rect fittedImageRect)
    {
        var imageRect = ImageRect(fittedImageRect);
        return new Point(imageRect.X + (normalizedPoint.X * imageRect.Width), imageRect.Y + (normalizedPoint.Y * imageRect.Height));
    }

    public Point ScreenToNormalized(Point screenPoint, Rect fittedImageRect)
    {
        var imageRect = ImageRect(fittedImageRect);
        return new Point((screenPoint.X - imageRect.X) / imageRect.Width, (screenPoint.Y - imageRect.Y) / imageRect.Height);
    }

    public ArtworkRegistrationViewportTransform WithPannedBy(Vector delta) => this with
    {
        PanX = PanX + delta.X,
        PanY = PanY + delta.Y
    };

    public ArtworkRegistrationViewportTransform WithZoomAt(Point pivotScreenPoint, double wheelDelta, Rect fittedImageRect)
    {
        var sourcePoint = ScreenToNormalized(pivotScreenPoint, fittedImageRect);
        var factor = wheelDelta > 0 ? ZoomStep : 1d / ZoomStep;
        var newZoom = Math.Clamp(NormalizedZoom * factor, MinZoom, MaxZoom);
        if (Math.Abs(newZoom - NormalizedZoom) < 0.0001d) return this with { Zoom = newZoom };

        var unpanned = new ArtworkRegistrationViewportTransform(newZoom, 0d, 0d)
            .NormalizedToScreen(sourcePoint, fittedImageRect);
        return new ArtworkRegistrationViewportTransform(
            newZoom,
            pivotScreenPoint.X - unpanned.X,
            pivotScreenPoint.Y - unpanned.Y);
    }
}
