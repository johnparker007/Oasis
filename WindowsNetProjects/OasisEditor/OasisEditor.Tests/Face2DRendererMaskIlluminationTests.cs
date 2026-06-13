using OasisEditor.Rendering;
using SkiaSharp;
using Xunit;

namespace OasisEditor.Tests;

public sealed class Face2DRendererMaskIlluminationTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly string _maskPath;

    public Face2DRendererMaskIlluminationTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"OasisFaceRendererMaskTests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);
        _maskPath = Path.Combine(_testDirectory, "mask.png");
    }

    [Fact]
    public void Render_LampOff_DrawsNoLampRectangleOverArtwork()
    {
        WriteMask(_maskPath, 20, 20, [(10, 10), (14, 10), (18, 10)]);
        var renderer = new Face2DRenderer(FaceRuntimeStateResolver.Instance, _ => _maskPath);
        var runtimeState = new MachineRuntimeState();
        var document = CreateDocument();

        using var surface = SKSurface.Create(new SKImageInfo(20, 20, SKColorType.Bgra8888, SKAlphaType.Premul));
        surface.Canvas.Clear(SKColors.White);

        renderer.Render(surface.Canvas, document, runtimeState, new PanelViewportTransform());

        using var snapshot = surface.Snapshot();
        using var bitmap = SKBitmap.FromImage(snapshot);
        Assert.Equal(SKColors.White, bitmap.GetPixel(10, 10));
        Assert.Equal(SKColors.White, bitmap.GetPixel(14, 10));
        Assert.Equal(SKColors.White, bitmap.GetPixel(18, 10));
    }

    [Fact]
    public void Render_TexturePreviewSuccess_DoesNotAlsoDrawFallbackLampIllumination()
    {
        WriteMask(_maskPath, 20, 20, [(10, 10), (14, 10), (18, 10)]);
        using var textureBitmap = new SKBitmap(20, 20, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        textureBitmap.Erase(SKColors.Black);
        var renderer = new Face2DRenderer(
            FaceRuntimeStateResolver.Instance,
            ResolveTestAssetPath,
            new StubTexturePreviewRenderer(FaceTexturePreviewRenderResult.FromCachedBitmap(textureBitmap)));
        var runtimeState = new MachineRuntimeState();
        runtimeState.SetLampIntensityIfChanged(MachineObjectReference.Lamp(17), 1d);
        var document = CreateDocument();

        using var surface = SKSurface.Create(new SKImageInfo(20, 20, SKColorType.Bgra8888, SKAlphaType.Premul));
        surface.Canvas.Clear(SKColors.White);

        renderer.Render(surface.Canvas, document, runtimeState, new PanelViewportTransform());

        using var snapshot = surface.Snapshot();
        using var bitmap = SKBitmap.FromImage(snapshot);
        Assert.Equal(SKColors.Black, bitmap.GetPixel(10, 10));
        Assert.Equal(SKColors.Black, bitmap.GetPixel(14, 10));
    }

    [Fact]
    public void Render_FallbackLampIllumination_KeepsLitRedArtworkRecognisablyRed()
    {
        var artworkPath = Path.Combine(_testDirectory, "red-artwork.png");
        WriteSolidPng(artworkPath, 20, 20, new SKColor(120, 0, 0));
        WriteMask(_maskPath, 20, 20, [(10, 10), (11, 10), (12, 10), (13, 10), (14, 10)]);
        var renderer = new Face2DRenderer(FaceRuntimeStateResolver.Instance, ResolveTestAssetPath);
        var runtimeState = new MachineRuntimeState();
        runtimeState.SetLampIntensityIfChanged(MachineObjectReference.Lamp(17), 1d);
        var document = CreateDocument(artworkPath: "red-artwork.png");

        using var surface = SKSurface.Create(new SKImageInfo(20, 20, SKColorType.Bgra8888, SKAlphaType.Premul));
        surface.Canvas.Clear(SKColors.Black);

        renderer.Render(surface.Canvas, document, runtimeState, new PanelViewportTransform());

        using var snapshot = surface.Snapshot();
        using var bitmap = SKBitmap.FromImage(snapshot);
        var unlit = bitmap.GetPixel(0, 0);
        var lit = bitmap.GetPixel(10, 10);
        Assert.True(lit.Red > unlit.Red);
        Assert.True(lit.Red > lit.Green * 3);
        Assert.True(lit.Red > lit.Blue * 3);
    }

    [Fact]
    public void Render_FallbackLampIllumination_KeepsLitBlueArtworkRecognisablyBlue()
    {
        var artworkPath = Path.Combine(_testDirectory, "blue-artwork.png");
        WriteSolidPng(artworkPath, 20, 20, new SKColor(0, 0, 120));
        WriteMask(_maskPath, 20, 20, [(10, 10), (11, 10), (12, 10), (13, 10), (14, 10)]);
        var renderer = new Face2DRenderer(FaceRuntimeStateResolver.Instance, ResolveTestAssetPath);
        var runtimeState = new MachineRuntimeState();
        runtimeState.SetLampIntensityIfChanged(MachineObjectReference.Lamp(17), 1d);
        var document = CreateDocument(artworkPath: "blue-artwork.png");

        using var surface = SKSurface.Create(new SKImageInfo(20, 20, SKColorType.Bgra8888, SKAlphaType.Premul));
        surface.Canvas.Clear(SKColors.Black);

        renderer.Render(surface.Canvas, document, runtimeState, new PanelViewportTransform());

        using var snapshot = surface.Snapshot();
        using var bitmap = SKBitmap.FromImage(snapshot);
        var unlit = bitmap.GetPixel(0, 0);
        var lit = bitmap.GetPixel(10, 10);
        Assert.True(lit.Blue > unlit.Blue);
        Assert.True(lit.Blue > lit.Red * 3);
        Assert.True(lit.Blue > lit.Green * 3);
    }

    [Fact]
    public void Render_LampOn_UsesRadialMaskIlluminationInsteadOfRectangleShape()
    {
        WriteMask(_maskPath, 20, 20, [(10, 10), (14, 10), (18, 10)]);
        var renderer = new Face2DRenderer(FaceRuntimeStateResolver.Instance, _ => _maskPath);
        var runtimeState = new MachineRuntimeState();
        runtimeState.SetLampIntensityIfChanged(MachineObjectReference.Lamp(17), 1d);
        var document = CreateDocument();

        using var surface = SKSurface.Create(new SKImageInfo(20, 20, SKColorType.Bgra8888, SKAlphaType.Premul));
        surface.Canvas.Clear(SKColors.White);

        renderer.Render(surface.Canvas, document, runtimeState, new PanelViewportTransform());

        using var snapshot = surface.Snapshot();
        using var bitmap = SKBitmap.FromImage(snapshot);
        Assert.NotEqual(SKColors.White, bitmap.GetPixel(10, 10));
        Assert.NotEqual(SKColors.White, bitmap.GetPixel(14, 10));
        Assert.Equal(SKColors.White, bitmap.GetPixel(12, 10));
        Assert.Equal(SKColors.White, bitmap.GetPixel(18, 10));
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }

    private static FaceDocumentModel CreateDocument(string? artworkPath = null)
    {
        var elements = new List<FaceElementModel>();
        if (!string.IsNullOrWhiteSpace(artworkPath))
        {
            elements.Add(new FaceArtworkElement
            {
                ObjectId = "artwork",
                Name = "Artwork",
                AssetPath = artworkPath,
                X = 0,
                Y = 0,
                Width = 20,
                Height = 20,
                IsVisible = true
            });
        }

        elements.Add(new FaceLampWindowElement
        {
            ObjectId = "lamp-window-17",
            Name = "Lamp 17",
            X = 9,
            Y = 9,
            Width = 2,
            Height = 2,
            IsVisible = true,
            LinkedMachineObjectReference = MachineObjectReference.Lamp(17)
        });

        return new FaceDocumentModel
        {
            Title = "Mask Renderer Face",
            MaskLayer = new FaceMaskLayerModel
            {
                Id = "face-mask-layer",
                Name = "Face Mask",
                AssetPath = "mask.png",
                Width = 20,
                Height = 20,
                Contributions =
                [
                    new FaceMaskContributionModel
                    {
                        LinkedMachineObjectReference = MachineObjectReference.Lamp(17),
                        Bounds = new FaceSourceRegionModel
                        {
                            X = 10,
                            Y = 10,
                            Width = 5,
                            Height = 1
                        },
                        PixelCount = 2
                    }
                ]
            },
            Elements = elements
        };
    }

    private string? ResolveTestAssetPath(string? assetPath)
    {
        if (string.IsNullOrWhiteSpace(assetPath))
        {
            return null;
        }

        return Path.Combine(_testDirectory, assetPath);
    }

    private static void WriteSolidPng(string path, int width, int height, SKColor color)
    {
        using var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        bitmap.Erase(color);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.Create(path);
        data.SaveTo(stream);
    }

    private sealed class StubTexturePreviewRenderer : IFaceTexturePreviewRenderer
    {
        private readonly FaceTexturePreviewRenderResult _result;

        public StubTexturePreviewRenderer(FaceTexturePreviewRenderResult result)
        {
            _result = result;
        }

        public FaceTexturePreviewRenderResult Render(FaceDocumentModel faceDocument, MachineRuntimeState runtimeState)
        {
            return _result;
        }
    }

    private static void WriteMask(string path, int width, int height, IReadOnlyCollection<(int X, int Y)> opaquePixels)
    {
        using var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Opaque);
        bitmap.Erase(SKColors.Black);
        foreach (var (x, y) in opaquePixels)
        {
            bitmap.SetPixel(x, y, SKColors.White);
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.OpenWrite(path);
        data.SaveTo(stream);
    }
}
