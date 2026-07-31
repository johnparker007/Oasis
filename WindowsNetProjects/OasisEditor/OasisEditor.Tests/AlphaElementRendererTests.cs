using OasisEditor.Rendering;
using SkiaSharp;
using Xunit;

namespace OasisEditor.Tests;

public sealed class AlphaElementRendererTests
{
    [Theory]
    [InlineData(0d, 0)]
    [InlineData(1d, 31)]
    [InlineData(-0.01d, 0)]
    [InlineData(1.01d, 31)]
    public void ToBrightnessBucket_ClampsToAmberRange(double brightness, int expectedBucket)
    {
        Assert.Equal(expectedBucket, AlphaElementRenderer.ToBrightnessBucket(brightness));
    }

    [Fact]
    public void ToBrightnessBucket_PreservesEveryNativeAmberLevel()
    {
        var buckets = Enumerable.Range(0, 32)
            .Select(level => AlphaElementRenderer.ToBrightnessBucket(level / 31d))
            .ToArray();

        Assert.Equal(Enumerable.Range(0, 32), buckets);
        Assert.Equal(15, AlphaElementRenderer.ToBrightnessBucket(15d / 31d));
        Assert.DoesNotContain(15d / 31d, new[] { 0d, 0.25d, 0.5d, 0.75d, 1d });
    }

    [Fact]
    public void FromBrightnessBucket_ReconstructsAmberNormalizedBrightness()
    {
        Assert.Equal(15d / 31d, AlphaElementRenderer.FromBrightnessBucket(15));
        Assert.NotEqual(15d / 4d, AlphaElementRenderer.FromBrightnessBucket(15));
    }

    [Fact]
    public void RenderAlphaDisplay_SameMaskAndBrightness_ReusesCachedVisual()
    {
        using var surface = SKSurface.Create(new SKImageInfo(137, 59));
        AlphaElementRenderer.ResetDiagnosticsCounters();

        Render(surface.Canvas, 0x1357, 12d / 31d, "#FF12A4C8");
        Render(surface.Canvas, 0x1357, 12d / 31d, "#FF12A4C8");

        Assert.Equal(1, AlphaElementRenderer.DiagnosticsCacheMisses);
        Assert.Equal(1, AlphaElementRenderer.DiagnosticsCacheHits);
    }

    [Fact]
    public void RenderAlphaDisplay_AdjacentAmberLevels_CreateDistinctCachedVisuals()
    {
        using var surface = SKSurface.Create(new SKImageInfo(139, 61));
        AlphaElementRenderer.ResetDiagnosticsCounters();

        Render(surface.Canvas, 0x2468, 14d / 31d, "#FF7B35D1");
        Render(surface.Canvas, 0x2468, 15d / 31d, "#FF7B35D1");

        Assert.Equal(2, AlphaElementRenderer.DiagnosticsCacheMisses);
        Assert.Equal(0, AlphaElementRenderer.DiagnosticsCacheHits);
    }

    private static void Render(SKCanvas canvas, int mask, double brightness, string onColorHex)
    {
        AlphaElementRenderer.RenderAlphaDisplay(
            canvas,
            SKRect.Create(137f, 59f),
            [mask],
            [brightness],
            "led16seg",
            onColorHex,
            "#FF010203",
            showDecimalPoint: true,
            showCommaTail: true);
    }
}
