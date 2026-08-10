namespace OasisEditor;

public interface IEmulationBackend : IAsyncDisposable
{
    EmulationBackendKind BackendKind { get; }

    EmulationBackendState State { get; }

    EmulationBackendCapabilities Capabilities { get; }

    event EventHandler<EmulationBackendState>? StateChanged;

    event EventHandler<MachineLampChangedEventArgs>? LampChanged;
    event EventHandler<MachineReelChangedEventArgs>? ReelChanged;
    event EventHandler<MachineSegmentChangedEventArgs>? SegmentChanged;
    event EventHandler<MachineVfdBrightnessChangedEventArgs>? VfdBrightnessChanged;
    event EventHandler<MachineDotMatrixChangedEventArgs>? DotMatrixChanged;

    Task StartAsync(EmulationLaunchRequest request, CancellationToken cancellationToken);
    Task StopAsync(CancellationToken cancellationToken);

    Task PauseAsync(CancellationToken cancellationToken);
    Task ResumeAsync(CancellationToken cancellationToken);

    Task ResetAsync(EmulationResetKind resetKind, CancellationToken cancellationToken);

    Task SetInputStateAsync(InputDefinitionModel inputDefinition, bool isPressed, CancellationToken cancellationToken);
    Task<CoinInputResult> InsertCoinAsync(InputDefinitionModel inputDefinition, CancellationToken cancellationToken);
    Task ReleaseCoinAsync(InputDefinitionModel inputDefinition, CancellationToken cancellationToken);
}

public enum CoinInputResult { Accepted, Rejected }

public enum EmulationBackendKind
{
    Fabric
}

public enum EmulationBackendState
{
    Stopped,
    Starting,
    Running,
    Paused,
    Stopping,
    Failed
}

public enum EmulationResetKind
{
    Soft,
    Hard
}

public sealed record EmulationBackendCapabilities(
    bool SupportsPause,
    bool SupportsResume,
    bool SupportsSoftReset,
    bool SupportsHardReset,
    bool SupportsSaveState,
    bool SupportsLoadState,
    bool SupportsThrottle);

public sealed record EmulationLaunchRequest(
    FruitMachinePlatformType Platform,
    System6NativeRomSettings? System6Configuration,
    Mpu5NativeRomSettings? Mpu5Configuration,
    EpochNativeRomSettings? EpochConfiguration,
    Mpu3ProjectSettings? Mpu3Configuration,
    M1ProjectSettings? M1Configuration,
    IReadOnlyList<int>? ConfiguredLampIds = null,
    IReadOnlyList<int>? ConfiguredSevenSegmentDisplayIds = null)
{
    public EmulationLaunchRequest(System6NativeRomSettings settings, IReadOnlyList<int>? lamps = null, IReadOnlyList<int>? segments = null)
        : this(FruitMachinePlatformType.Impact, settings, null, null, null, null, lamps, segments) { }

    public static EmulationLaunchRequest ForMpu5(Mpu5NativeRomSettings settings, IReadOnlyList<int>? lamps = null, IReadOnlyList<int>? segments = null) =>
        new(FruitMachinePlatformType.MPU5, null, settings, null, null, null, lamps, segments);

    public static EmulationLaunchRequest ForEpoch(EpochNativeRomSettings settings, IReadOnlyList<int>? lamps = null, IReadOnlyList<int>? segments = null) =>
        new(FruitMachinePlatformType.Epoch, null, null, settings, null, null, lamps, segments);

    public static EmulationLaunchRequest ForMpu3(Mpu3ProjectSettings settings, IReadOnlyList<int>? lamps = null, IReadOnlyList<int>? segments = null) =>
        new(FruitMachinePlatformType.MPU3, null, null, null, settings, null, lamps, segments);
    public static EmulationLaunchRequest ForM1(M1ProjectSettings settings, IReadOnlyList<int>? lamps=null, IReadOnlyList<int>? segments=null) =>
        new(FruitMachinePlatformType.MaygayM1, null, null, null, null, settings, lamps, segments);
}

public enum SegmentOutputType
{
    Digit,
    Vfd,
    NativeAlpha
}

public sealed class MachineLampChangedEventArgs : EventArgs
{
    public MachineLampChangedEventArgs(int lampId, int value)
    {
        LampId = lampId;
        Value = value;
    }

    public int LampId { get; }
    public int Value { get; }
}

public sealed class MachineReelChangedEventArgs : EventArgs
{
    public MachineReelChangedEventArgs(int reelId, int position, ReelPositionConvention convention = ReelPositionConvention.Oasis)
    {
        ReelId = reelId;
        Position = position;
        Convention = convention;
    }

    public int ReelId { get; }
    public int Position { get; }
    public ReelPositionConvention Convention { get; }
}

public enum ReelPositionConvention
{
    Oasis,
    Amber
}

public sealed class MachineSegmentChangedEventArgs : EventArgs
{
    /// <param name="cellId">For digit output, the dense zero-based Oasis digit-cell ID. For alpha output, the flattened character-cell ID.</param>
    public MachineSegmentChangedEventArgs(int cellId, int segmentMask, SegmentOutputType outputType)
    {
        CellId = cellId;
        SegmentMask = segmentMask;
        OutputType = outputType;
    }

    public int CellId { get; }
    public int SegmentMask { get; }
    public SegmentOutputType OutputType { get; }
}

public sealed class MachineVfdBrightnessChangedEventArgs : EventArgs
{
    public MachineVfdBrightnessChangedEventArgs(int cellId, double normalizedBrightness)
    {
        CellId = cellId;
        NormalizedBrightness = normalizedBrightness;
    }

    public int CellId { get; }
    public double NormalizedBrightness { get; }
}

public sealed class MachineDotMatrixChangedEventArgs : EventArgs
{
    public MachineDotMatrixChangedEventArgs(int dotIndex, int value)
    {
        DotIndex = dotIndex;
        Value = value;
    }

    public int DotIndex { get; }
    public int Value { get; }
}
