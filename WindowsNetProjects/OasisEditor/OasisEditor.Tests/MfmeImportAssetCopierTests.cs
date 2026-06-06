using System.Windows.Media;
using System.Windows.Media.Imaging;
using OasisEditor.Features.MfmeImport;
using Xunit;

namespace OasisEditor.Tests;

public sealed class MfmeImportAssetCopierTests
{
    [Fact]
    public void CopyAssets_CopiesKnownFoldersAndRewritesToProjectRelativePaths()
    {
        var extractRoot = CreateTempDirectory();
        var projectRoot = CreateTempDirectory();

        try
        {
            Directory.CreateDirectory(Path.Combine(extractRoot, "background"));
            Directory.CreateDirectory(Path.Combine(extractRoot, "lamps"));
            Directory.CreateDirectory(Path.Combine(extractRoot, "reels"));
            CreatePng(Path.Combine(extractRoot, "background", "bg.png"), 1, 1, _ => Color.FromArgb(255, 10, 20, 30));
            File.WriteAllText(Path.Combine(extractRoot, "lamps", "lamp.png"), "lamp");
            File.WriteAllText(Path.Combine(extractRoot, "reels", "band.png"), "band");

            var context = new MfmeImportContext
            {
                SourceExtractPath = extractRoot,
                ProjectRootPath = projectRoot,
                ProjectAssetsPath = Path.Combine(projectRoot, "Assets"),
                CopyAssets = true
            };

            var extract = new MfmeLegacyExtractData
            {
                ExtractRootPath = extractRoot,
                ManifestPath = Path.Combine(extractRoot, "layout.json"),
                LayoutName = "My Layout",
                Components = []
            };

            var elements = new[]
            {
                new PanelElementModel
                {
                    ObjectId = "bg-1",
                    Name = "Background",
                    Kind = PanelElementKind.Background,
                    Width = 1,
                    Height = 1,
                    AssetPath = "background/bg.png"
                },
                new PanelElementModel
                {
                    ObjectId = "lamp-1",
                    Name = "Lamp",
                    Kind = PanelElementKind.Lamp,
                    Width = 1,
                    Height = 1,
                    AssetPath = "lamps/lamp.png",
                    TextBoxFontName = "Lithograph",
                    TextBoxFontStyle = "Bold",
                    TextBoxFontSize = "12"
                },
                new PanelElementModel
                {
                    ObjectId = "reel-1",
                    Name = "Reel",
                    Kind = PanelElementKind.Reel,
                    Width = 1,
                    Height = 1,
                    AssetPath = "reels/band.png"
                }
            };

            var copier = new MfmeImportAssetCopier();
            var result = copier.CopyAssets(context, extract, elements);

            Assert.True(result.Succeeded);
            Assert.Empty(result.Warnings);
            Assert.Equal(3, result.CopiedAssetRelativePaths.Count);
            Assert.All(result.CopiedAssetRelativePaths, path => Assert.StartsWith("Assets/MfmeImport/My Layout/", path));

            var background = Assert.Single(result.Elements, element => element.Kind == PanelElementKind.Background);
            var lamp = Assert.Single(result.Elements, element => element.Kind == PanelElementKind.Lamp);
            var reel = Assert.Single(result.Elements, element => element.Kind == PanelElementKind.Reel);
            Assert.Equal("Assets/MfmeImport/My Layout/Background/bg.png", background.AssetPath);
            Assert.Equal("Assets/MfmeImport/My Layout/Lamps/lamp.png", lamp.AssetPath);
            Assert.Equal("Assets/MfmeImport/My Layout/Reels/band.png", reel.AssetPath);
            Assert.Equal("Lithograph", lamp.TextBoxFontName);
            Assert.Equal("Bold", lamp.TextBoxFontStyle);
            Assert.Equal("12", lamp.TextBoxFontSize);
        }
        finally
        {
            Directory.Delete(extractRoot, recursive: true);
            Directory.Delete(projectRoot, recursive: true);
        }
    }

    [Fact]
    public void CopyAssets_WithMissingFile_AddsWarningAndKeepsElementEditable()
    {
        var extractRoot = CreateTempDirectory();
        var projectRoot = CreateTempDirectory();

        try
        {
            Directory.CreateDirectory(Path.Combine(extractRoot, "background"));

            var context = new MfmeImportContext
            {
                SourceExtractPath = extractRoot,
                ProjectRootPath = projectRoot,
                ProjectAssetsPath = Path.Combine(projectRoot, "Assets"),
                CopyAssets = true
            };

            var extract = new MfmeLegacyExtractData
            {
                ExtractRootPath = extractRoot,
                ManifestPath = Path.Combine(extractRoot, "layout.json"),
                LayoutName = "layout",
                Components = []
            };

            var element = new PanelElementModel
            {
                ObjectId = "bg-1",
                Name = "Background",
                Kind = PanelElementKind.Background,
                Width = 1,
                Height = 1,
                AssetPath = "background/missing.png"
            };

            var copier = new MfmeImportAssetCopier();
            var result = copier.CopyAssets(context, extract, [element]);

            Assert.True(result.Succeeded);
            var warning = Assert.Single(result.Warnings);
            Assert.Equal("mfme.import.asset.missing", warning.Code);
            Assert.Null(result.Elements[0].AssetPath);
        }
        finally
        {
            Directory.Delete(extractRoot, recursive: true);
            Directory.Delete(projectRoot, recursive: true);
        }
    }

    [Fact]
    public void CopyAssets_WithNameCollision_UsesDeterministicSuffix()
    {
        var extractRoot = CreateTempDirectory();
        var projectRoot = CreateTempDirectory();

        try
        {
            Directory.CreateDirectory(Path.Combine(extractRoot, "lamps"));
            File.WriteAllText(Path.Combine(extractRoot, "lamps", "lamp.png"), "source");

            var context = new MfmeImportContext
            {
                SourceExtractPath = extractRoot,
                ProjectRootPath = projectRoot,
                ProjectAssetsPath = Path.Combine(projectRoot, "Assets"),
                CopyAssets = true
            };

            var extract = new MfmeLegacyExtractData
            {
                ExtractRootPath = extractRoot,
                ManifestPath = Path.Combine(extractRoot, "layout.json"),
                LayoutName = "layout",
                Components = []
            };

            var existingFolder = Path.Combine(projectRoot, "Assets", "MfmeImport", "layout", "Lamps");
            Directory.CreateDirectory(existingFolder);
            File.WriteAllText(Path.Combine(existingFolder, "lamp.png"), "existing");

            var element = new PanelElementModel
            {
                ObjectId = "lamp-1",
                Name = "Lamp",
                Kind = PanelElementKind.Lamp,
                Width = 1,
                Height = 1,
                AssetPath = "lamps/lamp.png"
            };

            var copier = new MfmeImportAssetCopier();
            var result = copier.CopyAssets(context, extract, [element]);

            Assert.True(result.Succeeded);
            Assert.Equal("Assets/MfmeImport/layout/Lamps/lamp_2.png", result.Elements[0].AssetPath);
            Assert.Contains("Assets/MfmeImport/layout/Lamps/lamp_2.png", result.CopiedAssetRelativePaths);
        }
        finally
        {
            Directory.Delete(extractRoot, recursive: true);
            Directory.Delete(projectRoot, recursive: true);
        }
    }

    [Fact]
    public void CopyAssets_WithEscapingRelativePath_IsRejectedByContainmentGuard()
    {
        var extractRoot = CreateTempDirectory();
        var projectRoot = CreateTempDirectory();

        try
        {
            var context = new MfmeImportContext
            {
                SourceExtractPath = extractRoot,
                ProjectRootPath = projectRoot,
                ProjectAssetsPath = Path.Combine(projectRoot, "Assets"),
                CopyAssets = true
            };

            var extract = new MfmeLegacyExtractData
            {
                ExtractRootPath = extractRoot,
                ManifestPath = Path.Combine(extractRoot, "layout.json"),
                LayoutName = "layout",
                Components = []
            };

            var element = new PanelElementModel
            {
                ObjectId = "x",
                Name = "Escape",
                Kind = PanelElementKind.Image,
                Width = 1,
                Height = 1,
                AssetPath = "background/../outside.png"
            };

            var copier = new MfmeImportAssetCopier();
            var result = copier.CopyAssets(context, extract, [element]);

            Assert.True(result.Succeeded);
            var warning = Assert.Single(result.Warnings);
            Assert.Equal("mfme.import.asset.path.invalid", warning.Code);
            Assert.Null(result.Elements[0].AssetPath);
        }
        finally
        {
            Directory.Delete(extractRoot, recursive: true);
            Directory.Delete(projectRoot, recursive: true);
        }
    }


    [Fact]
    public void CopyAssets_WithReelBmpOverlay_BakesOverlayPixelsAndAlphaIntoBackground()
    {
        var extractRoot = CreateTempDirectory();
        var projectRoot = CreateTempDirectory();

        try
        {
            Directory.CreateDirectory(Path.Combine(extractRoot, "background"));
            Directory.CreateDirectory(Path.Combine(extractRoot, "reels"));
            CreatePng(Path.Combine(extractRoot, "background", "bg.png"), 4, 4, _ => Color.FromArgb(255, 10, 20, 30));
            CreatePng(Path.Combine(extractRoot, "reels", "band.png"), 1, 1, _ => Color.FromArgb(255, 1, 2, 3));
            CreateTopDownBgra32Bmp(Path.Combine(extractRoot, "reels", "overlay.bmp"), 2, 2, index => index switch
            {
                0 => Color.FromArgb(128, 200, 0, 0),
                1 => Color.FromArgb(0, 0, 200, 0),
                2 => Color.FromArgb(255, 0, 0, 200),
                _ => Color.FromArgb(64, 200, 200, 0)
            });

            var context = new MfmeImportContext
            {
                SourceExtractPath = extractRoot,
                ProjectRootPath = projectRoot,
                ProjectAssetsPath = Path.Combine(projectRoot, "Assets"),
                CopyAssets = true
            };

            var extract = new MfmeLegacyExtractData
            {
                ExtractRootPath = extractRoot,
                ManifestPath = Path.Combine(extractRoot, "layout.json"),
                LayoutName = "layout",
                Components = []
            };

            var elements = new[]
            {
                new PanelElementModel
                {
                    ObjectId = "bg",
                    Name = "Background",
                    Kind = PanelElementKind.Background,
                    Width = 4,
                    Height = 4,
                    AssetPath = "background/bg.png"
                },
                new PanelElementModel
                {
                    ObjectId = "reel",
                    Name = "Reel",
                    Kind = PanelElementKind.Reel,
                    X = 1,
                    Y = 1,
                    Width = 2,
                    Height = 2,
                    AssetPath = "reels/band.png",
                    SecondaryAssetPath = "reels/overlay.bmp"
                }
            };

            var copier = new MfmeImportAssetCopier();
            var result = copier.CopyAssets(context, extract, elements);

            Assert.True(result.Succeeded);
            Assert.Empty(result.Errors);
            Assert.Equal(PanelElementKind.Reel, result.Elements[0].Kind);

            var background = Assert.Single(result.Elements, element => element.Kind == PanelElementKind.Background);
            var backgroundPath = Path.Combine(projectRoot, "Assets", background.AssetPath!["Assets/".Length..]);
            var pixels = ReadBgra32(backgroundPath, out var stride);

            var untouched = GetPixel(pixels, stride, 0, 0);
            Assert.Equal(255, untouched.A);
            Assert.Equal(10, untouched.R);
            Assert.Equal(20, untouched.G);
            Assert.Equal(30, untouched.B);

            var halfAlphaOverlayPixel = GetPixel(pixels, stride, 1, 1);
            Assert.Equal(128, halfAlphaOverlayPixel.A);
            Assert.Equal(200, halfAlphaOverlayPixel.R);
            Assert.Equal(0, halfAlphaOverlayPixel.G);
            Assert.Equal(0, halfAlphaOverlayPixel.B);

            var transparentOverlayPixel = GetPixel(pixels, stride, 2, 1);
            Assert.Equal(0, transparentOverlayPixel.A);

            var opaqueOverlayPixel = GetPixel(pixels, stride, 1, 2);
            Assert.Equal(255, opaqueOverlayPixel.A);
            Assert.Equal(0, opaqueOverlayPixel.R);
            Assert.Equal(0, opaqueOverlayPixel.G);
            Assert.Equal(200, opaqueOverlayPixel.B);
        }
        finally
        {
            Directory.Delete(extractRoot, recursive: true);
            Directory.Delete(projectRoot, recursive: true);
        }
    }


    [Fact]
    public void CopyAssets_WithReelAndAlphaWithoutOverlay_ClearsBackgroundRectsAndSendsDisplaysBehindBackground()
    {
        var extractRoot = CreateTempDirectory();
        var projectRoot = CreateTempDirectory();

        try
        {
            Directory.CreateDirectory(Path.Combine(extractRoot, "background"));
            Directory.CreateDirectory(Path.Combine(extractRoot, "reels"));
            CreatePng(Path.Combine(extractRoot, "background", "bg.png"), 5, 4, _ => Color.FromArgb(255, 10, 20, 30));
            CreatePng(Path.Combine(extractRoot, "reels", "band.png"), 1, 1, _ => Color.FromArgb(255, 1, 2, 3));

            var context = new MfmeImportContext
            {
                SourceExtractPath = extractRoot,
                ProjectRootPath = projectRoot,
                ProjectAssetsPath = Path.Combine(projectRoot, "Assets"),
                CopyAssets = true
            };

            var extract = new MfmeLegacyExtractData
            {
                ExtractRootPath = extractRoot,
                ManifestPath = Path.Combine(extractRoot, "layout.json"),
                LayoutName = "layout",
                Components = []
            };

            var elements = new[]
            {
                new PanelElementModel
                {
                    ObjectId = "bg",
                    Name = "Background",
                    Kind = PanelElementKind.Background,
                    Width = 5,
                    Height = 4,
                    AssetPath = "background/bg.png"
                },
                new PanelElementModel
                {
                    ObjectId = "reel",
                    Name = "Reel",
                    Kind = PanelElementKind.Reel,
                    X = 1,
                    Y = 1,
                    Width = 1,
                    Height = 1,
                    AssetPath = "reels/band.png"
                },
                new PanelElementModel
                {
                    ObjectId = "alpha",
                    Name = "Alpha",
                    Kind = PanelElementKind.Alpha,
                    X = 3,
                    Y = 2,
                    Width = 1,
                    Height = 1
                }
            };

            var copier = new MfmeImportAssetCopier();
            var result = copier.CopyAssets(context, extract, elements);

            Assert.True(result.Succeeded);
            Assert.Equal(PanelElementKind.Reel, result.Elements[0].Kind);
            Assert.Equal(PanelElementKind.Alpha, result.Elements[1].Kind);
            Assert.Equal(PanelElementKind.Background, result.Elements[2].Kind);

            var background = Assert.Single(result.Elements, element => element.Kind == PanelElementKind.Background);
            var backgroundPath = Path.Combine(projectRoot, "Assets", background.AssetPath!["Assets/".Length..]);
            var pixels = ReadBgra32(backgroundPath, out var stride);

            Assert.Equal(0, GetPixel(pixels, stride, 1, 1).A);
            Assert.Equal(0, GetPixel(pixels, stride, 3, 2).A);
            Assert.Equal(255, GetPixel(pixels, stride, 0, 0).A);
        }
        finally
        {
            Directory.Delete(extractRoot, recursive: true);
            Directory.Delete(projectRoot, recursive: true);
        }
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"oasis-mfme-copy-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }


    private static void CreatePng(string path, int width, int height, Func<int, Color> colorFactory)
    {
        var pixels = new byte[width * height * 4];
        for (var index = 0; index < width * height; index++)
        {
            var color = colorFactory(index);
            pixels[(index * 4) + 0] = color.B;
            pixels[(index * 4) + 1] = color.G;
            pixels[(index * 4) + 2] = color.R;
            pixels[(index * 4) + 3] = color.A;
        }

        var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, width * 4);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }

    private static void CreateTopDownBgra32Bmp(string path, int width, int height, Func<int, Color> colorFactory)
    {
        var pixelDataOffset = 14 + 40;
        var stride = width * 4;
        var fileSize = pixelDataOffset + (stride * height);

        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);
        writer.Write((byte)'B');
        writer.Write((byte)'M');
        writer.Write(fileSize);
        writer.Write(0);
        writer.Write(pixelDataOffset);
        writer.Write(40);
        writer.Write(width);
        writer.Write(-height);
        writer.Write((ushort)1);
        writer.Write((ushort)32);
        writer.Write(0);
        writer.Write(stride * height);
        writer.Write(0);
        writer.Write(0);
        writer.Write(0);
        writer.Write(0);

        for (var index = 0; index < width * height; index++)
        {
            var color = colorFactory(index);
            writer.Write(color.B);
            writer.Write(color.G);
            writer.Write(color.R);
            writer.Write(color.A);
        }
    }

    private static byte[] ReadBgra32(string path, out int stride)
    {
        using var stream = File.OpenRead(path);
        var decoder = new PngBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        var converted = new FormatConvertedBitmap(decoder.Frames[0], PixelFormats.Bgra32, null, 0);
        stride = converted.PixelWidth * 4;
        var pixels = new byte[stride * converted.PixelHeight];
        converted.CopyPixels(pixels, stride, 0);
        return pixels;
    }

    private static Color GetPixel(byte[] pixels, int stride, int x, int y)
    {
        var offset = (y * stride) + (x * 4);
        return Color.FromArgb(pixels[offset + 3], pixels[offset + 2], pixels[offset + 1], pixels[offset + 0]);
    }
}
