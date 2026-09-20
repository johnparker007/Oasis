using System.IO;

namespace OasisEditor.Features.MachineEditor.Services;

public static class MachineAssetDiscovery
{
    public const string MachineAssetFolderName = "Machines";
    public const string MachineManifestFileName = "asset.machine";

    public static IReadOnlyList<string> EnumerateMachineManifestPaths(EditorProject project)
    {
        ArgumentNullException.ThrowIfNull(project);

        var machinesRoot = Path.Combine(project.AssetsDirectory, MachineAssetFolderName);
        if (!Directory.Exists(machinesRoot))
        {
            return [];
        }

        var pathService = new ProjectAssetPathService();
        return Directory.EnumerateFiles(machinesRoot, MachineManifestFileName, SearchOption.AllDirectories)
            .Select(path => pathService.ToProjectRelativePath(project, path))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
