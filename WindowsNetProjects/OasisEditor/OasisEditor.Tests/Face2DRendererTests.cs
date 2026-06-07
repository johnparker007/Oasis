using OasisEditor.Rendering;
using SkiaSharp;
using Xunit;

namespace OasisEditor.Tests;

public sealed class Face2DRendererTests : IDisposable
{
    private readonly string _tempDirectory;

    public Face2DRendererTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"OasisFaceRendererTests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void Render_MasksFaceLampIlluminationWithFaceMaskLayer()
    {
        var maskPath = Path.Combine(_tempDirectory, "mask.png");
        SaveMask(maskPath, width: 10, height: 10, isOpen: x => x < 5);
        var renderer = new Face2DRenderer(FaceRuntimeStateResolver.Instance, assetPath => assetPath);
        var runtimeState = new MachineRuntimeState();
        runtimeState.SetLampIntensity(MachineObjectReference.Lamp(7), 1d);
        var lamp = new FaceLampWindowElement
        {
            ObjectId = "face-lamp-7",
            Name = "Lamp 7",
            X = 0,
            Y = 0,
            Width = 10,
            Height = 10,
            LinkedMachineObjectReference = MachineObjectReference.Lamp(7)
        };
        var maskLayer = new FaceMaskLayerModel
        {
            AssetPath = maskPath,
            Width = 10,
            Height = 10
        };
        using var surface = SKSurface.Create(new SKImageInfo(10, 10));
        surface.Canvas.Clear(SKColors.Black);

        renderer.Render(
            surface.Canvas,
            new FaceDocumentModel
            {
                MaskLayer = maskLayer,
                Elements = [lamp]
            },
            runtimeState,
            PanelViewportTransform.Identity);
        using var image = surface.Snapshot();
        using var bitmap = SKBitmap.FromImage(image);

        var openPixel = bitmap.GetPixel(2, 5);
        var closedPixel = bitmap.GetPixel(7, 5);
        Assert.True(openPixel.Red > closedPixel.Red + 100);
        Assert.True(openPixel.Green > closedPixel.Green + 100);
    }

    private static void SaveMask(string path, int width, int height, Func<int, bool> isOpen)
    {
        using var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Opaque);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var value = isOpen(x) ? (byte)255 : (byte)0;
                bitmap.SetPixel(x, y, new SKColor(value, value, value, 255));
            }
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.Open(path, FileMode.Create, FileAccess.Write, FileShare.None);
        data.SaveTo(stream);
    }
}
