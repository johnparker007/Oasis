using System.Windows;
using SkiaSharp;
using Xunit;

namespace OasisEditor.Tests;

public sealed class FaceMaskLayerExtractionTests : IDisposable
{
    private readonly string _projectDirectory;

    public FaceMaskLayerExtractionTests()
    {
        _projectDirectory = Path.Combine(Path.GetTempPath(), $"OasisFaceMaskTests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(_projectDirectory, "Assets"));
        Directory.CreateDirectory(Path.Combine(_projectDirectory, "Generated"));
    }

    [Fact]
    public void GenerateFromPanelRegion_CreatesSingleFaceSizedMaskLayerAsset()
    {
        WriteSolidPng(Path.Combine(_projectDirectory, "Assets", "background.png"), 4, 4, SKColors.Black);
        WriteLampPng(Path.Combine(_projectDirectory, "Assets", "lamp.png"));
        var panel = new Panel2DDocumentModel
        {
            Elements =
            [
                new PanelElementModel
                {
                    ObjectId = "background-1",
                    Kind = PanelElementKind.Background,
                    X = 0,
                    Y = 0,
                    Width = 4,
                    Height = 4,
                    AssetPath = "Assets/background.png"
                },
                new PanelElementModel
                {
                    ObjectId = "lamp-1",
                    Kind = PanelElementKind.Lamp,
                    X = 1,
                    Y = 1,
                    Width = 2,
                    Height = 2,
                    AssetPath = "Assets/lamp.png",
                    DisplayNumber = 7
                }
            ]
        };

        var result = new FaceGenerationService().GenerateFromPanelRegion(
            panel,
            FaceSourceRegionModel.FromRect(new Rect(0, 0, 4, 4)),
            "Mask Face",
            "panel-doc",
            [],
            _projectDirectory,
            Path.Combine(_projectDirectory, "Generated"));

        Assert.NotNull(result.Document.MaskLayer);
        var maskLayer = result.Document.MaskLayer!;
        Assert.Equal(4, maskLayer.Width);
        Assert.Equal(4, maskLayer.Height);
        Assert.Equal("panel-doc", maskLayer.SourcePanel2DDocumentId);
        Assert.Equal(FaceGenerationSettingsModel.DefaultMaskExtractionThreshold, maskLayer.ExtractionThreshold);
        Assert.StartsWith("Generated/Faces/", maskLayer.AssetPath);
        Assert.True(File.Exists(Path.Combine(_projectDirectory, maskLayer.AssetPath!.Replace('/', Path.DirectorySeparatorChar))));
        var contribution = Assert.Single(maskLayer.Contributions);
        Assert.Equal("lamp-1", contribution.SourcePanel2DElementId);
        Assert.Equal("lamp:7", contribution.LinkedMachineObjectReference?.ToString());
        Assert.Equal(2, contribution.PixelCount);
        Assert.Equal(1d, contribution.Bounds!.X);
        Assert.Equal(1d, contribution.Bounds.Y);
        Assert.Equal(2d, contribution.Bounds.Width);
        Assert.Equal(2d, contribution.Bounds.Height);
    }

    public void Dispose()
    {
        if (Directory.Exists(_projectDirectory))
        {
            Directory.Delete(_projectDirectory, recursive: true);
        }
    }

    private static void WriteSolidPng(string path, int width, int height, SKColor color)
    {
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(color);
        SavePng(bitmap, path);
    }

    private static void WriteLampPng(string path)
    {
        using var bitmap = new SKBitmap(2, 2);
        bitmap.Erase(SKColors.Black);
        bitmap.SetPixel(0, 0, SKColors.White);
        bitmap.SetPixel(1, 1, SKColors.White);
        SavePng(bitmap, path);
    }

    private static void SavePng(SKBitmap bitmap, string path)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.Open(path, FileMode.Create, FileAccess.Write, FileShare.None);
        data.SaveTo(stream);
    }
}
