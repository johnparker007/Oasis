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


    public static bool TryTransformPanelPointToFace(PanelFaceSourceShapeModel shape, int width, int height, double panelX, double panelY, out FacePointModel facePoint)
    {
        facePoint = new FacePointModel();
        if (!TryCreatePanelToFaceHomography(shape, width, height, out var h))
        {
            return false;
        }

        var denominator = (h[6] * panelX) + (h[7] * panelY) + h[8];
        if (!IsFinite(denominator) || Math.Abs(denominator) < 1e-9)
        {
            return false;
        }

        var x = ((h[0] * panelX) + (h[1] * panelY) + h[2]) / denominator;
        var y = ((h[3] * panelX) + (h[4] * panelY) + h[5]) / denominator;
        if (!IsFinite(x) || !IsFinite(y))
        {
            return false;
        }

        facePoint = new FacePointModel { X = x, Y = y };
        return true;
    }

    public static bool ContainsPanelPoint(PanelFaceSourceShapeModel shape, double panelX, double panelY)
    {
        var points = new[] { shape.TopLeft, shape.TopRight, shape.BottomRight, shape.BottomLeft };
        var inside = false;
        for (var i = 0, j = points.Length - 1; i < points.Length; j = i++)
        {
            var pi = points[i];
            var pj = points[j];
            if (((pi.Y > panelY) != (pj.Y > panelY))
                && panelX < ((pj.X - pi.X) * (panelY - pi.Y) / (pj.Y - pi.Y)) + pi.X)
            {
                inside = !inside;
            }
        }

        return inside;
    }

    private static bool TryCreatePanelToFaceHomography(PanelFaceSourceShapeModel shape, int width, int height, out double[] h)
    {
        h = new double[9];
        var source = new[] { shape.TopLeft, shape.TopRight, shape.BottomRight, shape.BottomLeft };
        var destination = new[]
        {
            new FacePointModel { X = 0, Y = 0 },
            new FacePointModel { X = Math.Max(0, width), Y = 0 },
            new FacePointModel { X = Math.Max(0, width), Y = Math.Max(0, height) },
            new FacePointModel { X = 0, Y = Math.Max(0, height) }
        };

        var a = new double[8, 9];
        for (var i = 0; i < 4; i++)
        {
            var x = source[i].X;
            var y = source[i].Y;
            var u = destination[i].X;
            var v = destination[i].Y;
            var row = i * 2;
            a[row, 0] = x; a[row, 1] = y; a[row, 2] = 1; a[row, 6] = -u * x; a[row, 7] = -u * y; a[row, 8] = u;
            a[row + 1, 3] = x; a[row + 1, 4] = y; a[row + 1, 5] = 1; a[row + 1, 6] = -v * x; a[row + 1, 7] = -v * y; a[row + 1, 8] = v;
        }

        for (var col = 0; col < 8; col++)
        {
            var pivot = col;
            for (var row = col + 1; row < 8; row++)
            {
                if (Math.Abs(a[row, col]) > Math.Abs(a[pivot, col])) pivot = row;
            }

            if (Math.Abs(a[pivot, col]) < 1e-9) return false;
            if (pivot != col)
            {
                for (var k = col; k < 9; k++) (a[col, k], a[pivot, k]) = (a[pivot, k], a[col, k]);
            }

            var divisor = a[col, col];
            for (var k = col; k < 9; k++) a[col, k] /= divisor;
            for (var row = 0; row < 8; row++)
            {
                if (row == col) continue;
                var factor = a[row, col];
                for (var k = col; k < 9; k++) a[row, k] -= factor * a[col, k];
            }
        }

        for (var i = 0; i < 8; i++) h[i] = a[i, 8];
        h[8] = 1;
        return h.All(IsFinite);
    }

    private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

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
