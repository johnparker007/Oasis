using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using OasisEditor;
using OasisEditor.Features.CabinetEditor.Models;

namespace OasisEditor.Authoring.Assets;

public sealed record LibraryAssetEntry(EditorAssetType Type, string DisplayName, AssetReference Reference, string ManifestPath);
public sealed record LibraryDiagnostic(string PackagePath, string Message);
public sealed record LibraryDiscoveryResult(IReadOnlyList<LibraryAssetEntry> Assets, IReadOnlyList<LibraryDiagnostic> Diagnostics);
public sealed record LibraryCopyResult(bool Success, AssetReference? Reference, string? ManifestPath, string? Error)
{
    public static LibraryCopyResult Failed(string error) => new(false, null, null, error);
}

/// <summary>Portable reusable package discovery and copying for the local Oasis Library.</summary>
public static class LibraryAssetService
{
    private sealed record PackageType(EditorAssetType Type, string Folder, string ProjectFolder, string Manifest);
    private static readonly PackageType[] Types =
    [
        new(EditorAssetType.Cabinet3D, "Cabinets", "Cabinet3D", ProjectAssetPathService.Cabinet3DManifestFileName),
        new(EditorAssetType.Reel, "Reels", "Reels", ProjectAssetPathService.ReelManifestFileName),
        new(EditorAssetType.Object3D, "Object3D", "Object3D", ProjectAssetPathService.Object3DManifestFileName)
    ];

    public static LibraryDiscoveryResult Discover(string explicitRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(explicitRoot);
        var assets = new List<LibraryAssetEntry>();
        var diagnostics = new List<LibraryDiagnostic>();
        var root = Path.GetFullPath(explicitRoot);
        if (!Directory.Exists(root)) return new(assets, diagnostics);
        try { WorkspacePathPolicy.RejectRootReparsePoints(root, requireExists: true); }
        catch (Exception ex) when (IsIoOrValidation(ex)) { diagnostics.Add(new(root, ex.Message)); return new(assets, diagnostics); }

        foreach (var packageType in Types)
        {
            var typeRoot = Path.Combine(root, packageType.Folder);
            if (!Directory.Exists(typeRoot)) continue;
            try
            {
                EnsureNoReparsePoints(root, typeRoot, includeRoot: true);
                foreach (var packageDirectory in EnumeratePackageDirectories(root, typeRoot, diagnostics))
                {
                    var manifest = Path.Combine(packageDirectory, packageType.Manifest);
                    if (!File.Exists(manifest)) continue;
                    try
                    {
                        EnsurePackageTree(root, packageDirectory);
                        var display = ValidateManifest(packageType.Type, manifest, packageDirectory);
                        var relative = AssetReference.Normalize(Path.GetRelativePath(root, manifest));
                        assets.Add(new(packageType.Type, display, AssetReference.Library(relative), manifest));
                    }
                    catch (Exception ex) when (IsIoOrValidation(ex)) { diagnostics.Add(new(packageDirectory, ex.Message)); }
                }
            }
            catch (Exception ex) when (IsIoOrValidation(ex)) { diagnostics.Add(new(typeRoot, ex.Message)); }
        }
        return new(assets.OrderBy(a => a.Type).ThenBy(a => a.DisplayName, StringComparer.OrdinalIgnoreCase).ThenBy(a => a.Reference.Path, StringComparer.OrdinalIgnoreCase).ToArray(), diagnostics);
    }

    public static LibraryCopyResult CopyToLibrary(EditorProject project, string projectManifest, string libraryRoot, string? packageName = null)
        => Copy(project, projectManifest, libraryRoot, packageName, toLibrary: true);

    public static LibraryCopyResult ImportToProject(EditorProject project, AssetReference libraryRef, string libraryRoot, string? packageName = null)
    {
        ArgumentNullException.ThrowIfNull(libraryRef);
        if (libraryRef.Scope != AssetReferenceScope.Library) return LibraryCopyResult.Failed("Import requires a Library asset reference.");
        try
        {
            var source = new AssetReferenceResolver().Resolve(project, libraryRoot, libraryRef);
            return Copy(project, source, libraryRoot, packageName, toLibrary: false);
        }
        catch (Exception ex) when (IsIoOrValidation(ex)) { return LibraryCopyResult.Failed(ex.Message); }
    }

    private static LibraryCopyResult Copy(EditorProject project, string manifestPath, string libraryRoot, string? requestedName, bool toLibrary)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(libraryRoot);
        string? stage = null;
        string? target = null;
        var committed = false;
        try
        {
            var sourceRoot = Path.GetFullPath(toLibrary ? project.ProjectDirectory : libraryRoot);
            var manifest = Path.GetFullPath(manifestPath,sourceRoot);
            if(toLibrary)_=WorkspacePathPolicy.ResolveConfined(project.AssetsDirectory,Path.GetRelativePath(project.AssetsDirectory,manifest),requireExists:true);
            var type = Types.SingleOrDefault(t => string.Equals(Path.GetFileName(manifest), t.Manifest, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException("Only Cabinet3D, Reel, and Object3D packages can be shared through the Library.");
            var sourcePackage = Path.GetDirectoryName(manifest) ?? throw new InvalidOperationException("Package manifest has no parent directory.");
            var packageRelative = Path.GetRelativePath(sourceRoot, sourcePackage);
            if (packageRelative == ".") throw new InvalidOperationException("Package must be contained by the source root.");
            _ = WorkspacePathPolicy.ResolveConfined(sourceRoot, packageRelative, requireExists: true);
            WorkspacePathPolicy.RejectRootReparsePoints(sourceRoot, requireExists: true);
            EnsurePackageTree(sourceRoot, sourcePackage);
            _ = ValidateManifest(type.Type, manifest, sourcePackage);
            var originalLeaf = Path.GetFileName(sourcePackage);
            var finalName = Sanitize(requestedName ?? originalLeaf);
            var destinationRoot = Path.GetFullPath(toLibrary ? libraryRoot : project.AssetsDirectory);
            var destinationFolder = toLibrary ? type.Folder : type.ProjectFolder;
            var destinationParent = Path.Combine(destinationRoot, destinationFolder);
            if(IsWithinOrEqual(sourcePackage,destinationParent))throw new InvalidOperationException("A package cannot be copied into its own contents. Choose a separate Library folder.");
            WorkspacePathPolicy.RejectRootReparsePoints(destinationRoot);
            Directory.CreateDirectory(destinationRoot);
            Directory.CreateDirectory(destinationParent);
            WorkspacePathPolicy.RejectRootReparsePoints(destinationRoot, requireExists: true);
            WorkspacePathPolicy.RejectReparsePoints(destinationRoot, destinationFolder, requireExists: true);
            target = Path.Combine(destinationParent, finalName);
            if (Directory.Exists(target) || File.Exists(target)) throw new IOException($"Destination package already exists: {target}");
            stage = Path.Combine(destinationParent, ".oasis-library-stage-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(stage);
            CopyPackage(sourceRoot, sourcePackage, stage);
            EnsurePackageTree(destinationParent, stage);
            var stagedManifest = Path.Combine(stage, type.Manifest);
            _ = ValidateManifest(type.Type, stagedManifest, stage);
            WorkspacePathPolicy.RejectRootReparsePoints(destinationParent, requireExists: true);
            WorkspacePathPolicy.RejectReparsePoints(destinationParent, Path.GetFileName(stage), requireExists: true);
            WorkspacePathPolicy.RejectReparsePoints(destinationParent, finalName, requireExists: false);
            if (Directory.Exists(target) || File.Exists(target)) throw new IOException($"Destination package already exists: {target}");
            Directory.Move(stage, target);
            committed = true;
            var finalManifest = Path.Combine(target, type.Manifest);
            var relative = Path.GetRelativePath(toLibrary ? libraryRoot : project.ProjectDirectory, finalManifest);
            var reference = toLibrary ? AssetReference.Library(relative) : AssetReference.Project(ProjectAssetPathService.NormalizeProjectRelativePath(relative));
            return new(true, reference, finalManifest, null);
        }
        catch (Exception ex) when (IsIoOrValidation(ex)) { return LibraryCopyResult.Failed(ex.Message); }
        finally
        {
            if (!committed && stage is not null && Directory.Exists(stage))
            {
                try
                {
                    WorkspacePathPolicy.RejectRootReparsePoints(stage, requireExists: true);
                    EnsurePackageTree(Path.GetDirectoryName(stage)!, stage);
                    Directory.Delete(stage, recursive: true);
                }
                catch { }
            }
        }
    }

    private static string ValidateManifest(EditorAssetType type, string manifest, string package)
    {
        var json = File.ReadAllText(manifest);
        switch (type)
        {
            case EditorAssetType.Reel:
                if (!ReelDocumentStorage.TryRead(json, out var reel, out var reelError)) throw new InvalidDataException(reelError);
                return reel.DisplayName;
            case EditorAssetType.Cabinet3D:
                if (!CabinetDocumentStorage.TryRead(json, out var cabinet)) throw new InvalidDataException("Invalid Cabinet3D manifest.");
                if (!string.Equals(Path.GetExtension(cabinet.Model.Path), ".glb", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Cabinet model path must identify a GLB file.");
                ValidateContainedPath(package, cabinet.Model.Path, required: true);
                foreach (var mask in (cabinet.Reflections ?? []).Select(r => r.VisibilityMask).Where(p => !string.IsNullOrWhiteSpace(p))) ValidateContainedPath(package, mask!, required: true);
                ValidateGlbDependencies(package, cabinet.Model.Path);
                return Path.GetFileName(package);
            case EditorAssetType.Object3D:
                if (!Object3DDocumentStorage.TryRead(json, out var object3d, out var objectError)) throw new InvalidDataException(objectError);
                Object3DValidationService.Validate(object3d, package);
                foreach (var path in EnumerateModelPaths(object3d.Root)) ValidateGlbDependencies(package, path);
                return object3d.DisplayName;
            default: throw new InvalidOperationException("Unsupported Library package type.");
        }
    }

    private static IEnumerable<string> EnumerateModelPaths(Object3DNodeDefinition node)
    {
        foreach (var model in node.Components.Where(c => c.Kind == Object3DComponentDefinition.ModelKind).Select(c => c.Model).Where(m => m is not null)) yield return model!.Path;
        foreach (var child in node.Children ?? []) foreach (var path in EnumerateModelPaths(child)) yield return path;
    }

    private static void ValidateGlbDependencies(string package, string modelPath)
    {
        var model = ValidateContainedPath(package, modelPath, required: true);
        var bytes = File.ReadAllBytes(model);
        if (bytes.Length < 20 || BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(0, 4)) != 0x46546C67
            || BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(4, 4)) != 2)
            throw new InvalidDataException($"Model '{modelPath}' is not a valid GLB 2 file.");
        var declaredLength = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(8, 4));
        if (declaredLength != bytes.Length || declaredLength < 20) throw new InvalidDataException($"GLB '{modelPath}' has an invalid declared length.");
        var offset = 12;
        while (offset + 8 <= declaredLength)
        {
            var length = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4));
            var kind = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset + 4, 4));
            offset += 8;
            if (length > declaredLength - offset) throw new InvalidDataException($"GLB '{modelPath}' has an invalid chunk.");
            if (kind == 0x4E4F534A)
            {
                using var json = JsonDocument.Parse(bytes.AsMemory(offset, checked((int)length)));
                if (!json.RootElement.TryGetProperty("asset", out var asset)
                    || !asset.TryGetProperty("version", out var assetVersion)
                    || assetVersion.GetString() != "2.0")
                    throw new InvalidDataException($"GLB '{modelPath}' does not declare glTF asset version 2.0.");
                foreach (var key in new[] { "buffers", "images" })
                    if (json.RootElement.TryGetProperty(key, out var items) && items.ValueKind == JsonValueKind.Array)
                        foreach (var item in items.EnumerateArray())
                            if (item.TryGetProperty("uri", out var uriElement) && uriElement.ValueKind == JsonValueKind.String)
                            {
                                var uri = uriElement.GetString()!;
                                if (uri.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) continue;
                                var decodedUri = Uri.UnescapeDataString(uri);
                                if (Uri.TryCreate(decodedUri, UriKind.Absolute, out _) || decodedUri.StartsWith('/') || decodedUri.StartsWith('\\') || decodedUri.Contains(':') || decodedUri.Contains('?') || decodedUri.Contains('#')) throw new InvalidDataException($"GLB dependency URI '{uri}' must be embedded or package-relative.");
                                var dependency = ValidateContainedPath(package, Path.Combine(Path.GetDirectoryName(modelPath)!, decodedUri.Replace('/', Path.DirectorySeparatorChar)), required: true);
                                EnsureNoReparsePoints(package, dependency, includeRoot: true);
                            }
                return;
            }
            offset += checked((int)length);
        }
        throw new InvalidDataException($"GLB '{modelPath}' is missing its JSON chunk.");
    }

    private static string ValidateContainedPath(string package, string relative, bool required)
    {
        if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative) || relative.StartsWith('/') || relative.StartsWith('\\') || (relative.Length > 1 && relative[1] == ':')) throw new InvalidDataException($"Package dependency path '{relative}' must be relative.");
        var normalized = relative.Replace('\\', '/');
        if (normalized.Split('/').Any(p => p is "" or "." or "..")) throw new InvalidDataException($"Package dependency path '{relative}' contains traversal.");
        var fullPackage = Path.GetFullPath(package).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var full = Path.GetFullPath(Path.Combine(fullPackage, normalized.Replace('/', Path.DirectorySeparatorChar)));
        if (!IsWithin(fullPackage, full)) throw new InvalidDataException($"Package dependency '{relative}' escapes its package.");
        if (required && !File.Exists(full)) throw new FileNotFoundException($"Package dependency '{relative}' is missing.", full);
        return full;
    }

    private static void CopyPackage(string root, string source, string destination)
    {
        WorkspacePathPolicy.RejectRootReparsePoints(root, requireExists: true);
        EnsurePackageTree(root, source);
        var sourceFiles = EnumerateFilesSafe(source).ToArray();
        var sourceHashes = sourceFiles.ToDictionary(path => Path.GetRelativePath(source, path), HashFile, PathComparer);
        var sourceDirectories = EnumerateDirectoriesSafe(source).Select(path => Path.GetRelativePath(source, path)).ToHashSet(PathComparer);
        foreach (var directory in EnumerateDirectoriesSafe(source))
        {
            EnsureNoReparsePoints(root, directory, includeRoot: true);
            var relativeDirectory = Path.GetRelativePath(source, directory);
            if (relativeDirectory != ".") Directory.CreateDirectory(Path.Combine(destination, relativeDirectory));
        }
        foreach (var file in sourceFiles)
        {
            EnsureNoReparsePoints(root, file, includeRoot: true);
            var relative = Path.GetRelativePath(source, file);
            var target = Path.Combine(destination, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: false);
        }
        EnsurePackageTree(root, source);
        var afterFiles = EnumerateFilesSafe(source).ToArray();
        var afterHashes = afterFiles.ToDictionary(path => Path.GetRelativePath(source, path), HashFile, PathComparer);
        var afterDirectories = EnumerateDirectoriesSafe(source).Select(path => Path.GetRelativePath(source, path)).ToHashSet(PathComparer);
        if (!HashSetsEqual(sourceHashes, afterHashes) || !sourceDirectories.SetEquals(afterDirectories)) throw new IOException("Source package changed while it was being copied; no package was published.");
        var stagedFiles = EnumerateFilesSafe(destination).ToArray();
        var stagedHashes = stagedFiles.ToDictionary(path => Path.GetRelativePath(destination, path), HashFile, PathComparer);
        var stagedDirectories = EnumerateDirectoriesSafe(destination).Select(path => Path.GetRelativePath(destination, path)).ToHashSet(PathComparer);
        if (!HashSetsEqual(sourceHashes, stagedHashes) || !sourceDirectories.SetEquals(stagedDirectories)) throw new IOException("Staged package did not match the source package; no package was published.");
    }

    private static void EnsurePackageTree(string allowedRoot, string package)
    {
        EnsureNoReparsePoints(allowedRoot, package, includeRoot: true);
        foreach (var file in EnumerateFilesSafe(package)) EnsureNoReparsePoints(allowedRoot, file, includeRoot: true);
    }

    private static IEnumerable<string> EnumerateFilesSafe(string directory)
    {
        foreach (var entry in Directory.EnumerateFileSystemEntries(directory).OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
        {
            var attributes = File.GetAttributes(entry);
            if ((attributes & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException($"Reparse points are not allowed in package paths: '{entry}'.");
            if ((attributes & FileAttributes.Directory) != 0)
            {
                foreach (var file in EnumerateFilesSafe(entry)) yield return file;
            }
            else yield return entry;
        }
    }

    private static IReadOnlyList<string> EnumeratePackageDirectories(string allowedRoot, string root, List<LibraryDiagnostic> diagnostics)
    {
        var results = new List<string>();
        var manifestName = Types.Single(t => string.Equals(t.Folder, Path.GetFileName(root), StringComparison.OrdinalIgnoreCase)).Manifest;
        foreach (var dir in Directory.EnumerateDirectories(root).OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
        {
            var pending = new Stack<string>();
            pending.Push(dir);
            while (pending.Count > 0)
            {
                var current = pending.Pop();
                try
                {
                    WorkspacePathPolicy.RejectReparsePoints(allowedRoot, Path.GetRelativePath(allowedRoot, current), requireExists: true);
                    var manifest = Path.Combine(current, manifestName);
                    if (File.Exists(manifest)) { results.Add(current); continue; }
                    foreach (var child in Directory.EnumerateDirectories(current).OrderByDescending(p => p, StringComparer.OrdinalIgnoreCase))
                    {
                        try { WorkspacePathPolicy.RejectReparsePoints(allowedRoot, Path.GetRelativePath(allowedRoot, child), requireExists: true); pending.Push(child); }
                        catch (Exception ex) when (IsIoOrValidation(ex)) { diagnostics.Add(new(child, ex.Message)); }
                    }
                }
                catch (Exception ex) when (IsIoOrValidation(ex)) { diagnostics.Add(new(current, ex.Message)); }
            }
        }
        return results;
    }

    private static void EnsureNoReparsePoints(string root, string path, bool includeRoot)
    {
        root = Path.GetFullPath(root); path = Path.GetFullPath(path);
        if (!IsWithinOrEqual(root, path)) throw new InvalidDataException("Path escapes its allowed root.");
        if (string.Equals(path, root, PathComparison)) { WorkspacePathPolicy.RejectRootReparsePoints(root, requireExists: true); return; }
        var relative = Path.GetRelativePath(root, path);
        WorkspacePathPolicy.RejectReparsePoints(root, relative, requireExists: File.Exists(path) || Directory.Exists(path));
    }

    private static IEnumerable<string> EnumerateDirectoriesSafe(string root)
    {
        foreach (var entry in Directory.EnumerateDirectories(root).OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
        {
            if ((File.GetAttributes(entry) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException($"Reparse points are not allowed in package paths: '{entry}'.");
            yield return entry;
            foreach (var child in EnumerateDirectoriesSafe(entry)) yield return child;
        }
    }

    private static string HashFile(string path)
    {
        using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Convert.ToHexString(SHA256.HashData(stream));
    }
    private static bool HashSetsEqual(IReadOnlyDictionary<string, string> left, IReadOnlyDictionary<string, string> right) => left.Count == right.Count && left.All(pair => right.TryGetValue(pair.Key, out var hash) && string.Equals(pair.Value, hash, StringComparison.Ordinal));
    private static StringComparer PathComparer => OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
    private static StringComparison PathComparison => OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
    private static bool IsWithin(string root, string path) => !string.Equals(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), PathComparison) && IsWithinOrEqual(root, path);
    private static bool IsWithinOrEqual(string root, string path)
    {
        var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var fullPath = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return string.Equals(fullRoot, fullPath, PathComparison) || fullPath.StartsWith(fullRoot + Path.DirectorySeparatorChar, PathComparison);
    }
    private static string Sanitize(string name) => new ProjectAssetPathService().SanitizePathSegment(name);
    private static bool IsIoOrValidation(Exception e) => e is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or JsonException or NotSupportedException or InvalidDataException;
}
