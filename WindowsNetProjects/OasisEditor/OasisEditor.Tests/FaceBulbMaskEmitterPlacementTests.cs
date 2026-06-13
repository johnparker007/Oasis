using SkiaSharp;
using Xunit;

namespace OasisEditor.Tests;

public sealed class FaceBulbMaskEmitterPlacementTests
{
    [Fact]
    public void Analyze_UsesAlphaWeightedLuminanceCentroid()
    {
        using var bitmap = new SKBitmap(10, 10, SKColorType.Rgba8888, SKAlphaType.Premul);
        bitmap.Erase(SKColors.Transparent);
        bitmap.SetPixel(7, 3, new SKColor(255, 255, 255, 255));

        var result = FaceBulbMaskCentroidAnalyzer.Analyze(bitmap);

        Assert.NotNull(result);
        Assert.Equal(0.75d, result!.NormalizedX, 2);
        Assert.Equal(0.35d, result.NormalizedY, 2);
        Assert.True(result.NormalizedRadius > 0d);
    }

    [Fact]
    public void AutoAuthor_MapsBlendMaskCentroidIntoLampWindowCoordinates()
    {
        using var temp = new TemporaryDirectory();
        Directory.CreateDirectory(Path.Combine(temp.Path, "lamps"));
        var maskPath = Path.Combine(temp.Path, "lamps", "mask.png");
        WriteMask(maskPath, 10, 10, (7, 3));

        var document = CreateFaceLamp(sourceBlend: true, bulbMaskAssetPath: "lamps/mask.png");
        var result = new FaceTrayAutoAuthoringService().AutoAuthor(document, temp.Path);

        var emitter = Assert.Single(result.Emitters);
        Assert.Equal("MfmeBulbMaskCentroid", emitter.EmitterPlacementSource);
        Assert.Equal(175d, emitter.CenterX, 1);
        Assert.Equal(70d, emitter.CenterY, 1);
        Assert.NotNull(emitter.Radius);
    }

    [Fact]
    public void AutoAuthor_FallsBackWhenBlendIsFalse()
    {
        var document = CreateFaceLamp(sourceBlend: false, bulbMaskAssetPath: "lamps/mask.png");
        var result = new FaceTrayAutoAuthoringService().AutoAuthor(document, projectDirectory: null);

        var emitter = Assert.Single(result.Emitters);
        Assert.Equal("ComponentCentreFallback", emitter.EmitterPlacementSource);
        Assert.Equal(150d, emitter.CenterX, 1);
        Assert.Equal(100d, emitter.CenterY, 1);
        Assert.Empty(emitter.Diagnostics);
    }

    [Fact]
    public void AutoAuthor_AddsDiagnosticsWhenBlendMaskMissing()
    {
        var document = CreateFaceLamp(sourceBlend: true, bulbMaskAssetPath: "lamps/missing.png");
        var result = new FaceTrayAutoAuthoringService().AutoAuthor(document, projectDirectory: "/missing-project");

        var emitter = Assert.Single(result.Emitters);
        Assert.Equal("ComponentCentreFallback", emitter.EmitterPlacementSource);
        Assert.Contains("bulb-mask-file-missing", emitter.Diagnostics);
        Assert.Contains("centroid-fallback-used", emitter.Diagnostics);
    }

    private static FaceDocumentModel CreateFaceLamp(bool sourceBlend, string? bulbMaskAssetPath)
    {
        return new FaceDocumentModel
        {
            Elements =
            [
                new FaceLampWindowElement
                {
                    ObjectId = "lamp-window-1",
                    Name = "Lamp 1",
                    X = 100,
                    Y = 50,
                    Width = 100,
                    Height = 100,
                    IsVisible = true,
                    LinkedPanel2DElementId = "panel-lamp-1",
                    LinkedMachineObjectReference = MachineObjectReference.Lamp(1),
                    SourceBlend = sourceBlend,
                    BulbMaskAssetPath = bulbMaskAssetPath
                }
            ]
        };
    }

    private static void WriteMask(string path, int width, int height, (int X, int Y) brightPixel)
    {
        using var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        bitmap.Erase(SKColors.Transparent);
        bitmap.SetPixel(brightPixel.X, brightPixel.Y, new SKColor(255, 255, 255, 255));
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.OpenWrite(path);
        data.SaveTo(stream);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"oasis-mask-test-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
