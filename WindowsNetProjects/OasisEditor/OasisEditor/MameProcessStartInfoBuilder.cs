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
        arguments.Append(EscapeArgument(request.MameRomName));
        arguments.Append(" -rompath ");
        arguments.Append(EscapeArgument(request.MameRomRootPath));
        if (request.IsDebuggerEnabled)
        {
            arguments.Append(" -debug");

            if (!string.IsNullOrWhiteSpace(request.DebuggerScriptPath))
            {
                EnsureDebuggerStartupScript(request.DebuggerScriptPath);
                arguments.Append(" -debugscript ");
                arguments.Append(EscapeArgument(request.DebuggerScriptPath));
            }
        }

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

    private static void EnsureDebuggerStartupScript(string scriptPath)
    {
        var scriptDirectory = Path.GetDirectoryName(scriptPath);
        if (!string.IsNullOrWhiteSpace(scriptDirectory))
        {
            Directory.CreateDirectory(scriptDirectory);
        }

        // MAME enters the debugger after the initial reset when -debug is active.
        // Resume immediately so the emulated machine starts running until Oasis
        // explicitly sends a break/step command.
        File.WriteAllText(scriptPath, "go" + Environment.NewLine);
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

}
