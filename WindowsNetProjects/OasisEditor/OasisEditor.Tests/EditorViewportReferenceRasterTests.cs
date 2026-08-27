using System.Windows;
using Xunit;

namespace OasisEditor.Tests;

public sealed class EditorViewportReferenceRasterTests
{
    [Theory]
    [InlineData(1d, 1d)]
    [InlineData(64d, 64d)]
    public void DifferentLogicalMappingsGiveSameRasterPixelSize(double zoom, double expectedPhysicalPixels)
    {
        var viewport = new Rect(0, 0, 800, 600);
        var bounds = new Rect(0, 0, 1000, 500);
        var panel = PanelViewportTransform.FromEditor(new EditorViewportTransform(zoom, 0, 0), viewport, bounds,
            1d, 1d, EditorViewportContentScale.FromMapping(1000, 500, 1000, 500));
        var face = PanelViewportTransform.FromEditor(new EditorViewportTransform(zoom, 0, 0), viewport, bounds,
            1d, 1d, EditorViewportContentScale.FromMapping(4000, 2000, 1000, 500));

        Assert.Equal(expectedPhysicalPixels, panel.NormalizedZoom / 1d, 10);
        Assert.Equal(expectedPhysicalPixels, face.NormalizedZoom / 4d, 10);
    }

    [Theory]
    [InlineData(1d)]
    [InlineData(1.25d)]
    [InlineData(1.5d)]
    [InlineData(2d)]
    public void SkiaDeviceTransformAndDipNavigationRemainConsistentAtEveryDpi(double dpi)
    {
        var viewport = new Rect(0, 0, 800, 600);
        var bounds = new Rect(0, 0, 1000, 500);
        var density = new EditorViewportContentScale(4d, 4d);
        var editor = new EditorViewportTransform(1d, 17d, -9d);
        var render = PanelViewportTransform.FromEditor(editor, viewport, bounds, dpi, dpi, density);
        var input = PanelViewportTransform.FromEditorNavigation(editor, viewport, bounds, dpi, dpi, density);
        var logical = new Point(237, 91);

        var dip = input.DocumentToScreen(logical);
        var physical = render.DocumentToScreen(logical);

        Assert.Equal(dip.X * dpi, physical.X, 8);
        Assert.Equal(dip.Y * dpi, physical.Y, 8);
        Assert.Equal(logical.X, input.ScreenToDocument(dip).X, 8);
        Assert.Equal(4d, render.NormalizedZoom, 8);
    }

    [Fact]
    public void FitPercentageUsesDisplayedRasterExtentRatherThanLogicalWidth()
    {
        var viewport = new Rect(0, 0, 1000, 1000);
        var first = EditorViewportTransform.CalculateFitZoom(viewport, 1000, 500, 1d, 1d,
            EditorViewportContentScale.FromMapping(4000, 2000, 1000, 500));
        var second = EditorViewportTransform.CalculateFitZoom(viewport, 973, 486.5, 1d, 1d,
            EditorViewportContentScale.FromMapping(4000, 2000, 973, 486.5));

        Assert.Equal(.25d, first, 10);
        Assert.Equal(first, second, 10);
    }

    [Fact]
    public void FixedPercentageIsIndependentOfViewportAndDocumentExtent()
    {
        var density = EditorViewportContentScale.FromMapping(4000, 2000, 1000, 500);
        var small = PanelViewportTransform.FromEditor(new EditorViewportTransform(8, 0, 0),
            new Rect(0, 0, 200, 100), new Rect(0, 0, 1000, 500), 1, 1, density);
        var large = PanelViewportTransform.FromEditor(new EditorViewportTransform(8, 0, 0),
            new Rect(0, 0, 2000, 1000), new Rect(-300, -200, 1800, 1200), 1, 1, density);

        Assert.Equal(small.NormalizedZoom, large.NormalizedZoom, 10);
        Assert.Equal(8d, small.NormalizedZoom / density.X, 10);
    }

    [Fact]
    public void FaceDensityUsesDisplayedProvenanceCropAndPreservesMaterialAspectMismatch()
    {
        var artwork = new FaceArtworkElement
        {
            Width = 1000,
            Height = 700,
            SourceRegion = new FaceSourceRegionModel { X = 250, Y = 100, Width = 500, Height = 700 },
            Provenance = new FaceArtworkProvenanceModel
            {
                SourceElementBounds = new FaceSourceRegionModel { X = 0, Y = 0, Width = 1000, Height = 1000 }
            }
        };

        var source = FaceArtworkRasterMapping.ResolveSourceRect(artwork, 4000, 2800);
        var density = FaceArtworkRasterMapping.ContentScale(artwork, 4000, 2800);

        Assert.Equal(2000f, source.Width);
        Assert.Equal(1960f, source.Height);
        Assert.Equal(2d, density.X, 10);
        Assert.Equal(2.8d, density.Y, 10);
        Assert.NotEqual(density.X, density.Y);
    }
}
