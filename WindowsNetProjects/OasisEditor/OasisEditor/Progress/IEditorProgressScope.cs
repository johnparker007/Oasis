namespace OasisEditor.Progress;

public interface IEditorProgressScope : IAsyncDisposable
{
    EditorProgressRequest Request { get; }
    IEditorProgressReporter Reporter { get; }
    CancellationToken CancellationToken { get; }
    Task SetErrorAsync(string message);
}
