using System.Diagnostics;

namespace OasisEditor;

public sealed class MameEmulationService : IMameEmulationService
{
    private const string OasisSaveStateName = "oasis_save_state";

    private readonly IMameProcessStartInfoBuilder _startInfoBuilder;
    private readonly IMameProcessRunner _processRunner;
    private readonly Func<MameProcessLaunchRequest?> _requestFactory;
    private readonly object _stateGate = new();
    private MameEmulationState _state = MameEmulationState.Stopped;

    public MameEmulationService(
        IMameProcessStartInfoBuilder startInfoBuilder,
        IMameProcessRunner processRunner,
        Func<MameProcessLaunchRequest?> requestFactory)
    {
        _startInfoBuilder = startInfoBuilder ?? throw new ArgumentNullException(nameof(startInfoBuilder));
        _processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
        _requestFactory = requestFactory ?? throw new ArgumentNullException(nameof(requestFactory));
    }

    public MameEmulationState State
    {
        get
        {
            lock (_stateGate)
            {
                return _state;
            }
        }
    }

    public event EventHandler<MameEmulationState>? StateChanged;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        return StartCoreAsync(loadState: false, debuggerEnabled: false, cancellationToken);
    }

    public Task StartAndLoadStateAsync(CancellationToken cancellationToken)
    {
        return StartCoreAsync(loadState: true, debuggerEnabled: false, cancellationToken);
    }

    public Task StartDebuggerAsync(CancellationToken cancellationToken)
    {
        return StartCoreAsync(loadState: false, debuggerEnabled: true, cancellationToken);
    }

    public Task StartDebuggerAndLoadStateAsync(CancellationToken cancellationToken)
    {
        return StartCoreAsync(loadState: true, debuggerEnabled: true, cancellationToken);
    }

    private async Task StartCoreAsync(bool loadState, bool debuggerEnabled, CancellationToken cancellationToken)
    {
        SetState(MameEmulationState.Starting);
        try
        {
            var request = _requestFactory();
            if (request is null)
            {
                throw new InvalidOperationException("No valid MAME launch request is available.");
            }

            if (debuggerEnabled)
            {
                request = request with { DebuggerEnabled = true };
            }

            if (loadState)
            {
                var stateArgument = $"-state {OasisSaveStateName}";
                request = request with
                {
                    AdditionalArguments = string.IsNullOrWhiteSpace(request.AdditionalArguments)
                        ? stateArgument
                        : $"{request.AdditionalArguments.Trim()} {stateArgument}"
                };
            }

            var startInfo = _startInfoBuilder.Build(request);
            await _processRunner.StartAsync(startInfo, cancellationToken).ConfigureAwait(false);
            SetState(MameEmulationState.Running);
        }
        catch
        {
            SetState(MameEmulationState.Failed);
            throw;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (State == MameEmulationState.Stopped)
        {
            return;
        }

        SetState(MameEmulationState.Stopping);
        await _processRunner.StopAsync(cancellationToken).ConfigureAwait(false);
        SetState(MameEmulationState.Stopped);
    }

    public async Task SaveStateAndExitAsync(CancellationToken cancellationToken)
    {
        await SendCommandAsync($"state_save_and_exit {OasisSaveStateName}", cancellationToken).ConfigureAwait(false);
        await StopAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task LoadStateAsync(CancellationToken cancellationToken)
    {
        return SendCommandAsync($"state_load {OasisSaveStateName}", cancellationToken);
    }

    public Task SaveStateAsync(CancellationToken cancellationToken)
    {
        return SendCommandAsync($"state_save {OasisSaveStateName}", cancellationToken);
    }

    public async Task PauseAsync(CancellationToken cancellationToken)
    {
        await SendCommandAsync("pause", cancellationToken).ConfigureAwait(false);
        SetState(MameEmulationState.Paused);
    }

    public async Task ResumeAsync(CancellationToken cancellationToken)
    {
        await SendCommandAsync("resume", cancellationToken).ConfigureAwait(false);
        SetState(MameEmulationState.Running);
    }

    public Task SetThrottleAsync(bool isThrottled, CancellationToken cancellationToken)
    {
        return SendCommandAsync($"throttled {isThrottled.ToString().ToLowerInvariant()}", cancellationToken);
    }

    public Task SoftResetAsync(CancellationToken cancellationToken)
    {
        return SendCommandAsync("soft_reset", cancellationToken);
    }

    public Task HardResetAsync(CancellationToken cancellationToken)
    {
        return SendCommandAsync("hard_reset", cancellationToken);
    }

    private Task SendCommandAsync(string command, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return _processRunner.WriteStandardInputAsync(command, cancellationToken);
    }

    private void SetState(MameEmulationState state)
    {
        lock (_stateGate)
        {
            if (_state == state)
            {
                return;
            }

            _state = state;
        }

        StateChanged?.Invoke(this, state);
    }
}
