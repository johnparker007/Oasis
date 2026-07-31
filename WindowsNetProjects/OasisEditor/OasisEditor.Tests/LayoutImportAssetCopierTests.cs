using OasisEditor.Features.FmlImport;
using OasisEditor.Features.LayoutImport;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Xunit;

namespace OasisEditor.Tests;

public sealed class LayoutImportAssetCopierTests
{
    [Fact]
    public void CopyAssetsFromStaging_CopiesAssetsIntoFmlImportFolderAndUpdatesElements()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "OasisEditorTests", Guid.NewGuid().ToString("N"));
        var stagingRoot = Path.Combine(tempRoot, "staging");
        var projectRoot = Path.Combine(tempRoot, "project");
        var assetsRoot = Path.Combine(projectRoot, "Assets");
        Directory.CreateDirectory(Path.Combine(stagingRoot, "background"));
        File.WriteAllBytes(Path.Combine(stagingRoot, "background", "bg.png"), [1, 2, 3]);

        var elements = new[]
        {
            new PanelElementModel { ObjectId = "bg", Name = "Background", Kind = PanelElementKind.Background, Width = 10, Height = 10, AssetPath = "background/bg.png" }
        };

        var result = new LayoutImportAssetCopier().CopyAssetsFromStaging(stagingRoot, "My Layout", assetsRoot, copyAssets: true, elements);

        Assert.True(result.Succeeded);
        Assert.Contains("Assets/FmlImport/My Layout/Background/bg.png", result.CopiedAssetRelativePaths);
        Assert.Contains(result.Elements, element => element.Kind == PanelElementKind.Background && element.AssetPath == "Assets/FmlImport/My Layout/Background/bg.png");
    }


    [Fact]
    public void CopyAssetsFromStaging_PreservesReelLampEnabledAndSlotsWhileMappingAssets()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "OasisEditorTests", Guid.NewGuid().ToString("N"));
        var stagingRoot = Path.Combine(tempRoot, "staging");
        var projectRoot = Path.Combine(tempRoot, "project");
        var assetsRoot = Path.Combine(projectRoot, "Assets");
        Directory.CreateDirectory(Path.Combine(stagingRoot, "reels"));
        File.WriteAllBytes(Path.Combine(stagingRoot, "reels", "reel.png"), [1, 2, 3]);
        var elements = new[]
        {
            new PanelElementModel
            {
                ObjectId = "reel", Name = "Reel 2", Kind = PanelElementKind.Reel, Width = 10, Height = 10, AssetPath = "reels/reel.png",
                ReelLampsEnabled = true,
                ReelLamps =
                [
                    new ReelLampSlotModel { Position = ReelLampSlotPosition.Top, LampNumber = 5, LocalVerticalCenter = 0.12d, Radius = 0.21d, Intensity = 1.1d },
                    new ReelLampSlotModel { Position = ReelLampSlotPosition.Middle, LampNumber = 4, LocalVerticalCenter = 0.45d, Radius = 0.31d, Intensity = 1.2d },
                    new ReelLampSlotModel { Position = ReelLampSlotPosition.Bottom, LampNumber = 3, LocalVerticalCenter = 0.88d, Radius = 0.41d, Intensity = 1.3d }
                ]
            }
        };

        var result = new LayoutImportAssetCopier().CopyAssetsFromStaging(stagingRoot, "My Layout", assetsRoot, copyAssets: true, elements, FmlBackgroundMode.NoBackground);

        Assert.True(result.Succeeded);
        var reel = Assert.Single(result.Elements);
        Assert.Equal("Assets/FmlImport/My Layout/Reels/reel.png", reel.AssetPath);
        Assert.True(reel.ReelLampsEnabled);
        Assert.Equal([5, 4, 3], reel.ReelLamps.Select(lamp => lamp.LampNumber).ToArray());
    }

    [Fact]
    public void CopyAssetsFromStaging_GraphicalLampPreservesOnImagePixelsAndIgnoresMaskAndOnColor()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "OasisEditorTests", Guid.NewGuid().ToString("N"));
        try
        {
            var stagingRoot = Path.Combine(tempRoot, "staging");
            var assetsRoot = Path.Combine(tempRoot, "project", "Assets");
            Directory.CreateDirectory(Path.Combine(stagingRoot, "lamps"));
            var sourcePixels = new[]
            {
                Color.FromArgb(255, 240, 40, 180), Color.FromArgb(255, 250, 210, 30),
                Color.FromArgb(255, 20, 15, 25), Color.FromArgb(0, 100, 50, 80)
            };
            WriteBgra32Bmp(Path.Combine(stagingRoot, "lamps", "on.bmp"), 2, 2, sourcePixels);
            WriteBgra32Bmp(Path.Combine(stagingRoot, "lamps", "mask.bmp"), 2, 2,
                Enumerable.Repeat(Color.FromArgb(255, 8, 16, 32), 4).ToArray());
            var element = new PanelElementModel
            {
                ObjectId = "graphical-lamp", Kind = PanelElementKind.Lamp, Width = 2, Height = 2,
                AssetPath = "lamps/on.bmp", SecondaryAssetPath = "lamps/mask.bmp", OnColorHex = "#FF00FF00"
            };

            var result = new LayoutImportAssetCopier().CopyAssetsFromStaging(stagingRoot, "Bright Lamps", assetsRoot, copyAssets: true, [element], FmlBackgroundMode.NoBackground);

            Assert.True(result.Succeeded);
            var imported = Assert.Single(result.Elements);
            Assert.Equal("#FF00FF00", imported.OnColorHex);
            Assert.NotNull(imported.SecondaryAssetPath);
            var outputPath = Path.Combine(assetsRoot, imported.AssetPath!["Assets/".Length..].Replace('/', Path.DirectorySeparatorChar));
            var outputPixels = ReadPixels(outputPath, out var width, out var height);
            Assert.Equal(2, width);
            Assert.Equal(2, height);
            Assert.Equal(sourcePixels[0], outputPixels[0]);
            Assert.Equal(sourcePixels[1], outputPixels[1]);
            Assert.Equal(sourcePixels[2], outputPixels[2]);
            Assert.Equal(sourcePixels[3].A, outputPixels[3].A);
            Assert.NotEqual(Color.FromArgb(255, 7, 2, 22), outputPixels[0]);
        }
        finally
        {
            if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static void WriteBgra32Bmp(string path, int width, int height, IReadOnlyList<Color> pixels)
    {
        const int headerSize = 54;
        var pixelBytes = width * height * 4;
        using var writer = new BinaryWriter(File.Create(path));
        writer.Write((byte)'B');
        writer.Write((byte)'M');
        writer.Write(headerSize + pixelBytes);
        writer.Write(0);
        writer.Write(headerSize);
        writer.Write(40);
        writer.Write(width);
        writer.Write(-height);
        writer.Write((ushort)1);
        writer.Write((ushort)32);
        writer.Write(0);
        writer.Write(pixelBytes);
        writer.Write(2835);
        writer.Write(2835);
        writer.Write(0);
        writer.Write(0);
        foreach (var pixel in pixels)
        {
            writer.Write(pixel.B);
            writer.Write(pixel.G);
            writer.Write(pixel.R);
            writer.Write(pixel.A);
        }
    }

    private static Color[] ReadPixels(string path, out int width, out int height)
    {
        using var stream = File.OpenRead(path);
        var decoder = new PngBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        var converted = new FormatConvertedBitmap(decoder.Frames[0], PixelFormats.Bgra32, null, 0);
        width = converted.PixelWidth;
        height = converted.PixelHeight;
        var pixels = new byte[width * height * 4];
        converted.CopyPixels(pixels, width * 4, 0);
        return Enumerable.Range(0, width * height)
            .Select(index => Color.FromArgb(pixels[(index * 4) + 3], pixels[(index * 4) + 2], pixels[(index * 4) + 1], pixels[index * 4]))
            .ToArray();
    }

}
