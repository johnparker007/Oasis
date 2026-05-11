using System.Diagnostics;

namespace OasisEditor;

public sealed class MameEmulationService : IMameEmulationService
{
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

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        SetState(MameEmulationState.Starting);
        try
        {
            var request = _requestFactory();
            if (request is null)
            {
                throw new InvalidOperationException("No valid MAME launch request is available.");
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

    public Task PauseAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SetState(MameEmulationState.Paused);
        return Task.CompletedTask;
    }

    public Task ResumeAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SetState(MameEmulationState.Running);
        return Task.CompletedTask;
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
