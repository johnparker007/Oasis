using System.Diagnostics;

namespace OasisEditor;

public sealed class OutputLogShellLauncher : IOutputLogShellLauncher
{
    public bool TryLaunch(ProcessStartInfo startInfo, out string? failureReason)
    {
        failureReason = null;
        try
        {
            Process.Start(startInfo);
            return true;
        }
        catch (Exception ex)
        {
            failureReason = ex.Message;
            return false;
        }
    }
}
