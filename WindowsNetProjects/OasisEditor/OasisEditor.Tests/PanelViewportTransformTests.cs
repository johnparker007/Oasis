using System.Windows;
using Xunit;

namespace OasisEditor.Tests;

public sealed class PanelViewportTransformTests
{
    [Fact]
    public void DocumentToScreen_And_Back_To_Document_RoundTrips()
    {
        var transform = new PanelViewportTransform(2.0d, 120d, -45d);
        var source = new Point(50d, 25d);

        var screen = transform.DocumentToScreen(source);
        var restored = transform.ScreenToDocument(screen);

        Assert.Equal(source.X, restored.X, 6);
        Assert.Equal(source.Y, restored.Y, 6);
    }

    [Fact]
    public void WithZoomAt_KeepsPivotDocumentPointStable()
    {
        var transform = new PanelViewportTransform(1d, 0d, 0d);
        var pivot = new Point(400d, 200d);
        var pivotDocumentBefore = transform.ScreenToDocument(pivot);

        var zoomed = transform.WithZoomAt(pivot, wheelDelta: 120);
        var pivotDocumentAfter = zoomed.ScreenToDocument(pivot);

        Assert.Equal(pivotDocumentBefore.X, pivotDocumentAfter.X, 6);
        Assert.Equal(pivotDocumentBefore.Y, pivotDocumentAfter.Y, 6);
    }

    [Fact]
    public void WithPannedBy_OffsetsPanCoordinates()
    {
        var transform = new PanelViewportTransform(1.5d, 10d, 20d);

        var moved = transform.WithPannedBy(new Vector(5d, -8d));

        Assert.Equal(15d, moved.PanX, 6);
        Assert.Equal(12d, moved.PanY, 6);
        Assert.Equal(1.5d, moved.Zoom, 6);
    }

    [Theory]
    [InlineData(0.001d, PanelViewportTransform.MinZoom)]
    [InlineData(99d, PanelViewportTransform.MaxZoom)]
    public void NormalizedZoom_ClampsToBounds(double inputZoom, double expected)
    {
        var transform = new PanelViewportTransform(inputZoom, 0d, 0d);

        Assert.Equal(expected, transform.NormalizedZoom, 6);
    }
}
