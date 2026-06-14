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

    private async Task<TResult> RunDialogOperationAsync<TResult>(
        EditorProgressRequest request,
        Func<IEditorProgressReporter, CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken)
    {
        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var currentState = EditorProgressState.FromRequest(request);
        var viewModel = new EditorProgressDialogViewModel(currentState, linkedCancellation.Cancel);
        var reporter = new EditorProgressReporter(currentState, state =>
        {
            currentState = state;
            _dispatcher.BeginInvoke(() => viewModel.UpdateState(state), DispatcherPriority.Normal);
        });

        var owner = ResolveOwnerWindow();
        var allowClose = false;
        var dialog = new EditorProgressDialogWindow(viewModel);
        if (owner is not null)
        {
            // Keep the progress window owned so it stays above the shell, but do not disable the owner.
            // Current first-pass integrations still perform WPF-bound document mutations while progress is shown.
            dialog.Owner = owner;
        }

        dialog.Closing += (_, eventArgs) =>
        {
            if (!allowClose)
            {
                eventArgs.Cancel = true;
            }
        };

        try
        {
            dialog.Show();
            await _dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
            return await operation(reporter, linkedCancellation.Token).ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            viewModel.UpdateState(currentState.WithError(exception.Message));
            throw;
        }
        finally
        {
            allowClose = true;
            if (dialog.IsVisible)
            {
                dialog.Close();
            }

            owner?.Activate();
        }
    }

    private Window? ResolveOwnerWindow()
    {
        var owner = _ownerProvider();
        if (owner is null || !owner.IsVisible)
        {
            return null;
        }

        return owner;
    }
}
