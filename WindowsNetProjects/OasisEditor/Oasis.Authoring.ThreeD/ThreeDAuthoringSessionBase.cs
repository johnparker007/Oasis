using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Buffers.Binary;

namespace OasisEditor;

public abstract class ThreeDAuthoringSessionBase(AuthoringSession projectSession, string assetFolder, string manifestName, string? libraryAssetFolder = null)
{
    private readonly Dictionary<string, string> _packageHashes = new(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
    private string? _boundProjectRoot;
    private string? _boundProjectFile;
    private string? _boundLibraryRoot;
    protected readonly AuthoringSession ProjectSession = projectSession ?? throw new ArgumentNullException(nameof(projectSession));
    protected readonly string AssetFolder = assetFolder;
    protected readonly string LibraryAssetFolder = libraryAssetFolder ?? assetFolder;
    protected readonly string ManifestName = manifestName;
    protected string? ManifestPath { get; private set; }
    protected string? PackageDirectory { get; private set; }
    protected List<string> History { get; } = [];
    protected int HistoryIndex { get; set; } = -1;
    protected string SavedPayload { get; set; } = string.Empty;
    protected abstract string CurrentPayload { get; }
    protected abstract void SetCurrentPayload(string payload);
    public string? FilePath => ManifestPath;
    public bool IsLibraryAsset => _boundLibraryRoot is not null;
    public string? LibraryRoot => _boundLibraryRoot;
    public bool CanUndo => IsProjectCurrent && HistoryIndex > 0;
    public bool CanRedo => IsProjectCurrent && HistoryIndex + 1 < History.Count;
    public bool IsDirty => ManifestPath is not null && CurrentPayload != SavedPayload;
    public bool IsProjectCurrent
    {
        get
        {
            try
            {
                var root = ProjectRoot;
                if (_boundProjectRoot is null) return true;
                var projectFile = Path.GetFullPath(ProjectSession.ProjectFilePath!);
                var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
                return string.Equals(root, _boundProjectRoot, comparison)
                    && string.Equals(projectFile, _boundProjectFile, comparison);
            }
            catch { return false; }
        }
    }

    protected string ProjectRoot
    {
        get
        {
            var projectFile = ProjectSession.ProjectFilePath ?? throw new InvalidOperationException("Open an Oasis project first.");
            var root = Path.GetDirectoryName(Path.GetFullPath(projectFile))!;
            WorkspacePathPolicy.RejectRootReparsePoints(root, requireExists: true);
            return root;
        }
    }

    protected void EnsureActiveProject()
    {
        if (!IsProjectCurrent)
            throw new InvalidOperationException("The active Oasis project changed. Reopen this authoring session.");
    }

    protected void EnsureEditingAsset()
    {
        EnsureActiveProject();
        if (ManifestPath is null || PackageDirectory is null) throw new InvalidOperationException("Open or create a 3D asset before editing it.");
    }

    protected string ResolveManifest(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var root = ProjectRoot;
        var full = Path.IsPathRooted(path) ? Path.GetFullPath(path) : WorkspacePathPolicy.ResolveConfined(root, path);
        var relative = Path.GetRelativePath(root, full);
        var segments = WorkspacePathPolicy.NormalizeRelativePath(relative).Split(Path.DirectorySeparatorChar);
        if (segments.Length != 4 || !segments[0].Equals("Assets", StringComparison.OrdinalIgnoreCase)
            || !segments[1].Equals(AssetFolder, StringComparison.OrdinalIgnoreCase)
            || !segments[3].Equals(ManifestName, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Expected Assets/{AssetFolder}/<package>/{ManifestName}.");
        var result = WorkspacePathPolicy.ResolveConfined(root, relative, requireExists: true);
        if (!File.Exists(result)) throw new FileNotFoundException("Asset manifest was not found.", result);
        return result;
    }

    protected string ResolveLibraryManifest(string libraryRoot, string libraryRelativeManifest)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(libraryRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(libraryRelativeManifest);
        var root = Path.GetFullPath(libraryRoot);
        WorkspacePathPolicy.RejectRootReparsePoints(root, requireExists: true);
        var relative = WorkspacePathPolicy.NormalizeRelativePath(libraryRelativeManifest);
        var segments = relative.Split(Path.DirectorySeparatorChar);
        if (segments.Length < 3 || !segments[0].Equals(LibraryAssetFolder, StringComparison.OrdinalIgnoreCase)
            || !segments[^1].Equals(ManifestName, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Expected {LibraryAssetFolder}/<package>/{ManifestName} inside the Oasis Library.");
        var result = WorkspacePathPolicy.ResolveConfined(root, relative, requireExists: true);
        if (!File.Exists(result)) throw new FileNotFoundException("Library asset manifest was not found.", result);
        return result;
    }

    protected string ResolveSource(string projectRelativePath)
    {
        _ = ProjectRoot;
        var source = ProjectSession.ResolveAsset(projectRelativePath);
        if (!string.Equals(Path.GetExtension(source), ".glb", StringComparison.OrdinalIgnoreCase) || !File.Exists(source))
            throw new ArgumentException("Model source must be an existing project GLB asset.", nameof(projectRelativePath));
        WorkspacePathPolicy.RejectReparsePoints(ProjectRoot, Path.GetRelativePath(ProjectRoot, source), requireExists: true);
        ValidateSelfContainedGlb(source);
        return source;
    }

    protected PackageReservation ReservePackage(string name)
    {
        var safe = new ProjectAssetPathService().SanitizePathSegment(name);
        var root = ProjectRoot;
        var typeRoot = WorkspacePathPolicy.ResolveConfined(root, Path.Combine("Assets", AssetFolder));
        Directory.CreateDirectory(typeRoot);
        _ = WorkspacePathPolicy.ResolveConfined(root, Path.Combine("Assets", AssetFolder), requireExists: true);
        var final = WorkspacePathPolicy.ResolveConfined(root, Path.Combine("Assets", AssetFolder, safe));
        if (Directory.Exists(final) || File.Exists(final)) throw new IOException($"A {AssetFolder} package named '{safe}' already exists.");
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var stageName = ".oasis-3d-" + Guid.NewGuid().ToString("N");
            var stage = WorkspacePathPolicy.ResolveConfined(root, Path.Combine("Assets", AssetFolder, stageName));
            if (Directory.Exists(stage) || File.Exists(stage)) continue;
            Directory.CreateDirectory(stage);
            WorkspacePathPolicy.RejectReparsePoints(root, Path.GetRelativePath(root, stage), requireExists: true);
            return new PackageReservation(root, stage, final);
        }
        throw new IOException("Could not reserve a temporary asset package directory.");
    }

    protected string CopyModel(string sourceProjectPath, string package, Dictionary<string, string>? expectedHashes = null)
    {
        var source = ResolveSource(sourceProjectPath);
        var updateBoundHashes = expectedHashes is null;
        var expected = expectedHashes ?? CurrentPackageHashes();
        var before = Fingerprint(package);
        if (!SameFiles(expected, before)) throw new IOException("The asset package changed outside this session. Reopen it before importing a model.");
        var stem = Path.GetFileNameWithoutExtension(source);
        var relative = "Models/" + Path.GetFileName(source);
        var destination = WorkspacePathPolicy.ResolveConfined(package, relative);
        for (var suffix = 2; File.Exists(destination); suffix++)
        {
            relative = $"Models/{stem} {suffix}.glb";
            destination = WorkspacePathPolicy.ResolveConfined(package, relative);
        }
        var modelDirectory = Path.GetDirectoryName(destination)!;
        if (!Directory.Exists(modelDirectory)) Directory.CreateDirectory(modelDirectory);
        _ = WorkspacePathPolicy.ResolveConfined(package, relative);
        var owned = false;
        try
        {
            File.Copy(source, destination, overwrite: false);
            owned = true;
            ValidateSelfContainedGlb(destination);
            var after = Fingerprint(package);
            var importedHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(destination)));
            if (!IsExpectedImport(before, after, relative, importedHash))
                throw new IOException("The asset package changed while the model was being imported.");
            expected.Clear();
            foreach (var pair in after) expected.Add(pair.Key, pair.Value);
            if (updateBoundHashes)
            {
                _packageHashes.Clear();
                foreach (var pair in after) _packageHashes.Add(pair.Key, pair.Value);
            }
        }
        catch
        {
            if (owned && File.Exists(destination)) File.Delete(destination);
            throw;
        }
        return relative.Replace('\\', '/');
    }

    protected void EnsureCanReplaceDocument()
    {
        if (IsDirty) throw new InvalidOperationException("Save or undo the current 3D asset edits before replacing the open document.");
    }

    protected void SetOpen(string manifest, string payload, IReadOnlyDictionary<string, string> packageHashes, string? libraryRoot = null)
    {
        EnsureCanReplaceDocument();
        var root = ProjectRoot;
        ManifestPath = manifest;
        _boundProjectRoot = root;
        _boundProjectFile = Path.GetFullPath(ProjectSession.ProjectFilePath!);
        _boundLibraryRoot = libraryRoot is null ? null : Path.GetFullPath(libraryRoot);
        PackageDirectory = Path.GetDirectoryName(ManifestPath)!;
        ReplacePackageHashes(packageHashes);
        SavedPayload = payload;
        History.Clear(); History.Add(payload); HistoryIndex = 0;
    }

    protected void SetCreated(string manifest, string payload, IReadOnlyDictionary<string, string> packageHashes)
    {
        EnsureCanReplaceDocument();
        var root = ProjectRoot;
        ManifestPath = manifest;
        PackageDirectory = Path.GetDirectoryName(manifest)!;
        _boundProjectRoot = root;
        _boundProjectFile = Path.GetFullPath(ProjectSession.ProjectFilePath!);
        _boundLibraryRoot = null;
        ReplacePackageHashes(packageHashes);
        SavedPayload = payload;
        History.Clear(); History.Add(payload); HistoryIndex = 0;
    }

    public void Close()
    {
        EnsureCanReplaceDocument();
        ManifestPath = null;
        PackageDirectory = null;
        _boundProjectRoot = null;
        _boundProjectFile = null;
        _boundLibraryRoot = null;
        SavedPayload = string.Empty;
        History.Clear(); HistoryIndex = -1;
        _packageHashes.Clear();
        ResetDocument();
    }

    protected abstract void ResetDocument();

    protected Dictionary<string, string> CurrentPackageHashes()
    {
        if (PackageDirectory is null) throw new InvalidOperationException("Open or create an asset package first.");
        EnsurePackageUnchanged();
        return new Dictionary<string, string>(_packageHashes, _packageHashes.Comparer);
    }

    protected static Dictionary<string, string> CapturePackageHashes(string package) => Fingerprint(package);

    protected static void ValidatePackageModelGlb(string path, string? allowedPackageRoot = null) => ValidateGlb(path, allowedPackageRoot);

    protected static Dictionary<string, string> CaptureAfterOwnedFileWrite(string package, IReadOnlyDictionary<string, string> before, string relativePath, string absolutePath)
    {
        var after = Fingerprint(package);
        var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(absolutePath)));
        if (!IsExpectedImport(before, after, relativePath, hash))
            throw new IOException("The reserved package changed during creation.");
        return after;
    }

    private void ReplacePackageHashes(IReadOnlyDictionary<string, string> hashes)
    {
        _packageHashes.Clear();
        foreach (var pair in hashes) _packageHashes.Add(pair.Key, pair.Value);
    }

    public sealed class PackageReservation(string projectRoot, string stagingDirectory, string finalDirectory) : IDisposable
    {
        private bool _committed;
        public string StagingDirectory { get; } = stagingDirectory;
        public string FinalDirectory { get; } = finalDirectory;
        public void Commit()
        {
            if (_committed) throw new InvalidOperationException("Package reservation was already committed.");
            _ = WorkspacePathPolicy.ResolveConfined(projectRoot, Path.GetRelativePath(projectRoot, FinalDirectory));
            WorkspacePathPolicy.RejectReparsePoints(projectRoot, Path.GetRelativePath(projectRoot, StagingDirectory), requireExists: true);
            if (Directory.Exists(FinalDirectory) || File.Exists(FinalDirectory)) throw new IOException($"Asset package already exists: {FinalDirectory}");
            Directory.Move(StagingDirectory, FinalDirectory);
            _committed = true;
        }
        public void Dispose()
        {
            if (!_committed && Directory.Exists(StagingDirectory))
            {
                var info = new DirectoryInfo(StagingDirectory);
                if ((info.Attributes & FileAttributes.ReparsePoint) == 0) Directory.Delete(StagingDirectory, recursive: true);
            }
        }
    }

    protected static void WriteNewFile(string path, string content)
    {
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        writer.Write(content);
    }

    protected void CommitPayload(string payload)
    {
        EnsureActiveProject();
        if (payload == CurrentPayload) return;
        if (HistoryIndex + 1 < History.Count) History.RemoveRange(HistoryIndex + 1, History.Count - HistoryIndex - 1);
        History.Add(payload); HistoryIndex = History.Count - 1;
        SetCurrentPayload(payload);
    }

    public void Undo() => MoveHistory(redo: false);
    public void Redo() => MoveHistory(redo: true);

    private void MoveHistory(bool redo)
    {
        EnsureActiveProject();
        if (redo ? !CanRedo : !CanUndo) return;
        HistoryIndex += redo ? 1 : -1;
        SetCurrentPayload(History[HistoryIndex]);
    }

    protected void SavePayload(string payload)
    {
        EnsureActiveProject();
        var manifest = ManifestPath ?? throw new InvalidOperationException("Open or create an asset before saving.");
        EnsurePackageUnchanged();
        var baseline = new Dictionary<string, string>(_packageHashes, _packageHashes.Comparer);
        var manifestRelative = Path.GetRelativePath(PackageDirectory!, manifest).Replace('\\', '/');
        var assetRoot = _boundLibraryRoot ?? ProjectRoot;
        WorkspacePathPolicy.RejectRootReparsePoints(assetRoot, requireExists: true);
        var safeManifest = WorkspacePathPolicy.ResolveConfined(assetRoot, Path.GetRelativePath(assetRoot, manifest), requireExists: true);
        var temp = safeManifest + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
                writer.Write(payload);
            File.Move(temp, safeManifest, overwrite: true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
        SavedPayload = CurrentPayload;
        if (HistoryIndex >= 0) History[HistoryIndex] = CurrentPayload;
        var after = Fingerprint(PackageDirectory!);
        var expectedManifestHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
        if (!IsExpectedManifestSave(baseline, after, manifestRelative, expectedManifestHash))
            throw new IOException("The asset package changed during save. Your manifest was saved, but reopen the package before further edits.");
        ReplacePackageHashes(after);
    }

    protected void EnsurePackageUnchanged()
    {
        if (PackageDirectory is null) throw new InvalidOperationException("Open or create an asset package first.");
        var actual = Fingerprint(PackageDirectory);
        if (actual.Count != _packageHashes.Count || actual.Any(pair => !_packageHashes.TryGetValue(pair.Key, out var hash) || hash != pair.Value))
            throw new IOException("The asset package changed outside this session. Reopen it before editing or saving.");
    }

    private static Dictionary<string, string> Fingerprint(string package)
    {
        var result = new Dictionary<string, string>(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        ValidatePackageTree(package);
        var stack = new Stack<string>();
        stack.Push(package);
        while (stack.TryPop(out var directory))
        {
            foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
            {
                var relative = Path.GetRelativePath(package, entry);
                WorkspacePathPolicy.RejectReparsePoints(package, relative, requireExists: true);
                if (Directory.Exists(entry)) stack.Push(entry);
                else
                {
                    using var stream = File.OpenRead(entry);
                    result[relative.Replace('\\', '/')] = Convert.ToHexString(SHA256.HashData(stream));
                }
            }
        }
        return result;
    }

    private static void ValidatePackageTree(string package)
    {
        WorkspacePathPolicy.RejectRootReparsePoints(package, requireExists: true);
        var stack = new Stack<string>();
        stack.Push(package);
        while (stack.TryPop(out var directory))
        {
            foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
            {
                var relative = Path.GetRelativePath(package, entry);
                WorkspacePathPolicy.RejectReparsePoints(package, relative, requireExists: true);
                if (Directory.Exists(entry)) stack.Push(entry);
            }
        }
    }

    private static bool SameFiles(IReadOnlyDictionary<string, string> expected, IReadOnlyDictionary<string, string> actual)
        => expected.Count == actual.Count && expected.All(pair => actual.TryGetValue(pair.Key, out var hash) && hash == pair.Value);

    private static bool IsExpectedManifestSave(IReadOnlyDictionary<string, string> before, IReadOnlyDictionary<string, string> after, string manifestPath, string manifestHash)
        => before.Count == after.Count && before.ContainsKey(manifestPath)
            && before.All(pair => string.Equals(pair.Key, manifestPath, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)
                ? after.TryGetValue(pair.Key, out var hash) && hash == manifestHash
                : after.TryGetValue(pair.Key, out var otherHash) && otherHash == pair.Value);

    private static bool IsExpectedImport(IReadOnlyDictionary<string, string> before, IReadOnlyDictionary<string, string> after, string importedPath, string importedHash)
        => after.Count == before.Count + 1 && before.All(pair => after.TryGetValue(pair.Key, out var hash) && hash == pair.Value)
            && after.TryGetValue(importedPath, out var newHash) && newHash == importedHash && !before.ContainsKey(importedPath);

    private static void ValidateSelfContainedGlb(string path) => ValidateGlb(path, allowedPackageRoot: null);

    private static void ValidateGlb(string path, string? allowedPackageRoot)
    {
        var bytes = File.ReadAllBytes(path);
        if (bytes.Length < 20 || BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(0, 4)) != 0x46546C67
            || BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(4, 4)) != 2
            || BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(8, 4)) != bytes.Length)
            throw new InvalidDataException("Model import requires a valid, self-contained GLB file.");
        var jsonLength = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(12, 4));
        var jsonType = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(16, 4));
        if (jsonType != 0x4E4F534A || jsonLength > bytes.Length - 20 || jsonLength % 4 != 0)
            throw new InvalidDataException("Model import requires a valid GLB JSON chunk.");
        var offset = 12;
        var chunks = 0;
        while (offset < bytes.Length)
        {
            if (bytes.Length - offset < 8) throw new InvalidDataException("Model GLB contains a truncated chunk header.");
            var chunkLength = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4));
            var chunkType = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset + 4, 4));
            if (chunkLength % 4 != 0 || chunkLength > bytes.Length - offset - 8)
                throw new InvalidDataException("Model GLB contains an invalid chunk length.");
            if (chunks == 0 && chunkType != 0x4E4F534A)
                throw new InvalidDataException("Model GLB must begin with its JSON chunk.");
            if (chunks > 0 && chunkType == 0x4E4F534A)
                throw new InvalidDataException("Model GLB contains multiple JSON chunks.");
            offset += 8 + (int)chunkLength;
            chunks++;
        }
        try
        {
            using var document = JsonDocument.Parse(bytes.AsMemory(20, (int)jsonLength));
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("asset", out var asset)
                || asset.ValueKind != JsonValueKind.Object
                || !asset.TryGetProperty("version", out var version) || version.ValueKind != JsonValueKind.String || version.GetString() != "2.0")
                throw new InvalidDataException("Model import requires a glTF 2.0 GLB document.");
            RejectExternalUris(document.RootElement, path, allowedPackageRoot);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Model GLB contains invalid JSON.", exception);
        }
    }

    private static void RejectExternalUris(JsonElement element, string modelPath, string? allowedPackageRoot)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, "uri", StringComparison.OrdinalIgnoreCase)
                    && property.Value.ValueKind == JsonValueKind.String
                    && !property.Value.GetString()!.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                {
                    if (allowedPackageRoot is null)
                        throw new InvalidDataException("Model import only supports GLB files with embedded resources; external buffer or image URIs are not allowed.");
                    ValidatePackageUri(property.Value.GetString()!, modelPath, allowedPackageRoot);
                }
                RejectExternalUris(property.Value, modelPath, allowedPackageRoot);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray()) RejectExternalUris(item, modelPath, allowedPackageRoot);
        }
    }

    private static void ValidatePackageUri(string uri, string modelPath, string packageRoot)
    {
        var decoded = Uri.UnescapeDataString(uri);
        if (Uri.TryCreate(decoded, UriKind.Absolute, out _) || decoded.Contains(':') || decoded.StartsWith('/') || decoded.StartsWith('\\')
            || decoded.Contains('?') || decoded.Contains('#'))
            throw new InvalidDataException($"GLB dependency URI '{uri}' must be relative to its package.");
        var normalized = decoded.Replace('\\', '/');
        if (normalized.Split('/').Any(segment => segment.Length == 0 || segment is "." or ".."))
            throw new InvalidDataException($"GLB dependency URI '{uri}' contains an unsafe path segment.");

        var package = Path.GetFullPath(packageRoot);
        var modelDirectory = Path.GetDirectoryName(Path.GetFullPath(modelPath))!;
        var dependencyPath = Path.GetFullPath(Path.Combine(modelDirectory, normalized.Replace('/', Path.DirectorySeparatorChar)));
        var relative = Path.GetRelativePath(package, dependencyPath);
        var contained = WorkspacePathPolicy.ResolveConfined(package, relative, requireExists: true);
        WorkspacePathPolicy.RejectReparsePoints(package, relative, requireExists: true);
        if (!File.Exists(contained)) throw new FileNotFoundException($"GLB dependency '{uri}' is missing from its package.", contained);
    }
}
