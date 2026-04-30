using System.Windows.Media;
using System.Windows.Media.Imaging;
using OasisEditor.Features.MfmeImport;
using Xunit;

namespace OasisEditor.Tests;

public sealed class MfmeImportLampPostProcessorTests
{
    [Fact]
    public void CopyAssets_LampProcessing_TransparentPixelsRemainTransparent_AndMaskAppliesAlpha()
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
            var lampAsset = Assert.Single(result.Elements.Where(e => e.Kind == PanelElementKind.Lamp));
            Assert.EndsWith(".png", lampAsset.AssetPath, StringComparison.OrdinalIgnoreCase);
            Assert.NotNull(lampAsset.SecondaryAssetPath);

            var lampPath = Path.Combine(projectRoot, "Assets", lampAsset.AssetPath!["Assets/".Length..]);
            var pixels = ReadBgra32(lampPath, out _);

            var alphas = Enumerable.Range(0, pixels.Length / 4)
                .Select(i => pixels[(i * 4) + 3])
                .ToArray();

            Assert.Equal(2, alphas.Count(a => a == 0)); // transparent palette entries remain transparent
            Assert.Contains((byte)255, alphas); // at least one masked-on pixel remains opaque

            var imageAsset = Assert.Single(result.Elements.Where(e => e.Kind == PanelElementKind.Image));
            Assert.EndsWith("symbol.png", imageAsset.AssetPath, StringComparison.OrdinalIgnoreCase);
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
        var palette = new BitmapPalette([Color.FromArgb(0, 255, 0, 0), Color.FromArgb(255, 200, 100, 50)]);
        var pixels = new byte[] { 0, 1, 1, 0 };
        var bitmap = BitmapSource.Create(2, 2, 96, 96, PixelFormats.Indexed8, palette, pixels, 2);
        SaveBmp(bitmap, path);
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
