using System.Windows.Media;
using System.Windows.Media.Imaging;
using OasisEditor.Features.MfmeImport;
using Xunit;

namespace OasisEditor.Tests;

public sealed class MfmeImportLampPostProcessorTests
{
    [Fact]
    public void CopyAssets_LampProcessing_PreservesSourceAlpha_AndMaskTintUsesVisiblePixels()
    {
        var extractRoot = CreateTempDirectory();
        var projectRoot = CreateTempDirectory();

        try
        {
            Directory.CreateDirectory(Path.Combine(extractRoot, "lamps"));
            CreateIndexedLampBmp(Path.Combine(extractRoot, "lamps", "lamp.bmp"));
            CreateMaskBmp(Path.Combine(extractRoot, "lamps", "lamp-mask.bmp"), new byte[] { 255, 0, 0, 255 });
            File.WriteAllText(Path.Combine(extractRoot, "lamps", "symbol.png"), "noop");

            var copier = new MfmeImportAssetCopier();
            var result = copier.CopyAssets(CreateContext(extractRoot, projectRoot), CreateExtract(extractRoot),
            [
                new PanelElementModel { ObjectId = "lamp", Name = "lamp", Kind = PanelElementKind.Lamp, Width = 2, Height = 2, AssetPath = "lamps/lamp.bmp", SecondaryAssetPath = "lamps/lamp-mask.bmp" },
                new PanelElementModel { ObjectId = "img", Name = "img", Kind = PanelElementKind.Image, Width = 1, Height = 1, AssetPath = "lamps/symbol.png" }
            ]);

            Assert.True(result.Succeeded);
            var lampAsset = Assert.Single(result.Elements, e => e.Kind == PanelElementKind.Lamp);
            Assert.EndsWith(".png", lampAsset.AssetPath, StringComparison.OrdinalIgnoreCase);
            Assert.NotNull(lampAsset.SecondaryAssetPath);

            var lampPath = Path.Combine(projectRoot, "Assets", lampAsset.AssetPath!["Assets/".Length..]);
            var pixels = ReadBgra32(lampPath, out _);

            var alphas = Enumerable.Range(0, pixels.Length / 4)
                .Select(i => pixels[(i * 4) + 3])
                .ToArray();

            Assert.Equal(new byte[] { 0, 255, 255, 0 }, alphas);
            Assert.Equal(0, pixels[3]);
            Assert.Equal(0, pixels[15]);
            Assert.Equal(0, pixels[4]);
            Assert.Equal(0, pixels[5]);
            Assert.Equal((byte)50, pixels[6]);

            var imageAsset = Assert.Single(result.Elements, e => e.Kind == PanelElementKind.Image);
            Assert.EndsWith("symbol.png", imageAsset.AssetPath, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(extractRoot, true);
            Directory.Delete(projectRoot, true);
        }
    }


    [Fact]
    public void CopyAssets_LampWithoutMask_DoesNotDeriveTransparentColorFromEdges()
    {
        var extractRoot = CreateTempDirectory();
        var projectRoot = CreateTempDirectory();

        try
        {
            Directory.CreateDirectory(Path.Combine(extractRoot, "lamps"));
            CreateUnmaskedLampBmp(Path.Combine(extractRoot, "lamps", "flat.bmp"));

            var copier = new MfmeImportAssetCopier();
            var result = copier.CopyAssets(CreateContext(extractRoot, projectRoot), CreateExtract(extractRoot),
            [
                new PanelElementModel { ObjectId = "lamp-no-mask", Name = "lamp", Kind = PanelElementKind.Lamp, Width = 3, Height = 3, AssetPath = "lamps/flat.bmp" }
            ]);

            Assert.True(result.Succeeded);
            var lampAsset = Assert.Single(result.Elements);
            var lampPath = Path.Combine(projectRoot, "Assets", lampAsset.AssetPath!["Assets/".Length..]);
            var pixels = ReadBgra32(lampPath, out _);

            var alphas = Enumerable.Range(0, pixels.Length / 4).Select(i => pixels[(i * 4) + 3]).ToArray();
            Assert.All(alphas, alpha => Assert.Equal(255, alpha));
            Assert.Equal(255, alphas[4]); // center pixel remains visible
        }
        finally
        {
            Directory.Delete(extractRoot, true);
            Directory.Delete(projectRoot, true);
        }
    }


    [Fact]
    public void CopyAssets_LampWithoutMask_DoesNotRepairTransparentFringePixels()
    {
        var extractRoot = CreateTempDirectory();
        var projectRoot = CreateTempDirectory();

        try
        {
            Directory.CreateDirectory(Path.Combine(extractRoot, "lamps"));
            CreateTransparentFringeLampBmp(Path.Combine(extractRoot, "lamps", "fringe.bmp"));

            var copier = new MfmeImportAssetCopier();
            var result = copier.CopyAssets(CreateContext(extractRoot, projectRoot), CreateExtract(extractRoot),
            [ new PanelElementModel { ObjectId = "lamp-fringe", Name = "lamp", Kind = PanelElementKind.Lamp, Width = 2, Height = 2, AssetPath = "lamps/fringe.bmp" } ]);

            Assert.True(result.Succeeded);
            var lampPath = Path.Combine(projectRoot, "Assets", result.Elements[0].AssetPath!["Assets/".Length..]);
            var pixels = ReadBgra32(lampPath, out _);

            Assert.Equal(0, pixels[3]);
            Assert.Equal(0, pixels[0]);
            Assert.Equal(0, pixels[1]);
            Assert.Equal(255, pixels[2]);
            Assert.Equal(255, pixels[7]);
        }
        finally
        {
            Directory.Delete(extractRoot, true);
            Directory.Delete(projectRoot, true);
        }
    }


    [Fact]
    public void CopyAssets_LampIndexed4WithoutMask_PreservesPaletteAlpha()
    {
        var extractRoot = CreateTempDirectory();
        var projectRoot = CreateTempDirectory();

        try
        {
            Directory.CreateDirectory(Path.Combine(extractRoot, "lamps"));
            CreateIndexed4LampBmp(Path.Combine(extractRoot, "lamps", "indexed4.bmp"));

            var copier = new MfmeImportAssetCopier();
            var result = copier.CopyAssets(CreateContext(extractRoot, projectRoot), CreateExtract(extractRoot),
            [ new PanelElementModel { ObjectId = "lamp-idx4", Name = "lamp", Kind = PanelElementKind.Lamp, Width = 2, Height = 2, AssetPath = "lamps/indexed4.bmp" } ]);

            Assert.True(result.Succeeded);
            var lampPath = Path.Combine(projectRoot, "Assets", result.Elements[0].AssetPath!["Assets/".Length..]);
            var pixels = ReadBgra32(lampPath, out _);
            var alphas = Enumerable.Range(0, pixels.Length / 4).Select(i => pixels[(i * 4) + 3]).ToArray();
            Assert.Contains((byte)0, alphas);
            Assert.Contains((byte)255, alphas);
        }
        finally
        {
            Directory.Delete(extractRoot, true);
            Directory.Delete(projectRoot, true);
        }
    }

    private static MfmeImportContext CreateContext(string extractRoot, string projectRoot) => new()
    {
        SourceExtractPath = extractRoot,
        ProjectRootPath = projectRoot,
        ProjectAssetsPath = Path.Combine(projectRoot, "Assets"),
        CopyAssets = true
    };

    private static MfmeLegacyExtractData CreateExtract(string extractRoot) => new()
    {
        ExtractRootPath = extractRoot,
        ManifestPath = Path.Combine(extractRoot, "layout.json"),
        LayoutName = "fixtures",
        Components = []
    };

    private static void CreateIndexedLampBmp(string path)
    {
        WriteIndexedBmp(
            path,
            width: 2,
            height: 2,
            bitsPerPixel: 8,
            palette: [Color.FromArgb(0, 255, 0, 0), Color.FromArgb(255, 200, 100, 50)],
            rows: [[0, 1], [1, 0]]);
    }



    private static void CreateIndexed4LampBmp(string path)
    {
        WriteIndexedBmp(
            path,
            width: 2,
            height: 2,
            bitsPerPixel: 4,
            palette: [Color.FromArgb(0, 255, 0, 255), Color.FromArgb(255, 255, 255, 0)],
            rows: [[0, 1], [1, 0]]);
    }

    private static void CreateUnmaskedLampBmp(string path)
    {
        var pixels = new byte[3 * 3 * 4];
        for (var y = 0; y < 3; y++)
        {
            for (var x = 0; x < 3; x++)
            {
                var i = ((y * 3) + x) * 4;
                var edge = x == 0 || x == 2 || y == 0 || y == 2;
                pixels[i + 0] = edge ? (byte)255 : (byte)0; // B
                pixels[i + 1] = edge ? (byte)0 : (byte)255; // G
                pixels[i + 2] = 0; // R
                pixels[i + 3] = 255;
            }
        }

        var bitmap = BitmapSource.Create(3, 3, 96, 96, PixelFormats.Bgra32, null, pixels, 12);
        SaveBmp(bitmap, path);
    }

    private static void CreateTransparentFringeLampBmp(string path)
    {
        var pixels = new byte[]
        {
            0, 0, 255, 0,
            255, 0, 0, 255,
            0, 255, 0, 255,
            255, 255, 255, 255
        };

        WriteBgra32Bmp(path, width: 2, height: 2, pixels: pixels);
    }

    private static void CreateMaskBmp(string path, byte[] alphas)
    {
        var pixels = new byte[2 * 2 * 4];
        for (var i = 0; i < 4; i++)
        {
            pixels[(i * 4) + 0] = alphas[i];
            pixels[(i * 4) + 1] = 0;
            pixels[(i * 4) + 2] = 0;
            pixels[(i * 4) + 3] = 255;
        }

        var bitmap = BitmapSource.Create(2, 2, 96, 96, PixelFormats.Bgra32, null, pixels, 8);
        SaveBmp(bitmap, path);
    }

    private static void SaveBmp(BitmapSource bitmap, string path)
    {
        var encoder = new BmpBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }

    private static void WriteIndexedBmp(string path, int width, int height, int bitsPerPixel, Color[] palette, byte[][] rows)
    {
        var rowStride = ((width * bitsPerPixel + 31) / 32) * 4;
        var pixelDataSize = rowStride * height;
        var paletteSize = palette.Length * 4;
        var pixelDataOffset = 14 + 40 + paletteSize;
        var fileSize = pixelDataOffset + pixelDataSize;

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
        writer.Write((short)1);
        writer.Write((short)bitsPerPixel);
        writer.Write(0);
        writer.Write(pixelDataSize);
        writer.Write(2835);
        writer.Write(2835);
        writer.Write(palette.Length);
        writer.Write(0);

        foreach (var color in palette)
        {
            writer.Write(color.B);
            writer.Write(color.G);
            writer.Write(color.R);
            writer.Write(color.A);
        }

        var row = new byte[rowStride];
        for (var y = 0; y < height; y++)
        {
            Array.Clear(row);

            if (bitsPerPixel == 8)
            {
                for (var x = 0; x < width; x++)
                {
                    row[x] = rows[y][x];
                }
            }
            else
            {
                for (var x = 0; x < width; x++)
                {
                    var shift = x % 2 == 0 ? 4 : 0;
                    row[x / 2] |= (byte)(rows[y][x] << shift);
                }
            }

            writer.Write(row);
        }
    }

    private static void WriteBgra32Bmp(string path, int width, int height, byte[] pixels)
    {
        var rowStride = width * 4;
        var pixelDataSize = rowStride * height;
        var pixelDataOffset = 14 + 108;
        var fileSize = pixelDataOffset + pixelDataSize;

        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);

        writer.Write((byte)'B');
        writer.Write((byte)'M');
        writer.Write(fileSize);
        writer.Write(0);
        writer.Write(pixelDataOffset);

        writer.Write(108);
        writer.Write(width);
        writer.Write(-height);
        writer.Write((short)1);
        writer.Write((short)32);
        writer.Write(3);
        writer.Write(pixelDataSize);
        writer.Write(2835);
        writer.Write(2835);
        writer.Write(0);
        writer.Write(0);
        writer.Write(0x00FF0000);
        writer.Write(0x0000FF00);
        writer.Write(0x000000FF);
        writer.Write(unchecked((int)0xFF000000));
        writer.Write(0x73524742);

        for (var i = 0; i < 12; i++)
        {
            writer.Write(0);
        }

        writer.Write(pixels);
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

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"oasis-mfme-lamp-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
