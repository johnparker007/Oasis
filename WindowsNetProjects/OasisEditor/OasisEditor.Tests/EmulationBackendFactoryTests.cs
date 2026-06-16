namespace OasisEditor.Tests;

public sealed class EmulationBackendFactoryTests
{
    [Fact]
    public void CreateBackend_ForNone_ReturnsNull()
    {
        var factory = new EmulationBackendFactory(() => new FakeBackend(), () => "C:/cores/system6.dll");

        Assert.Null(factory.CreateBackend(FruitMachinePlatformType.None));
    }

    [Fact]
    public void CreateBackend_ForImpactWithSystem6Path_ReturnsSystem6Backend()
    {
        var factory = new EmulationBackendFactory(() => new FakeBackend(), () => "C:/cores/system6.dll");

        Assert.IsType<System6NativeBackend>(factory.CreateBackend(FruitMachinePlatformType.Impact));
    }

    [Fact]
    public void CreateBackend_ForImpactWithoutSystem6Path_FallsBackToMameBackend()
    {
        var mameBackend = new FakeBackend();
        var factory = new EmulationBackendFactory(() => mameBackend, () => string.Empty);

        Assert.Same(mameBackend, factory.CreateBackend(FruitMachinePlatformType.Impact));
    }

    [Fact]
    public void CreateBackend_ForMpu4_ReturnsMameBackend()
    {
        var mameBackend = new FakeBackend();
        var factory = new EmulationBackendFactory(() => mameBackend, () => "C:/cores/system6.dll");

        Assert.Same(mameBackend, factory.CreateBackend(FruitMachinePlatformType.MPU4));
    }

    private sealed class FakeBackend : IEmulationBackend
    {
        public EmulationBackendState State => EmulationBackendState.Stopped;
        public EmulationBackendCapabilities Capabilities { get; } = new(false, false, false, false, false, false, false, false);
        public event EventHandler<EmulationBackendState>? StateChanged;
        public event EventHandler<MachineLampChangedEventArgs>? LampChanged;
        public event EventHandler<MachineReelChangedEventArgs>? ReelChanged;
        public event EventHandler<MachineSegmentChangedEventArgs>? SegmentChanged;
        public event EventHandler<MachineVfdBrightnessChangedEventArgs>? VfdBrightnessChanged;
        public event EventHandler<MachineDotMatrixChangedEventArgs>? DotMatrixChanged;
        public Task StartAsync(EmulationLaunchRequest request, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task PauseAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task ResumeAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task ResetAsync(EmulationResetKind resetKind, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task SetInputStateAsync(MachineInputReference input, bool isPressed, CancellationToken cancellationToken) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
