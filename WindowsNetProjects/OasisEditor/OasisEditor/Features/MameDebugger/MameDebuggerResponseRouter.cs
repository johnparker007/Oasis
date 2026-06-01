using System.Collections.Concurrent;

namespace OasisEditor.Features.MameDebugger;

public sealed class MameDebuggerResponseRouter
{
    private readonly ConcurrentDictionary<long, TaskCompletionSource<MameDebuggerResponse>> _pendingResponses = new();

    public Task<MameDebuggerResponse> RegisterAsync(long requestId, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var completion = new TaskCompletionSource<MameDebuggerResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_pendingResponses.TryAdd(requestId, completion))
        {
            throw new InvalidOperationException($"Debugger request id {requestId} is already pending.");
        }

        var cancellationRegistration = cancellationToken.Register(() =>
        {
            if (_pendingResponses.TryRemove(requestId, out var pending))
            {
                pending.TrySetCanceled(cancellationToken);
            }
        });

        _ = CompleteWithTimeoutAsync(requestId, timeout, completion, cancellationRegistration);
        return completion.Task;
    }

    public bool TryRoute(MameDebuggerResponse response)
    {
        if (!_pendingResponses.TryRemove(response.Id, out var completion))
        {
            return false;
        }

        completion.TrySetResult(response);
        return true;
    }

    public void FailAll(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        foreach (var pair in _pendingResponses.ToArray())
        {
            if (_pendingResponses.TryRemove(pair.Key, out var completion))
            {
                completion.TrySetException(exception);
            }
        }
    }

    private async Task CompleteWithTimeoutAsync(
        long requestId,
        TimeSpan timeout,
        TaskCompletionSource<MameDebuggerResponse> completion,
        CancellationTokenRegistration cancellationRegistration)
    {
        try
        {
            await Task.Delay(timeout).ConfigureAwait(false);
            if (_pendingResponses.TryRemove(requestId, out _))
            {
                completion.TrySetException(new TimeoutException($"Timed out waiting for MAME debugger response id {requestId} after {timeout.TotalSeconds:0.#} seconds."));
            }
        }
        finally
        {
            await cancellationRegistration.DisposeAsync().ConfigureAwait(false);
        }
    }
}
