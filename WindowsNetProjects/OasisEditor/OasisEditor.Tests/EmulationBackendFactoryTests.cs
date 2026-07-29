using OasisEditor;
using Xunit;

namespace OasisEditor.Tests;

public sealed class EmulationBackendFactoryTests
{
    [Fact]
    public void CreateBackend_ForNone_ReturnsNull()
    {
        var factory = new EmulationBackendFactory(() => new FakeBackend(), () => "C:/cores/AmberBridge.dll");

        Assert.Null(factory.CreateBackend(FruitMachinePlatformType.None));
    }

    [Fact]
    public void CreateBackend_ForImpactWithSystem6Path_ReturnsSystem6Backend()
    {
        var factory = new EmulationBackendFactory(() => new FakeBackend(), () => "C:/cores/AmberBridge.dll", amberBridgeFactory: _ => throw new InvalidOperationException("created only on start"));

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
        var factory = new EmulationBackendFactory(() => mameBackend, () => "C:/cores/AmberBridge.dll");

        Assert.Same(mameBackend, factory.CreateBackend(FruitMachinePlatformType.MPU4));
    }

    [Fact]
    public void CreateBackend_WithValidFabricFeature_ReturnsFabricBackend()
    {
        var factory = new EmulationBackendFactory(() => new FakeBackend(), () => "legacy.dll",
            fabricConfigurationProvider: () => (true, "runtime.dll", "amber.dll"));

        Assert.IsType<FabricEmulationBackend>(factory.CreateBackend(FruitMachinePlatformType.Impact));
    }

    [Fact]
    public void CreateBackend_WithIncompleteEnabledFabricFeature_ThrowsActionableError()
    {
        var factory = new EmulationBackendFactory(() => new FakeBackend(), () => "legacy.dll",
            fabricConfigurationProvider: () => (true, "runtime.dll", null));

        var error = Assert.Throws<InvalidOperationException>(() => factory.CreateBackend(FruitMachinePlatformType.Impact));
        Assert.Contains("both the Fabric runtime DLL path and Amber API v2 DLL path", error.Message);
    }

    private sealed class FakeBackend : IEmulationBackend
    {
        public EmulationBackendKind BackendKind => EmulationBackendKind.Mame;
        public EmulationBackendState State => EmulationBackendState.Stopped;
        public EmulationBackendCapabilities Capabilities { get; } = new(false, false, false, false, false, false, false, false);
        public event EventHandler<EmulationBackendState>? StateChanged
        {
            add { }
            remove { }
        }

        public event EventHandler<MachineLampChangedEventArgs>? LampChanged
        {
            add { }
            remove { }
        }

        public event EventHandler<MachineReelChangedEventArgs>? ReelChanged
        {
            add { }
            remove { }
        }

        public event EventHandler<MachineSegmentChangedEventArgs>? SegmentChanged
        {
            add { }
            remove { }
        }

        public event EventHandler<MachineVfdBrightnessChangedEventArgs>? VfdBrightnessChanged
        {
            add { }
            remove { }
        }

        public event EventHandler<MachineDotMatrixChangedEventArgs>? DotMatrixChanged
        {
            add { }
            remove { }
        }
        public Task StartAsync(EmulationLaunchRequest request, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task PauseAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task ResumeAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task ResetAsync(EmulationResetKind resetKind, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task SetInputStateAsync(InputDefinitionModel inputDefinition, bool isPressed, CancellationToken cancellationToken) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
