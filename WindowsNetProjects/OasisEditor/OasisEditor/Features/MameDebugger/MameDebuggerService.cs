using System.Text.Json;
using System.Threading;

namespace OasisEditor.Features.MameDebugger;

public interface IMameDebuggerService
{
    MameDebuggerStateSnapshot State { get; }
    event EventHandler<MameDebuggerEvent>? DebuggerEventReceived;
    Task<MameDebuggerPing> PingAsync(CancellationToken cancellationToken);
    Task<MameDebuggerStatus> GetStatusAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<MameDebuggerCpu>> GetCpuListAsync(CancellationToken cancellationToken);
    Task RunAsync(CancellationToken cancellationToken);
    Task BreakAsync(CancellationToken cancellationToken);
    Task StepAsync(CancellationToken cancellationToken);
    Task<MameDebuggerBreakpoint> SetBreakpointAsync(MameDebuggerBreakpointRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<MameDebuggerBreakpoint>> GetBreakpointsAsync(string? cpu, CancellationToken cancellationToken);
    Task<MameDebuggerBreakpoint> EnableBreakpointAsync(MameDebuggerBreakpointRequest request, CancellationToken cancellationToken);
    Task<MameDebuggerBreakpoint> DisableBreakpointAsync(MameDebuggerBreakpointRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<MameDebuggerBreakpoint>> ClearBreakpointAsync(MameDebuggerBreakpointRequest request, CancellationToken cancellationToken);
    Task<MameDebuggerWatchpoint> SetWatchpointAsync(MameDebuggerWatchpointRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<MameDebuggerWatchpoint>> GetWatchpointsAsync(string? cpu, CancellationToken cancellationToken, string? addressSpace = null);
    Task<MameDebuggerWatchpoint> EnableWatchpointAsync(MameDebuggerWatchpointRequest request, CancellationToken cancellationToken);
    Task<MameDebuggerWatchpoint> DisableWatchpointAsync(MameDebuggerWatchpointRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<MameDebuggerWatchpoint>> ClearWatchpointAsync(MameDebuggerWatchpointRequest request, CancellationToken cancellationToken);
    void ProcessStdoutLine(string line);
    void SetDebuggerLaunchActive(bool isActive);
}

public sealed class MameDebuggerService : IMameDebuggerService
{
    private static readonly TimeSpan DefaultRequestTimeout = TimeSpan.FromSeconds(15);

    private readonly IMameProcessRunner _processRunner;
    private readonly MameDebuggerStdoutParser _stdoutParser;
    private readonly MameDebuggerResponseRouter _responseRouter;
    private readonly MameDebuggerState _state;
    private readonly Action<string>? _diagnosticLogger;
    private long _nextRequestId;

    public MameDebuggerService(IMameProcessRunner processRunner, Action<string>? diagnosticLogger = null)
        : this(processRunner, new MameDebuggerStdoutParser(), new MameDebuggerResponseRouter(), new MameDebuggerState(), diagnosticLogger)
    {
    }

    internal MameDebuggerService(
        IMameProcessRunner processRunner,
        MameDebuggerStdoutParser stdoutParser,
        MameDebuggerResponseRouter responseRouter,
        MameDebuggerState state,
        Action<string>? diagnosticLogger = null)
    {
        _processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
        _stdoutParser = stdoutParser ?? throw new ArgumentNullException(nameof(stdoutParser));
        _responseRouter = responseRouter ?? throw new ArgumentNullException(nameof(responseRouter));
        _state = state ?? throw new ArgumentNullException(nameof(state));
        _diagnosticLogger = diagnosticLogger;
    }

    public MameDebuggerStateSnapshot State => _state.Snapshot();

    public event EventHandler<MameDebuggerEvent>? DebuggerEventReceived;

    public void SetDebuggerLaunchActive(bool isActive)
    {
        _state.MarkLaunchMode(isActive);
    }

    public async Task<MameDebuggerPing> PingAsync(CancellationToken cancellationToken)
    {
        var response = await SendRequestAsync("ping", cancellationToken).ConfigureAwait(false);
        return DeserializeResult<MameDebuggerPing>(response);
    }

    public async Task<MameDebuggerStatus> GetStatusAsync(CancellationToken cancellationToken)
    {
        var response = await SendRequestAsync("status", cancellationToken).ConfigureAwait(false);
        var status = DeserializeResult<MameDebuggerStatus>(response);
        _state.ApplyStatus(status);
        return status;
    }

    public async Task<IReadOnlyList<MameDebuggerCpu>> GetCpuListAsync(CancellationToken cancellationToken)
    {
        var response = await SendRequestAsync("cpus", cancellationToken).ConfigureAwait(false);
        return DeserializeResult<List<MameDebuggerCpu>>(response);
    }

    public Task RunAsync(CancellationToken cancellationToken)
    {
        return SendControlRequestAsync("run", cancellationToken);
    }

    public Task BreakAsync(CancellationToken cancellationToken)
    {
        return SendControlRequestAsync("break", cancellationToken);
    }

    public Task StepAsync(CancellationToken cancellationToken)
    {
        return SendControlRequestAsync("step", cancellationToken);
    }

    public async Task<MameDebuggerBreakpoint> SetBreakpointAsync(MameDebuggerBreakpointRequest request, CancellationToken cancellationToken)
    {
        var response = await SendRequestAsync("bp.set", request, cancellationToken).ConfigureAwait(false);
        return DeserializeResult<MameDebuggerBreakpoint>(response);
    }

    public async Task<IReadOnlyList<MameDebuggerBreakpoint>> GetBreakpointsAsync(string? cpu, CancellationToken cancellationToken)
    {
        var response = await SendRequestAsync("bp.list", new MameDebuggerBreakpointRequest(cpu, 0), cancellationToken).ConfigureAwait(false);
        return DeserializeResult<List<MameDebuggerBreakpoint>>(response);
    }

    public async Task<MameDebuggerBreakpoint> EnableBreakpointAsync(MameDebuggerBreakpointRequest request, CancellationToken cancellationToken)
    {
        var response = await SendRequestAsync("bp.enable", request, cancellationToken).ConfigureAwait(false);
        return DeserializeResult<MameDebuggerBreakpoint>(response);
    }

    public async Task<MameDebuggerBreakpoint> DisableBreakpointAsync(MameDebuggerBreakpointRequest request, CancellationToken cancellationToken)
    {
        var response = await SendRequestAsync("bp.disable", request, cancellationToken).ConfigureAwait(false);
        return DeserializeResult<MameDebuggerBreakpoint>(response);
    }

    public async Task<IReadOnlyList<MameDebuggerBreakpoint>> ClearBreakpointAsync(MameDebuggerBreakpointRequest request, CancellationToken cancellationToken)
    {
        var response = await SendRequestAsync("bp.clear", request, cancellationToken).ConfigureAwait(false);
        return DeserializeResult<List<MameDebuggerBreakpoint>>(response);
    }

    public async Task<MameDebuggerWatchpoint> SetWatchpointAsync(MameDebuggerWatchpointRequest request, CancellationToken cancellationToken)
    {
        var response = await SendRequestAsync("wp.set", request, cancellationToken).ConfigureAwait(false);
        return DeserializeResult<MameDebuggerWatchpoint>(response);
    }

    public async Task<IReadOnlyList<MameDebuggerWatchpoint>> GetWatchpointsAsync(string? cpu, CancellationToken cancellationToken, string? addressSpace = null)
    {
        var response = await SendRequestAsync("wp.list", new MameDebuggerWatchpointRequest(cpu, 0, 1, MameDebuggerWatchpointType.ReadWrite, AddressSpace: addressSpace), cancellationToken).ConfigureAwait(false);
        return DeserializeResult<List<MameDebuggerWatchpoint>>(response);
    }

    public async Task<MameDebuggerWatchpoint> EnableWatchpointAsync(MameDebuggerWatchpointRequest request, CancellationToken cancellationToken)
    {
        var response = await SendRequestAsync("wp.enable", request, cancellationToken).ConfigureAwait(false);
        return DeserializeResult<MameDebuggerWatchpoint>(response);
    }

    public async Task<MameDebuggerWatchpoint> DisableWatchpointAsync(MameDebuggerWatchpointRequest request, CancellationToken cancellationToken)
    {
        var response = await SendRequestAsync("wp.disable", request, cancellationToken).ConfigureAwait(false);
        return DeserializeResult<MameDebuggerWatchpoint>(response);
    }

    public async Task<IReadOnlyList<MameDebuggerWatchpoint>> ClearWatchpointAsync(MameDebuggerWatchpointRequest request, CancellationToken cancellationToken)
    {
        var response = await SendRequestAsync("wp.clear", request, cancellationToken).ConfigureAwait(false);
        return DeserializeResult<List<MameDebuggerWatchpoint>>(response);
    }

    public void ProcessStdoutLine(string line)
    {
        if (!_stdoutParser.TryParse(line, out var message))
        {
            return;
        }

        _diagnosticLogger?.Invoke($"[MAME-DEBUG-PROTOCOL <-] {line}");

        try
        {
            if (message.Kind == MameDebuggerStdoutMessageKind.Event)
            {
                var debuggerEvent = MameDebuggerProtocol.ParseEvent(message.Payload);
                _state.ApplyEvent(debuggerEvent);
                DebuggerEventReceived?.Invoke(this, debuggerEvent);
                return;
            }

            var response = MameDebuggerProtocol.ParseResponse(message.Payload);
            if (!_responseRouter.TryRoute(response))
            {
                _diagnosticLogger?.Invoke($"[MAME-DEBUG-PROTOCOL] Unmatched response id {response.Id}.");
            }
        }
        catch (JsonException ex)
        {
            _diagnosticLogger?.Invoke($"[MAME-DEBUG-PROTOCOL] Failed to parse debugger payload: {ex.Message}");
        }
    }

    private async Task SendControlRequestAsync(string operation, CancellationToken cancellationToken)
    {
        var response = await SendRequestAsync(operation, cancellationToken).ConfigureAwait(false);
        var status = DeserializeResult<MameDebuggerStatus>(response);
        _state.ApplyStatus(status);
    }

    private Task<MameDebuggerResponse> SendRequestAsync(string operation, CancellationToken cancellationToken)
    {
        return SendRequestCoreAsync<object>(operation, payload: null, cancellationToken);
    }

    private Task<MameDebuggerResponse> SendRequestAsync<TPayload>(string operation, TPayload payload, CancellationToken cancellationToken)
        where TPayload : class
    {
        ArgumentNullException.ThrowIfNull(payload);
        return SendRequestCoreAsync(operation, payload, cancellationToken);
    }

    private async Task<MameDebuggerResponse> SendRequestCoreAsync<TPayload>(string operation, TPayload? payload, CancellationToken cancellationToken)
        where TPayload : class
    {
        cancellationToken.ThrowIfCancellationRequested();
        var requestId = Interlocked.Increment(ref _nextRequestId);
        var responseTask = _responseRouter.RegisterAsync(requestId, DefaultRequestTimeout, cancellationToken);
        var command = payload is null
            ? MameDebuggerProtocol.CreateCommand(requestId, operation)
            : MameDebuggerProtocol.CreateCommand(requestId, operation, payload);
        _diagnosticLogger?.Invoke($"[MAME-DEBUG-PROTOCOL ->] {command}");
        await _processRunner.WriteStandardInputAsync(command, cancellationToken).ConfigureAwait(false);
        var response = await responseTask.ConfigureAwait(false);
        if (!response.Ok)
        {
            var error = response.Error;
            throw new InvalidOperationException(error is null
                ? $"MAME debugger request '{operation}' failed."
                : $"MAME debugger request '{operation}' failed: {error.Code}: {error.Message}");
        }

        return response;
    }

    private static T DeserializeResult<T>(MameDebuggerResponse response)
    {
        if (response.Result is null)
        {
            throw new InvalidOperationException($"MAME debugger response {response.Id} did not include a result payload.");
        }

        return response.Result.Value.Deserialize<T>(MameDebuggerProtocol.JsonOptions)
            ?? throw new InvalidOperationException($"MAME debugger response {response.Id} result payload was empty.");
    }
}
