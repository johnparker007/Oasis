using SkiaSharp;

namespace OasisEditor;

/// <summary>WPF compatibility adapter for the shared Core perspective rasterizer.</summary>
internal static class PerspectiveRasterizer
{
    public static SKBitmap Rectify(SKBitmap source, IReadOnlyList<FacePointModel> sourceQuad, int width, int height,
        ImageProcessingExecutionOptions? executionOptions = null)
    {
        var options = executionOptions ?? ImageProcessingExecutionPolicy.Current;
        return FacePipeline.PerspectiveRasterizer.Rectify(source, sourceQuad, width, height,
            new FacePipeline.ImageProcessingExecutionOptions(options.MaxDegreeOfParallelism, options.CancellationToken));
    }

    public static SKBitmap RectifyPreview(SKBitmap source, IReadOnlyList<FacePointModel> sourceQuad, int width,
        int height, CancellationToken cancellationToken = default)
        => FacePipeline.PerspectiveRasterizer.RectifyPreview(source, sourceQuad, width, height, cancellationToken);

    internal static SKColor SampleBicubic(SKBitmap source, double x, double y)
        => FacePipeline.PerspectiveRasterizer.SampleBicubic(source, x, y);
}
