using System.Globalization;
using System.Windows;

namespace OasisEditor;

/// <summary>Transient, DPI-aware navigation for raster editor viewports.</summary>
public readonly record struct EditorViewportTransform(double Zoom, double PanX, double PanY)
{
    public const double MinZoom = 0.01d;
    public const double MaxZoom = 64d;
    public const double ZoomStep = 1.1d;

    public double ClampedZoom => Math.Clamp(Zoom, MinZoom, MaxZoom);

    public static double CalculateFitZoom(Rect viewport, double contentWidth, double contentHeight,
        double dpiScaleX, double dpiScaleY)
    {
        if (contentWidth <= 0 || contentHeight <= 0 || viewport.Width <= 0 || viewport.Height <= 0) return MinZoom;
        return Math.Clamp(Math.Min(viewport.Width * ValidDpi(dpiScaleX) / contentWidth,
            viewport.Height * ValidDpi(dpiScaleY) / contentHeight), MinZoom, MaxZoom);
    }

    public static EditorViewportTransform Fit(Rect viewport, double contentWidth, double contentHeight,
        double dpiScaleX, double dpiScaleY) => new(CalculateFitZoom(viewport, contentWidth, contentHeight, dpiScaleX, dpiScaleY), 0, 0);

    public Rect ContentRect(Rect viewport, double contentWidth, double contentHeight, double dpiScaleX, double dpiScaleY)
    {
        var width = contentWidth * ClampedZoom / ValidDpi(dpiScaleX);
        var height = contentHeight * ClampedZoom / ValidDpi(dpiScaleY);
        return new Rect(viewport.X + (viewport.Width - width) / 2d + PanX,
            viewport.Y + (viewport.Height - height) / 2d + PanY, width, height);
    }

    public Point ContentToScreen(Point content, Rect viewport, double width, double height, double dpiX, double dpiY)
    {
        var rect = ContentRect(viewport, width, height, dpiX, dpiY);
        return new Point(rect.X + content.X * rect.Width / width, rect.Y + content.Y * rect.Height / height);
    }

    public Point ScreenToContent(Point screen, Rect viewport, double width, double height, double dpiX, double dpiY)
    {
        var rect = ContentRect(viewport, width, height, dpiX, dpiY);
        return new Point((screen.X - rect.X) * width / rect.Width, (screen.Y - rect.Y) * height / rect.Height);
    }

    public EditorViewportTransform WithZoomAt(Point pivot, double newZoom, Rect viewport, double width, double height,
        double dpiX, double dpiY)
    {
        var content = ScreenToContent(pivot, viewport, width, height, dpiX, dpiY);
        var next = new EditorViewportTransform(Math.Clamp(newZoom, MinZoom, MaxZoom), 0, 0);
        var withoutPan = next.ContentToScreen(content, viewport, width, height, dpiX, dpiY);
        return next with { PanX = pivot.X - withoutPan.X, PanY = pivot.Y - withoutPan.Y };
    }

    public static bool TryParseZoomPercentage(string? text, out double zoom)
    {
        zoom = 0;
        var value = text?.Trim().TrimEnd('%').Trim();
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out var percentage) ||
            !double.IsFinite(percentage) || percentage <= 0) return false;
        zoom = Math.Clamp(percentage / 100d, MinZoom, MaxZoom);
        return true;
    }

    private static double ValidDpi(double value) => double.IsFinite(value) && value > 0 ? value : 1d;
}
