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

        await operation(NoOpEditorProgressReporter.Instance, cancellationToken).ConfigureAwait(false);
    }

    public async Task<TResult> RunAsync<TResult>(
        EditorProgressRequest request,
        Func<IEditorProgressReporter, CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(operation);

        return await operation(NoOpEditorProgressReporter.Instance, cancellationToken).ConfigureAwait(false);
    }
}
