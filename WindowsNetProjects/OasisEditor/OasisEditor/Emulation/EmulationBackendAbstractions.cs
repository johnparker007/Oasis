namespace OasisEditor;

public interface IEmulationBackend : IAsyncDisposable
{
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

    Task SetInputStateAsync(MachineInputReference input, bool isPressed, CancellationToken cancellationToken);
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
    bool SupportsThrottle,
    bool SupportsDebugger);

public sealed record EmulationLaunchRequest(
    FruitMachinePlatformType Platform,
    string MachineName,
    string RomRootPath,
    IReadOnlyList<string> RomPaths,
    string AdditionalArguments);

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
    public MachineReelChangedEventArgs(int reelId, int position)
    {
        ReelId = reelId;
        Position = position;
    }

    public int ReelId { get; }
    public int Position { get; }
}

public sealed class MachineSegmentChangedEventArgs : EventArgs
{
    public MachineSegmentChangedEventArgs(int cellId, int segmentMask, MameSegmentOutputType outputType)
    {
        CellId = cellId;
        SegmentMask = segmentMask;
        OutputType = outputType;
    }

    public int CellId { get; }
    public int SegmentMask { get; }
    public MameSegmentOutputType OutputType { get; }
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
