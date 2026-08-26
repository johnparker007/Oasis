using System.Windows;
using Xunit;

namespace OasisEditor.Tests;

public sealed class ArtworkRegistrationViewportTransformTests
{
    private static readonly Rect Viewport = new(24, 24, 1200, 800);

    [Theory]
    [InlineData(800, 600, 1, 1.3333333333333333)]
    [InlineData(6000, 4000, 1, 0.2)]
    [InlineData(6000, 4000, 1.5, 0.3)]
    [InlineData(6000, 4000, 2, 0.4)]
    public void FitZoomIsAnActualPixelPercentage(double width, double height, double dpi, double expected) =>
        Assert.Equal(expected, EditorViewportTransform.CalculateFitZoom(Viewport, width, height, dpi, dpi), 10);

    [Theory]
    [InlineData(1, 1)]
    [InlineData(1.5, 0.6666666666666666)]
    [InlineData(2, 0.5)]
    public void ActualPixelsMapsOneSourcePixelToOnePhysicalPixel(double dpi, double expectedDips)
    {
        var rect = new EditorViewportTransform(1, 0, 0).ContentRect(Viewport, 500, 500, dpi, dpi);
        Assert.Equal(expectedDips, rect.Width / 500, 10);
        Assert.Equal(1, rect.Width / 500 * dpi, 10);
    }

    [Fact]
    public void MagnificationIsIndependentOfSourceResolution()
    {
        var transform = new EditorViewportTransform(16, 0, 0);
        var small = transform.ContentRect(Viewport, 500, 500, 1.5, 1.5).Width / 500;
        var large = transform.ContentRect(Viewport, 6000, 4000, 1.5, 1.5).Width / 6000;
        Assert.Equal(small, large, 10);
    }

    [Theory]
    [InlineData(0.2, 0, 0)]
    [InlineData(1, 0, 0)]
    [InlineData(4, 0, 0)]
    [InlineData(16, 137, -92)]
    public void NormalizedCoordinatesRoundTrip(double zoom, double panX, double panY)
    {
        var transform = new ArtworkRegistrationViewportTransform(zoom, panX, panY);
        var source = new Point(.73, .16);
        var screen = transform.NormalizedToScreen(source, Viewport, 6000, 4000, 1.5, 1.5);
        var result = transform.ScreenToNormalized(screen, Viewport, 6000, 4000, 1.5, 1.5);
        Assert.Equal(source.X, result.X, 10); Assert.Equal(source.Y, result.Y, 10);
    }

    [Fact]
    public void ZoomKeepsSourcePointUnderCursor()
    {
        var transform = new ArtworkRegistrationViewportTransform(.3, 41, -23);
        var cursor = new Point(218, 301);
        var source = transform.ScreenToNormalized(cursor, Viewport, 6000, 4000, 1.5, 1.5);
        var zoomed = transform.WithZoomAt(cursor, 16, Viewport, 6000, 4000, 1.5, 1.5);
        var result = zoomed.NormalizedToScreen(source, Viewport, 6000, 4000, 1.5, 1.5);
        Assert.Equal(cursor.X, result.X, 9); Assert.Equal(cursor.Y, result.Y, 9);
    }

    [Fact]
    public void FitRecentresAndResetsPan()
    {
        var fit = ArtworkRegistrationViewportTransform.FitTo(Viewport, 6000, 4000, 1.5, 1.5);
        Assert.Equal(.3, fit.Zoom, 10); Assert.Equal(0, fit.PanX); Assert.Equal(0, fit.PanY);
    }

    [Theory]
    [InlineData("100", 1)] [InlineData("100%", 1)] [InlineData("12.5%", .125)]
    [InlineData("2400%", 24)] [InlineData("99999%", 64)]
    public void ParsesAndClampsZoom(string text, double expected)
    {
        Assert.True(EditorViewportTransform.TryParseZoomPercentage(text, out var zoom)); Assert.Equal(expected, zoom);
    }

    [Theory] [InlineData("")] [InlineData("Fit")] [InlineData("nope")] [InlineData("-1")]
    public void RejectsInvalidZoom(string text) => Assert.False(EditorViewportTransform.TryParseZoomPercentage(text, out _));

    [Fact]
    public void SourceCoordinatesUseZeroBasedPixelsOnlyInsideImage()
    {
        var transform = new EditorViewportTransform(1, 0, 0);
        var screen = transform.ContentToScreen(new Point(2841.9, 1267.2), Viewport, 6000, 4000, 1, 1);
        var source = transform.ScreenToContent(screen, Viewport, 6000, 4000, 1, 1);
        Assert.Equal(2841, (int)Math.Floor(source.X)); Assert.Equal(1267, (int)Math.Floor(source.Y));
    }
}
