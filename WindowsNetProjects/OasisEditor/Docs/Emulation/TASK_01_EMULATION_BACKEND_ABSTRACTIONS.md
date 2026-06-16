Task 01 - Emulation Backend Abstractions
Goal

Introduce backend-neutral emulation abstractions without changing existing runtime behaviour.

This task sets up the shape that both MAME and native DLL cores will eventually implement.

Scope

Create neutral interfaces and models.

Do not implement a real backend in this task.

Do not modify MAME behaviour.

Do not alter the MAME Lua protocol.

Do not add the native DLL wrapper yet.

Required Types

Create types in the main OasisEditor project unless there is already a better established location.

Suggested file:

OasisEditor/EmulationBackendAbstractions.cs

or a small folder such as:

OasisEditor/Emulation/

Use existing project conventions.

IEmulationBackend

Represents a backend-neutral emulation runtime.

Suggested shape:

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

The exact shape may be adjusted if existing code suggests a cleaner fit, but keep it backend-neutral.

EmulationBackendState

Suggested states:

public enum EmulationBackendState
{
    Stopped,
    Starting,
    Running,
    Paused,
    Stopping,
    Failed
}
EmulationResetKind

Suggested values:

public enum EmulationResetKind
{
    Soft,
    Hard
}
EmulationBackendCapabilities

Suggested record:

public sealed record EmulationBackendCapabilities(
    bool SupportsPause,
    bool SupportsResume,
    bool SupportsSoftReset,
    bool SupportsHardReset,
    bool SupportsSaveState,
    bool SupportsLoadState,
    bool SupportsThrottle,
    bool SupportsDebugger);

These are expected to differ between MAME and native DLL cores.

EmulationLaunchRequest

Backend-neutral launch request.

Suggested fields:

public sealed record EmulationLaunchRequest(
    FruitMachinePlatformType Platform,
    string MachineName,
    string RomRootPath,
    IReadOnlyList<string> RomPaths,
    string AdditionalArguments);

MAME may use MachineName, RomRootPath, and AdditionalArguments.

Native DLL cores may use RomPaths.

If an existing project machine configuration object already contains some of this information, do not duplicate unnecessarily. Keep this request thin.

MachineInputReference

Represents a backend-neutral input.

Suggested shape:

public sealed record MachineInputReference(
    string Id,
    int? Index = null,
    string? Tag = null,
    int? Mask = null);

MAME can use tag/mask information.

Native DLL cores may use index or logical input ID.

Runtime Event Args

Create lightweight event args for incremental state updates.

Suggested types:

public sealed class MachineLampChangedEventArgs : EventArgs
{
    public MachineLampChangedEventArgs(int lampId, int value) { ... }

    public int LampId { get; }
    public int Value { get; }
}
public sealed class MachineReelChangedEventArgs : EventArgs
{
    public MachineReelChangedEventArgs(int reelId, int position) { ... }

    public int ReelId { get; }
    public int Position { get; }
}
public sealed class MachineSegmentChangedEventArgs : EventArgs
{
    public MachineSegmentChangedEventArgs(int cellId, int segmentMask, MameSegmentOutputType outputType) { ... }

    public int CellId { get; }
    public int SegmentMask { get; }
    public MameSegmentOutputType OutputType { get; }
}
public sealed class MachineVfdBrightnessChangedEventArgs : EventArgs
{
    public MachineVfdBrightnessChangedEventArgs(int cellId, double normalizedBrightness) { ... }

    public int CellId { get; }
    public double NormalizedBrightness { get; }
}
public sealed class MachineDotMatrixChangedEventArgs : EventArgs
{
    public MachineDotMatrixChangedEventArgs(int dotIndex, int value) { ... }

    public int DotIndex { get; }
    public int Value { get; }
}

If MameSegmentOutputType feels too MAME-specific at the abstraction boundary, introduce a neutral enum and map MAME onto it. However, avoid unnecessary churn in Task 01.

Testing

Add simple tests if appropriate.

Suggested tests:

EmulationBackendCapabilities can represent a MAME-like backend.
EmulationLaunchRequest stores platform and ROM information.
Event args preserve constructor values.

Avoid over-testing trivial records if the project convention does not do that.

Success Criteria
Project compiles.
Existing tests pass.
No runtime behaviour changes.
No MAME implementation changes except possibly namespace/import compatibility if necessary.
New abstractions are backend-neutral and do not mention MAME except where unavoidable for existing segment output types.
Deliverable Summary

When complete, report:

Files added
Files modified
Tests added
Any deviations from this task spec
Recommended next task
