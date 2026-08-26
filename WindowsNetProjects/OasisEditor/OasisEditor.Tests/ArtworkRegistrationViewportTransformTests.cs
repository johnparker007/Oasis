using System.Windows;
using Xunit;

namespace OasisEditor.Tests;

public sealed class ArtworkRegistrationViewportTransformTests
{
    private static readonly Rect Fitted = new(24d, 54d, 800d, 450d);

    [Theory]
    [InlineData(1d, 0d, 0d, 0d, 0d)]
    [InlineData(1d, 0d, 0d, 1d, 1d)]
    [InlineData(2.5d, 0d, 0d, 0.02d, 0.98d)]
    [InlineData(3.75d, 137d, -92d, 0.73d, 0.16d)]
    public void NormalizedCoordinatesRoundTrip(double zoom, double panX, double panY, double x, double y)
    {
        var transform = new ArtworkRegistrationViewportTransform(zoom, panX, panY);
        var source = new Point(x, y);

        var result = transform.ScreenToNormalized(transform.NormalizedToScreen(source, Fitted), Fitted);

        Assert.Equal(source.X, result.X, 10);
        Assert.Equal(source.Y, result.Y, 10);
    }

    [Fact]
    public void ZoomKeepsSourcePointUnderCursor()
    {
        var transform = new ArtworkRegistrationViewportTransform(1.6d, 41d, -23d);
        var cursor = new Point(218d, 301d);
        var sourceUnderCursor = transform.ScreenToNormalized(cursor, Fitted);

        var zoomed = transform.WithZoomAt(cursor, 120d, Fitted);

        var result = zoomed.NormalizedToScreen(sourceUnderCursor, Fitted);
        Assert.Equal(cursor.X, result.X, 10);
        Assert.Equal(cursor.Y, result.Y, 10);
    }

    [Fact]
    public void FitRestoresFittedImageRectangle()
    {
        Assert.Equal(Fitted, ArtworkRegistrationViewportTransform.Fit.ImageRect(Fitted));
        Assert.Equal(1d, ArtworkRegistrationViewportTransform.Fit.Zoom);
        Assert.Equal(0d, ArtworkRegistrationViewportTransform.Fit.PanX);
        Assert.Equal(0d, ArtworkRegistrationViewportTransform.Fit.PanY);
    }
}
