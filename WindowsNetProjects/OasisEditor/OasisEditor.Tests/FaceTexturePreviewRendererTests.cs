using OasisEditor;
using OasisEditor.Rendering;
using SkiaSharp;
using Xunit;

namespace OasisEditor.Tests;

public sealed class FaceTexturePreviewRendererTests : IDisposable
{
    private readonly string _testDirectory;

    public FaceTexturePreviewRendererTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"OasisFaceTexturePreviewTests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);
    }

    [Fact]
    public void Render_MissingRuntimeRenderAssets_ReturnsFallbackReason()
    {
        var renderer = CreateRenderer();
        var result = renderer.Render(new FaceDocumentModel(), new MachineRuntimeState());

        Assert.False(result.Rendered);
        Assert.Equal("missing runtime render assets", result.FallbackReason);
    }

    [Fact]
    public void Render_DimensionMismatch_ReturnsFallbackReason()
    {
        WriteSolidPng("artwork.png", 2, 2, new SKColor(100, 80, 60, 255));
        WriteSolidPng("mask.png", 3, 2, SKColors.White);
        WriteSolidPng("trayId.png", 2, 2, SKColors.Black);
        WriteSolidPng("lampIds0.png", 2, 2, new SKColor(7, 0, 0, 255));
        WriteSolidPng("lampWeights0.png", 2, 2, new SKColor(255, 0, 0, 255));
        var renderer = CreateRenderer();

        using var result = renderer.Render(CreateDocument(), new MachineRuntimeState());

        Assert.False(result.Rendered);
        Assert.Contains("dimension mismatch", result.FallbackReason);
    }

    [Fact]
    public void Render_LampIdAndWeight_ProducesExpectedLitPixel()
    {
        WriteSolidPng("artwork.png", 1, 1, new SKColor(100, 40, 20, 192));
        WriteSolidPng("mask.png", 1, 1, SKColors.White);
        WriteSolidPng("trayId.png", 1, 1, new SKColor(1, 0, 0, 255));
        WriteSolidPng("lampIds0.png", 1, 1, new SKColor(7, 0, 0, 255));
        WriteSolidPng("lampWeights0.png", 1, 1, new SKColor(255, 0, 0, 255));
        var renderer = CreateRenderer();
        var runtimeState = new MachineRuntimeState();
        runtimeState.SetLampIntensityIfChanged(MachineObjectReference.Lamp(7), 1d);

        using var result = renderer.Render(CreateDocument(width: 1, height: 1), runtimeState);

        Assert.True(result.Rendered);
        Assert.NotNull(result.Bitmap);
        var pixel = result.Bitmap.GetPixel(0, 0);
        Assert.Equal(125, pixel.Red);
        Assert.Equal(50, pixel.Green);
        Assert.Equal(25, pixel.Blue);
        Assert.Equal(192, pixel.Alpha);
    }

    [Fact]
    public void Render_LitRedArtwork_RemainsRecognisablyRedAndBrighter()
    {
        WriteSolidPng("artwork.png", 1, 1, new SKColor(120, 0, 0, 255));
        WriteSolidPng("mask.png", 1, 1, SKColors.White);
        WriteSolidPng("trayId.png", 1, 1, new SKColor(1, 0, 0, 255));
        WriteSolidPng("lampIds0.png", 1, 1, new SKColor(7, 0, 0, 255));
        WriteSolidPng("lampWeights0.png", 1, 1, new SKColor(255, 0, 0, 255));
        var renderer = CreateRenderer();
        var runtimeState = new MachineRuntimeState();
        runtimeState.SetLampIntensityIfChanged(MachineObjectReference.Lamp(7), 1d);

        using var result = renderer.Render(CreateDocument(width: 1, height: 1), runtimeState);

        Assert.True(result.Rendered);
        var pixel = result.Bitmap!.GetPixel(0, 0);
        Assert.True(pixel.Red > 120);
        Assert.Equal(0, pixel.Green);
        Assert.Equal(0, pixel.Blue);
    }

    [Fact]
    public void Render_LitBlueArtwork_RemainsRecognisablyBlueAndBrighter()
    {
        WriteSolidPng("artwork.png", 1, 1, new SKColor(0, 0, 120, 255));
        WriteSolidPng("mask.png", 1, 1, SKColors.White);
        WriteSolidPng("trayId.png", 1, 1, new SKColor(1, 0, 0, 255));
        WriteSolidPng("lampIds0.png", 1, 1, new SKColor(7, 0, 0, 255));
        WriteSolidPng("lampWeights0.png", 1, 1, new SKColor(255, 0, 0, 255));
        var renderer = CreateRenderer();
        var runtimeState = new MachineRuntimeState();
        runtimeState.SetLampIntensityIfChanged(MachineObjectReference.Lamp(7), 1d);

        using var result = renderer.Render(CreateDocument(width: 1, height: 1), runtimeState);

        Assert.True(result.Rendered);
        var pixel = result.Bitmap!.GetPixel(0, 0);
        Assert.Equal(0, pixel.Red);
        Assert.Equal(0, pixel.Green);
        Assert.True(pixel.Blue > 120);
    }

    [Fact]
    public void Render_ZeroLampState_LeavesArtworkAtAmbientOnly()
    {
        WriteSolidPng("artwork.png", 1, 1, new SKColor(100, 40, 20, 255));
        WriteSolidPng("mask.png", 1, 1, SKColors.White);
        WriteSolidPng("trayId.png", 1, 1, new SKColor(1, 0, 0, 255));
        WriteSolidPng("lampIds0.png", 1, 1, new SKColor(7, 0, 0, 255));
        WriteSolidPng("lampWeights0.png", 1, 1, new SKColor(255, 0, 0, 255));
        var renderer = CreateRenderer();

        using var result = renderer.Render(CreateDocument(width: 1, height: 1), new MachineRuntimeState());

        Assert.True(result.Rendered);
        Assert.NotNull(result.Bitmap);
        var pixel = result.Bitmap.GetPixel(0, 0);
        Assert.Equal(25, pixel.Red);
        Assert.Equal(10, pixel.Green);
        Assert.Equal(5, pixel.Blue);
        Assert.Equal(255, pixel.Alpha);
    }


    [Fact]
    public void Render_DefaultSettings_ZeroLampStateKeepsArtworkBrightness()
    {
        WriteSolidPng("artwork.png", 1, 1, new SKColor(100, 40, 20, 255));
        WriteSolidPng("mask.png", 1, 1, SKColors.White);
        WriteSolidPng("trayId.png", 1, 1, new SKColor(1, 0, 0, 255));
        WriteSolidPng("lampIds0.png", 1, 1, new SKColor(7, 0, 0, 255));
        WriteSolidPng("lampWeights0.png", 1, 1, new SKColor(255, 0, 0, 255));
        var renderer = new FaceTexturePreviewRenderer(path => string.IsNullOrWhiteSpace(path) ? null : Path.Combine(_testDirectory, path));

        using var result = renderer.Render(CreateDocument(width: 1, height: 1), new MachineRuntimeState());

        Assert.True(result.Rendered);
        Assert.NotNull(result.Bitmap);
        var pixel = result.Bitmap.GetPixel(0, 0);
        Assert.Equal(100, pixel.Red);
        Assert.Equal(40, pixel.Green);
        Assert.Equal(20, pixel.Blue);
        Assert.Equal(255, pixel.Alpha);
    }

    [Fact]
    public void Render_PreservesArtworkAlpha()
    {
        WriteSolidPng("artwork.png", 2, 1, SKColors.Transparent);
        WritePixel("artwork.png", 0, 0, 2, 1, new SKColor(100, 100, 100, 0));
        WritePixel("artwork.png", 1, 0, 2, 1, new SKColor(100, 100, 100, 127));
        WriteSolidPng("mask.png", 2, 1, SKColors.White);
        WriteSolidPng("trayId.png", 2, 1, new SKColor(1, 0, 0, 255));
        WriteSolidPng("lampIds0.png", 2, 1, new SKColor(7, 0, 0, 255));
        WriteSolidPng("lampWeights0.png", 2, 1, new SKColor(255, 0, 0, 255));
        var renderer = CreateRenderer();
        var runtimeState = new MachineRuntimeState();
        runtimeState.SetLampIntensityIfChanged(MachineObjectReference.Lamp(7), 1d);

        using var result = renderer.Render(CreateDocument(width: 2, height: 1), runtimeState);

        Assert.True(result.Rendered);
        Assert.NotNull(result.Bitmap);
        Assert.Equal(0, result.Bitmap.GetPixel(0, 0).Alpha);
        Assert.Equal(127, result.Bitmap.GetPixel(1, 0).Alpha);
    }


    [Fact]
    public void Render_ReusesDecodedTextureCacheOnSecondFrame()
    {
        WriteSolidPng("artwork.png", 1, 1, new SKColor(100, 40, 20, 255));
        WriteSolidPng("mask.png", 1, 1, SKColors.White);
        WriteSolidPng("trayId.png", 1, 1, new SKColor(1, 0, 0, 255));
        WriteSolidPng("lampIds0.png", 1, 1, new SKColor(7, 0, 0, 255));
        WriteSolidPng("lampWeights0.png", 1, 1, new SKColor(255, 0, 0, 255));
        var renderer = CreateRenderer();
        var runtimeState = new MachineRuntimeState();

        using var first = renderer.Render(CreateDocument(width: 1, height: 1), runtimeState);
        using var second = renderer.Render(CreateDocument(width: 1, height: 1), runtimeState);

        Assert.True(first.Rendered);
        Assert.True(second.Rendered);
        Assert.True(renderer.LastDiagnostics.ReusedTextureCache);
    }

    [Fact]
    public void Render_InvalidatesTextureCacheWhenAssetPathChanges()
    {
        WriteSolidPng("artwork.png", 1, 1, new SKColor(100, 40, 20, 255));
        WriteSolidPng("artwork-other.png", 1, 1, new SKColor(200, 80, 40, 255));
        WriteSolidPng("mask.png", 1, 1, SKColors.White);
        WriteSolidPng("trayId.png", 1, 1, new SKColor(1, 0, 0, 255));
        WriteSolidPng("lampIds0.png", 1, 1, new SKColor(7, 0, 0, 255));
        WriteSolidPng("lampWeights0.png", 1, 1, new SKColor(255, 0, 0, 255));
        var renderer = CreateRenderer();

        using var first = renderer.Render(CreateDocument(width: 1, height: 1), new MachineRuntimeState());
        using var second = renderer.Render(CreateDocument(width: 1, height: 1, artworkPath: "artwork-other.png"), new MachineRuntimeState());

        Assert.True(first.Rendered);
        Assert.True(second.Rendered);
        Assert.False(renderer.LastDiagnostics.ReusedTextureCache);
        Assert.Equal(50, second.Bitmap!.GetPixel(0, 0).Red);
    }

    [Fact]
    public void Render_InvalidatesTextureCacheWhenDimensionsChange()
    {
        WriteSolidPng("artwork.png", 2, 1, new SKColor(100, 40, 20, 255));
        WriteSolidPng("mask.png", 2, 1, SKColors.White);
        WriteSolidPng("trayId.png", 2, 1, new SKColor(1, 0, 0, 255));
        WriteSolidPng("lampIds0.png", 2, 1, new SKColor(7, 0, 0, 255));
        WriteSolidPng("lampWeights0.png", 2, 1, new SKColor(255, 0, 0, 255));
        var renderer = CreateRenderer();

        using var first = renderer.Render(CreateDocument(width: 2, height: 1), new MachineRuntimeState());
        using var second = renderer.Render(CreateDocument(width: 1, height: 1), new MachineRuntimeState());

        Assert.True(first.Rendered);
        Assert.False(second.Rendered);
        Assert.Contains("dimension mismatch", second.FallbackReason);
    }

    [Fact]
    public void Render_UsesLampLookupTableForConfiguredChannels()
    {
        WriteSolidPng("artwork.png", 1, 1, new SKColor(100, 40, 20, 255));
        WriteSolidPng("mask.png", 1, 1, SKColors.White);
        WriteSolidPng("trayId.png", 1, 1, new SKColor(1, 0, 0, 255));
        WriteSolidPng("lampIds0.png", 1, 1, new SKColor(7, 8, 0, 255));
        WriteSolidPng("lampWeights0.png", 1, 1, new SKColor(128, 128, 0, 255));
        var renderer = new FaceTexturePreviewRenderer(
            path => string.IsNullOrWhiteSpace(path) ? null : Path.Combine(_testDirectory, path),
            new FaceTexturePreviewSettings
            {
                AmbientStrength = 0.25d,
                EmissionStrength = 1d,
                MaskStrength = 1d,
                LampIds0ChannelCount = 2
            });
        var runtimeState = new MachineRuntimeState();
        runtimeState.SetLampIntensityIfChanged(MachineObjectReference.Lamp(7), 1d);
        runtimeState.SetLampIntensityIfChanged(MachineObjectReference.Lamp(8), 1d);

        using var result = renderer.Render(CreateDocument(width: 1, height: 1), runtimeState);

        Assert.True(result.Rendered);
        Assert.Equal(125, result.Bitmap!.GetPixel(0, 0).Red);
    }

    [Fact]
    public void Render_DefaultSettings_UsesRgbLampInfluenceChannels()
    {
        WriteSolidPng("artwork.png", 1, 1, new SKColor(100, 40, 20, 255));
        WriteSolidPng("mask.png", 1, 1, SKColors.White);
        WriteSolidPng("trayId.png", 1, 1, new SKColor(1, 0, 0, 255));
        WriteSolidPng("lampIds0.png", 1, 1, new SKColor(214, 215, 0, 255));
        WriteSolidPng("lampWeights0.png", 1, 1, new SKColor(128, 127, 0, 255));
        var renderer = new FaceTexturePreviewRenderer(path => string.IsNullOrWhiteSpace(path) ? null : Path.Combine(_testDirectory, path));
        var runtimeState = new MachineRuntimeState();
        runtimeState.SetLampIntensityIfChanged(MachineObjectReference.Lamp(214), 1d);
        runtimeState.SetLampIntensityIfChanged(MachineObjectReference.Lamp(215), 1d);

        using var result = renderer.Render(CreateDocument(width: 1, height: 1), runtimeState);

        Assert.True(result.Rendered);
        Assert.Equal(215, result.Bitmap!.GetPixel(0, 0).Red);
    }

    [Fact]
    public void Render_DefaultSettings_IncludesSecondRgbChannelContribution()
    {
        WriteSolidPng("artwork.png", 1, 1, new SKColor(100, 40, 20, 255));
        WriteSolidPng("mask.png", 1, 1, SKColors.White);
        WriteSolidPng("trayId.png", 1, 1, new SKColor(1, 0, 0, 255));
        WriteSolidPng("lampIds0.png", 1, 1, new SKColor(214, 215, 0, 255));
        WriteSolidPng("lampWeights0.png", 1, 1, new SKColor(128, 127, 0, 255));
        var renderer = new FaceTexturePreviewRenderer(path => string.IsNullOrWhiteSpace(path) ? null : Path.Combine(_testDirectory, path));
        var runtimeState = new MachineRuntimeState();
        runtimeState.SetLampIntensityIfChanged(MachineObjectReference.Lamp(214), 1d);

        using var firstChannelOnly = renderer.Render(CreateDocument(width: 1, height: 1), runtimeState);
        Assert.True(firstChannelOnly.Rendered);
        var firstChannelRed = firstChannelOnly.Bitmap!.GetPixel(0, 0).Red;

        runtimeState.SetLampIntensityIfChanged(MachineObjectReference.Lamp(215), 1d);
        using var bothChannels = renderer.Render(CreateDocument(width: 1, height: 1), runtimeState);

        Assert.True(bothChannels.Rendered);
        Assert.True(bothChannels.Bitmap!.GetPixel(0, 0).Red > firstChannelRed);
        Assert.Equal(215, bothChannels.Bitmap.GetPixel(0, 0).Red);
    }

    [Fact]
    public void Render_DefaultSettings_VariesBrightnessWithRgbInfluenceIntensity()
    {
        WriteSolidPng("artwork.png", 2, 1, new SKColor(100, 40, 20, 255));
        WriteSolidPng("mask.png", 2, 1, SKColors.White);
        WriteSolidPng("trayId.png", 2, 1, new SKColor(1, 0, 0, 255));
        WriteSolidPng("lampIds0.png", 2, 1, new SKColor(214, 215, 0, 255));
        WriteSolidPng("lampWeights0.png", 2, 1, new SKColor(128, 127, 0, 255));
        WritePixel("lampWeights0.png", 1, 0, 2, 1, new SKColor(64, 64, 0, 255));
        var renderer = new FaceTexturePreviewRenderer(path => string.IsNullOrWhiteSpace(path) ? null : Path.Combine(_testDirectory, path));
        var runtimeState = new MachineRuntimeState();
        runtimeState.SetLampIntensityIfChanged(MachineObjectReference.Lamp(214), 1d);
        runtimeState.SetLampIntensityIfChanged(MachineObjectReference.Lamp(215), 1d);

        using var result = renderer.Render(CreateDocument(width: 2, height: 1), runtimeState);

        Assert.True(result.Rendered);
        Assert.True(result.Bitmap!.GetPixel(0, 0).Red > result.Bitmap.GetPixel(1, 0).Red);
        Assert.Equal(215, result.Bitmap.GetPixel(0, 0).Red);
        Assert.Equal(158, result.Bitmap.GetPixel(1, 0).Red);
    }

    [Fact]
    public void Render_ReusesCompositionWhenLampStateIsUnchanged()
    {
        WriteSolidPng("artwork.png", 1, 1, new SKColor(100, 40, 20, 255));
        WriteSolidPng("mask.png", 1, 1, SKColors.White);
        WriteSolidPng("trayId.png", 1, 1, new SKColor(1, 0, 0, 255));
        WriteSolidPng("lampIds0.png", 1, 1, new SKColor(7, 0, 0, 255));
        WriteSolidPng("lampWeights0.png", 1, 1, new SKColor(255, 0, 0, 255));
        var renderer = CreateRenderer();
        var runtimeState = new MachineRuntimeState();
        runtimeState.SetLampIntensityIfChanged(MachineObjectReference.Lamp(7), 1d);

        using var first = renderer.Render(CreateDocument(width: 1, height: 1), runtimeState);
        using var second = renderer.Render(CreateDocument(width: 1, height: 1), runtimeState);

        Assert.True(first.Rendered);
        Assert.True(second.Rendered);
        Assert.True(renderer.LastDiagnostics.ReusedComposition);
        Assert.Same(first.Bitmap, second.Bitmap);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }

    private FaceTexturePreviewRenderer CreateRenderer()
    {
        return new FaceTexturePreviewRenderer(
            path => string.IsNullOrWhiteSpace(path) ? null : Path.Combine(_testDirectory, path),
            new FaceTexturePreviewSettings
            {
                AmbientStrength = 0.25d,
                EmissionStrength = 1d,
                MaskStrength = 1d
            });
    }

    private static FaceDocumentModel CreateDocument(int width = 2, int height = 2, string artworkPath = "artwork.png")
    {
        return new FaceDocumentModel
        {
            RuntimeRenderAssets = new FaceRuntimeRenderAssetsModel
            {
                ArtworkPath = artworkPath,
                MaskPath = "mask.png",
                TrayIdPath = "trayId.png",
                LampIds0Path = "lampIds0.png",
                LampWeights0Path = "lampWeights0.png",
                Width = width,
                Height = height
            }
        };
    }

    private void WriteSolidPng(string relativePath, int width, int height, SKColor color)
    {
        using var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        bitmap.Erase(color);
        WriteBitmap(relativePath, bitmap);
    }

    private void WritePixel(string relativePath, int x, int y, int width, int height, SKColor color)
    {
        using var bitmap = SKBitmap.Decode(Path.Combine(_testDirectory, relativePath));
        bitmap.SetPixel(x, y, color);
        WriteBitmap(relativePath, bitmap);
    }

    private void WriteBitmap(string relativePath, SKBitmap bitmap)
    {
        var path = Path.Combine(_testDirectory, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.Create(path);
        data.SaveTo(stream);
    }
}
