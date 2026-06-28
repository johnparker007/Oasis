using System.IO;
using SkiaSharp;

namespace OasisEditor;

internal readonly record struct FaceSourceShapeOutputSize(int Width, int Height);

internal static class FaceSourceShapeTransformService
{
    public static FaceSourceShapeOutputSize EstimateOutputSize(PanelFaceSourceShapeModel shape, double? targetAspectRatio = null)
    {
        var sourceWidth = Math.Max(Distance(shape.TopLeft, shape.TopRight), Distance(shape.BottomLeft, shape.BottomRight));
        var sourceHeight = Math.Max(Distance(shape.TopLeft, shape.BottomLeft), Distance(shape.TopRight, shape.BottomRight));
        if (targetAspectRatio is > 0d and < double.PositiveInfinity)
        {
            var widthFromHeight = sourceHeight * targetAspectRatio.Value;
            var heightFromWidth = sourceWidth / targetAspectRatio.Value;
            if (widthFromHeight >= sourceWidth) sourceWidth = widthFromHeight;
            else sourceHeight = heightFromWidth;
        }
        return new FaceSourceShapeOutputSize(Math.Max(1, (int)Math.Ceiling(sourceWidth)), Math.Max(1, (int)Math.Ceiling(sourceHeight)));
    }

    public static string? TryGenerateBackground(Panel2DDocumentModel panel, PanelFaceSourceShapeModel shape, int width, int height, string? projectDirectory, string? generatedDirectory)
    {
        var background = panel.Elements.FirstOrDefault(e => e.Kind == PanelElementKind.Background && !string.IsNullOrWhiteSpace(e.AssetPath));
        if (background is null || string.IsNullOrWhiteSpace(projectDirectory)) return null;
        var sourcePath = Path.IsPathRooted(background.AssetPath!) ? background.AssetPath! : Path.Combine(projectDirectory, background.AssetPath!);
        if (!File.Exists(sourcePath)) return null;
        using var source = SKBitmap.Decode(sourcePath);
        if (source is null) return null;
        using var output = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            var u = width <= 1 ? 0d : x / (double)(width - 1);
            var v = height <= 1 ? 0d : y / (double)(height - 1);
            var px = Bilinear(shape.TopLeft.X, shape.TopRight.X, shape.BottomRight.X, shape.BottomLeft.X, u, v) - background.X;
            var py = Bilinear(shape.TopLeft.Y, shape.TopRight.Y, shape.BottomRight.Y, shape.BottomLeft.Y, u, v) - background.Y;
            output.SetPixel(x, y, SampleBicubic(source, px / Math.Max(1d, background.Width) * source.Width, py / Math.Max(1d, background.Height) * source.Height));
        }
        var generatedRoot = string.IsNullOrWhiteSpace(generatedDirectory) ? Path.Combine(projectDirectory, "Generated") : generatedDirectory;
        var relative = Path.Combine("Generated", "Faces", $"face-source-shape-{Guid.NewGuid():N}.png");
        var path = Path.Combine(projectDirectory, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var image = SKImage.FromBitmap(output);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.Create(path);
        data.SaveTo(stream);
        return relative.Replace(Path.DirectorySeparatorChar, '/');
    }

    private static double Distance(FacePointModel a, FacePointModel b)
    {
        var dx = a.X - b.X; var dy = a.Y - b.Y; return Math.Sqrt(dx * dx + dy * dy);
    }

    private static double Bilinear(double tl, double tr, double br, double bl, double u, double v) =>
        tl * (1 - u) * (1 - v) + tr * u * (1 - v) + br * u * v + bl * (1 - u) * v;

    private static SKColor SampleBicubic(SKBitmap bitmap, double x, double y)
    {
        var ix = Math.Clamp((int)Math.Round(x), 0, bitmap.Width - 1);
        var iy = Math.Clamp((int)Math.Round(y), 0, bitmap.Height - 1);
        return bitmap.GetPixel(ix, iy);
    }
}
