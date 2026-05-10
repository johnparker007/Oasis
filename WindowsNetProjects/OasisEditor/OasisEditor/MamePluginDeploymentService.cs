using System.IO;

namespace OasisEditor;

public sealed class MamePluginDeploymentService
{
    public int SyncPluginFiles(string pluginSourceDirectory, string mameExecutablePath)
    {
        if (string.IsNullOrWhiteSpace(pluginSourceDirectory) || !Directory.Exists(pluginSourceDirectory))
        {
            throw new DirectoryNotFoundException($"Plugin source directory not found: {pluginSourceDirectory}");
        }

        if (string.IsNullOrWhiteSpace(mameExecutablePath) || !File.Exists(mameExecutablePath))
        {
            throw new FileNotFoundException("MAME executable not found.", mameExecutablePath);
        }

        var mameRoot = Path.GetDirectoryName(mameExecutablePath);
        if (string.IsNullOrWhiteSpace(mameRoot))
        {
            throw new InvalidOperationException("Unable to resolve MAME installation directory from executable path.");
        }

        var destinationRoot = Path.Combine(mameRoot, "plugins", "oasis");
        Directory.CreateDirectory(destinationRoot);

        var files = Directory.GetFiles(pluginSourceDirectory, "*", SearchOption.AllDirectories);
        var copiedCount = 0;

        foreach (var sourceFile in files)
        {
            var relativePath = Path.GetRelativePath(pluginSourceDirectory, sourceFile);
            var destinationFile = Path.Combine(destinationRoot, relativePath);
            var destinationDirectory = Path.GetDirectoryName(destinationFile);
            if (!string.IsNullOrWhiteSpace(destinationDirectory))
            {
                Directory.CreateDirectory(destinationDirectory);
            }

            File.Copy(sourceFile, destinationFile, overwrite: true);
            copiedCount++;
        }

        return copiedCount;
    }
}
