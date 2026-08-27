using SkiaSharp;
using Xunit;

namespace OasisEditor.Tests;

public sealed class FaceArtworkSharpeningServiceTests
{
    [Theory]
    [InlineData(false, 1.0)]
    [InlineData(true, 0.0)]
    public void Apply_BypassSettingsPreserveEveryPixel(bool enabled, double amount)
    {
        using var source = Gradient();
        using var result = FaceArtworkSharpeningService.Apply(source, Settings(enabled, amount));
        AssertPixelsEqual(source, result);
    }

    [Fact]
    public void Apply_IncreasesSoftEdgeContrastAndPreservesDimensions()
    {
        using var source = new SKBitmap(7, 1, SKColorType.Rgba8888, SKAlphaType.Premul);
        byte[] values = [20, 35, 80, 120, 175, 220, 235];
        for (var x = 0; x < values.Length; x++) source.SetPixel(x, 0, new SKColor(values[x], values[x], values[x]));
        using var result = FaceArtworkSharpeningService.Apply(source, Settings(true, 1));
        Assert.Equal(source.Width, result.Width);
        Assert.True(result.GetPixel(4, 0).Red - result.GetPixel(2, 0).Red > source.GetPixel(4, 0).Red - source.GetPixel(2, 0).Red);
    }

    [Fact]
    public void Apply_FlatColorRemainsFlat()
    {
        using var source = new SKBitmap(8, 8, SKColorType.Rgba8888, SKAlphaType.Premul);
        source.Erase(new SKColor(80, 120, 180, 160));
        using var result = FaceArtworkSharpeningService.Apply(source, Settings(true, 1));
        AssertPixelsEqual(source, result);
    }

    [Fact]
    public void Apply_ThresholdSuppressesSubtleButRetainsStrongDetail()
    {
        using var source = new SKBitmap(9, 1, SKColorType.Rgba8888, SKAlphaType.Premul);
        byte[] values = [100, 100, 102, 102, 102, 102, 180, 180, 180];
        for (var x = 0; x < values.Length; x++) source.SetPixel(x, 0, new SKColor(values[x], values[x], values[x]));
        using var result = FaceArtworkSharpeningService.Apply(source, new FaceGenerationSettingsModel { PostWarpSharpeningEnabled = true, PostWarpSharpeningAmount = 1, PostWarpSharpeningRadiusPixels = .75, PostWarpSharpeningThreshold = 10 });
        Assert.Equal(source.GetPixel(2, 0), result.GetPixel(2, 0));
        Assert.NotEqual(source.GetPixel(6, 0), result.GetPixel(6, 0));
    }

    [Fact]
    public void Apply_PreservesAlphaAndTransparentPixelsRemainHarmless()
    {
        using var source = new SKBitmap(4, 1, SKColorType.Rgba8888, SKAlphaType.Premul);
        source.SetPixel(0, 0, SKColors.Transparent);
        source.SetPixel(1, 0, new SKColor(255, 20, 20, 64));
        source.SetPixel(2, 0, new SKColor(255, 20, 20, 160));
        source.SetPixel(3, 0, new SKColor(255, 20, 20, 255));
        using var result = FaceArtworkSharpeningService.Apply(source, Settings(true, 1));
        for (var x = 0; x < source.Width; x++) Assert.Equal(source.GetPixel(x, 0).Alpha, result.GetPixel(x, 0).Alpha);
        var transparent = result.GetPixel(0, 0);
        Assert.Equal(0, transparent.Alpha);
        Assert.Equal(0, transparent.Red);
        Assert.Equal(0, transparent.Green);
        Assert.Equal(0, transparent.Blue);
    }

    [Fact]
    public void Apply_BgraStoragePreservesFlatColourAndAlpha()
    {
        using var source = new SKBitmap(5, 3, SKColorType.Bgra8888, SKAlphaType.Premul);
        source.Erase(new SKColor(210, 70, 20, 127));
        using var result = FaceArtworkSharpeningService.Apply(source, Settings(true, 1));
        AssertPixelsEqual(source, result);
    }

    private static FaceGenerationSettingsModel Settings(bool enabled, double amount) => new() { PostWarpSharpeningEnabled = enabled, PostWarpSharpeningAmount = amount, PostWarpSharpeningRadiusPixels = .75, PostWarpSharpeningThreshold = 0 };
    private static SKBitmap Gradient() { var bitmap = new SKBitmap(5, 2, SKColorType.Rgba8888, SKAlphaType.Premul); for (var y = 0; y < 2; y++) for (var x = 0; x < 5; x++) bitmap.SetPixel(x, y, new SKColor((byte)(x * 40), 30, 90, (byte)(100 + x * 30))); return bitmap; }
    private static void AssertPixelsEqual(SKBitmap expected, SKBitmap actual) { Assert.Equal(expected.Width, actual.Width); Assert.Equal(expected.Height, actual.Height); for (var y = 0; y < expected.Height; y++) for (var x = 0; x < expected.Width; x++) Assert.Equal(expected.GetPixel(x, y), actual.GetPixel(x, y)); }
}
