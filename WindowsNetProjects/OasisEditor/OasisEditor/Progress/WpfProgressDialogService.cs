using System.Windows;
using System.Windows.Threading;

namespace OasisEditor.Progress;

public sealed class WpfProgressDialogService : IProgressDialogService
{
    private readonly Func<Window?> _ownerProvider;
    private readonly Dispatcher _dispatcher;
    private bool _isOperationActive;

    public WpfProgressDialogService(Func<Window?>? ownerProvider = null, Dispatcher? dispatcher = null)
    {
        _ownerProvider = ownerProvider ?? (() => Application.Current?.MainWindow);
        _dispatcher = dispatcher ?? Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
    }

    public bool IsOperationActive => _isOperationActive;

    public Task RunAsync(
        EditorProgressRequest request,
        Func<IEditorProgressReporter, CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        return RunAsync<object?>(
            request,
            async (progress, token) =>
            {
                await operation(progress, token).ConfigureAwait(false);
                return null;
            },
            cancellationToken);
    }

    public async Task<TResult> RunAsync<TResult>(
        EditorProgressRequest request,
        Func<IEditorProgressReporter, CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(operation);

        if (!_dispatcher.CheckAccess())
        {
            return await _dispatcher.InvokeAsync(() => RunAsync(request, operation, cancellationToken)).Task.Unwrap().ConfigureAwait(false);
        }

        if (_isOperationActive)
        {
            throw new InvalidOperationException("A modal editor progress operation is already active.");
        }

        _isOperationActive = true;
        try
        {
            return await RunDialogOperationAsync(request.Normalize(), operation, cancellationToken).ConfigureAwait(true);
        }
        finally
        {
            _isOperationActive = false;
        }
    }

    private Task<TResult> RunDialogOperationAsync<TResult>(
        EditorProgressRequest request,
        Func<IEditorProgressReporter, CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken)
    {
        var operationCompletion = new TaskCompletionSource<TResult>();
        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var currentState = EditorProgressState.FromRequest(request);
        EditorProgressDialogViewModel? viewModel = null;
        EditorProgressDialogWindow? dialog = null;
        var reporter = new EditorProgressReporter(currentState, state =>
        {
            currentState = state;
            if (viewModel is null)
            {
                return;
            }

            _dispatcher.BeginInvoke(() => viewModel.UpdateState(state), DispatcherPriority.Normal);
        });

        viewModel = new EditorProgressDialogViewModel(currentState, linkedCancellation.Cancel);
        dialog = new EditorProgressDialogWindow(viewModel)
        {
            Owner = _ownerProvider()
        };

        dialog.Closing += (_, eventArgs) =>
        {
            if (!operationCompletion.Task.IsCompleted)
            {
                eventArgs.Cancel = true;
            }
        };

        dialog.Loaded += async (_, _) =>
        {
            try
            {
                var result = await operation(reporter, linkedCancellation.Token).ConfigureAwait(true);
                operationCompletion.TrySetResult(result);
                dialog.Close();
            }
            catch (Exception exception)
            {
                viewModel.UpdateState(currentState.WithError(exception.Message));
                operationCompletion.TrySetException(exception);
                dialog.Close();
            }
        };

        dialog.ShowDialog();
        return operationCompletion.Task;
    }
}
