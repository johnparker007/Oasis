using OasisEditor;
using OasisEditor.Rendering;
using SkiaSharp;
using Xunit;

namespace OasisEditor.Avalonia.Tests;

[CollectionDefinition("Specialist renderers", DisableParallelization = true)]
public sealed class SpecialistRendererCollection { }

[Collection("Specialist renderers")]
public sealed class SpecialistRendererPixelTests
{
    [Fact]
    public void PanelRendererUsesActualLampIntensityAndAuthoredColors()
    {
        var element = new PanelElementModel
        {
            ObjectId = "lamp-object", Name = "Lamp", Kind = PanelElementKind.Lamp,
            X = 2, Y = 2, Width = 20, Height = 20, OnColorHex = "#FF0000", OffColorHex = "#000000", IsVisible = true
        };
        var renderer = Panel2DRendererFactory.Create("PixelTest");
        using var surface = SKSurface.Create(new SKImageInfo(32, 32, SKColorType.Bgra8888, SKAlphaType.Premul));
        Assert.NotNull(surface);
        surface.Canvas.Clear(SKColors.Transparent);

        renderer.Render(surface.Canvas, [element], new MachineRuntimeState(), PanelViewportTransform.Identity);
        using var offFrame = surface.Snapshot();
        Assert.Equal(SKColors.Black, Pixel(offFrame, 12, 12));

        var runtime = new MachineRuntimeState();
        runtime.SetLampIntensity("lamp-object", 1);
        surface.Canvas.Clear(SKColors.Transparent);
        renderer.Render(surface.Canvas, [element], runtime, PanelViewportTransform.Identity);
        using var onFrame = surface.Snapshot();
        Assert.Equal(SKColors.Red, Pixel(onFrame, 12, 12));
    }

    [Fact]
    public void FaceRendererDrawsAuthoredArtworkPixelsWithoutFallbackShapes()
    {
        var folder = Path.Combine(Path.GetTempPath(), "OasisRendererPixels-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var assetRoot = Path.Combine(folder, "Assets");
            Directory.CreateDirectory(assetRoot);
            using (var bitmap = new SKBitmap(new SKImageInfo(4, 4, SKColorType.Bgra8888, SKAlphaType.Premul)))
            {
                for (var y = 0; y < bitmap.Height; y++)
                    for (var x = 0; x < bitmap.Width; x++) bitmap.SetPixel(x, y, SKColors.Blue);
                using var encoded = bitmap.Encode(SKEncodedImageFormat.Png, 100);
                File.WriteAllBytes(Path.Combine(assetRoot, "art.png"), encoded.ToArray());
            }
            ProjectAssetPathResolver.ProjectDirectoryPath = folder;
            var face = new FaceDocumentModel
            {
                Elements = [new FaceArtworkElement { ObjectId = "art", Name = "Artwork", X = 2, Y = 2, Width = 20, Height = 20, AssetPath = "Assets/art.png", IsVisible = true }]
            };
            using var surface = SKSurface.Create(new SKImageInfo(32, 32, SKColorType.Bgra8888, SKAlphaType.Premul));
            Assert.NotNull(surface);
            surface.Canvas.Clear(SKColors.Transparent);
            new Face2DRenderer().Render(surface.Canvas, face, new MachineRuntimeState(), PanelViewportTransform.Identity);
            using var frame = surface.Snapshot();
            Assert.Equal(SKColors.Blue, Pixel(frame, 12, 12));
        }
        finally
        {
            ProjectAssetPathResolver.ProjectDirectoryPath = null;
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void PanelRendererProjectsReelStripAtLiveOutputOffset()
    {
        var folder = Path.Combine(Path.GetTempPath(), "OasisReelPixels-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(folder, "Assets"));
        try
        {
            using (var strip = new SKBitmap(new SKImageInfo(4, 16, SKColorType.Bgra8888, SKAlphaType.Premul)))
            {
                for (var y = 0; y < strip.Height; y++)
                    for (var x = 0; x < strip.Width; x++) strip.SetPixel(x, y, y < 8 ? SKColors.Red : SKColors.Blue);
                using var encoded = strip.Encode(SKEncodedImageFormat.Png, 100);
                File.WriteAllBytes(Path.Combine(folder, "Assets", "reel.png"), encoded.ToArray());
            }
            ProjectAssetPathResolver.ProjectDirectoryPath = folder;
            var element = new PanelElementModel
            {
                ObjectId = "reel-object", Name = "Reel", Kind = PanelElementKind.Reel,
                X = 0, Y = 0, Width = 8, Height = 8, AssetPath = "Assets/reel.png", Stops = 2, VisibleScale = 1, IsVisible = true
            };
            var renderer = Panel2DRendererFactory.Create("ReelPixelTest");
            using var surface = SKSurface.Create(new SKImageInfo(8, 8, SKColorType.Bgra8888, SKAlphaType.Premul));
            Assert.NotNull(surface);
            renderer.Render(surface.Canvas, [element], new MachineRuntimeState(), PanelViewportTransform.Identity);
            using var first = surface.Snapshot();
            Assert.Equal(SKColors.Red, Pixel(first, 4, 1));

            var output = new MachineRuntimeState(); output.SetReelPositionIfChanged("reel-object", 48);
            surface.Canvas.Clear(SKColors.Transparent);
            renderer.Render(surface.Canvas, [element], output, PanelViewportTransform.Identity);
            using var second = surface.Snapshot();
            Assert.Equal(SKColors.Blue, Pixel(second, 4, 1));
        }
        finally
        {
            ProjectAssetPathResolver.ProjectDirectoryPath = null;
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void HeadlessViewportCaptureContainsRenderedAuthoredImagePixels()
    {
        HeadlessTestHost.Run(() =>
        {
        var folder = Path.Combine(Path.GetTempPath(), "OasisViewportPixels-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var sourcePath = Path.Combine(folder, "source.png");
            using (var source = new SKBitmap(new SKImageInfo(8, 8, SKColorType.Bgra8888, SKAlphaType.Premul)))
            {
                for (var y = 0; y < source.Height; y++) for (var x = 0; x < source.Width; x++) source.SetPixel(x, y, SKColors.Blue);
                using var encoded = source.Encode(SKEncodedImageFormat.Png, 100);
                File.WriteAllBytes(sourcePath, encoded.ToArray());
            }
            var project = Path.Combine(folder, "Viewport.oasisproj");
            File.WriteAllText(project, """{"name":"Viewport test","version":9,"layout":{"assets":"Assets","generated":"Generated"}}""");
            var session = new AuthoringSession(folder);
            session.OpenProject(project); session.CreatePanel("Panel"); session.ImportImage(sourcePath, "Blue artwork");
            using var capture = new WorkspaceCapture(session);
            var png = capture.Capture(1200, 800, "Dark");
            using var frame = SKBitmap.Decode(png);
            Assert.NotNull(frame);
            var bluePixels = 0;
            for (var y = 0; y < frame.Height; y++)
                for (var x = 0; x < frame.Width; x++)
                {
                    var color = frame.GetPixel(x, y);
                    if (color.Blue > 180 && color.Red < 80 && color.Green < 80 && color.Alpha > 240) bluePixels++;
                }
            Assert.True(bluePixels > 100, $"Expected the imported blue artwork in the headless screenshot, found {bluePixels} matching pixels.");

            var renderedAssetPath = session.ResolveAsset(Assert.Single(session.Elements).AssetPath!);
            using (var replacement = new SKBitmap(new SKImageInfo(8, 8, SKColorType.Bgra8888, SKAlphaType.Premul)))
            {
                replacement.Erase(SKColors.Red);
                using var encoded = replacement.Encode(SKEncodedImageFormat.Png, 100);
                File.WriteAllBytes(renderedAssetPath, encoded.ToArray());
            }
            File.SetLastWriteTimeUtc(renderedAssetPath, DateTime.UtcNow.AddSeconds(5));
            using var refreshedFrame = SKBitmap.Decode(capture.Capture(1200, 800, "Dark"));
            Assert.NotNull(refreshedFrame);
            var redPixels = 0;
            for (var y = 0; y < refreshedFrame.Height; y++)
                for (var x = 0; x < refreshedFrame.Width; x++)
                {
                    var color = refreshedFrame.GetPixel(x, y);
                    if (color.Red > 180 && color.Green < 80 && color.Blue < 80 && color.Alpha > 240) redPixels++;
                }
            Assert.True(redPixels > 100, $"Expected a regenerated red image to invalidate the viewport cache, found {redPixels} matching pixels.");
        }
        finally { Directory.Delete(folder, recursive: true); }
        });
    }

    private static SKColor Pixel(SKImage image, int x, int y)
    {
        using var bitmap = SKBitmap.FromImage(image);
        return bitmap.GetPixel(x, y);
    }
}
