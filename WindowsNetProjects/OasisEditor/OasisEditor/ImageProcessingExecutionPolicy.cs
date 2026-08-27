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
    private static ImageProcessingExecutionOptions _current = Resolve(new ProcessingPreferences(), Environment.ProcessorCount);

    public static ImageProcessingExecutionOptions Current => _current;

    public static void Configure(ProcessingPreferences preferences) =>
        _current = Resolve(preferences, Environment.ProcessorCount);

    public static ImageProcessingExecutionOptions Resolve(ProcessingPreferences preferences, int availableLogicalProcessors,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        var available = Math.Max(1, availableLogicalProcessors);
        var workers = preferences.CpuMode switch
        {
            CpuImageProcessingMode.Maximum => available,
            CpuImageProcessingMode.Custom => Math.Clamp(preferences.CustomMaximumWorkers, 1, available),
            _ => available switch
            {
                1 or 2 => 1,
                3 or 4 => available - 1,
                _ => available - 2
            }
        };
        return new ImageProcessingExecutionOptions(Math.Clamp(workers, 1, available), cancellationToken);
    }

    public static void ForEachRow(int rowCount, ImageProcessingExecutionOptions options, Action<int> processRow)
    {
        if (options.MaxDegreeOfParallelism <= 1)
        {
            for (var y = 0; y < rowCount; y++)
            {
                options.CancellationToken.ThrowIfCancellationRequested();
                processRow(y);
            }
            return;
        }
        Parallel.For(0, rowCount, new ParallelOptions
        {
            MaxDegreeOfParallelism = options.MaxDegreeOfParallelism,
            CancellationToken = options.CancellationToken
        }, processRow);
    }
}
