namespace OasisEditor.Features.MameDebugger;

public sealed class MameDebuggerStdoutParser
{
    public bool TryParse(string line, out MameDebuggerStdoutMessage message)
    {
        message = default!;
        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        if (TryReadPayload(line, MameDebuggerProtocol.EventPrefix, out var eventPayload))
        {
            message = MameDebuggerStdoutMessage.Event(eventPayload);
            return true;
        }

        if (TryReadPayload(line, MameDebuggerProtocol.ResponsePrefix, out var responsePayload))
        {
            message = MameDebuggerStdoutMessage.Response(responsePayload);
            return true;
        }

        return false;
    }

    private static bool TryReadPayload(string line, string prefix, out string payload)
    {
        payload = string.Empty;
        if (!line.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        payload = line[prefix.Length..].TrimStart();
        return payload.Length > 0;
    }
}

public readonly record struct MameDebuggerStdoutMessage(MameDebuggerStdoutMessageKind Kind, string Payload)
{
    public static MameDebuggerStdoutMessage Response(string payload) => new(MameDebuggerStdoutMessageKind.Response, payload);
    public static MameDebuggerStdoutMessage Event(string payload) => new(MameDebuggerStdoutMessageKind.Event, payload);
}

public enum MameDebuggerStdoutMessageKind
{
    Response,
    Event
}
