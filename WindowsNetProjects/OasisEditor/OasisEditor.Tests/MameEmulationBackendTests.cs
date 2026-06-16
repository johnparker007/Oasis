using OasisEditor;
using Xunit;

namespace OasisEditor.Tests;

public sealed class MameEmulationBackendTests
{
    [Fact]
    public void Capabilities_ExposeMameSupportedOperations()
    {
        var backend = CreateBackend(out _, out _);

        Assert.True(backend.Capabilities.SupportsPause);
        Assert.True(backend.Capabilities.SupportsResume);
        Assert.True(backend.Capabilities.SupportsSoftReset);
        Assert.True(backend.Capabilities.SupportsHardReset);
        Assert.True(backend.Capabilities.SupportsSaveState);
        Assert.True(backend.Capabilities.SupportsLoadState);
        Assert.True(backend.Capabilities.SupportsThrottle);
        Assert.True(backend.Capabilities.SupportsDebugger);
    }

    [Fact]
    public async Task LifecycleMethods_DelegateToMameEmulationService()
    {
        var backend = CreateBackend(out var service, out _);
        var request = CreateLaunchRequest();

        await backend.StartAsync(request, CancellationToken.None);
        await backend.PauseAsync(CancellationToken.None);
        await backend.ResumeAsync(CancellationToken.None);
        await backend.ResetAsync(EmulationResetKind.Soft, CancellationToken.None);
        await backend.ResetAsync(EmulationResetKind.Hard, CancellationToken.None);
        await backend.StopAsync(CancellationToken.None);

        Assert.Equal(1, service.StartCount);
        Assert.Equal(1, service.PauseCount);
        Assert.Equal(1, service.ResumeCount);
        Assert.Equal(1, service.SoftResetCount);
        Assert.Equal(1, service.HardResetCount);
        Assert.Equal(1, service.StopCount);
    }

    [Fact]
    public async Task StateChanged_MapsMameStateToBackendState()
    {
        var backend = CreateBackend(out var service, out _);
        var states = new List<EmulationBackendState>();
        backend.StateChanged += (_, state) => states.Add(state);

        await service.StartAsync(CancellationToken.None);
        await service.PauseAsync(CancellationToken.None);
        await service.ResumeAsync(CancellationToken.None);
        await service.StopAsync(CancellationToken.None);

        Assert.Equal(
            [EmulationBackendState.Starting, EmulationBackendState.Running, EmulationBackendState.Paused, EmulationBackendState.Running, EmulationBackendState.Stopping, EmulationBackendState.Stopped],
            states);
        Assert.Equal(EmulationBackendState.Stopped, backend.State);
    }

    [Fact]
    public async Task SetInputStateAsync_DelegatesToMameInputCommandService()
    {
        var backend = CreateBackend(out _, out var inputService);

        await backend.SetInputStateAsync(new MachineInputReference("start"), isPressed: true, CancellationToken.None);

        var call = Assert.Single(inputService.Calls);
        Assert.Equal(FruitMachinePlatformType.MPU4, call.Platform);
        Assert.Equal("start", call.InputDefinition.Id);
        Assert.True(call.IsPressed);
    }

    [Fact]
    public async Task DisposeAsync_UnsubscribesAndStopsRunningMameService()
    {
        var backend = CreateBackend(out var service, out _);
        await backend.StartAsync(CreateLaunchRequest(), CancellationToken.None);

        await backend.DisposeAsync();
        await service.PauseAsync(CancellationToken.None);

        Assert.Equal(1, service.StopCount);
        Assert.Equal(0, service.StateChangedSubscriberCount);
    }

    private static MameEmulationBackend CreateBackend(out RecordingMameEmulationService service, out RecordingMameInputCommandService inputService)
    {
        service = new RecordingMameEmulationService();
        inputService = new RecordingMameInputCommandService();
        var processRunner = new RecordingMameProcessRunner();
        var inputDefinitions = new Dictionary<string, InputDefinitionModel>(StringComparer.Ordinal)
        {
            ["start"] = new() { Id = "start", Kind = InputDefinitionKind.Button, ButtonNumber = "1" }
        };

        return new MameEmulationBackend(
            service,
            inputService,
            processRunner,
            () => FruitMachinePlatformType.MPU4,
            input => inputDefinitions.TryGetValue(input.Id, out var definition) ? definition : null);
    }

    private static EmulationLaunchRequest CreateLaunchRequest()
    {
        return new EmulationLaunchRequest(
            FruitMachinePlatformType.MPU4,
            "m4test",
            @"C:\MAME\roms",
            [],
            string.Empty);
    }

    private sealed class RecordingMameEmulationService : IMameEmulationService
    {
        private EventHandler<MameEmulationState>? _stateChanged;
        private MameEmulationState _state = MameEmulationState.Stopped;

        public MameEmulationState State => _state;
        public int StateChangedSubscriberCount => _stateChanged?.GetInvocationList().Length ?? 0;
        public int StartCount { get; private set; }
        public int StopCount { get; private set; }
        public int PauseCount { get; private set; }
        public int ResumeCount { get; private set; }
        public int SoftResetCount { get; private set; }
        public int HardResetCount { get; private set; }

        public event EventHandler<MameEmulationState>? StateChanged
        {
            add => _stateChanged += value;
            remove => _stateChanged -= value;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            StartCount++;
            SetState(MameEmulationState.Starting);
            SetState(MameEmulationState.Running);
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            StopCount++;
            SetState(MameEmulationState.Stopping);
            SetState(MameEmulationState.Stopped);
            return Task.CompletedTask;
        }

        public Task PauseAsync(CancellationToken cancellationToken)
        {
            PauseCount++;
            SetState(MameEmulationState.Paused);
            return Task.CompletedTask;
        }

        public Task ResumeAsync(CancellationToken cancellationToken)
        {
            ResumeCount++;
            SetState(MameEmulationState.Running);
            return Task.CompletedTask;
        }

        public Task SoftResetAsync(CancellationToken cancellationToken)
        {
            SoftResetCount++;
            return Task.CompletedTask;
        }

        public Task HardResetAsync(CancellationToken cancellationToken)
        {
            HardResetCount++;
            return Task.CompletedTask;
        }

        public Task StartAndLoadStateAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task StartDebuggerAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task StartDebuggerAndLoadStateAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task SaveStateAndExitAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task LoadStateAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task SaveStateAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task SetThrottleAsync(bool isThrottled, CancellationToken cancellationToken) => throw new NotSupportedException();

        private void SetState(MameEmulationState state)
        {
            _state = state;
            _stateChanged?.Invoke(this, state);
        }
    }

    private sealed class RecordingMameInputCommandService : IMameInputCommandService
    {
        public List<InputCall> Calls { get; } = [];

        public Task<bool> TrySendInputStateAsync(IMameProcessRunner processRunner, FruitMachinePlatformType platform, InputDefinitionModel inputDefinition, bool isPressed, CancellationToken cancellationToken)
        {
            Calls.Add(new InputCall(processRunner, platform, inputDefinition, isPressed));
            return Task.FromResult(true);
        }
    }

    private sealed class RecordingMameProcessRunner : IMameProcessRunner
    {
        public Task StartAsync(System.Diagnostics.ProcessStartInfo startInfo, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task WriteStandardInputAsync(string command, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed record InputCall(IMameProcessRunner ProcessRunner, FruitMachinePlatformType Platform, InputDefinitionModel InputDefinition, bool IsPressed);
}
