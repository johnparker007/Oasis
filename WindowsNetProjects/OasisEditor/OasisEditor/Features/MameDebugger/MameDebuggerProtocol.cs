using System.Text.Json;
using System.Text.Json.Serialization;

namespace OasisEditor.Features.MameDebugger;

public static class MameDebuggerProtocol
{
    public const string ResponsePrefix = "@OASIS_DEBUG";
    public const string EventPrefix = "@OASIS_DEBUG_EVENT";
    public const string CommandName = "debug";

    public static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }

    public static string CreateCommandLine(long id, string op, object? parameters = null)
    {
        var request = new Dictionary<string, object?>
        {
            ["id"] = id,
            ["op"] = op
        };

        if (parameters is not null)
        {
            request["params"] = parameters;
        }

        return $"{CommandName} {JsonSerializer.Serialize(request, JsonOptions)}";
    }
}

public enum MameDebuggerExecutionState
{
    Unknown,
    Running,
    Stopped
}

public sealed record MameDebuggerCpuInfo(string Tag, string Name, bool IsCurrent);

public sealed record MameDebuggerStatus(
    bool IsAvailable,
    MameDebuggerExecutionState ExecutionState,
    string? CurrentCpu,
    long? ProgramCounter,
    string? Message);

public sealed record MameDebuggerEvent(
    string Event,
    MameDebuggerExecutionState ExecutionState,
    string? Cpu,
    long? ProgramCounter,
    JsonElement Payload);
