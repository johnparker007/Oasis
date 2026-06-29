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
            if (!TryTransformFacePointToPanel(shape, width, height, x, y, out var panelPoint))
            {
                output.SetPixel(x, y, SKColors.Transparent);
                continue;
            }

            var px = panelPoint.X - background.X;
            var py = panelPoint.Y - background.Y;
            output.SetPixel(x, y, SampleBicubic(source, px / Math.Max(1d, background.Width) * source.Width, py / Math.Max(1d, background.Height) * source.Height));
        }
        var relative = Path.Combine("Generated", "Faces", $"face-source-shape-{Guid.NewGuid():N}.png");
        var path = Path.Combine(projectDirectory, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var image = SKImage.FromBitmap(output);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.Create(path);
        data.SaveTo(stream);
        return relative.Replace(Path.DirectorySeparatorChar, '/');
    }

    public static string? TryGenerateTransformedElementAsset(
        PanelElementModel sourceElement,
        string? sourceAssetPath,
        PanelFaceSourceShapeModel shape,
        int faceWidth,
        int faceHeight,
        FaceSourceRegionModel faceBounds,
        string? projectDirectory,
        string fileNamePrefix)
    {
        if (string.IsNullOrWhiteSpace(sourceAssetPath) || string.IsNullOrWhiteSpace(projectDirectory) || !faceBounds.IsValid)
        {
            return null;
        }

        var sourcePath = Path.IsPathRooted(sourceAssetPath!) ? sourceAssetPath! : Path.Combine(projectDirectory, sourceAssetPath!);
        if (!File.Exists(sourcePath))
        {
            return null;
        }

        using var source = SKBitmap.Decode(sourcePath);
        if (source is null)
        {
            return null;
        }

        var outputWidth = Math.Max(1, (int)Math.Ceiling(faceBounds.Width));
        var outputHeight = Math.Max(1, (int)Math.Ceiling(faceBounds.Height));
        using var output = new SKBitmap(outputWidth, outputHeight, SKColorType.Rgba8888, SKAlphaType.Premul);
        output.Erase(SKColors.Transparent);

        for (var y = 0; y < outputHeight; y++)
        for (var x = 0; x < outputWidth; x++)
        {
            var faceX = faceBounds.X + x;
            var faceY = faceBounds.Y + y;
            if (!TryTransformFacePointToPanel(shape, faceWidth, faceHeight, faceX, faceY, out var panelPoint)
                || panelPoint.X < sourceElement.X
                || panelPoint.Y < sourceElement.Y
                || panelPoint.X > sourceElement.X + sourceElement.Width
                || panelPoint.Y > sourceElement.Y + sourceElement.Height)
            {
                continue;
            }

            var sourceX = (panelPoint.X - sourceElement.X) / Math.Max(1d, sourceElement.Width) * source.Width;
            var sourceY = (panelPoint.Y - sourceElement.Y) / Math.Max(1d, sourceElement.Height) * source.Height;
            output.SetPixel(x, y, SampleBicubic(source, sourceX, sourceY));
        }

        var safePrefix = string.IsNullOrWhiteSpace(fileNamePrefix) ? "face-source-shape-asset" : fileNamePrefix.Trim();
        var relative = Path.Combine("Generated", "Faces", $"{safePrefix}-{Guid.NewGuid():N}.png");
        var path = Path.Combine(projectDirectory, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var image = SKImage.FromBitmap(output);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.Create(path);
        data.SaveTo(stream);
        return relative.Replace(Path.DirectorySeparatorChar, '/');
    }

    public static bool TryTransformFacePointToPanel(PanelFaceSourceShapeModel shape, int width, int height, double faceX, double faceY, out FacePointModel panelPoint)
    {
        panelPoint = new FacePointModel();
        if (!TryCreateFaceToPanelHomography(shape, width, height, out var h))
        {
            return false;
        }

        return TryApplyHomography(h, faceX, faceY, out panelPoint);
    }


    public static bool TryTransformPanelPointToFace(PanelFaceSourceShapeModel shape, int width, int height, double panelX, double panelY, out FacePointModel facePoint)
    {
        facePoint = new FacePointModel();
        if (!TryCreatePanelToFaceHomography(shape, width, height, out var h))
        {
            return false;
        }

        return TryApplyHomography(h, panelX, panelY, out facePoint);
    }

    public static bool ContainsPanelPoint(PanelFaceSourceShapeModel shape, double panelX, double panelY)
    {
        var points = new[] { shape.TopLeft, shape.TopRight, shape.BottomRight, shape.BottomLeft };
        var inside = false;
        for (int i = 0, j = points.Length - 1; i < points.Length; j = i++)
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
        var source = new[] { shape.TopLeft, shape.TopRight, shape.BottomRight, shape.BottomLeft };
        var destination = CreateFaceCorners(width, height);
        return TryCreateHomography(source, destination, out h);
    }

    private static bool TryCreateFaceToPanelHomography(PanelFaceSourceShapeModel shape, int width, int height, out double[] h)
    {
        var source = CreateFaceCorners(width, height);
        var destination = new[] { shape.TopLeft, shape.TopRight, shape.BottomRight, shape.BottomLeft };
        return TryCreateHomography(source, destination, out h);
    }

    private static FacePointModel[] CreateFaceCorners(int width, int height) =>
    [
        new FacePointModel { X = 0, Y = 0 },
        new FacePointModel { X = Math.Max(0, width), Y = 0 },
        new FacePointModel { X = Math.Max(0, width), Y = Math.Max(0, height) },
        new FacePointModel { X = 0, Y = Math.Max(0, height) }
    ];

    private static bool TryCreateHomography(IReadOnlyList<FacePointModel> source, IReadOnlyList<FacePointModel> destination, out double[] h)
    {
        h = new double[9];
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

    private static bool TryApplyHomography(double[] h, double sourceX, double sourceY, out FacePointModel point)
    {
        point = new FacePointModel();
        var denominator = (h[6] * sourceX) + (h[7] * sourceY) + h[8];
        if (!IsFinite(denominator) || Math.Abs(denominator) < 1e-9)
        {
            return false;
        }

        var x = ((h[0] * sourceX) + (h[1] * sourceY) + h[2]) / denominator;
        var y = ((h[3] * sourceX) + (h[4] * sourceY) + h[5]) / denominator;
        if (!IsFinite(x) || !IsFinite(y))
        {
            return false;
        }

        point = new FacePointModel { X = x, Y = y };
        return true;
    }

    private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

    private static double Distance(FacePointModel a, FacePointModel b)
    {
        var dx = a.X - b.X; var dy = a.Y - b.Y; return Math.Sqrt(dx * dx + dy * dy);
    }

    private static SKColor SampleBicubic(SKBitmap bitmap, double x, double y)
    {
        var ix = Math.Clamp((int)Math.Round(x), 0, bitmap.Width - 1);
        var iy = Math.Clamp((int)Math.Round(y), 0, bitmap.Height - 1);
        return bitmap.GetPixel(ix, iy);
    }
}
