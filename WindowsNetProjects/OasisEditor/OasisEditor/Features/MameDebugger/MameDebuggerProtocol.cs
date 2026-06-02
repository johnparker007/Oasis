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

    public static string CreateCommand<TPayload>(long requestId, string operation, TPayload payload)
        where TPayload : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);
        ArgumentNullException.ThrowIfNull(payload);
        var request = new MameDebuggerRequest<TPayload>(requestId, operation, payload);
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

public sealed record MameDebuggerRequest<TPayload>(long Id, string Op, TPayload Payload)
    where TPayload : class;

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
    long? Pc = null,
    long? MameId = null,
    long? Address = null,
    long? Data = null,
    int? Size = null);

public sealed record MameDebuggerStatus(
    bool Available,
    MameDebuggerExecutionState State,
    string? Cpu,
    long? Pc);

public sealed record MameDebuggerPing(bool Pong, bool Available);

public sealed record MameDebuggerCpu(string Tag, string Name, bool IsCurrent);

public sealed record MameDebuggerBreakpoint(
    long DebuggerId,
    long MameId,
    string Cpu,
    long Address,
    bool Enabled,
    string? Condition,
    string? Action,
    long? HitCount = null);

public sealed record MameDebuggerWatchpoint(
    long DebuggerId,
    long MameId,
    string Cpu,
    long Address,
    long Length,
    MameDebuggerWatchpointType Type,
    bool Enabled,
    string? Condition,
    string? Action,
    MameDebuggerWatchpointHit? LatestHit = null,
    string? AddressSpace = null);

public sealed record MameDebuggerBreakpointRequest(
    string? Cpu,
    long Address,
    string? Condition = null,
    string? Action = null,
    long? MameId = null,
    long? DebuggerId = null);

public sealed record MameDebuggerWatchpointRequest(
    string? Cpu,
    long Address,
    long Length,
    MameDebuggerWatchpointType Type,
    string? Condition = null,
    string? Action = null,
    string? AddressSpace = null,
    long? MameId = null,
    long? DebuggerId = null);

public sealed record MameDebuggerWatchpointHit(long Address, long Data, int Size);

public enum MameDebuggerWatchpointType
{
    Read,
    Write,
    ReadWrite
}
