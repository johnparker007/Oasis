using OasisEditor;
using OasisEditor.Avalonia;
using Xunit;

namespace OasisEditor.Avalonia.Tests;

public sealed class NativePreviewOutputsTests
{
    [Fact]
    public void CaptureMapsLiveOutputsToPanelAndFaceAuthoredReferences()
    {
        var backend = new EventBackend();
        var machine = CreateMachine(FruitMachinePlatformType.Impact);
        using var outputs = new NativePreviewOutputs(backend, machine);
        var panel = new Panel2DDocumentModel
        {
            Elements =
            [
                Element("panel-lamp", PanelElementKind.Lamp, 7),
                new PanelElementModel { ObjectId = "panel-reel", Kind = PanelElementKind.Reel, DisplayNumber = 2, Stops = 12 },
                Element("panel-alpha", PanelElementKind.Alpha, 10),
                Element("panel-seven", PanelElementKind.SevenSegment, 20),
                Element("panel-dots", PanelElementKind.VfdDotMatrix, 4)
            ]
        };
        var face = new FaceDocumentModel
        {
            Elements =
            [
                new FaceLampWindowElement { ObjectId = "face-lamp", LinkedMachineObjectReference = MachineObjectReference.Lamp(7) },
                new FaceReelMount { ObjectId = "face-reel", LinkedMachineObjectReference = MachineObjectReference.Reel(2) },
                new FaceAlphaDisplayElement { ObjectId = "face-alpha", LinkedMachineObjectReference = MachineObjectReference.AlphaDisplay(10) },
                new FaceSevenSegmentDisplayElement { ObjectId = "face-seven", LinkedMachineObjectReference = MachineObjectReference.SevenSegmentDisplay(20), DigitCount = 2 }
            ]
        };

        backend.EmitLamp(7, 128);
        backend.EmitReel(2, 24, ReelPositionConvention.Oasis);
        for (var cell = 10; cell < 26; cell++) backend.EmitSegment(cell, cell, SegmentOutputType.Vfd);
        backend.EmitBrightness(10, .25);
        backend.EmitSegment(20, 0x3f, SegmentOutputType.Digit);
        var mutableDots = Enumerable.Range(0, 96 * 8).Select(i => i % 3 == 0 ? 7 : 0).ToArray();
        backend.EmitDots(4, 96, 8, mutableDots, .1);
        mutableDots[0] = 0;

        var snapshot = outputs.Capture(panel, face);
        Assert.Equal(128d / 255d, snapshot.LampIntensities["panel-lamp"], 6);
        Assert.Equal(128d / 255d, snapshot.LampIntensities["lamp:7"], 6);
        Assert.Equal(26.4d, snapshot.ReelPositions["panel-reel"], 6); // Impact's authored 12-stop offset is included.
        Assert.Equal(24d, snapshot.ReelPositions["reel:2"], 6);
        Assert.Equal(25d, snapshot.SegmentMasks["panel-alpha"][0]);
        Assert.Equal(24d, snapshot.SegmentMasks["panel-alpha"][1]);
        Assert.Equal(.25d, snapshot.SegmentBrightness["panel-alpha"][0]);
        Assert.Equal(0x3f, snapshot.SegmentMasks["panel-seven"][0]);
        Assert.Equal(0x3f, snapshot.SegmentMasks["sevenSegment:20"][0]);
        Assert.Equal(128d / 255d, snapshot.LampIntensities["lamp:7"], 6);
        Assert.Equal(24d, snapshot.ReelPositions["reel:2"], 6);
        Assert.Equal(25d, snapshot.SegmentMasks["alpha:10"][0]);
        Assert.Equal(.25d, snapshot.SegmentBrightness["alpha:10"][15]);
        Assert.Equal(0x3f, snapshot.SegmentMasks["sevenSegment:20"][0]);
        Assert.Equal(0, snapshot.SegmentMasks["sevenSegment:20"][1]);
        Assert.Equal(1, snapshot.VfdDots["panel-dots"][0]);
        Assert.Equal(0, snapshot.VfdDots["panel-dots"][1]);
        Assert.Equal(FruitMachinePlatformType.Impact, snapshot.Platform);
        Assert.Equal(1, outputs.LampCount);
        Assert.Equal(1, outputs.ReelCount);
        Assert.Equal(17, outputs.SegmentCount);
    }

    [Fact]
    public void AmberConventionAndAuthoredReelDirectionUseWpfPolicies()
    {
        var backend = new EventBackend();
        var machine = CreateMachine(FruitMachinePlatformType.Epoch, steps: 48);
        using var outputs = new NativePreviewOutputs(backend, machine);
        var panel = new Panel2DDocumentModel
        {
            Elements = [new PanelElementModel { ObjectId = "reel", Kind = PanelElementKind.Reel, DisplayNumber = 0, Stops = 16, IsReversed = true, BandOffset = .125 }]
        };
        backend.EmitReel(0, 12, ReelPositionConvention.Amber);

        var snapshot = outputs.Capture(panel, null);
        Assert.Equal(72d, snapshot.ReelPositions["reel:0"], 6);
        Assert.Equal(84d, snapshot.ReelPositions["reel"], 6);
    }

    [Fact]
    public void ClearDiscardsStateAndInvalidDotMatrixEventsAreIgnored()
    {
        var backend = new EventBackend();
        using var outputs = new NativePreviewOutputs(backend, CreateMachine(FruitMachinePlatformType.Impact));
        backend.EmitLamp(3, 1);
        backend.EmitLamp(-1, 255);
        backend.EmitDots(2, 8, 8, new int[64], 1);
        Assert.Equal(2, outputs.LampCount); // Signed IDs are preserved; the authored panel below selects only lamp 3.
        outputs.Clear();

        var snapshot = outputs.Capture(new Panel2DDocumentModel { Elements = [Element("lamp", PanelElementKind.Lamp, 3)] }, null);
        Assert.Empty(snapshot.LampIntensities);
        Assert.Empty(snapshot.VfdDots);
        Assert.Equal(0, outputs.LampCount);
    }

    [Fact]
    public void DisposeUnsubscribesAndCaptureRejectsDisposedCollector()
    {
        var backend = new EventBackend();
        var outputs = new NativePreviewOutputs(backend, CreateMachine(FruitMachinePlatformType.Impact));
        outputs.Dispose();
        backend.EmitLamp(1, 255);
        Assert.Equal(0, outputs.LampCount);
        Assert.Throws<ObjectDisposedException>(() => outputs.Capture(null, null));
    }

    [Fact]
    public void Mpu3AndM1ReelsUseTheirMachineScopedStepCounts()
    {
        foreach (var platform in new[] { FruitMachinePlatformType.MPU3, FruitMachinePlatformType.MaygayM1 })
        {
            var machine = CreateMachine(platform, steps: 48);
            var backend = new EventBackend();
            using var outputs = new NativePreviewOutputs(backend, machine);
            backend.EmitReel(0, 24, ReelPositionConvention.Oasis);
            var snapshot = outputs.Capture(null, new FaceDocumentModel
            {
                Elements = [new FaceReelMount { ObjectId = "face-reel", LinkedMachineObjectReference = MachineObjectReference.Reel(0) }]
            });
            Assert.Equal(48d, snapshot.ReelPositions["reel:0"], 6);
        }
    }

    private static MachineDocument CreateMachine(FruitMachinePlatformType platform, int? steps = null)
    {
        object settings = platform switch
        {
            FruitMachinePlatformType.Epoch => new EpochNativeRomSettings
            {
                Reels = [new EpochReelSettings { ReelIndex = 0, Steps = steps ?? 96 }]
            },
            FruitMachinePlatformType.MPU3 => new Mpu3ProjectSettings
            {
                Reels = [new Mpu3ReelSettings { ReelIndex = 0, Steps = steps ?? 96 }]
            },
            FruitMachinePlatformType.MaygayM1 => new M1ProjectSettings
            {
                Reels = [new M1ReelSettings { ReelIndex = 0, Steps = steps ?? 96 }]
            },
            _ => new System6NativeRomSettings
            {
                ReelOptos = [new System6ReelOptoSettings { ReelIndex = 0, Steps = steps ?? 96 }, new() { ReelIndex = 2, Steps = steps ?? 96 }]
            }
        };
        return MachineDocument.Create("Preview test") with { Runtime = new EmulationRuntimeDefinition(platform, settings) };
    }

    private static PanelElementModel Element(string id, PanelElementKind kind, int displayNumber)
        => new() { ObjectId = id, Kind = kind, DisplayNumber = displayNumber };

    private sealed class EventBackend : IEmulationBackend
    {
        public EmulationBackendKind BackendKind => EmulationBackendKind.Fabric;
        public EmulationBackendState State => EmulationBackendState.Running;
        public EmulationBackendCapabilities Capabilities { get; } = new(false, false, false, false, false, false);
        public bool IsThrottleEnabled => false;
        public event EventHandler<EmulationBackendState>? StateChanged;
        public event EventHandler<MachineLampChangedEventArgs>? LampChanged;
        public event EventHandler<MachineReelChangedEventArgs>? ReelChanged;
        public event EventHandler<MachineSegmentChangedEventArgs>? SegmentChanged;
        public event EventHandler<MachineVfdBrightnessChangedEventArgs>? VfdBrightnessChanged;
        public event EventHandler<MachineDotMatrixChangedEventArgs>? DotMatrixChanged;
        public void EmitLamp(int id, int value) => LampChanged?.Invoke(this, new(id, value));
        public void EmitReel(int id, int position, ReelPositionConvention convention) => ReelChanged?.Invoke(this, new(id, position, convention));
        public void EmitSegment(int id, int mask, SegmentOutputType type) => SegmentChanged?.Invoke(this, new(id, mask, type));
        public void EmitBrightness(int id, double brightness) => VfdBrightnessChanged?.Invoke(this, new(id, brightness));
        public void EmitDots(int id, int width, int height, IReadOnlyList<int> dots, double brightness) => DotMatrixChanged?.Invoke(this, new(id, width, height, dots, brightness));
        public Task StartAsync(EmulationLaunchRequest request, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task PauseAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task ResumeAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task SetThrottleEnabledAsync(bool enabled, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task ResetAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task SetInputStateAsync(InputDefinitionModel inputDefinition, bool isPressed, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<CoinInputResult> InsertCoinAsync(InputDefinitionModel inputDefinition, CancellationToken cancellationToken) => Task.FromResult(CoinInputResult.Accepted);
        public Task ReleaseCoinAsync(InputDefinitionModel inputDefinition, CancellationToken cancellationToken) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
