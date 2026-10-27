using SkiaSharp;

namespace OasisEditor;

/// <summary>WPF compatibility adapter over the portable Face artwork processing implementation.</summary>
internal sealed class FaceArtworkProcessingPipeline
{
    public SKBitmap Evaluate(SKBitmap input, ImageProcessingPipelineModel pipeline, int? operationCount = null,
        ImageProcessingExecutionOptions? executionOptions = null)
    {
        var options = executionOptions ?? ImageProcessingExecutionPolicy.Current;
        return new FacePipeline.FaceArtworkProcessingPipeline().Evaluate(input, pipeline, operationCount,
            new FacePipeline.ImageProcessingExecutionOptions(options.MaxDegreeOfParallelism, options.CancellationToken));
    }

    internal static string? MeasureSampleHex(SKBitmap image, CalibrationSampleModel sample)
        => FacePipeline.FaceArtworkProcessingPipeline.MeasureSampleHex(image, sample);

    internal static bool TryResolveReferenceColors(SKBitmap image, ArtworkCalibrationOperationModel operation,
        out string? black, out string? white)
        => FacePipeline.FaceArtworkProcessingPipeline.TryResolveReferenceColors(image, operation, out black, out white);
}
