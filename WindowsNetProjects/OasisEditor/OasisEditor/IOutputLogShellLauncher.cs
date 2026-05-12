using System.Diagnostics;

namespace OasisEditor;

public interface IOutputLogShellLauncher
{
    bool TryLaunch(ProcessStartInfo startInfo, out string? failureReason);
}
