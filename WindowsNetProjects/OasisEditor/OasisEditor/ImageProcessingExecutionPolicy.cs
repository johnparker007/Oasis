namespace OasisEditor;

public readonly record struct ImageProcessingExecutionOptions(
    int MaxDegreeOfParallelism,
    CancellationToken CancellationToken = default)
{
    public ImageProcessingExecutionOptions WithCancellation(CancellationToken cancellationToken) =>
        this with { CancellationToken = cancellationToken };
}

internal static class ImageProcessingExecutionPolicy
{
    public static ImageProcessingExecutionOptions Current => FromPortable(FacePipeline.ImageProcessingExecutionPolicy.Current);

    public static void Configure(ProcessingPreferences preferences) => FacePipeline.ImageProcessingExecutionPolicy.Configure(preferences);

    public static ImageProcessingExecutionOptions Resolve(ProcessingPreferences preferences, int availableLogicalProcessors,
        CancellationToken cancellationToken = default)
    {
        var options = FacePipeline.ImageProcessingExecutionPolicy.Resolve(preferences, availableLogicalProcessors, cancellationToken);
        return FromPortable(options);
    }

    public static void ForEachRow(int rowCount, ImageProcessingExecutionOptions options, Action<int> processRow)
    {
        FacePipeline.ImageProcessingExecutionPolicy.ForEachRow(rowCount,
            new FacePipeline.ImageProcessingExecutionOptions(options.MaxDegreeOfParallelism, options.CancellationToken), processRow);
    }

    private static ImageProcessingExecutionOptions FromPortable(FacePipeline.ImageProcessingExecutionOptions options)
        => new(options.MaxDegreeOfParallelism, options.CancellationToken);
}
