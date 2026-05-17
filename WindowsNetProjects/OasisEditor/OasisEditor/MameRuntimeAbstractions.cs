using System.Diagnostics;

namespace OasisEditor;

public interface IMameEmulationService
{
    MameEmulationState State { get; }
    event EventHandler<MameEmulationState>? StateChanged;
    Task StartAsync(CancellationToken cancellationToken);
    Task StopAsync(CancellationToken cancellationToken);
    Task PauseAsync(CancellationToken cancellationToken);
    Task ResumeAsync(CancellationToken cancellationToken);
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
}

public sealed record MameProcessLaunchRequest(
    string MameExecutablePath,
    string MameRomName,
    string MameRomRootPath,
    string OasisPluginPath,
    string AdditionalArguments);
