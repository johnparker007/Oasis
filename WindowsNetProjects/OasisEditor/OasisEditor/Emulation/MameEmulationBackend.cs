namespace OasisEditor;

public sealed class MameEmulationBackend : IEmulationBackend
{
    private static readonly EmulationBackendCapabilities MameCapabilities = new(
        SupportsPause: true,
        SupportsResume: true,
        SupportsSoftReset: true,
        SupportsHardReset: true,
        SupportsSaveState: true,
        SupportsLoadState: true,
        SupportsThrottle: true,
        SupportsDebugger: true);

    private readonly IMameEmulationService _emulationService;
    private readonly IMameInputCommandService _inputCommandService;
    private readonly IMameProcessRunner _processRunner;
    private readonly Func<FruitMachinePlatformType> _platformProvider;
    private readonly Func<MachineInputReference, InputDefinitionModel?> _inputDefinitionResolver;

    public MameEmulationBackend(
        IMameEmulationService emulationService,
        IMameInputCommandService inputCommandService,
        IMameProcessRunner processRunner,
        Func<FruitMachinePlatformType> platformProvider,
        Func<MachineInputReference, InputDefinitionModel?> inputDefinitionResolver)
    {
        _emulationService = emulationService ?? throw new ArgumentNullException(nameof(emulationService));
        _inputCommandService = inputCommandService ?? throw new ArgumentNullException(nameof(inputCommandService));
        _processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
        _platformProvider = platformProvider ?? throw new ArgumentNullException(nameof(platformProvider));
        _inputDefinitionResolver = inputDefinitionResolver ?? throw new ArgumentNullException(nameof(inputDefinitionResolver));

        _emulationService.StateChanged += OnMameStateChanged;
    }

    public EmulationBackendState State => MapState(_emulationService.State);

    public EmulationBackendCapabilities Capabilities => MameCapabilities;

    public event EventHandler<EmulationBackendState>? StateChanged;

    public event EventHandler<MachineLampChangedEventArgs>? LampChanged;
    public event EventHandler<MachineReelChangedEventArgs>? ReelChanged;
    public event EventHandler<MachineSegmentChangedEventArgs>? SegmentChanged;
    public event EventHandler<MachineVfdBrightnessChangedEventArgs>? VfdBrightnessChanged;
    public event EventHandler<MachineDotMatrixChangedEventArgs>? DotMatrixChanged;

    public Task StartAsync(EmulationLaunchRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return _emulationService.StartAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return _emulationService.StopAsync(cancellationToken);
    }

    public Task PauseAsync(CancellationToken cancellationToken)
    {
        return _emulationService.PauseAsync(cancellationToken);
    }

    public Task ResumeAsync(CancellationToken cancellationToken)
    {
        return _emulationService.ResumeAsync(cancellationToken);
    }

    public Task ResetAsync(EmulationResetKind resetKind, CancellationToken cancellationToken)
    {
        return resetKind switch
        {
            EmulationResetKind.Soft => _emulationService.SoftResetAsync(cancellationToken),
            EmulationResetKind.Hard => _emulationService.HardResetAsync(cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(resetKind), resetKind, null)
        };
    }

    public async Task SetInputStateAsync(MachineInputReference input, bool isPressed, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);

        var inputDefinition = _inputDefinitionResolver(input);
        if (inputDefinition is null)
        {
            throw new InvalidOperationException($"No MAME input definition was found for emulation input '{input.Id}'.");
        }

        var sent = await _inputCommandService
            .TrySendInputStateAsync(_processRunner, _platformProvider(), inputDefinition, isPressed, cancellationToken)
            .ConfigureAwait(false);
        if (!sent)
        {
            throw new InvalidOperationException($"MAME input '{input.Id}' could not be resolved to a MAME input command.");
        }
    }

    public async ValueTask DisposeAsync()
    {
        _emulationService.StateChanged -= OnMameStateChanged;
        if (_emulationService.State is not MameEmulationState.Stopped)
        {
            await _emulationService.StopAsync(CancellationToken.None).ConfigureAwait(false);
        }
    }

    private void OnMameStateChanged(object? sender, MameEmulationState state)
    {
        StateChanged?.Invoke(this, MapState(state));
    }

    private static EmulationBackendState MapState(MameEmulationState state)
    {
        return state switch
        {
            MameEmulationState.Stopped => EmulationBackendState.Stopped,
            MameEmulationState.Starting => EmulationBackendState.Starting,
            MameEmulationState.Running => EmulationBackendState.Running,
            MameEmulationState.Paused => EmulationBackendState.Paused,
            MameEmulationState.Stopping => EmulationBackendState.Stopping,
            MameEmulationState.Failed => EmulationBackendState.Failed,
            _ => EmulationBackendState.Failed
        };
    }
}
