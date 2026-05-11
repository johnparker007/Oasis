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
        ArgumentException.ThrowIfNullOrWhiteSpace(request.OasisPluginPath);

        var arguments = new StringBuilder();
        arguments.Append(EscapeArgument(request.MameRomName));
        arguments.Append(" -rompath ");
        arguments.Append(EscapeArgument(request.MameRomRootPath));
        arguments.Append(" -plugin oasis");
        arguments.Append(" -plugins_path ");
        arguments.Append(EscapeArgument(request.OasisPluginPath));

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
}
