using System.Text.Json;
using System.Threading;

namespace OasisEditor.Features.MameDebugger;

public interface IMameDebuggerService
{
    MameDebuggerStateSnapshot State { get; }
    event EventHandler<MameDebuggerEvent>? DebuggerEventReceived;
    Task<MameDebuggerStatus> GetStatusAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<MameDebuggerCpu>> GetCpuListAsync(CancellationToken cancellationToken);
    Task RunAsync(CancellationToken cancellationToken);
    Task BreakAsync(CancellationToken cancellationToken);
    Task StepAsync(CancellationToken cancellationToken);
    void ProcessStdoutLine(string line);
    void SetDebuggerLaunchActive(bool isActive);
}

public sealed class MameDebuggerService : IMameDebuggerService
{
    private static readonly TimeSpan DefaultRequestTimeout = TimeSpan.FromSeconds(5);

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

    private async Task<MameDebuggerResponse> SendRequestAsync(string operation, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var requestId = Interlocked.Increment(ref _nextRequestId);
        var responseTask = _responseRouter.RegisterAsync(requestId, DefaultRequestTimeout, cancellationToken);
        var command = MameDebuggerProtocol.CreateCommand(requestId, operation);
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
