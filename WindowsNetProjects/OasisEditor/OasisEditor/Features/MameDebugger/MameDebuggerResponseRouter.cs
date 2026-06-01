using System.Collections.Concurrent;
using System.Text.Json;

namespace OasisEditor.Features.MameDebugger;

public sealed class MameDebuggerResponseRouter
{
    private readonly ConcurrentDictionary<long, TaskCompletionSource<JsonElement>> _pendingRequests = new();

    public Task<JsonElement> Register(long requestId, CancellationToken cancellationToken)
    {
        var completion = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_pendingRequests.TryAdd(requestId, completion))
        {
            throw new InvalidOperationException($"A debugger request with id {requestId} is already pending.");
        }

        if (cancellationToken.CanBeCanceled)
        {
            cancellationToken.Register(static state =>
            {
                var context = ((MameDebuggerResponseRouter Router, long RequestId, CancellationToken Token))state!;
                if (context.Router._pendingRequests.TryRemove(context.RequestId, out var removed))
                {
                    removed.TrySetCanceled(context.Token);
                }
            }, (this, requestId, cancellationToken));
        }

        return completion.Task;
    }

    public bool TryRouteResponse(JsonElement response)
    {
        if (!response.TryGetProperty("id", out var idElement) || !idElement.TryGetInt64(out var requestId))
        {
            return false;
        }

        if (!_pendingRequests.TryRemove(requestId, out var completion))
        {
            return false;
        }

        if (response.TryGetProperty("ok", out var okElement) && okElement.ValueKind == JsonValueKind.False)
        {
            var error = response.TryGetProperty("error", out var errorElement)
                ? errorElement.ToString()
                : "MAME debugger request failed.";
            completion.TrySetException(new InvalidOperationException(error));
            return true;
        }

        completion.TrySetResult(response.Clone());
        return true;
    }
}
