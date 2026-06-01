using System.Threading;
using System.Text.Json;

namespace OasisEditor.Features.MameDebugger;

public interface IMameDebuggerTransport
{
    Task SendAsync(string line, CancellationToken cancellationToken);
}

public sealed class MameDebuggerStdioTransport : IMameDebuggerTransport
{
    private readonly IMameProcessRunner _processRunner;

    public MameDebuggerStdioTransport(IMameProcessRunner processRunner)
    {
        _processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
    }

    public Task SendAsync(string line, CancellationToken cancellationToken)
    {
        return _processRunner.WriteStandardInputAsync(line, cancellationToken);
    }
}

public sealed class MameDebuggerService
{
    private readonly IMameDebuggerTransport _transport;
    private readonly MameDebuggerResponseRouter _responseRouter;
    private readonly MameDebuggerStdoutParser _stdoutParser;
    private readonly MameDebuggerState _state;
    private readonly Func<bool> _debuggerSupportActiveProvider;
    private readonly Action<string>? _diagnosticLogger;
    private long _nextRequestId;

    public MameDebuggerService(
        IMameDebuggerTransport transport,
        MameDebuggerResponseRouter responseRouter,
        MameDebuggerStdoutParser stdoutParser,
        MameDebuggerState state,
        Func<bool> debuggerSupportActiveProvider,
        Action<string>? diagnosticLogger = null)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _responseRouter = responseRouter ?? throw new ArgumentNullException(nameof(responseRouter));
        _stdoutParser = stdoutParser ?? throw new ArgumentNullException(nameof(stdoutParser));
        _state = state ?? throw new ArgumentNullException(nameof(state));
        _debuggerSupportActiveProvider = debuggerSupportActiveProvider ?? throw new ArgumentNullException(nameof(debuggerSupportActiveProvider));
        _diagnosticLogger = diagnosticLogger;
    }

    public event EventHandler<MameDebuggerEvent>? DebuggerEventReceived;

    public bool IsDebuggerSupportActive => _debuggerSupportActiveProvider();

    public Task RunAsync() => RunAsync(CancellationToken.None);

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var status = await SendRequestAsync<MameDebuggerStatus>("run", cancellationToken: cancellationToken).ConfigureAwait(false);
        _state.ApplyStatus(status);
    }

    public Task BreakAsync() => BreakAsync(CancellationToken.None);

    public async Task BreakAsync(CancellationToken cancellationToken)
    {
        var status = await SendRequestAsync<MameDebuggerStatus>("break", cancellationToken: cancellationToken).ConfigureAwait(false);
        _state.ApplyStatus(status);
    }

    public Task StepAsync() => StepAsync(null, CancellationToken.None);

    public async Task StepAsync(string? cpu = null, CancellationToken cancellationToken = default)
    {
        var parameters = string.IsNullOrWhiteSpace(cpu) ? null : new { cpu };
        var status = await SendRequestAsync<MameDebuggerStatus>("step", parameters, cancellationToken).ConfigureAwait(false);
        _state.ApplyStatus(status);
    }

    public Task<MameDebuggerStatus> GetStatusAsync() => GetStatusAsync(CancellationToken.None);

    public async Task<MameDebuggerStatus> GetStatusAsync(CancellationToken cancellationToken)
    {
        if (!IsDebuggerSupportActive)
        {
            return new MameDebuggerStatus(false, MameDebuggerExecutionState.Unknown, null, null, "Current MAME process was not launched with -debug.");
        }

        var status = await SendRequestAsync<MameDebuggerStatus>("status", cancellationToken: cancellationToken).ConfigureAwait(false);
        _state.ApplyStatus(status);
        return status;
    }

    public Task<IReadOnlyList<MameDebuggerCpuInfo>> GetCpuListAsync() => GetCpuListAsync(CancellationToken.None);

    public async Task<IReadOnlyList<MameDebuggerCpuInfo>> GetCpuListAsync(CancellationToken cancellationToken)
    {
        var cpus = await SendRequestAsync<MameDebuggerCpuInfo[]>("cpus", cancellationToken: cancellationToken).ConfigureAwait(false);
        return cpus;
    }

    public async Task<bool> PingAsync(CancellationToken cancellationToken)
    {
        var response = await SendRequestAsync<PingResponse>("ping", cancellationToken: cancellationToken).ConfigureAwait(false);
        return string.Equals(response.Pong, "pong", StringComparison.OrdinalIgnoreCase);
    }

    public bool ProcessStdoutLine(string line)
    {
        if (!_stdoutParser.TryParse(line, out var message))
        {
            return false;
        }

        _diagnosticLogger?.Invoke($"[MAME-DEBUGGER-IN] {line}");

        if (message.IsEvent)
        {
            var debuggerEvent = CreateEvent(message.Payload);
            _state.ApplyEvent(debuggerEvent);
            DebuggerEventReceived?.Invoke(this, debuggerEvent);
            return true;
        }

        _responseRouter.TryRouteResponse(message.Payload);
        return true;
    }

    private async Task<T> SendRequestAsync<T>(string op, object? parameters = null, CancellationToken cancellationToken = default)
    {
        EnsureDebuggerSupportActive(op);

        var requestId = Interlocked.Increment(ref _nextRequestId);
        var commandLine = MameDebuggerProtocol.CreateCommandLine(requestId, op, parameters);
        var responseTask = _responseRouter.Register(requestId, cancellationToken);

        _diagnosticLogger?.Invoke($"[MAME-DEBUGGER-OUT] {commandLine}");
        await _transport.SendAsync(commandLine, cancellationToken).ConfigureAwait(false);

        var response = await responseTask.ConfigureAwait(false);
        var result = response.TryGetProperty("result", out var resultElement) ? resultElement : response;
        var value = result.Deserialize<T>(MameDebuggerProtocol.JsonOptions);
        return value ?? throw new InvalidOperationException($"Debugger response for '{op}' did not contain a valid result.");
    }

    private void EnsureDebuggerSupportActive(string op)
    {
        if (!IsDebuggerSupportActive)
        {
            throw new InvalidOperationException($"Cannot send debugger operation '{op}' because the current MAME process was not launched with -debug.");
        }
    }

    private static MameDebuggerEvent CreateEvent(JsonElement payload)
    {
        var eventName = payload.TryGetProperty("event", out var eventElement) ? eventElement.GetString() ?? string.Empty : string.Empty;
        var state = payload.TryGetProperty("state", out var stateElement)
            ? ParseExecutionState(stateElement.GetString())
            : MameDebuggerExecutionState.Unknown;
        var cpu = payload.TryGetProperty("cpu", out var cpuElement) ? cpuElement.GetString() : null;
        long? pc = payload.TryGetProperty("pc", out var pcElement) && pcElement.TryGetInt64(out var pcValue) ? pcValue : null;

        return new MameDebuggerEvent(eventName, state, cpu, pc, payload.Clone());
    }

    private static MameDebuggerExecutionState ParseExecutionState(string? value)
    {
        return value?.ToLowerInvariant() switch
        {
            "running" => MameDebuggerExecutionState.Running,
            "stopped" => MameDebuggerExecutionState.Stopped,
            _ => MameDebuggerExecutionState.Unknown
        };
    }

    private sealed record PingResponse(string Pong);
}
