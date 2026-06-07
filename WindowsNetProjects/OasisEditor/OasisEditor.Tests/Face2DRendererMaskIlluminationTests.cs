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

    private static FaceDocumentModel CreateDocument()
    {
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
            Elements =
            [
                new FaceLampWindowElement
                {
                    ObjectId = "lamp-window-17",
                    Name = "Lamp 17",
                    X = 9,
                    Y = 9,
                    Width = 2,
                    Height = 2,
                    IsVisible = true,
                    LinkedMachineObjectReference = MachineObjectReference.Lamp(17)
                }
            ]
        };
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
