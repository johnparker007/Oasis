namespace OasisEditor;

public sealed record AssetSummary(string Type, string Name, string ManifestPath);

public sealed partial class AuthoringSession
{
    private static readonly (EditorAssetType Type, string Folder, string Manifest)[] ProjectAssetKinds =
    [
        (EditorAssetType.Panel2D, "Panel2D", ProjectAssetPathService.Panel2DManifestFileName),
        (EditorAssetType.Face, "Faces", ProjectAssetPathService.FaceManifestFileName),
        (EditorAssetType.Cabinet3D, "Cabinet3D", ProjectAssetPathService.Cabinet3DManifestFileName),
        (EditorAssetType.Machine, "Machines", ProjectAssetPathService.MachineManifestFileName),
        (EditorAssetType.Reel, "Reels", ProjectAssetPathService.ReelManifestFileName),
        (EditorAssetType.Object3D, "Object3D", ProjectAssetPathService.Object3DManifestFileName)
    ];

    /// <summary>Creates the full standard Oasis project in this session's workspace and opens it.</summary>
    public string CreateProject(string name)
    {
        if (HasRetainedChanges) throw new InvalidOperationException("Save or undo all Panel edits before changing project.");
        EnsureCanReplaceDocument();
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var projectName = name.Trim();
        if (projectName is "." or ".." || Path.IsPathRooted(projectName)
            || projectName.Contains('/') || projectName.Contains('\\')
            || projectName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            throw new ArgumentException("Project name must be a single safe folder name.", nameof(name));

        WorkspacePathPolicy.RejectRootReparsePoints(_workspacePath, requireExists: true);
        var projectDirectory = WorkspacePathPolicy.ResolveConfined(_workspacePath, projectName);
        if (Directory.Exists(projectDirectory) || File.Exists(projectDirectory))
            throw new InvalidOperationException($"A project named '{projectName}' already exists in this workspace.");

        var createdDirectory = new ProjectScaffolder().CreateProject(projectName, _workspacePath);
        try
        {
            var projectFile = Path.Combine(createdDirectory, projectName + ".oasisproj");
            OpenProject(projectFile);
            return ProjectFilePath!;
        }
        catch
        {
            if (string.Equals(ProjectFilePath, Path.Combine(createdDirectory, projectName + ".oasisproj"),
                    OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
                throw;
            var relative = Path.GetRelativePath(_workspacePath, createdDirectory);
            var ownedDirectory = WorkspacePathPolicy.ResolveConfined(_workspacePath, relative, requireExists: true);
            Directory.Delete(ownedDirectory, recursive: true);
            throw;
        }
    }

    /// <summary>Lists manifests found in the standard project asset package folders.</summary>
    public IReadOnlyList<AssetSummary> ListAssets()
    {
        var root = RequireProject();
        var results = new List<AssetSummary>();
        foreach (var (type, folder, manifestName) in ProjectAssetKinds)
        {
            var typeRootRelative = Path.Combine("Assets", folder);
            var typeRoot = WorkspacePathPolicy.ResolveConfined(root, typeRootRelative);
            if (!Directory.Exists(typeRoot)) continue;
            WorkspacePathPolicy.RejectReparsePoints(root, typeRootRelative);

            foreach (var packageDirectory in Directory.EnumerateDirectories(typeRoot))
            {
                var packageName = Path.GetFileName(packageDirectory);
                var relativeManifest = Path.Combine(typeRootRelative, packageName, manifestName);
                var manifest = WorkspacePathPolicy.ResolveConfined(root, relativeManifest);
                if (!File.Exists(manifest)) continue;
                WorkspacePathPolicy.RejectReparsePoints(root, relativeManifest, requireExists: true);
                results.Add(new(type.ToString(), packageName, ProjectAssetPathService.NormalizeProjectRelativePath(relativeManifest)));
            }
        }
        return results.OrderBy(asset => asset.Type, StringComparer.Ordinal)
            .ThenBy(asset => asset.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(asset => asset.ManifestPath, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
