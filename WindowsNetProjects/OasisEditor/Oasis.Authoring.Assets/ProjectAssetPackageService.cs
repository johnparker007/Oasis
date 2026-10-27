using System.Text;
using System.Security.Cryptography;
using System.Text.Json;

namespace OasisEditor.Authoring.Assets;

public sealed record ProjectAssetPackageResult(bool Success, string? ManifestPath, string? RecoveryPath, string? Error)
{
    public static ProjectAssetPackageResult Failed(string error) => new(false, null, null, error);
}

/// <summary>Safe filesystem operations for saved, project-owned asset packages.</summary>
public static class ProjectAssetPackageService
{
    private static readonly (EditorAssetType Type, string Manifest)[] Types =
    [
        (EditorAssetType.Panel2D, ProjectAssetPathService.Panel2DManifestFileName),
        (EditorAssetType.Face, ProjectAssetPathService.FaceManifestFileName),
        (EditorAssetType.Cabinet3D, ProjectAssetPathService.Cabinet3DManifestFileName),
        (EditorAssetType.Machine, ProjectAssetPathService.MachineManifestFileName),
        (EditorAssetType.Reel, ProjectAssetPathService.ReelManifestFileName),
        (EditorAssetType.Object3D, ProjectAssetPathService.Object3DManifestFileName)
    ];

    public static ProjectAssetPackageResult Rename(EditorProject project, string manifestPath, string requestedName, string? expectedFingerprint = null)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestPath);
        try
        {
            var package = ResolvePackage(project, manifestPath, out var type);
            EnsureFingerprint(project, package, expectedFingerprint);
            var name = new ProjectAssetPathService().SanitizePathSegment(requestedName);
            var parent = Path.GetDirectoryName(package)!;
            var destination = Path.Combine(parent, name);
            if (SamePath(package, destination))
                return new(true, Path.Combine(package, type.Manifest), null, null);
            if (Directory.Exists(destination) || File.Exists(destination))
                throw new IOException($"An asset package named '{name}' already exists.");
            EnsureNoProjectReferences(project, package, type.Manifest);

            var stage = Path.Combine(parent, ".oasis-asset-rename-" + Guid.NewGuid().ToString("N"));
            Directory.Move(package, stage);
            try
            {
                if (Directory.Exists(destination) || File.Exists(destination))
                    throw new IOException($"An asset package named '{name}' already exists.");
                Directory.Move(stage, destination);
            }
            catch (Exception renameError)
            {
                if (Directory.Exists(stage) && !Directory.Exists(package))
                {
                    try { Directory.Move(stage, package); }
                    catch (Exception restoreError) when (IsExpected(restoreError))
                    { throw new IOException($"Rename failed and the package could not be restored. Its contents are preserved at '{stage}'.", new AggregateException(renameError, restoreError)); }
                }
                throw;
            }
            return new(true, Path.Combine(destination, type.Manifest), null, null);
        }
        catch (Exception ex) when (IsExpected(ex)) { return ProjectAssetPackageResult.Failed(ex.Message); }
    }

    public static ProjectAssetPackageResult MoveToTrash(EditorProject project, string manifestPath, string? expectedFingerprint = null)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestPath);
        try
        {
            var package = ResolvePackage(project, manifestPath, out var type);
            EnsureFingerprint(project, package, expectedFingerprint);
            EnsureNoProjectReferences(project, package, type.Manifest, includeSelf: false);
            var generatedRelative = Path.GetRelativePath(project.ProjectDirectory, project.GeneratedDirectory);
            var trashRelative = Path.Combine(generatedRelative, "Trash");
            var trashRoot = WorkspacePathPolicy.ResolveConfined(project.ProjectDirectory, trashRelative);
            WorkspacePathPolicy.RejectRootReparsePoints(project.ProjectDirectory, requireExists: true);
            Directory.CreateDirectory(trashRoot);
            WorkspacePathPolicy.RejectReparsePoints(project.ProjectDirectory, trashRelative, requireExists: true);
            var bucket = Path.Combine(trashRoot, DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(bucket);
            var destination = Path.Combine(bucket, Path.GetFileName(package));
            Directory.Move(package, destination);
            return new(true, null, destination, null);
        }
        catch (Exception ex) when (IsExpected(ex)) { return ProjectAssetPackageResult.Failed(ex.Message); }
    }

    public static string GetFingerprint(EditorProject project, string manifestPath)
    {
        ArgumentNullException.ThrowIfNull(project);
        var package = ResolvePackage(project, manifestPath, out _);
        return Fingerprint(package);
    }

    private static void EnsureFingerprint(EditorProject project, string package, string? expected)
    {
        if (expected is not null && !string.Equals(expected, Fingerprint(package), StringComparison.Ordinal))
            throw new IOException("The saved package changed after it was selected. Refresh the Project pane and try again.");
    }

    private static string Fingerprint(string package)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var file in EnumerateFilesWithoutLinks(package).OrderBy(path => Path.GetRelativePath(package, path), StringComparer.Ordinal))
        {
            var relative = ProjectAssetPathService.NormalizeProjectRelativePath(Path.GetRelativePath(package, file));
            hash.AppendData(Encoding.UTF8.GetBytes(relative));
            hash.AppendData([0]);
            using var stream = File.OpenRead(file);
            var buffer = new byte[81920];
            int read;
            while ((read = stream.Read(buffer, 0, buffer.Length)) > 0) hash.AppendData(buffer, 0, read);
            hash.AppendData([0]);
        }
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    private static string ResolvePackage(EditorProject project, string manifestPath, out (EditorAssetType Type, string Manifest) type)
    {
        var assets = Path.GetFullPath(project.AssetsDirectory);
        var manifest = Path.GetFullPath(manifestPath, project.ProjectDirectory);
        type = Types.FirstOrDefault(candidate => string.Equals(Path.GetFileName(manifest), candidate.Manifest, StringComparison.OrdinalIgnoreCase));
        if (type.Manifest is null) throw new InvalidOperationException("Choose a supported saved asset package manifest.");
        var relative = Path.GetRelativePath(assets, manifest);
        var confined = WorkspacePathPolicy.ResolveConfined(assets, relative, requireExists: true);
        var package = Path.GetDirectoryName(confined) ?? throw new InvalidOperationException("Asset manifest has no package directory.");
        if (!string.Equals(Path.GetFileName(Path.GetDirectoryName(package)), Folder(type.Type), PathComparison))
            throw new InvalidOperationException("The selected manifest is not inside its asset type folder.");
        WorkspacePathPolicy.RejectRootReparsePoints(assets, requireExists: true);
        var packageRelative = Path.GetRelativePath(assets, package);
        WorkspacePathPolicy.RejectReparsePoints(assets, packageRelative, requireExists: true);
        EnsureNoReparseTree(package);
        return package;
    }

    private static void EnsureNoProjectReferences(EditorProject project, string package, string manifestFile, bool includeSelf = true)
    {
        var relativeManifest = ProjectAssetPathService.NormalizeProjectRelativePath(Path.GetRelativePath(project.ProjectDirectory, Path.Combine(package, manifestFile)));
        var relativePackage = ProjectAssetPathService.NormalizeProjectRelativePath(Path.GetRelativePath(project.ProjectDirectory, package));
        var assetRelativeManifest = ProjectAssetPathService.NormalizeProjectRelativePath(Path.GetRelativePath(project.AssetsDirectory, Path.Combine(package, manifestFile)));
        var assetRelativePackage = ProjectAssetPathService.NormalizeProjectRelativePath(Path.GetRelativePath(project.AssetsDirectory, package));
        foreach (var file in EnumerateFilesWithoutLinks(project.ProjectDirectory))
        {
            var full = Path.GetFullPath(file);
            if (IsWithinOrEqual(project.GeneratedDirectory, full) || (!includeSelf && IsWithinOrEqual(package, full))) continue;
            var extension = Path.GetExtension(file);
            if (extension.Equals(".png", StringComparison.OrdinalIgnoreCase) || extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) || extension.Equals(".glb", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".bin", StringComparison.OrdinalIgnoreCase)) continue;
            string contents;
            try { contents = File.ReadAllText(file, new UTF8Encoding(false, true)); }
            catch (DecoderFallbackException) { continue; }
            if (ContainsReference(contents, relativeManifest, relativePackage, assetRelativeManifest, assetRelativePackage))
                throw new InvalidOperationException($"Package is referenced by '{Path.GetRelativePath(project.ProjectDirectory, file)}'. Remove or update that reference first.");
        }
    }

    private static bool ContainsReference(string contents, params string[] paths)
    {
        bool Matches(string value)
        {
            var normalized = value.Replace('\\', '/');
            while (normalized.Contains("//", StringComparison.Ordinal)) normalized = normalized.Replace("//", "/", StringComparison.Ordinal);
            foreach (var path in paths.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var candidate = path.TrimEnd('/');
                foreach (var delimiter in new[] { "/", "\"", "'", " ", ",", "}", "]" })
                    if (normalized.Contains(candidate + delimiter, StringComparison.OrdinalIgnoreCase)) return true;
                if (normalized.EndsWith(candidate, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        if (Matches(contents)) return true;
        try
        {
            using var document = JsonDocument.Parse(contents);
            return ContainsJsonString(document.RootElement);

            bool ContainsJsonString(JsonElement element)
            {
                if (element.ValueKind == JsonValueKind.String) return Matches(element.GetString() ?? string.Empty);
                if (element.ValueKind == JsonValueKind.Array) return element.EnumerateArray().Any(ContainsJsonString);
                if (element.ValueKind == JsonValueKind.Object) return element.EnumerateObject().Any(property => ContainsJsonString(property.Value));
                return false;
            }
        }
        catch (JsonException) { return false; }
    }

    private static IEnumerable<string> EnumerateFilesWithoutLinks(string root)
    {
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var directory = pending.Pop();
            foreach (var child in Directory.EnumerateDirectories(directory))
            {
                if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) == 0) pending.Push(child);
            }
            foreach (var file in Directory.EnumerateFiles(directory))
            {
                if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) == 0) yield return file;
            }
        }
    }

    private static void EnsureNoReparseTree(string root)
    {
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var directory = pending.Pop();
            var info = new DirectoryInfo(directory);
            if ((info.Attributes & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Asset packages cannot contain symbolic links or reparse points.");
            foreach (var child in Directory.EnumerateDirectories(directory)) pending.Push(child);
            foreach (var file in Directory.EnumerateFiles(directory))
                if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Asset packages cannot contain symbolic links or reparse points.");
        }
    }

    private static string Folder(EditorAssetType type) => type switch
    {
        EditorAssetType.Panel2D => "Panel2D", EditorAssetType.Face => "Faces", EditorAssetType.Cabinet3D => "Cabinet3D",
        EditorAssetType.Machine => "Machines", EditorAssetType.Reel => "Reels", EditorAssetType.Object3D => "Object3D",
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };

    private static bool SamePath(string a, string b) => string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), PathComparison);
    private static bool IsWithinOrEqual(string root, string path)
    {
        var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var fullPath = Path.GetFullPath(path);
        return string.Equals(fullPath, fullRoot.TrimEnd(Path.DirectorySeparatorChar), PathComparison) || fullPath.StartsWith(fullRoot, PathComparison);
    }
    private static StringComparison PathComparison => OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
    private static bool IsExpected(Exception ex) => ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or InvalidDataException or NotSupportedException;
}
