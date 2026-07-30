using Xunit;

namespace OasisEditor.Tests;

public sealed class PlayViewInputRouterTests
{
    [Fact]
    public async Task PressReleaseAndReleaseAll_UseActiveBackendOnly()
    {
        var backend = new RecordingBackend();
        var router = new PlayViewInputRouter(backend);
        var first = new InputDefinitionModel { Id = "first", ButtonNumber = "1" };
        var second = new InputDefinitionModel { Id = "second", ButtonNumber = "2" };
        Assert.True(await router.TryPressAsync(FruitMachinePlatformType.Impact, first, CancellationToken.None));
        Assert.True(await router.TryReleaseAsync(FruitMachinePlatformType.Impact, first, CancellationToken.None));
        Assert.True(await router.TryPressAsync(FruitMachinePlatformType.Impact, second, CancellationToken.None));
        Assert.Equal(1, await router.ReleaseAllAsync(FruitMachinePlatformType.Impact,
            new Dictionary<string, InputDefinitionModel> { [second.Id] = second }, CancellationToken.None));
        Assert.Equal([(first.Id, true), (first.Id, false), (second.Id, true), (second.Id, false)], backend.Inputs);
    }

    private sealed class RecordingBackend : IEmulationBackend
    {
        public List<(string, bool)> Inputs { get; } = [];
        public EmulationBackendKind BackendKind => EmulationBackendKind.Fabric;
        public EmulationBackendState State => EmulationBackendState.Running;
        public EmulationBackendCapabilities Capabilities { get; } = new(true, true, true, true, false, false, false);
        public event EventHandler<EmulationBackendState>? StateChanged { add { } remove { } }
        public event EventHandler<MachineLampChangedEventArgs>? LampChanged { add { } remove { } }
        public event EventHandler<MachineReelChangedEventArgs>? ReelChanged { add { } remove { } }
        public event EventHandler<MachineSegmentChangedEventArgs>? SegmentChanged { add { } remove { } }
        public event EventHandler<MachineVfdBrightnessChangedEventArgs>? VfdBrightnessChanged { add { } remove { } }
        public event EventHandler<MachineDotMatrixChangedEventArgs>? DotMatrixChanged { add { } remove { } }
        public Task StartAsync(EmulationLaunchRequest request, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task PauseAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task ResumeAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task ResetAsync(EmulationResetKind resetKind, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task SetInputStateAsync(InputDefinitionModel inputDefinition, bool isPressed, CancellationToken cancellationToken) { Inputs.Add((inputDefinition.Id, isPressed)); return Task.CompletedTask; }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
