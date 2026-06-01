using System.Text.Json;
using System.Text.Json.Serialization;

namespace OasisEditor.Features.MameDebugger;

public static class MameDebuggerProtocol
{
    public const string ResponsePrefix = "@OASIS_DEBUG";
    public const string EventPrefix = "@OASIS_DEBUG_EVENT";
    public const string CommandName = "debug";

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    static MameDebuggerProtocol()
    {
        JsonOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
    }

    public static string CreateCommand(long requestId, string operation, string? cpu = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);
        var request = new MameDebuggerRequest(requestId, operation, cpu);
        return $"{CommandName} {JsonSerializer.Serialize(request, JsonOptions)}";
    }

    public static MameDebuggerResponse ParseResponse(string json)
    {
        return JsonSerializer.Deserialize<MameDebuggerResponse>(json, JsonOptions)
            ?? throw new JsonException("Debugger response payload was empty.");
    }

    public static MameDebuggerEvent ParseEvent(string json)
    {
        return JsonSerializer.Deserialize<MameDebuggerEvent>(json, JsonOptions)
            ?? throw new JsonException("Debugger event payload was empty.");
    }
}

public sealed record MameDebuggerRequest(long Id, string Op, string? Cpu = null);

public sealed record MameDebuggerResponse(
    long Id,
    bool Ok,
    JsonElement? Result = null,
    MameDebuggerError? Error = null);

public sealed record MameDebuggerError(string Code, string Message);

public sealed record MameDebuggerEvent(
    string Event,
    string? State = null,
    string? Cpu = null,
    long? Pc = null);

public sealed record MameDebuggerStatus(
    bool Available,
    MameDebuggerExecutionState State,
    string? Cpu,
    long? Pc);

public sealed record MameDebuggerCpu(string Tag, string Name, bool IsCurrent);
