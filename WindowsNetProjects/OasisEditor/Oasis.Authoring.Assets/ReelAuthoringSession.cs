using System.Security.Cryptography;
using System.Text;

namespace OasisEditor;

/// <summary>Portable, single-document authoring session for a reusable physical Reel asset.</summary>
public sealed partial class ReelAuthoringSession
{
    private readonly AuthoringSession _projectSession;
    private readonly Dictionary<string, string> _packageHashes = new(PathComparer);
    private readonly List<ReelDocument> _history = [];
    private string? _boundProjectRoot;
    private string? _boundProjectFile;
    private string? _libraryRoot;
    private string? _filePath;
    private ReelDocument? _document;
    private string? _savedPayload;
    private int _historyIndex = -1;

    private static StringComparer PathComparer => OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    public ReelAuthoringSession(AuthoringSession projectSession)
        => _projectSession = projectSession ?? throw new ArgumentNullException(nameof(projectSession));

    public ReelDocument Document => _document ?? throw new InvalidOperationException("Open or create a Reel asset first.");
    public string? FilePath => _filePath;
    public string? LibraryRoot => _libraryRoot;
    public bool IsLibraryAsset => _libraryRoot is not null;
    public bool IsDirty => _document is not null && Payload(_document) != _savedPayload;
    public bool CanUndo => IsProjectCurrent && _historyIndex > 0;
    public bool CanRedo => IsProjectCurrent && _historyIndex >= 0 && _historyIndex + 1 < _history.Count;
    public bool IsProjectCurrent
    {
        get
        {
            if (_boundProjectRoot is null) return true;
            try
            {
                var projectFile = _projectSession.ProjectFilePath;
                if (projectFile is null) return false;
                var root = CurrentProjectRoot();
                var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
                return string.Equals(root, _boundProjectRoot, comparison)
                    && string.Equals(Path.GetFullPath(projectFile), _boundProjectFile, comparison);
            }
            catch { return false; }
        }
    }

    public event Action? Changed;

    /// <summary>Create Assets/Reels/&lt;name&gt;/asset.reel without replacing an existing package.</summary>
    public string Create(string name)
    {
        EnsureCanReplaceDocument();
        var root = CurrentProjectRoot();
        var safeName = new ProjectAssetPathService().SanitizePathSegment(name);
        var typeRootRelative = Path.Combine("Assets", "Reels");
        var typeRoot = WorkspacePathPolicy.ResolveConfined(root, typeRootRelative);
        Directory.CreateDirectory(typeRoot);
        _ = WorkspacePathPolicy.ResolveConfined(root, typeRootRelative, requireExists: true);

        var finalRelative = Path.Combine(typeRootRelative, safeName);
        var final = WorkspacePathPolicy.ResolveConfined(root, finalRelative);
        if (Directory.Exists(final) || File.Exists(final)) throw new IOException($"A Reel package named '{safeName}' already exists.");

        var stagingRelative = Path.Combine(typeRootRelative, ".oasis-reel-" + Guid.NewGuid().ToString("N"));
        var staging = WorkspacePathPolicy.ResolveConfined(root, stagingRelative);
        Directory.CreateDirectory(staging);
        try
        {
            WorkspacePathPolicy.RejectReparsePoints(root, stagingRelative, requireExists: true);
            var candidate = ReelDocument.Create(safeName);
            var payload = SerializeAndReparse(candidate);
            var stagedManifest = Path.Combine(staging, ProjectAssetPathService.ReelManifestFileName);
            WriteNewFile(stagedManifest, payload);
            var hashes = Fingerprint(staging);

            EnsureCanReplaceDocument();
            if (Directory.Exists(final) || File.Exists(final)) throw new IOException($"A Reel package named '{safeName}' already exists.");
            _ = WorkspacePathPolicy.ResolveConfined(root, finalRelative);
            Directory.Move(staging, final);
            var manifest = Path.Combine(final, ProjectAssetPathService.ReelManifestFileName);
            SetOpen(manifest, candidate, payload, hashes, root, libraryRoot: null);
            Changed?.Invoke();
            return manifest;
        }
        finally
        {
            if (Directory.Exists(staging))
            {
                var info = new DirectoryInfo(staging);
                if ((info.Attributes & FileAttributes.ReparsePoint) == 0) Directory.Delete(staging, recursive: true);
            }
        }
    }

    /// <summary>Open a Reel manifest after validating its location, schema, package tree and contents.</summary>
    public void Open(string path)
    {
        EnsureCanReplaceDocument();
        var root = CurrentProjectRoot();
        var manifest = ResolveManifest(path, root);
        var content = File.ReadAllText(manifest);
        if (!ReelDocumentStorage.TryRead(content, out var candidate, out var error))
            throw new InvalidDataException(error);
        var payload = SerializeAndReparse(candidate);
        var package = Path.GetDirectoryName(manifest)!;
        var hashes = Fingerprint(package);
        SetOpen(manifest, candidate, payload, hashes, root, libraryRoot: null);
        Changed?.Invoke();
    }

    /// <summary>Open an editable Library Reel manifest relative to an explicit Library root.</summary>
    public void OpenLibrary(string libraryRoot, string libraryRelativeManifest)
    {
        EnsureCanReplaceDocument();
        var projectRoot = CurrentProjectRoot();
        ArgumentException.ThrowIfNullOrWhiteSpace(libraryRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(libraryRelativeManifest);
        var fullLibraryRoot = Path.GetFullPath(libraryRoot);
        WorkspacePathPolicy.RejectRootReparsePoints(fullLibraryRoot, requireExists: true);
        var relative = WorkspacePathPolicy.NormalizeRelativePath(libraryRelativeManifest);
        var segments = relative.Split(Path.DirectorySeparatorChar);
        if (segments.Length < 3 || !segments[0].Equals("Reels", StringComparison.OrdinalIgnoreCase)
            || !segments[^1].Equals(ProjectAssetPathService.ReelManifestFileName, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Expected a Library Reel manifest at Reels/<package>/asset.reel.");
        var manifest = WorkspacePathPolicy.ResolveConfined(fullLibraryRoot, relative, requireExists: true);
        if (!File.Exists(manifest)) throw new FileNotFoundException("Library Reel manifest was not found.", manifest);
        var content = File.ReadAllText(manifest);
        if (!ReelDocumentStorage.TryRead(content, out var candidate, out var error)) throw new InvalidDataException(error);
        var payload = SerializeAndReparse(candidate);
        var package = Path.GetDirectoryName(manifest)!;
        var hashes = Fingerprint(package);
        SetOpen(manifest, candidate, payload, hashes, projectRoot, fullLibraryRoot);
        Changed?.Invoke();
    }

    /// <summary>Change supplied Reel fields. Omitted fields retain their current values.</summary>
    public bool Update(string? displayName = null, double? diameterMm = null, double? widthMm = null)
    {
        EnsureEditingAsset();
        var current = Document;
        var candidate = current with
        {
            DisplayName = displayName is null ? current.DisplayName : displayName.Trim(),
            DiameterMm = diameterMm ?? current.DiameterMm,
            WidthMm = widthMm ?? current.WidthMm
        };
        _ = SerializeAndReparse(candidate);
        if (candidate == current) return false;
        if (_historyIndex + 1 < _history.Count) _history.RemoveRange(_historyIndex + 1, _history.Count - _historyIndex - 1);
        _history.Add(candidate);
        _historyIndex = _history.Count - 1;
        _document = candidate;
        Changed?.Invoke();
        return true;
    }

    public void Undo() => MoveHistory(redo: false);
    public void Redo() => MoveHistory(redo: true);

    public void Save()
    {
        EnsureEditingAsset();
        var manifest = _filePath!;
        var package = Path.GetDirectoryName(manifest)!;
        var payload = SerializeAndReparse(Document);
        EnsurePackageUnchanged(package);
        var baseline = new Dictionary<string, string>(_packageHashes, PathComparer);
        var temp = manifest + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            WriteNewFile(temp, payload);
            if ((File.GetAttributes(temp) & FileAttributes.ReparsePoint) != 0 || Hash(File.ReadAllText(temp)) != Hash(payload))
                throw new IOException("The staged Reel manifest changed before it could be saved.");
            if (!ReelDocumentStorage.TryRead(File.ReadAllText(temp), out var stagedDocument, out var stagedError) || stagedDocument != Document)
                throw new InvalidDataException("The staged Reel manifest could not be re-parsed before save: " + stagedError);
            EnsurePackageUnchanged(package, temp);
            File.Move(temp, manifest, overwrite: true);
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }

        var after = Fingerprint(package);
        var relativeManifest = "F:" + Path.GetFileName(manifest);
        var expectedHash = Hash(payload);
        if (!IsExpectedSave(baseline, after, relativeManifest, expectedHash))
            throw new IOException("The Reel package changed during save. The manifest was written, but reopen the package before further edits.");

        _savedPayload = Payload(Document);
        _history[_historyIndex] = Document;
        ReplaceHashes(after);
        Changed?.Invoke();
    }

    public void Close()
    {
        if (IsDirty) throw new InvalidOperationException("Save or undo Reel edits before closing.");
        if (_filePath is {} path) _retainedReels.Remove(path);
        _filePath = null;
        _boundProjectRoot = null;
        _boundProjectFile = null;
        _libraryRoot = null;
        _document = null;
        _savedPayload = null;
        _history.Clear();
        _historyIndex = -1;
        _packageHashes.Clear();
        Changed?.Invoke();
    }

    private void MoveHistory(bool redo)
    {
        EnsureActiveProject();
        if (redo ? !CanRedo : !CanUndo) return;
        _historyIndex += redo ? 1 : -1;
        _document = _history[_historyIndex];
        Changed?.Invoke();
    }

    private void SetOpen(string manifest, ReelDocument document, string payload, IReadOnlyDictionary<string, string> hashes, string root, string? libraryRoot)
    {
        if (RestoreRetainedReel(manifest)) return;
        RetainCurrentReel();
        _filePath = manifest;
        _document = document;
        _savedPayload = Payload(document);
        _boundProjectRoot = root;
        _boundProjectFile = Path.GetFullPath(_projectSession.ProjectFilePath!);
        _libraryRoot = libraryRoot;
        _history.Clear();
        _history.Add(document);
        _historyIndex = 0;
        ReplaceHashes(hashes);
    }

    private string CurrentProjectRoot()
    {
        var projectFile = _projectSession.ProjectFilePath ?? throw new InvalidOperationException("Open an Oasis project first.");
        var root = Path.GetDirectoryName(Path.GetFullPath(projectFile))!;
        WorkspacePathPolicy.RejectRootReparsePoints(root, requireExists: true);
        return root;
    }

    private void EnsureActiveProject()
    {
        if (!IsProjectCurrent) throw new InvalidOperationException("The active Oasis project changed. Reopen this authoring session.");
    }

    private void EnsureEditingAsset()
    {
        EnsureActiveProject();
        if (_filePath is null || _document is null) throw new InvalidOperationException("Open or create a Reel asset before editing it.");
    }

    private void EnsureCanReplaceDocument()
    {
        if (!RetainDocuments && IsDirty) throw new InvalidOperationException("Save or undo the current Reel edits before replacing the open document.");
    }

    private string ResolveManifest(string path, string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var full = Path.IsPathRooted(path) ? Path.GetFullPath(path) : WorkspacePathPolicy.ResolveConfined(root, path);
        var relative = Path.GetRelativePath(root, full);
        var segments = WorkspacePathPolicy.NormalizeRelativePath(relative).Split(Path.DirectorySeparatorChar);
        if (segments.Length != 4 || !segments[0].Equals("Assets", StringComparison.OrdinalIgnoreCase)
            || !segments[1].Equals("Reels", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(segments[2])
            || !segments[3].Equals(ProjectAssetPathService.ReelManifestFileName, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Expected Assets/Reels/<package>/asset.reel.");
        var resolved = WorkspacePathPolicy.ResolveConfined(root, relative, requireExists: true);
        WorkspacePathPolicy.RejectReparsePoints(root, relative, requireExists: true);
        if (!File.Exists(resolved)) throw new FileNotFoundException("Reel manifest was not found.", resolved);
        return resolved;
    }

    private void EnsurePackageUnchanged(string package, string? excludedFile = null)
    {
        var excludedRelative = excludedFile is null ? null : Path.GetRelativePath(package, excludedFile).Replace('\\', '/');
        var current = Fingerprint(package, excludedRelative);
        if (current.Count != _packageHashes.Count || current.Any(pair => !_packageHashes.TryGetValue(pair.Key, out var hash) || hash != pair.Value))
            throw new IOException("The Reel package changed outside this session. Reopen it before editing or saving.");
    }

    private void ReplaceHashes(IReadOnlyDictionary<string, string> hashes)
    {
        _packageHashes.Clear();
        foreach (var pair in hashes) _packageHashes.Add(pair.Key, pair.Value);
    }

    private static Dictionary<string, string> Fingerprint(string package, string? excludedRelativeFile = null)
    {
        WorkspacePathPolicy.RejectRootReparsePoints(package, requireExists: true);
        var result = new Dictionary<string, string>(PathComparer);
        var pending = new Stack<string>();
        pending.Push(package);
        while (pending.TryPop(out var directory))
        {
            foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
            {
                var relative = Path.GetRelativePath(package, entry).Replace('\\', '/');
                WorkspacePathPolicy.RejectReparsePoints(package, relative, requireExists: true);
                if (excludedRelativeFile is not null && string.Equals(relative, excludedRelativeFile, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)) continue;
                if (Directory.Exists(entry))
                {
                    result["D:" + relative] = string.Empty;
                    pending.Push(entry);
                }
                else
                {
                    using var stream = File.OpenRead(entry);
                    result["F:" + relative] = Convert.ToHexString(SHA256.HashData(stream));
                }
            }
        }
        return result;
    }

    private static bool IsExpectedSave(IReadOnlyDictionary<string, string> before, IReadOnlyDictionary<string, string> after, string manifest, string expectedManifestHash)
    {
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return before.Count == after.Count && before.ContainsKey(manifest)
            && before.All(pair => string.Equals(pair.Key, manifest, comparison)
                ? after.TryGetValue(pair.Key, out var hash) && hash == expectedManifestHash
                : after.TryGetValue(pair.Key, out var otherHash) && otherHash == pair.Value);
    }

    private static string SerializeAndReparse(ReelDocument document)
    {
        var payload = ReelDocumentStorage.Serialize(document);
        if (!ReelDocumentStorage.TryRead(payload, out var reparsed, out var error)) throw new InvalidDataException(error);
        if (reparsed != document) throw new InvalidDataException("The Reel manifest could not be re-parsed without changing its authored values.");
        return payload;
    }

    private static string Payload(ReelDocument document) => ReelDocumentStorage.Serialize(document);
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static void WriteNewFile(string path, string content)
    {
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        writer.Write(content);
    }
}
