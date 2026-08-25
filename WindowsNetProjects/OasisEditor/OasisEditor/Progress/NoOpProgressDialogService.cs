namespace OasisEditor.Progress;

public sealed class NoOpProgressDialogService : IProgressDialogService
{
    public static NoOpProgressDialogService Instance { get; } = new();

    private NoOpProgressDialogService()
    {
    }

    public bool IsOperationActive => false;

    public async Task RunAsync(
        EditorProgressRequest request,
        Func<IEditorProgressReporter, CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(operation);

        var normalized = request.Normalize();
        if (normalized.ExecutionMode == EditorProgressExecutionMode.Background)
        {
            await Task.Run(() => operation(NoOpEditorProgressReporter.Instance, cancellationToken), cancellationToken).ConfigureAwait(false);
            return;
        }

        await operation(NoOpEditorProgressReporter.Instance, cancellationToken).ConfigureAwait(false);
    }

    public async Task<TResult> RunAsync<TResult>(
        EditorProgressRequest request,
        Func<IEditorProgressReporter, CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(operation);

        var normalized = request.Normalize();
        return normalized.ExecutionMode == EditorProgressExecutionMode.Background
            ? await Task.Run(() => operation(NoOpEditorProgressReporter.Instance, cancellationToken), cancellationToken).ConfigureAwait(false)
            : await operation(NoOpEditorProgressReporter.Instance, cancellationToken).ConfigureAwait(false);
    }
}
