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

        var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linked.CancelAfter(timeout);
        linked.Token.Register(() =>
        {
            if (_pendingResponses.TryRemove(requestId, out var pending))
            {
                pending.TrySetCanceled(linked.Token);
            }

            linked.Dispose();
        });

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
}
