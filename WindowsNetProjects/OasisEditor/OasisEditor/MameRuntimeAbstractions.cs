using System.Diagnostics;

namespace OasisEditor;

public interface IMameEmulationService
{
    MameEmulationState State { get; }
    event EventHandler<MameEmulationState>? StateChanged;
    Task StartAsync(CancellationToken cancellationToken);
    Task StartAndLoadStateAsync(CancellationToken cancellationToken);
    Task StartDebuggerAsync(CancellationToken cancellationToken);
    Task StartDebuggerAndLoadStateAsync(CancellationToken cancellationToken);
    Task StopAsync(CancellationToken cancellationToken);
    Task SaveStateAndExitAsync(CancellationToken cancellationToken);
    Task LoadStateAsync(CancellationToken cancellationToken);
    Task SaveStateAsync(CancellationToken cancellationToken);
    Task PauseAsync(CancellationToken cancellationToken);
    Task ResumeAsync(CancellationToken cancellationToken);
    Task SetThrottleAsync(bool isThrottled, CancellationToken cancellationToken);
    Task SoftResetAsync(CancellationToken cancellationToken);
    Task HardResetAsync(CancellationToken cancellationToken);
}

public interface IMameProcessRunner
{
    Task StartAsync(ProcessStartInfo startInfo, CancellationToken cancellationToken);
    Task StopAsync(CancellationToken cancellationToken);
    Task WriteStandardInputAsync(string command, CancellationToken cancellationToken);
}

public interface IMameProcessStartInfoBuilder
{
    ProcessStartInfo Build(MameProcessLaunchRequest request);
}

public interface IMameStdoutParser
{
    void ProcessLine(string line);
}

public interface IMameLampRuntimeAdapter
{
    void ApplyLampState(int lampId, int lampValue);
}

public interface IMameReelRuntimeAdapter
{
    void ApplyReelState(int reelId, int reelValue);
}

public interface IMameSegmentRuntimeAdapter
{
    void ApplySegmentState(int cellId, int segmentMask, MameSegmentOutputType outputType);
    void ApplyVfdBrightness(int cellId, double normalizedBrightness);
}

public sealed record MameProcessLaunchRequest(
    string MameExecutablePath,
    string MameRomName,
    string MameRomRootPath,
    string OasisPluginPath,
    string AdditionalArguments,
    bool DebuggerEnabled = false);
