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
        WriteBgra32Bmp(Path.Combine(stagingRoot, "background", "bg.bmp"), 10, 10,
            Enumerable.Repeat(Colors.Black, 100).ToArray());

        var elements = new[]
        {
            new PanelElementModel { ObjectId = "bg", Name = "Background", Kind = PanelElementKind.Background, Width = 10, Height = 10, AssetPath = "background/bg.bmp" }
        };

        var result = new LayoutImportAssetCopier().CopyAssetsFromStaging(stagingRoot, "My Layout", assetsRoot, copyAssets: true, elements);

        Assert.True(result.Succeeded);
        Assert.Contains("Assets/FmlImport/My Layout/Background/bg.png", result.CopiedAssetRelativePaths);
        Assert.Contains(result.Elements, element => element.Kind == PanelElementKind.Background && element.AssetPath == "Assets/FmlImport/My Layout/Background/bg.png");
    }

    [Fact]
    public void CopyAssetsFromStaging_OversizedMfmeBackgroundIsCroppedAtNativePixelScale()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "OasisEditorTests", Guid.NewGuid().ToString("N"));
        try
        {
            var stagingRoot = Path.Combine(tempRoot, "staging");
            var assetsRoot = Path.Combine(tempRoot, "project", "Assets");
            Directory.CreateDirectory(Path.Combine(stagingRoot, "background"));
            var sourcePixels = CreateCoordinatePixels(1000, 750);
            WriteBgra32Bmp(Path.Combine(stagingRoot, "background", "bg.bmp"), 1000, 750, sourcePixels);
            var background = new PanelElementModel { ObjectId = "bg", Kind = PanelElementKind.Background, X = 0, Y = 0, Width = 800, Height = 600, AssetPath = "background/bg.bmp" };
            var lamp = new PanelElementModel { ObjectId = "lamp", Kind = PanelElementKind.Lamp, X = 91, Y = 123, Width = 20, Height = 30 };

            var result = new LayoutImportAssetCopier().CopyAssetsFromStaging(stagingRoot, "Native Background", assetsRoot, true, [background, lamp]);

            Assert.True(result.Succeeded);
            var importedBackground = Assert.Single(result.Elements.Where(element => element.Kind == PanelElementKind.Background));
            var importedLamp = Assert.Single(result.Elements.Where(element => element.Kind == PanelElementKind.Lamp));
            var pixels = ReadPixels(ResolveAsset(assetsRoot, importedBackground.AssetPath!), out var width, out var height);
            Assert.Equal((800, 600), (width, height));
            Assert.Equal(sourcePixels[0], pixels[0]);
            Assert.Equal(sourcePixels[(599 * 1000) + 799], pixels[(599 * 800) + 799]);
            Assert.NotEqual(sourcePixels[^1], pixels[^1]);
            Assert.Equal((0d, 0d, 800d, 600d), (importedBackground.X, importedBackground.Y, importedBackground.Width, importedBackground.Height));
            Assert.Equal((91d, 123d, 20d, 30d), (importedLamp.X, importedLamp.Y, importedLamp.Width, importedLamp.Height));
        }
        finally
        {
            if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void CopyAssetsFromStaging_OversizedBackgroundCutoutUsesUnscaledCoordinates()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "OasisEditorTests", Guid.NewGuid().ToString("N"));
        try
        {
            var stagingRoot = Path.Combine(tempRoot, "staging");
            var assetsRoot = Path.Combine(tempRoot, "project", "Assets");
            Directory.CreateDirectory(Path.Combine(stagingRoot, "background"));
            WriteBgra32Bmp(Path.Combine(stagingRoot, "background", "bg.bmp"), 1000, 750,
                Enumerable.Repeat(Colors.Red, 1000 * 750).ToArray());
            var background = new PanelElementModel { ObjectId = "bg", Kind = PanelElementKind.Background, Width = 800, Height = 600, AssetPath = "background/bg.bmp" };
            var reel = new PanelElementModel { ObjectId = "reel", Kind = PanelElementKind.Reel, X = 100, Y = 200, Width = 50, Height = 80 };

            var result = new LayoutImportAssetCopier().CopyAssetsFromStaging(stagingRoot, "Cutout", assetsRoot, true, [background, reel]);

            Assert.True(result.Succeeded);
            var importedBackground = Assert.Single(result.Elements.Where(element => element.Kind == PanelElementKind.Background));
            var pixels = ReadPixels(ResolveAsset(assetsRoot, importedBackground.AssetPath!), out var width, out _);
            Assert.Equal(0, pixels[(200 * width) + 100].A);
            Assert.Equal(0, pixels[(279 * width) + 149].A);
            Assert.Equal(255, pixels[(199 * width) + 100].A);
            Assert.Equal(255, pixels[(250 * width) + 160].A);
        }
        finally
        {
            if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Theory]
    [InlineData(800, 600)]
    [InlineData(400, 300)]
    public void CopyAssetsFromStaging_BackgroundAtOrBelowLayoutSizeIsNotScaled(int sourceWidth, int sourceHeight)
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "OasisEditorTests", Guid.NewGuid().ToString("N"));
        try
        {
            var stagingRoot = Path.Combine(tempRoot, "staging");
            var assetsRoot = Path.Combine(tempRoot, "project", "Assets");
            Directory.CreateDirectory(Path.Combine(stagingRoot, "background"));
            var sourcePixels = CreateCoordinatePixels(sourceWidth, sourceHeight);
            WriteBgra32Bmp(Path.Combine(stagingRoot, "background", "bg.bmp"), sourceWidth, sourceHeight, sourcePixels);
            var background = new PanelElementModel { ObjectId = "bg", Kind = PanelElementKind.Background, Width = 800, Height = 600, AssetPath = "background/bg.bmp" };

            var result = new LayoutImportAssetCopier().CopyAssetsFromStaging(stagingRoot, "No Scaling", assetsRoot, true, [background]);

            Assert.True(result.Succeeded);
            var imported = Assert.Single(result.Elements);
            var pixels = ReadPixels(ResolveAsset(assetsRoot, imported.AssetPath!), out var width, out var height);
            Assert.Equal((800, 600), (width, height));
            Assert.Equal(sourcePixels[(sourceHeight - 1) * sourceWidth + sourceWidth - 1], pixels[(sourceHeight - 1) * width + sourceWidth - 1]);
            if (sourceWidth < width || sourceHeight < height)
            {
                Assert.Equal(0, pixels[^1].A);
            }
        }
        finally
        {
            if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, recursive: true);
        }
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
    public void CopyAssetsFromStaging_BakesExplicitOffImageNotDynamicImageWithSourceOverAlpha()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "OasisEditorTests", Guid.NewGuid().ToString("N"));
        try
        {
            var stagingRoot = Path.Combine(tempRoot, "staging");
            var assetsRoot = Path.Combine(tempRoot, "project", "Assets");
            Directory.CreateDirectory(Path.Combine(stagingRoot, "background"));
            Directory.CreateDirectory(Path.Combine(stagingRoot, "lamps"));
            var backgroundColor = Colors.Blue;
            WriteBgra32Bmp(Path.Combine(stagingRoot, "background", "bg.bmp"), 30, 40,
                Enumerable.Repeat(backgroundColor, 30 * 40).ToArray());
            var lampPixels = Enumerable.Repeat(Colors.Green, 12).ToArray();
            lampPixels[0] = Color.FromArgb(0, 240, 1, 2);
            lampPixels[1] = Color.FromArgb(128, 220, 120, 20);
            WriteBgra32Bmp(Path.Combine(stagingRoot, "lamps", "on.bmp"), 4, 3,
                Enumerable.Repeat(Colors.Red, 12).ToArray());
            WriteBgra32Bmp(Path.Combine(stagingRoot, "lamps", "off.bmp"), 4, 3, lampPixels);
            var background = new PanelElementModel { ObjectId = "bg", Kind = PanelElementKind.Background, Width = 30, Height = 40, AssetPath = "background/bg.bmp" };
            var lamp = new PanelElementModel
            {
                ObjectId = "lamp", Kind = PanelElementKind.Lamp, X = 10, Y = 20, Width = 4, Height = 3,
                AssetPath = "lamps/on.bmp", SourceOffImageAssetPath = "lamps/off.bmp",
                DisplayNumber = 7, SourceComponentIndex = 1, SourceElementIndex = 0
            };

            var result = new LayoutImportAssetCopier().CopyAssetsFromStaging(stagingRoot, "Lamp Base", assetsRoot, true, [background, lamp]);

            Assert.True(result.Succeeded);
            var importedBackground = Assert.Single(result.Elements.Where(element => element.Kind == PanelElementKind.Background));
            var importedLamp = Assert.Single(result.Elements.Where(element => element.Kind == PanelElementKind.Lamp));
            var output = ReadPixels(ResolveAsset(assetsRoot, importedBackground.AssetPath!), out var width, out _);
            Assert.Equal(backgroundColor, output[(20 * width) + 10]);
            Assert.Equal(Color.FromArgb(255, 110, 60, 137), output[(20 * width) + 11]);
            Assert.Equal(lampPixels[2], output[(20 * width) + 12]);
            Assert.Equal((10d, 20d, 4d, 3d, 7), (importedLamp.X, importedLamp.Y, importedLamp.Width, importedLamp.Height, importedLamp.DisplayNumber!.Value));
            Assert.NotEqual(Colors.Red, output[(20 * width) + 12]);
            Assert.EndsWith("/Lamps/on.png", importedLamp.AssetPath, StringComparison.Ordinal);
            Assert.EndsWith("/Lamps/off.png", importedLamp.SourceOffImageAssetPath, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void CopyAssetsFromStaging_BakesLampRelativeToBackgroundWithoutApplyingBackgroundImageOffsetAndClips()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "OasisEditorTests", Guid.NewGuid().ToString("N"));
        try
        {
            var stagingRoot = Path.Combine(tempRoot, "staging");
            var assetsRoot = Path.Combine(tempRoot, "project", "Assets");
            Directory.CreateDirectory(Path.Combine(stagingRoot, "background"));
            Directory.CreateDirectory(Path.Combine(stagingRoot, "lamps"));
            var source = CreateCoordinatePixels(8, 6);
            WriteBgra32Bmp(Path.Combine(stagingRoot, "background", "bg.bmp"), 8, 6, source);
            WriteBgra32Bmp(Path.Combine(stagingRoot, "lamps", "off.bmp"), 3, 2,
                Enumerable.Repeat(Colors.Magenta, 6).ToArray());
            WriteBgra32Bmp(Path.Combine(stagingRoot, "lamps", "on.bmp"), 3, 2,
                Enumerable.Repeat(Colors.Red, 6).ToArray());
            var background = new PanelElementModel
            {
                ObjectId = "bg", Kind = PanelElementKind.Background, X = 100, Y = 200, Width = 8, Height = 6,
                AssetPath = "background/bg.bmp", SourceImageOffsetX = 2, SourceImageOffsetY = 1
            };
            var lamp = new PanelElementModel
            {
                ObjectId = "lamp", Kind = PanelElementKind.Lamp, X = 106, Y = 204, Width = 3, Height = 2,
                AssetPath = "lamps/on.bmp", SourceOffImageAssetPath = "lamps/off.bmp", SourceComponentIndex = 1
            };

            var result = new LayoutImportAssetCopier().CopyAssetsFromStaging(stagingRoot, "Relative Lamp", assetsRoot, true, [background, lamp]);

            Assert.True(result.Succeeded);
            var imported = Assert.Single(result.Elements.Where(element => element.Kind == PanelElementKind.Background));
            var output = ReadPixels(ResolveAsset(assetsRoot, imported.AssetPath!), out var width, out _);
            Assert.Equal(source[0], output[(1 * width) + 2]);
            Assert.Equal(Colors.Magenta, output[(4 * width) + 6]);
            Assert.Equal(Colors.Magenta, output[(5 * width) + 7]);
            Assert.NotEqual(Colors.Magenta, output[(3 * width) + 6]);
        }
        finally
        {
            if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void CopyAssetsFromStaging_BakesOverlappingLampsInSourceOrderAndSharedMainOnlyOnce()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "OasisEditorTests", Guid.NewGuid().ToString("N"));
        try
        {
            var stagingRoot = Path.Combine(tempRoot, "staging");
            var assetsRoot = Path.Combine(tempRoot, "project", "Assets");
            Directory.CreateDirectory(Path.Combine(stagingRoot, "background"));
            Directory.CreateDirectory(Path.Combine(stagingRoot, "lamps"));
            WriteBgra32Bmp(Path.Combine(stagingRoot, "background", "bg.bmp"), 2, 1, [Colors.Black, Colors.Black]);
            WriteBgra32Bmp(Path.Combine(stagingRoot, "lamps", "shared.bmp"), 1, 1, [Color.FromArgb(128, 200, 0, 0)]);
            WriteBgra32Bmp(Path.Combine(stagingRoot, "lamps", "later.bmp"), 1, 1, [Color.FromArgb(255, 0, 100, 0)]);
            var background = new PanelElementModel { ObjectId = "bg", Kind = PanelElementKind.Background, Width = 2, Height = 1, AssetPath = "background/bg.bmp" };
            var sharedA = new PanelElementModel { ObjectId = "a", Kind = PanelElementKind.Lamp, Width = 1, Height = 1, AssetPath = "lamps/later.bmp", SourceOffImageAssetPath = "lamps/shared.bmp", SourceComponentIndex = 3, SourceElementIndex = 0, SharedSourceSetId = "same", SharedSourceSetCount = 2 };
            var sharedB = PanelElementModelCloner.Clone(sharedA, objectId: "b");
            var later = new PanelElementModel { ObjectId = "later", Kind = PanelElementKind.Lamp, Width = 1, Height = 1, AssetPath = "lamps/shared.bmp", SourceOffImageAssetPath = "lamps/later.bmp", SourceComponentIndex = 4 };

            var result = new LayoutImportAssetCopier().CopyAssetsFromStaging(stagingRoot, "Shared Lamp", assetsRoot, true, [background, later, sharedB, sharedA]);

            Assert.True(result.Succeeded);
            var imported = Assert.Single(result.Elements.Where(element => element.Kind == PanelElementKind.Background));
            var output = ReadPixels(ResolveAsset(assetsRoot, imported.AssetPath!), out _, out _);
            Assert.Equal(Color.FromArgb(255, 0, 100, 0), output[0]);
            Assert.Equal(3, result.Elements.Count(element => element.Kind == PanelElementKind.Lamp));

            // Remove the later opaque component to make repeated semi-transparent compositing observable.
            var onceResult = new LayoutImportAssetCopier().CopyAssetsFromStaging(stagingRoot, "Shared Once", assetsRoot, true, [background, sharedA, sharedB]);
            var onceBackground = Assert.Single(onceResult.Elements.Where(element => element.Kind == PanelElementKind.Background));
            var once = ReadPixels(ResolveAsset(assetsRoot, onceBackground.AssetPath!), out _, out _);
            Assert.Equal(Color.FromArgb(255, 100, 0, 0), once[0]);
        }
        finally
        {
            if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, recursive: true);
        }
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

    [Theory]
    [InlineData(0, 0, 0, 0, 0, 0)]
    [InlineData(2, 1, 0, 0, 2, 1)]
    [InlineData(-2, -1, 2, 1, 0, 0)]
    public void CopyAssetsFromStaging_MfmeBackgroundOffsetPlacesPixelsAtNativeScale(
        int offsetX,
        int offsetY,
        int sourceX,
        int sourceY,
        int destinationX,
        int destinationY)
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "OasisEditorTests", Guid.NewGuid().ToString("N"));
        try
        {
            var stagingRoot = Path.Combine(tempRoot, "staging");
            var assetsRoot = Path.Combine(tempRoot, "project", "Assets");
            Directory.CreateDirectory(Path.Combine(stagingRoot, "background"));
            var sourcePixels = CreateCoordinatePixels(8, 6);
            WriteBgra32Bmp(Path.Combine(stagingRoot, "background", "bg.bmp"), 8, 6, sourcePixels);
            var background = new PanelElementModel
            {
                ObjectId = "bg", Kind = PanelElementKind.Background, Width = 8, Height = 6,
                AssetPath = "background/bg.bmp", SourceImageOffsetX = offsetX, SourceImageOffsetY = offsetY
            };

            var result = new LayoutImportAssetCopier().CopyAssetsFromStaging(stagingRoot, "Offset", assetsRoot, true, [background]);

            Assert.True(result.Succeeded);
            var imported = Assert.Single(result.Elements);
            var output = ReadPixels(ResolveAsset(assetsRoot, imported.AssetPath!), out var width, out _);
            Assert.Equal(sourcePixels[(sourceY * 8) + sourceX], output[(destinationY * width) + destinationX]);
            if (offsetX > 0 || offsetY > 0)
            {
                Assert.Equal(0, output[0].A);
            }
        }
        finally
        {
            if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void CopyAssetsFromStaging_OversizedShiftedBackgroundIsClippedBeforeUnshiftedReelCutout()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "OasisEditorTests", Guid.NewGuid().ToString("N"));
        try
        {
            var stagingRoot = Path.Combine(tempRoot, "staging");
            var assetsRoot = Path.Combine(tempRoot, "project", "Assets");
            Directory.CreateDirectory(Path.Combine(stagingRoot, "background"));
            var sourcePixels = CreateCoordinatePixels(1000, 750);
            WriteBgra32Bmp(Path.Combine(stagingRoot, "background", "bg.bmp"), 1000, 750, sourcePixels);
            var background = new PanelElementModel
            {
                ObjectId = "bg", Kind = PanelElementKind.Background, Width = 800, Height = 600,
                AssetPath = "background/bg.bmp", SourceImageOffsetX = -50, SourceImageOffsetY = -25
            };
            var reel = new PanelElementModel { ObjectId = "reel", Kind = PanelElementKind.Reel, X = 200, Y = 150, Width = 50, Height = 100 };

            var result = new LayoutImportAssetCopier().CopyAssetsFromStaging(stagingRoot, "Shifted Cutout", assetsRoot, true, [background, reel]);

            Assert.True(result.Succeeded);
            var imported = Assert.Single(result.Elements.Where(element => element.Kind == PanelElementKind.Background));
            var output = ReadPixels(ResolveAsset(assetsRoot, imported.AssetPath!), out var width, out var height);
            Assert.Equal((800, 600), (width, height));
            Assert.Equal(sourcePixels[(25 * 1000) + 50], output[0]);
            Assert.Equal(sourcePixels[(149 + 25) * 1000 + (199 + 50)], output[(149 * width) + 199]);
            Assert.Equal(0, output[(150 * width) + 200].A);
            Assert.Equal(0, output[(249 * width) + 249].A);
            Assert.NotEqual(0, output[(250 * width) + 250].A);
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

    private static Color[] CreateCoordinatePixels(int width, int height)
    {
        return Enumerable.Range(0, width * height)
            .Select(index => Color.FromArgb(255, (byte)(index / width % 251), (byte)(index % width % 253), (byte)((index / width + index % width) % 249)))
            .ToArray();
    }

    private static string ResolveAsset(string assetsRoot, string assetPath)
    {
        return Path.Combine(assetsRoot, assetPath["Assets/".Length..].Replace('/', Path.DirectorySeparatorChar));
    }

}
