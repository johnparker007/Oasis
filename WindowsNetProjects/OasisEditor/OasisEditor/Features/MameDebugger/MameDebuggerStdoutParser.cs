using System.Text.Json;

namespace OasisEditor.Features.MameDebugger;

public sealed class MameDebuggerStdoutParser
{
    public bool TryParse(string line, out MameDebuggerProtocolMessage message)
    {
        message = default;

        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        if (TryReadPayload(line, MameDebuggerProtocol.EventPrefix, out var eventPayload))
        {
            message = new MameDebuggerProtocolMessage(true, eventPayload);
            return true;
        }

        if (TryReadPayload(line, MameDebuggerProtocol.ResponsePrefix, out var responsePayload))
        {
            message = new MameDebuggerProtocolMessage(false, responsePayload);
            return true;
        }

        return false;
    }

    private static bool TryReadPayload(string line, string prefix, out JsonElement payload)
    {
        payload = default;

        if (!line.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var json = line[prefix.Length..].Trim();
        if (json.Length == 0)
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            payload = document.RootElement.Clone();
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}

public readonly record struct MameDebuggerProtocolMessage(bool IsEvent, JsonElement Payload);
