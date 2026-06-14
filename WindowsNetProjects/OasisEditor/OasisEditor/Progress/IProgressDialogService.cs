namespace OasisEditor.Progress;

public interface IProgressDialogService
{
    bool IsOperationActive { get; }

    Task RunAsync(
        EditorProgressRequest request,
        Func<IEditorProgressReporter, CancellationToken, Task> operation,
        CancellationToken cancellationToken = default);

    Task<TResult> RunAsync<TResult>(
        EditorProgressRequest request,
        Func<IEditorProgressReporter, CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default);
}
