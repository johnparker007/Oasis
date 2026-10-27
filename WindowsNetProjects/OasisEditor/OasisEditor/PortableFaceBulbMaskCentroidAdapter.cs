using SkiaSharp;

namespace OasisEditor;

internal sealed record FaceBulbMaskCentroidResult(double NormalizedX, double NormalizedY, double NormalizedRadius,
    int MeaningfulPixelCount, string? Diagnostic);

/// <summary>WPF adapter for the shared Core bulb-mask centroid analyzer.</summary>
internal static class FaceBulbMaskCentroidAnalyzer
{
    public static FaceBulbMaskCentroidResult? Analyze(SKBitmap bitmap)
        => Map(FacePipeline.FaceBulbMaskCentroidAnalyzer.Analyze(bitmap));

    public static FaceBulbMaskCentroidResult? AnalyzeFile(string? assetPath, string? projectDirectory, out string? diagnostic)
    {
        var result = FacePipeline.FaceBulbMaskCentroidAnalyzer.AnalyzeFile(assetPath, projectDirectory, out diagnostic);
        return Map(result);
    }

    private static FaceBulbMaskCentroidResult? Map(FacePipeline.FaceBulbMaskCentroidResult? result)
        => result is null ? null : new(result.NormalizedX, result.NormalizedY, result.NormalizedRadius,
            result.MeaningfulPixelCount, result.Diagnostic);
}
