using OasisEditor.Rendering;
using SkiaSharp;
using Xunit;
using System.IO;

namespace OasisEditor.Tests;

public sealed class Panel2DRendererTests
{
    [Fact]
    public void Render_DispatchesVisibleElementsToMatchingRenderer()
    {
        var lampRenderer = new FakeRenderer(PanelElementKind.Lamp);
        var reelRenderer = new FakeRenderer(PanelElementKind.Reel);
        var renderer = new Panel2DRenderer([lampRenderer, reelRenderer]);
        var runtimeState = new PanelRuntimeState();
        using var surface = SKSurface.Create(new SKImageInfo(64, 64));

        renderer.Render(
            surface.Canvas,
            [
                new PanelElementModel { Kind = PanelElementKind.Lamp, IsVisible = true, ObjectId = "lamp-1", Name = "Lamp" },
                new PanelElementModel { Kind = PanelElementKind.Reel, IsVisible = true, ObjectId = "reel-1", Name = "Reel" },
                new PanelElementModel { Kind = PanelElementKind.Alpha, IsVisible = true, ObjectId = "alpha-1", Name = "Alpha" }
            ],
            runtimeState,
            PanelViewportTransform.Identity);

        Assert.Equal(1, lampRenderer.Rendered.Count);
        Assert.Equal("lamp-1", lampRenderer.Rendered[0].ObjectId);
        Assert.Equal(1, reelRenderer.Rendered.Count);
        Assert.Equal("reel-1", reelRenderer.Rendered[0].ObjectId);
    }

    [Fact]
    public void Render_SkipsHiddenElements()
    {
        var lampRenderer = new FakeRenderer(PanelElementKind.Lamp);
        var renderer = new Panel2DRenderer([lampRenderer]);
        using var surface = SKSurface.Create(new SKImageInfo(64, 64));

        renderer.Render(
            surface.Canvas,
            [
                new PanelElementModel { Kind = PanelElementKind.Lamp, IsVisible = false, ObjectId = "lamp-hidden", Name = "Hidden" }
            ],
            new PanelRuntimeState(),
            PanelViewportTransform.Identity);

        Assert.Empty(lampRenderer.Rendered);
    }


    [Fact]
    public void Render_WithLampRenderer_DoesNotThrow()
    {
        var renderer = new Panel2DRenderer([new LampElementRenderer()]);
        var runtimeState = new PanelRuntimeState();
        runtimeState.SetLampIntensity("lamp-1", 0.75d);
        using var surface = SKSurface.Create(new SKImageInfo(64, 64));

        renderer.Render(
            surface.Canvas,
            [
                new PanelElementModel
                {
                    Kind = PanelElementKind.Lamp,
                    IsVisible = true,
                    ObjectId = "lamp-1",
                    Name = "Lamp",
                    Width = 20,
                    Height = 20,
                    OnColorHex = "#FF0000",
                    OffColorHex = "#110000",
                    DisplayText = "HI",
                    TextColorHex = "#FFFFFF",
                    TextBoxFontSize = "8"
                }
            ],
            runtimeState,
            PanelViewportTransform.Identity);
    }

    [Fact]
    public void Render_WithReelRenderer_DoesNotThrow()
    {
        var renderer = new Panel2DRenderer([new ReelElementRenderer()]);
        var runtimeState = new PanelRuntimeState();
        runtimeState.SetReelPositionIfChanged("reel-1", 31);
        using var surface = SKSurface.Create(new SKImageInfo(64, 64));

        renderer.Render(
            surface.Canvas,
            [
                new PanelElementModel
                {
                    Kind = PanelElementKind.Reel,
                    IsVisible = true,
                    ObjectId = "reel-1",
                    Name = "Reel",
                    Width = 24,
                    Height = 24,
                    Stops = 16
                }
            ],
            runtimeState,
            PanelViewportTransform.Identity);
    }

    [Fact]
    public void Render_WithReelRendererAndMissingAsset_UsesPlaceholderFallback()
    {
        var renderer = new Panel2DRenderer([new ReelElementRenderer()]);
        var runtimeState = new PanelRuntimeState();
        runtimeState.SetReelPositionIfChanged("reel-1", 31);
        using var surface = SKSurface.Create(new SKImageInfo(64, 64));

        renderer.Render(
            surface.Canvas,
            [
                new PanelElementModel
                {
                    Kind = PanelElementKind.Reel,
                    IsVisible = true,
                    ObjectId = "reel-1",
                    Name = "Reel",
                    Width = 24,
                    Height = 24,
                    Stops = 16,
                    AssetPath = "Assets/does-not-exist.png"
                }
            ],
            runtimeState,
            PanelViewportTransform.Identity);
    }

    [Fact]
    public void Render_WithReelRendererAndAvailableAsset_UsesAssetStripPath()
    {
        var renderer = new Panel2DRenderer([new ReelElementRenderer()]);
        var runtimeState = new PanelRuntimeState();
        runtimeState.SetReelPositionIfChanged("reel-1", 17);
        using var surface = SKSurface.Create(new SKImageInfo(64, 64));

        var projectDirectory = Path.Combine(Path.GetTempPath(), $"oasis-reel-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(projectDirectory, "Assets"));
        var assetPath = Path.Combine(projectDirectory, "Assets", "reel-strip.png");
        using (var imageSurface = SKSurface.Create(new SKImageInfo(16, 16)))
        {
            imageSurface.Canvas.Clear(SKColors.Gold);
            using var image = imageSurface.Snapshot();
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            using var stream = File.OpenWrite(assetPath);
            data.SaveTo(stream);
        }

        var previousProjectDirectory = PanelElementFactory.ProjectDirectoryPath;
        try
        {
            PanelElementFactory.ProjectDirectoryPath = projectDirectory;
            renderer.Render(
                surface.Canvas,
                [
                    new PanelElementModel
                    {
                        Kind = PanelElementKind.Reel,
                        IsVisible = true,
                        ObjectId = "reel-1",
                        Name = "Reel",
                        Width = 24,
                        Height = 24,
                        Stops = 16,
                        AssetPath = "Assets/reel-strip.png"
                    }
                ],
                runtimeState,
                PanelViewportTransform.Identity);
        }
        finally
        {
            PanelElementFactory.ProjectDirectoryPath = previousProjectDirectory;
            Directory.Delete(projectDirectory, recursive: true);
        }
    }


    [Fact]
    public void Render_WithBackgroundRendererAndAvailableAsset_UsesBackgroundImage()
    {
        var renderer = new Panel2DRenderer([new BackgroundElementRenderer()]);
        using var surface = SKSurface.Create(new SKImageInfo(64, 64));

        var projectDirectory = Path.Combine(Path.GetTempPath(), $"oasis-background-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(projectDirectory, "Assets"));
        var assetPath = Path.Combine(projectDirectory, "Assets", "background.png");
        using (var imageSurface = SKSurface.Create(new SKImageInfo(16, 16)))
        {
            imageSurface.Canvas.Clear(SKColors.CornflowerBlue);
            using var image = imageSurface.Snapshot();
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            using var stream = File.OpenWrite(assetPath);
            data.SaveTo(stream);
        }

        var previousProjectDirectory = PanelElementFactory.ProjectDirectoryPath;
        try
        {
            PanelElementFactory.ProjectDirectoryPath = projectDirectory;
            renderer.Render(
                surface.Canvas,
                [
                    new PanelElementModel
                    {
                        Kind = PanelElementKind.Background,
                        IsVisible = true,
                        ObjectId = "background-1",
                        Name = "Background",
                        Width = 64,
                        Height = 64,
                        AssetPath = "Assets/background.png"
                    }
                ],
                new PanelRuntimeState(),
                PanelViewportTransform.Identity);
        }
        finally
        {
            PanelElementFactory.ProjectDirectoryPath = previousProjectDirectory;
            Directory.Delete(projectDirectory, recursive: true);
        }
    }

    [Fact]
    public void Render_WithLampRendererAndAvailableAsset_UsesLampImage()
    {
        var renderer = new Panel2DRenderer([new LampElementRenderer()]);
        var runtimeState = new PanelRuntimeState();
        runtimeState.SetLampIntensity("lamp-image-1", 1d);
        using var surface = SKSurface.Create(new SKImageInfo(64, 64));

        var projectDirectory = Path.Combine(Path.GetTempPath(), $"oasis-lamp-image-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(projectDirectory, "Assets"));
        var assetPath = Path.Combine(projectDirectory, "Assets", "lamp.png");
        using (var imageSurface = SKSurface.Create(new SKImageInfo(16, 16)))
        {
            imageSurface.Canvas.Clear(SKColors.OrangeRed);
            using var image = imageSurface.Snapshot();
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            using var stream = File.OpenWrite(assetPath);
            data.SaveTo(stream);
        }

        var previousProjectDirectory = PanelElementFactory.ProjectDirectoryPath;
        try
        {
            PanelElementFactory.ProjectDirectoryPath = projectDirectory;
            renderer.Render(
                surface.Canvas,
                [
                    new PanelElementModel
                    {
                        Kind = PanelElementKind.Lamp,
                        IsVisible = true,
                        ObjectId = "lamp-image-1",
                        Name = "Lamp",
                        Width = 24,
                        Height = 24,
                        AssetPath = "Assets/lamp.png",
                        OnColorHex = "#FF0000",
                        OffColorHex = "#220000"
                    }
                ],
                runtimeState,
                PanelViewportTransform.Identity);
        }
        finally
        {
            PanelElementFactory.ProjectDirectoryPath = previousProjectDirectory;
            Directory.Delete(projectDirectory, recursive: true);
        }
    }

    [Theory]
    [InlineData(0, 12, 0d)]
    [InlineData(96, 12, 0d)]
    [InlineData(-1, 12, 95d / 96d)]
    [InlineData(24, 12, 0.25d)]
    public void ReelOffset_ComputesExpectedWrappedOffset(int position, int stops, double expected)
    {
        var actual = ReelElementRenderer.ComputeWrappedOffset(position, stops);

        Assert.Equal(expected, actual, 4);
    }

    [Theory]
    [InlineData(null, 24f)]
    [InlineData(1d, 24f)]
    [InlineData(0.5d, 48f)]
    [InlineData(0d, 2400f)]
    public void ReelBandHeight_RespectsVisibleScale(double? visibleScale, float expectedHeight)
    {
        var actual = ReelElementRenderer.ResolveBandHeight(24f, visibleScale);

        Assert.Equal(expectedHeight, actual, 3);
    }

    [Theory]
    [InlineData(24, 12, 48f, 12d)]
    [InlineData(-1, 12, 48f, 47.5d)]
    [InlineData(96, 12, 48f, 0d)]
    public void ReelBandOffset_ComputesExpectedValue(int position, int stops, float bandHeight, double expected)
    {
        var actual = ReelElementRenderer.ComputeBandOffset(position, stops, bandHeight);

        Assert.Equal(expected, actual, 3);
    }


    [Fact]
    public void Render_WithSevenSegmentRenderer_DoesNotThrow()
    {
        var renderer = new Panel2DRenderer([new SevenSegmentElementRenderer()]);
        var runtimeState = new PanelRuntimeState();
        runtimeState.SetSegmentCellMasksIfChanged("seg-1", [0x3F]);
        runtimeState.SetSegmentCellBrightnessIfChanged("seg-1", [0.8d]);
        using var surface = SKSurface.Create(new SKImageInfo(64, 64));

        renderer.Render(
            surface.Canvas,
            [
                new PanelElementModel
                {
                    Kind = PanelElementKind.SevenSegment,
                    IsVisible = true,
                    ObjectId = "seg-1",
                    Name = "Seven",
                    Width = 24,
                    Height = 36,
                    OnColorHex = "#FF4444",
                    OffColorHex = "#220000"
                }
            ],
            runtimeState,
            PanelViewportTransform.Identity);
    }


    [Fact]
    public void Render_WithAlphaRenderer_DoesNotThrow()
    {
        var renderer = new Panel2DRenderer([new AlphaElementRenderer()]);
        var runtimeState = new PanelRuntimeState();
        runtimeState.SetSegmentCellMasksIfChanged("alpha-1", [0x3FFF, 0x0001, 0x00FF]);
        runtimeState.SetSegmentCellBrightnessIfChanged("alpha-1", [1d, 0.5d, 0.75d]);
        using var surface = SKSurface.Create(new SKImageInfo(320, 64));

        renderer.Render(
            surface.Canvas,
            [
                new PanelElementModel
                {
                    Kind = PanelElementKind.Alpha,
                    IsVisible = true,
                    ObjectId = "alpha-1",
                    Name = "Alpha",
                    Width = 240,
                    Height = 32,
                    OnColorHex = "#FF4444",
                    OffColorHex = "#220000"
                }
            ],
            runtimeState,
            PanelViewportTransform.Identity);
    }

    private sealed class FakeRenderer(PanelElementKind kind) : IPanelElementRenderer
    {
        public PanelElementKind Kind { get; } = kind;

        public List<PanelElementModel> Rendered { get; } = [];

        public void Render(in PanelElementRenderContext context, PanelElementModel element)
        {
            Rendered.Add(element);
        }
    }
}
