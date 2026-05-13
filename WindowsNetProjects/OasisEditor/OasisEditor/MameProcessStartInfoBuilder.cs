using System.Diagnostics;
using System.IO;
using System.Text;

namespace OasisEditor;

public sealed class MameProcessStartInfoBuilder : IMameProcessStartInfoBuilder
{
    public ProcessStartInfo Build(MameProcessLaunchRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.MameExecutablePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.MameRomName);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.MameRomRootPath);
        var arguments = new StringBuilder();
        var effectiveRomPath = BuildRomPathArgument(request.MameExecutablePath, request.MameRomRootPath);

        arguments.Append("-rompath ");
        arguments.Append(EscapeArgument(effectiveRomPath));
        arguments.Append(' ');
        arguments.Append(EscapeArgument(request.MameRomName));
        arguments.Append(" -output console");
        arguments.Append(" -plugin oasis");
        arguments.Append(" -skip_gameinfo");
        arguments.Append(" -video none");
        arguments.Append(" -seconds_to_run 999999999");

        if (!string.IsNullOrWhiteSpace(request.AdditionalArguments))
        {
            arguments.Append(' ');
            arguments.Append(request.AdditionalArguments.Trim());
        }

        return new ProcessStartInfo
        {
            FileName = request.MameExecutablePath,
            Arguments = arguments.ToString(),
            WorkingDirectory = Path.GetDirectoryName(request.MameExecutablePath) ?? string.Empty,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };
    }

    private static string EscapeArgument(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "\"\"";
        }

        return value.Contains(' ') || value.Contains('"')
            ? $"\"{value.Replace("\"", "\\\"")}\""
            : value;
    }

    private static string BuildRomPathArgument(string executablePath, string managedRomRootPath)
    {
        var executableDirectory = Path.GetDirectoryName(executablePath) ?? string.Empty;
        var localRomDirectory = string.IsNullOrWhiteSpace(executableDirectory)
            ? string.Empty
            : Path.Combine(executableDirectory, "roms");

        if (string.IsNullOrWhiteSpace(localRomDirectory))
        {
            return managedRomRootPath;
        }

        return string.Join(';', new[] { managedRomRootPath, localRomDirectory });
    }
}
