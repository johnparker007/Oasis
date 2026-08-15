using Xunit;

namespace OasisEditor.Tests;

public sealed class PlayViewInputRouterTests
{
    [Fact]
    public async Task PointerRouter_ReturnsFalseForVisualWithoutGenuineInput()
    {
        var backend = new RecordingBackend();
        var router = new PlayViewPointerInputRouter(new PlayViewInputRouter(backend), []);

        Assert.False(await router.TryHandlePointerDownAsync(
            FruitMachinePlatformType.Impact, Guid.NewGuid(), isFocused: true, CancellationToken.None));
        Assert.Empty(backend.Inputs);
    }

    [Fact]
    public async Task PointerRouter_RoutesVisualLinkedByGenuineInput()
    {
        var backend = new RecordingBackend();
        var visualId = Guid.NewGuid();
        var input = new InputDefinitionModel
        {
            Id = "genuine",
            ButtonNumber = "0",
            LinkedVisualElementId = visualId
        };
        var router = new PlayViewPointerInputRouter(new PlayViewInputRouter(backend), [input]);

        Assert.True(await router.TryHandlePointerDownAsync(
            FruitMachinePlatformType.Impact, visualId, isFocused: true, CancellationToken.None));
        Assert.Equal([("genuine", true)], backend.Inputs);
    }

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

    [Fact]
    public async Task CoinPressIsSingleShotUntilReleaseAndReleaseAllUsesCoinRelease()
    {
        var backend = new RecordingBackend();
        var router = new PlayViewInputRouter(backend);
        var coin = new InputDefinitionModel { Id = "coin", CoinInput = true, CoinChannel = 2, CoinValue = 3 };

        Assert.True(await router.TryPressAsync(FruitMachinePlatformType.Impact, coin, CancellationToken.None));
        Assert.False(await router.TryPressAsync(FruitMachinePlatformType.Impact, coin, CancellationToken.None));
        Assert.True(await router.TryReleaseAsync(FruitMachinePlatformType.Impact, coin, CancellationToken.None));
        Assert.True(await router.TryPressAsync(FruitMachinePlatformType.Impact, coin, CancellationToken.None));
        Assert.Equal(1, await router.ReleaseAllAsync(FruitMachinePlatformType.Impact,
            new Dictionary<string, InputDefinitionModel> { [coin.Id] = coin }, CancellationToken.None));
        Assert.Equal([(coin.Id, true), (coin.Id, false), (coin.Id, true), (coin.Id, false)], backend.Inputs);
    }

    [Fact]
    public async Task UnresolvedCoinIsNotActivated()
    {
        var backend = new RecordingBackend();
        var router = new PlayViewInputRouter(backend);
        Assert.False(await router.TryPressAsync(FruitMachinePlatformType.Impact,
            new InputDefinitionModel { Id = "coin", CoinInput = true }, CancellationToken.None));
        Assert.Empty(backend.Inputs);
    }

    private sealed class RecordingBackend : IEmulationBackend
    {
        public List<(string, bool)> Inputs { get; } = [];
        public EmulationBackendKind BackendKind => EmulationBackendKind.Fabric;
        public EmulationBackendState State => EmulationBackendState.Running;
        public EmulationBackendCapabilities Capabilities { get; } = new(true, true, true, false, false, false);
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
        public Task ResetAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task SetInputStateAsync(InputDefinitionModel inputDefinition, bool isPressed, CancellationToken cancellationToken) { Inputs.Add((inputDefinition.Id, isPressed)); return Task.CompletedTask; }
        public Task<CoinInputResult> InsertCoinAsync(InputDefinitionModel inputDefinition, CancellationToken cancellationToken) { Inputs.Add((inputDefinition.Id, true)); return Task.FromResult(CoinInputResult.Accepted); }
        public Task ReleaseCoinAsync(InputDefinitionModel inputDefinition, CancellationToken cancellationToken) { Inputs.Add((inputDefinition.Id, false)); return Task.CompletedTask; }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
