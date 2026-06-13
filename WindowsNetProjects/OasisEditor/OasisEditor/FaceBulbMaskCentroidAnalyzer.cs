using System.IO;
using SkiaSharp;

namespace OasisEditor;

internal sealed record FaceBulbMaskCentroidResult(double NormalizedX, double NormalizedY, double NormalizedRadius, int MeaningfulPixelCount, string? Diagnostic);

internal static class FaceBulbMaskCentroidAnalyzer
{
    private const double MinimumWeight = 0.0001d;
    private const double MeaningfulWeightThreshold = 8d;

    public static FaceBulbMaskCentroidResult? Analyze(SKBitmap bitmap)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        if (bitmap.Width <= 0 || bitmap.Height <= 0)
        {
            return null;
        }

        double totalWeight = 0d;
        double weightedX = 0d;
        double weightedY = 0d;
        var minX = bitmap.Width;
        var minY = bitmap.Height;
        var maxX = -1;
        var maxY = -1;
        var meaningfulPixels = 0;

        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                var pixel = bitmap.GetPixel(x, y);
                var luminance = (0.2126d * pixel.Red) + (0.7152d * pixel.Green) + (0.0722d * pixel.Blue);
                var weight = (pixel.Alpha / 255d) * luminance;
                if (weight <= MinimumWeight)
                {
                    continue;
                }

                totalWeight += weight;
                weightedX += (x + 0.5d) * weight;
                weightedY += (y + 0.5d) * weight;

                if (weight >= MeaningfulWeightThreshold)
                {
                    meaningfulPixels++;
                    minX = Math.Min(minX, x);
                    minY = Math.Min(minY, y);
                    maxX = Math.Max(maxX, x);
                    maxY = Math.Max(maxY, y);
                }
            }
        }

        if (totalWeight <= MinimumWeight || !IsFinite(totalWeight))
        {
            return null;
        }

        var centerX = weightedX / totalWeight;
        var centerY = weightedY / totalWeight;
        if (!IsFinite(centerX) || !IsFinite(centerY))
        {
            return null;
        }

        double radius;
        if (meaningfulPixels > 0 && maxX >= minX && maxY >= minY)
        {
            radius = Math.Max((maxX - minX) + 1d, (maxY - minY) + 1d) / 2d;
        }
        else
        {
            meaningfulPixels = Math.Max(1, (int)Math.Round(totalWeight / 255d));
            radius = Math.Sqrt(meaningfulPixels / Math.PI);
        }

        var normalizedRadius = radius / Math.Max(bitmap.Width, bitmap.Height);
        return new FaceBulbMaskCentroidResult(
            Math.Clamp(centerX / bitmap.Width, 0d, 1d),
            Math.Clamp(centerY / bitmap.Height, 0d, 1d),
            Math.Clamp(normalizedRadius, 0d, 1d),
            meaningfulPixels,
            null);
    }

    public static FaceBulbMaskCentroidResult? AnalyzeFile(string? assetPath, string? projectDirectory, out string? diagnostic)
    {
        diagnostic = null;
        if (!TryResolveAssetPath(assetPath, projectDirectory, out var resolvedPath))
        {
            diagnostic = "bulb-mask-missing";
            return null;
        }

        if (!File.Exists(resolvedPath))
        {
            diagnostic = "bulb-mask-file-missing";
            return null;
        }

        using var codec = SKCodec.Create(resolvedPath);
        if (codec is null)
        {
            diagnostic = "bulb-mask-invalid";
            return null;
        }

        using var bitmap = SKBitmap.Decode(codec);
        if (bitmap is null)
        {
            diagnostic = "bulb-mask-invalid";
            return null;
        }

        var result = Analyze(bitmap);
        diagnostic = result is null ? "bulb-mask-empty" : null;
        return result;
    }

    private static bool TryResolveAssetPath(string? assetPath, string? projectDirectory, out string resolvedPath)
    {
        resolvedPath = string.Empty;
        if (string.IsNullOrWhiteSpace(assetPath))
        {
            return false;
        }

        var candidate = assetPath.Trim();
        if (Path.IsPathRooted(candidate))
        {
            resolvedPath = candidate;
            return true;
        }

        if (string.IsNullOrWhiteSpace(projectDirectory))
        {
            return false;
        }

        resolvedPath = Path.GetFullPath(Path.Combine(projectDirectory, candidate.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar)));
        return true;
    }

    private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
}
