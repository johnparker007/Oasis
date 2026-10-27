using System.Security.Cryptography;
using System.Text;

namespace OasisEditor;

public abstract class ThreeDAuthoringSessionBase(AuthoringSession projectSession, string assetFolder, string manifestName)
{
    private readonly Dictionary<string, string> _packageHashes = new(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
    private string? _boundProjectRoot;
    private string? _boundProjectFile;
    protected readonly AuthoringSession ProjectSession = projectSession ?? throw new ArgumentNullException(nameof(projectSession));
    protected readonly string AssetFolder = assetFolder;
    protected readonly string ManifestName = manifestName;
    protected string? ManifestPath { get; private set; }
    protected string? PackageDirectory { get; private set; }
    protected List<string> History { get; } = [];
    protected int HistoryIndex { get; set; } = -1;
    protected string SavedPayload { get; set; } = string.Empty;
    protected abstract string CurrentPayload { get; }
    protected abstract void SetCurrentPayload(string payload);
    public string? FilePath => ManifestPath;
    public bool CanUndo => HistoryIndex > 0;
    public bool CanRedo => HistoryIndex + 1 < History.Count;
    public bool IsDirty => ManifestPath is not null && CurrentPayload != SavedPayload;

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
        var root = ProjectRoot;
        var projectFile = Path.GetFullPath(ProjectSession.ProjectFilePath!);
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if ((_boundProjectRoot is not null && !string.Equals(root, _boundProjectRoot, comparison))
            || (_boundProjectFile is not null && !string.Equals(projectFile, _boundProjectFile, comparison)))
            throw new InvalidOperationException("The active Oasis project changed. Reopen this authoring session.");
    }

    protected void EnsureEditingAsset()
    {
        EnsureActiveProject();
        if (ManifestPath is null || PackageDirectory is null) throw new InvalidOperationException("Open or create a 3D asset before editing it.");
    }

    protected string ResolveManifest(string path)
    {
        EnsureActiveProject();
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

    protected string ResolveSource(string projectRelativePath)
    {
        EnsureActiveProject();
        var source = ProjectSession.ResolveAsset(projectRelativePath);
        if (!string.Equals(Path.GetExtension(source), ".glb", StringComparison.OrdinalIgnoreCase) || !File.Exists(source))
            throw new ArgumentException("Model source must be an existing project GLB asset.", nameof(projectRelativePath));
        WorkspacePathPolicy.RejectReparsePoints(ProjectRoot, Path.GetRelativePath(ProjectRoot, source), requireExists: true);
        return source;
    }

    protected string CreatePackage(string name)
    {
        EnsureActiveProject();
        var safe = new ProjectAssetPathService().SanitizePathSegment(name);
        var directory = WorkspacePathPolicy.ResolveConfined(ProjectRoot, Path.Combine("Assets", AssetFolder, safe));
        if (Directory.Exists(directory) || File.Exists(directory)) throw new IOException($"A {AssetFolder} package named '{safe}' already exists.");
        Directory.CreateDirectory(directory);
        return directory;
    }

    protected string CopyModel(string sourceProjectPath)
    {
        var source = ResolveSource(sourceProjectPath);
        var package = PackageDirectory ?? throw new InvalidOperationException("Create or open an asset package first.");
        EnsurePackageUnchanged();
        var stem = Path.GetFileNameWithoutExtension(source);
        var relative = "Models/" + Path.GetFileName(source);
        var destination = WorkspacePathPolicy.ResolveConfined(package, relative);
        for (var suffix = 2; File.Exists(destination); suffix++)
        {
            relative = $"Models/{stem} {suffix}.glb";
            destination = WorkspacePathPolicy.ResolveConfined(package, relative);
        }
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.Copy(source, destination, overwrite: false);
        RefreshPackageHashes();
        return relative.Replace('\\', '/');
    }

    protected void SetOpen(string manifest, string payload)
    {
        EnsureActiveProject();
        ManifestPath = ResolveManifest(manifest);
        _boundProjectRoot = ProjectRoot;
        _boundProjectFile = Path.GetFullPath(ProjectSession.ProjectFilePath!);
        PackageDirectory = Path.GetDirectoryName(ManifestPath)!;
        RefreshPackageHashes();
        SavedPayload = payload;
        History.Clear(); History.Add(payload); HistoryIndex = 0;
    }

    protected void SetCreated(string manifest, string payload)
    {
        _boundProjectRoot = ProjectRoot;
        _boundProjectFile = Path.GetFullPath(ProjectSession.ProjectFilePath!);
        ManifestPath = manifest;
        PackageDirectory = Path.GetDirectoryName(manifest)!;
        History.Clear(); History.Add(payload); HistoryIndex = 0;
        RefreshPackageHashes();
        SavedPayload = payload;
        History.Clear(); History.Add(payload); HistoryIndex = 0;
    }

    protected void SetPackageForCreate(string package)
    {
        _boundProjectRoot = ProjectRoot;
        _boundProjectFile = Path.GetFullPath(ProjectSession.ProjectFilePath!);
        PackageDirectory = package;
        RefreshPackageHashes();
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
        var safeManifest = WorkspacePathPolicy.ResolveConfined(ProjectRoot, Path.GetRelativePath(ProjectRoot, manifest), requireExists: true);
        var temp = safeManifest + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            File.WriteAllText(temp, payload, new UTF8Encoding(false));
            File.Move(temp, safeManifest, overwrite: true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
        SavedPayload = CurrentPayload;
        if (HistoryIndex >= 0) History[HistoryIndex] = CurrentPayload;
        RefreshPackageHashes();
    }

    protected void EnsurePackageUnchanged()
    {
        if (PackageDirectory is null) throw new InvalidOperationException("Open or create an asset package first.");
        var actual = Fingerprint(PackageDirectory);
        if (actual.Count != _packageHashes.Count || actual.Any(pair => !_packageHashes.TryGetValue(pair.Key, out var hash) || hash != pair.Value))
            throw new IOException("The asset package changed outside this session. Reopen it before editing or saving.");
    }

    private void RefreshPackageHashes()
    {
        _packageHashes.Clear();
        if (PackageDirectory is null || !Directory.Exists(PackageDirectory)) return;
        foreach (var pair in Fingerprint(PackageDirectory)) _packageHashes.Add(pair.Key, pair.Value);
    }

    private static Dictionary<string, string> Fingerprint(string package)
    {
        var result = new Dictionary<string, string>(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(package, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(package, file);
            WorkspacePathPolicy.RejectReparsePoints(package, relative, requireExists: true);
            using var stream = File.OpenRead(file);
            result[relative] = Convert.ToHexString(SHA256.HashData(stream));
        }
        return result;
    }
}
