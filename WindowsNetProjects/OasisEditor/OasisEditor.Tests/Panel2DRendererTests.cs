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


    [Fact]
    public void Render_WithLampRendererGraphicLampOff_DoesNotDrawBlackFallback()
    {
        var renderer = new Panel2DRenderer([new LampElementRenderer()]);
        var runtimeState = new PanelRuntimeState();
        runtimeState.SetLampIntensity("lamp-image-off-1", 0d);
        using var surface = SKSurface.Create(new SKImageInfo(64, 64));

        var projectDirectory = Path.Combine(Path.GetTempPath(), $"oasis-lamp-image-off-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(projectDirectory, "Assets"));
        var assetPath = Path.Combine(projectDirectory, "Assets", "lamp-off.png");
        using (var imageSurface = SKSurface.Create(new SKImageInfo(16, 16)))
        {
            imageSurface.Canvas.Clear(SKColors.LawnGreen);
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
                        ObjectId = "lamp-image-off-1",
                        Name = "Lamp Off",
                        X = 8,
                        Y = 8,
                        Width = 24,
                        Height = 24,
                        AssetPath = "Assets/lamp-off.png",
                        OnColorHex = "#FF0000",
                        OffColorHex = "#220000"
                    }
                ],
                runtimeState,
                PanelViewportTransform.Identity);

            using var snapshot = surface.Snapshot();
            using var bitmap = SKBitmap.FromImage(snapshot);
            var centerPixel = bitmap.GetPixel(20, 20);
            Assert.Equal(0, centerPixel.Alpha);
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
    [InlineData(24.5d, 12, 24.5d / 96d)]
    public void ReelOffset_ComputesExpectedWrappedOffset(double position, int stops, double expected)
    {
        var actual = ReelElementRenderer.ComputeWrappedOffset(position, stops);

        Assert.Equal(expected, actual, 4);
    }


    [Fact]
    public void ReelPreviewPosition_UsesBandOffsetBeforeRuntimeStateExists()
    {
        var runtimeState = new PanelRuntimeState();
        var element = new PanelElementModel
        {
            ObjectId = "reel-1",
            Kind = PanelElementKind.Reel,
            Stops = 12,
            BandOffset = 0.25d
        };

        var actual = ReelElementRenderer.ResolvePreviewReelPosition(runtimeState, element, 12);

        Assert.Equal(24d, actual);
    }

    [Fact]
    public void ReelPreviewPosition_PrefersRuntimeStateAfterEmulationUpdatesReel()
    {
        var runtimeState = new PanelRuntimeState();
        runtimeState.SetReelPositionIfChanged("reel-1", 12d);
        var element = new PanelElementModel
        {
            ObjectId = "reel-1",
            Kind = PanelElementKind.Reel,
            Stops = 12,
            BandOffset = 0.25d
        };

        var actual = ReelElementRenderer.ResolvePreviewReelPosition(runtimeState, element, 12);

        Assert.Equal(12d, actual);
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
    [InlineData(24.5d, 12, 48f, 12.25d)]
    public void ReelBandOffset_ComputesExpectedValue(double position, int stops, float bandHeight, double expected)
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

    [Fact]
    public void ImageElementRenderer_DrawsImageAsset()
    {
        var projectDirectory = CreateProjectWithImage("test-image.png", SKColors.LimeGreen);
        var previousProjectDirectory = PanelElementFactory.ProjectDirectoryPath;
        try
        {
            PanelElementFactory.ProjectDirectoryPath = projectDirectory;
            using var surface = SKSurface.Create(new SKImageInfo(32, 32, SKColorType.Bgra8888, SKAlphaType.Premul));
            surface.Canvas.Clear(SKColors.Black);

            new ImageElementRenderer().Render(
                new PanelElementRenderContext(surface.Canvas, new PanelRuntimeState(), PanelViewportTransform.Identity),
                new PanelElementModel
                {
                    Kind = PanelElementKind.Image,
                    IsVisible = true,
                    ObjectId = "image-1",
                    Name = "Image",
                    X = 8,
                    Y = 8,
                    Width = 16,
                    Height = 16,
                    AssetPath = "Assets/test-image.png"
                });

            var pixel = ReadPixel(surface, 16, 16);
            Assert.Equal(SKColors.LimeGreen.Red, pixel.Red);
            Assert.Equal(SKColors.LimeGreen.Green, pixel.Green);
            Assert.Equal(SKColors.LimeGreen.Blue, pixel.Blue);
        }
        finally
        {
            PanelElementFactory.ProjectDirectoryPath = previousProjectDirectory;
            Directory.Delete(projectDirectory, recursive: true);
        }
    }

    [Fact]
    public void ImageElementRenderer_PreservesAlpha()
    {
        var projectDirectory = CreateProjectWithImage("alpha-image.png", new SKColor(255, 0, 0, 128));
        var previousProjectDirectory = PanelElementFactory.ProjectDirectoryPath;
        try
        {
            PanelElementFactory.ProjectDirectoryPath = projectDirectory;
            using var surface = SKSurface.Create(new SKImageInfo(16, 16, SKColorType.Bgra8888, SKAlphaType.Premul));
            surface.Canvas.Clear(SKColors.White);

            new ImageElementRenderer().Render(
                new PanelElementRenderContext(surface.Canvas, new PanelRuntimeState(), PanelViewportTransform.Identity),
                new PanelElementModel
                {
                    Kind = PanelElementKind.Image,
                    IsVisible = true,
                    ObjectId = "image-alpha-1",
                    Name = "Image",
                    Width = 16,
                    Height = 16,
                    AssetPath = "Assets/alpha-image.png"
                });

            var pixel = ReadPixel(surface, 8, 8);
            Assert.Equal(255, pixel.Alpha);
            Assert.True(pixel.Red > 240, $"Expected red channel to stay high after alpha blend, got {pixel.Red}.");
            Assert.InRange(pixel.Green, 120, 140);
            Assert.InRange(pixel.Blue, 120, 140);
        }
        finally
        {
            PanelElementFactory.ProjectDirectoryPath = previousProjectDirectory;
            Directory.Delete(projectDirectory, recursive: true);
        }
    }

    [Fact]
    public void ImageElementRenderer_MissingAsset_DoesNotThrow()
    {
        using var surface = SKSurface.Create(new SKImageInfo(16, 16, SKColorType.Bgra8888, SKAlphaType.Premul));
        surface.Canvas.Clear(SKColors.MidnightBlue);

        var exception = Record.Exception(() => new ImageElementRenderer().Render(
            new PanelElementRenderContext(surface.Canvas, new PanelRuntimeState(), PanelViewportTransform.Identity),
            new PanelElementModel
            {
                Kind = PanelElementKind.Image,
                IsVisible = true,
                ObjectId = "missing-image-1",
                Name = "Image",
                Width = 16,
                Height = 16,
                AssetPath = "Assets/missing.png"
            }));

        Assert.Null(exception);
        Assert.Equal(SKColors.MidnightBlue, ReadPixel(surface, 8, 8));
    }

    [Fact]
    public void Panel2DRenderer_RendersImageKind()
    {
        var projectDirectory = CreateProjectWithImage("panel-image.png", SKColors.HotPink);
        var previousProjectDirectory = PanelElementFactory.ProjectDirectoryPath;
        try
        {
            PanelElementFactory.ProjectDirectoryPath = projectDirectory;
            var renderer = new Panel2DRenderer([new ImageElementRenderer()]);
            using var surface = SKSurface.Create(new SKImageInfo(24, 24, SKColorType.Bgra8888, SKAlphaType.Premul));
            surface.Canvas.Clear(SKColors.Black);

            renderer.Render(
                surface.Canvas,
                [
                    new PanelElementModel
                    {
                        Kind = PanelElementKind.Image,
                        IsVisible = true,
                        ObjectId = "panel-image-1",
                        Name = "Image",
                        X = 4,
                        Y = 4,
                        Width = 16,
                        Height = 16,
                        AssetPath = "Assets/panel-image.png"
                    }
                ],
                new PanelRuntimeState(),
                PanelViewportTransform.Identity);

            Assert.Equal(SKColors.HotPink, ReadPixel(surface, 12, 12));
        }
        finally
        {
            PanelElementFactory.ProjectDirectoryPath = previousProjectDirectory;
            Directory.Delete(projectDirectory, recursive: true);
        }
    }

    [Fact]
    public void Panel2DRendererFactory_DefaultRenderersContainOneImageRenderer()
    {
        var imageRenderers = Panel2DRendererFactory.CreateDefaultRenderers()
            .Where(renderer => renderer.Kind == PanelElementKind.Image)
            .ToArray();

        var renderer = Assert.Single(imageRenderers);
        Assert.IsType<ImageElementRenderer>(renderer);
    }

    private static string CreateProjectWithImage(string fileName, SKColor color)
    {
        var projectDirectory = Path.Combine(Path.GetTempPath(), $"oasis-image-renderer-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(projectDirectory, "Assets"));
        var assetPath = Path.Combine(projectDirectory, "Assets", fileName);
        using var bitmap = new SKBitmap(4, 4, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        bitmap.Erase(color);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.OpenWrite(assetPath);
        data.SaveTo(stream);
        return projectDirectory;
    }

    private static SKColor ReadPixel(SKSurface surface, int x, int y)
    {
        using var snapshot = surface.Snapshot();
        using var bitmap = SKBitmap.FromImage(snapshot);
        return bitmap.GetPixel(x, y);
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
