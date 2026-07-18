using System.ComponentModel;
using System.Diagnostics;
using System.IO;

namespace OasisEditor;

public interface IOasisPlayerProcessStarter
{
    void Start(ProcessStartInfo startInfo);
}

public sealed class OasisPlayerProcessStarter : IOasisPlayerProcessStarter
{
    public void Start(ProcessStartInfo startInfo)
    {
        Process.Start(startInfo);
    }
}

public sealed class OasisPlayerLaunchService
{
    public const int DefaultPreviewWidth = 1280;
    public const int DefaultPreviewHeight = 800;

    private readonly IOasisPlayerProcessStarter _processStarter;

    public OasisPlayerLaunchService(IOasisPlayerProcessStarter? processStarter = null)
    {
        _processStarter = processStarter ?? new OasisPlayerProcessStarter();
    }

    public OasisPlayerLaunchResult Launch(OasisPlayerLaunchRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validationError = Validate(request);
        if (validationError is not null)
        {
            return OasisPlayerLaunchResult.Fail(validationError);
        }

        try
        {
            var startInfo = CreateStartInfo(request);
            _processStarter.Start(startInfo);
            return OasisPlayerLaunchResult.Ok(startInfo.FileName, Path.GetFullPath(request.BuildRoot), startInfo.ArgumentList.ToArray());
        }
        catch (Exception ex) when (ex is Win32Exception or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return OasisPlayerLaunchResult.Fail($"Failed to launch Oasis Player: {ex.Message}");
        }
    }

    public static ProcessStartInfo CreateStartInfo(OasisPlayerLaunchRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var startInfo = new ProcessStartInfo
        {
            FileName = Path.GetFullPath(request.ExecutablePath),
            UseShellExecute = false
        };

        startInfo.ArgumentList.Add("--mode");
        startInfo.ArgumentList.Add("machine-preview");
        startInfo.ArgumentList.Add("--build");
        startInfo.ArgumentList.Add(Path.GetFullPath(request.BuildRoot));
        startInfo.ArgumentList.Add(request.Fullscreen ? "--fullscreen" : "--windowed");
        startInfo.ArgumentList.Add("--width");
        startInfo.ArgumentList.Add(request.Width.ToString(System.Globalization.CultureInfo.InvariantCulture));
        startInfo.ArgumentList.Add("--height");
        startInfo.ArgumentList.Add(request.Height.ToString(System.Globalization.CultureInfo.InvariantCulture));

        return startInfo;
    }

    public static string? Validate(OasisPlayerLaunchRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ExecutablePath))
        {
            return "The Oasis Player executable has not been configured. Set it under Preferences > Player.";
        }

        if (request.Width <= 0)
        {
            return "Oasis Player preview width must be greater than zero.";
        }

        if (request.Height <= 0)
        {
            return "Oasis Player preview height must be greater than zero.";
        }

        if (Directory.Exists(request.ExecutablePath))
        {
            return $"The configured Oasis Player path points to a directory, not an executable: {request.ExecutablePath}";
        }

        if (!File.Exists(request.ExecutablePath))
        {
            return $"The configured Oasis Player executable does not exist: {request.ExecutablePath}";
        }

        if (OperatingSystem.IsWindows() && !string.Equals(Path.GetExtension(request.ExecutablePath), ".exe", StringComparison.OrdinalIgnoreCase))
        {
            return $"The configured Oasis Player path must be a Windows .exe file: {request.ExecutablePath}";
        }

        if (string.IsNullOrWhiteSpace(request.BuildRoot))
        {
            return "The Oasis Player build directory was not provided.";
        }

        return null;
    }
}

public sealed record OasisPlayerLaunchRequest(string ExecutablePath, string BuildRoot, bool Fullscreen, int Width, int Height);

public sealed record OasisPlayerLaunchResult(bool Success, string? ExecutablePath, string? BuildRoot, IReadOnlyList<string> Arguments, string? ErrorMessage)
{
    public static OasisPlayerLaunchResult Ok(string executablePath, string buildRoot, IReadOnlyList<string> arguments) => new(true, executablePath, buildRoot, arguments, null);
    public static OasisPlayerLaunchResult Fail(string errorMessage) => new(false, null, null, Array.Empty<string>(), errorMessage);
}
