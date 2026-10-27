using SkiaSharp;

namespace OasisEditor;

/// <summary>WPF compatibility adapter for the portable, linear-light Face sharpening service.</summary>
internal static class FaceArtworkSharpeningService
{
    public static SKBitmap Apply(SKBitmap source, FaceGenerationSettingsModel settings,
        ImageProcessingExecutionOptions? executionOptions = null)
    {
        var options = executionOptions ?? ImageProcessingExecutionPolicy.Current;
        return FacePipeline.FaceArtworkSharpeningService.Apply(source, settings,
            new FacePipeline.ImageProcessingExecutionOptions(options.MaxDegreeOfParallelism, options.CancellationToken));
    }
}
