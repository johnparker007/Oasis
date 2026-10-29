using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace OasisEditor;

public sealed record ElementSnapshot(
    string Id, string Kind, string Name, double X, double Y, double Width, double Height,
    string? AssetPath, string? SecondaryAssetPath, string? OnColorHex, string? OffColorHex,
    string? TextColorHex, string? DisplayColorHex, string? DisplayText, bool Visible, bool Locked);

/// <summary>Portable, single-document authoring session shared by desktop UI and MCP.</summary>
public sealed partial class AuthoringSession
{
    private const string PanelManifest = "asset.panel2d";
    private readonly string _workspacePath;
    private readonly List<Panel2DDocumentFile> _history = [];
    private readonly Dictionary<string, PanelState> _panelStates = new(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
    private string? _projectDirectory;
    private string? _projectFilePath;
    private string? _panelPath;
    private string? _diskHash;
    private string _savedContent = string.Empty;
    private int _historyIndex;
    private Panel2DDocumentFile? _document;
    private string? _selectedId;
    private List<string> _selectedIds = [];

    public AuthoringSession(string workspacePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspacePath);
        _workspacePath = Path.GetFullPath(workspacePath);
    }

    public string? ProjectName { get; private set; }
    public string WorkspacePath => _workspacePath;
    public string? ProjectFilePath => _projectFilePath;
    public string? PanelPath => _panelPath;
    public string PanelTitle => _document?.Title ?? string.Empty;
    public bool IsDirty => _document is not null && Payload(_document) != _savedContent;
    public bool CanUndo => _historyIndex > 0;
    public bool CanRedo => _historyIndex + 1 < _history.Count;
    public string? SelectedId => _selectedId;
    public IReadOnlyList<ElementSnapshot> Elements => (_document?.Elements ?? []).Select(Project).ToArray();
    public event Action? Changed;

    internal string RequireProjectDirectory() => RequireProject();

    internal EditorProject RequireEditorProject()
    {
        var directory = RequireProject();
        var projectFile = _projectFilePath ?? throw new InvalidOperationException("Open an Oasis project first.");
        return new EditorProject
        {
            Name = ProjectName ?? Path.GetFileNameWithoutExtension(projectFile),
            ProjectFilePath = projectFile,
            ProjectDirectory = directory,
            AssetsDirectory = Path.Combine(directory, "Assets"),
            GeneratedDirectory = Path.Combine(directory, "Generated")
        };
    }

    internal Panel2DDocumentModel RequirePanelDocument()
        => _document is null ? throw new InvalidOperationException("Open a Panel2D document first.") : Panel2DDocumentStorage.ToModel(_document);

    internal void EnsurePanelMatchesSavedFile()
    {
        if (_panelPath is null || _diskHash is null)
            throw new InvalidOperationException("Save the source Panel2D document before generating a Face.");
        if (IsDirty)
            throw new InvalidOperationException("Save the source Panel2D edits before generating a Face.");
        if (!File.Exists(_panelPath) || !string.Equals(HashFile(_panelPath), _diskHash, StringComparison.Ordinal))
            throw new IOException("The source Panel2D manifest changed outside this session. Reopen it before generating a Face.");
    }

    public void OpenProject(string path)
    {
        if (HasRetainedChanges) throw new InvalidOperationException("Save or undo all Panel edits before changing project.");
        EnsureCanReplaceDocument();
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        WorkspacePathPolicy.RejectRootReparsePoints(_workspacePath, requireExists: true);
        var fullPath = Path.GetFullPath(path, _workspacePath);
        var workspaceComparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var secureInput = string.Equals(fullPath, _workspacePath, workspaceComparison)
            ? _workspacePath
            : WorkspacePathPolicy.ResolveConfined(_workspacePath, Path.GetRelativePath(_workspacePath, fullPath), requireExists: true);
        var projectFile = Directory.Exists(secureInput)
            ? FindProjectFile(secureInput)
            : secureInput;
        if (!File.Exists(projectFile) || !string.Equals(Path.GetExtension(projectFile), ".oasisproj", StringComparison.OrdinalIgnoreCase))
            throw new FileNotFoundException("Choose an Oasis project file or project folder.", projectFile);
        var workspaceRelative = Path.GetRelativePath(_workspacePath, projectFile);
        projectFile = WorkspacePathPolicy.ResolveConfined(_workspacePath, workspaceRelative, requireExists: true);
        var directory = Path.GetDirectoryName(projectFile)!;
        var relative = Path.GetRelativePath(directory, projectFile);
        WorkspacePathPolicy.RejectReparsePoints(directory, relative, requireExists: true);
        var name = ReadProjectName(projectFile);
        _ = WorkspacePathPolicy.ResolveConfined(directory, "Assets/Panel2D", requireExists: false);
        _projectDirectory = directory;
        _projectFilePath = projectFile;
        ProjectName = name;
        _panelPath = null;
        _document = null;
        _selectedId = null;
        _selectedIds.Clear();
        _panelStates.Clear(); _createdFmlImports.Clear();
        _diskHash = null;
        _savedContent = string.Empty;
        ClearHistory();
        Changed?.Invoke();
    }

    public IReadOnlyList<string> ListPanels()
    {
        var root = RequireProject();
        var panelRoot = WorkspacePathPolicy.ResolveConfined(root, "Assets/Panel2D", requireExists: false);
        if (!Directory.Exists(panelRoot)) return [];
        var paths = new List<string>();
        foreach (var directory in Directory.EnumerateDirectories(panelRoot))
        {
            var relative = Path.GetRelativePath(root, Path.Combine(directory, PanelManifest));
            var path = WorkspacePathPolicy.ResolveConfined(root, relative);
            WorkspacePathPolicy.RejectReparsePoints(root, relative);
            if (File.Exists(path)) paths.Add(path);
        }
        return paths.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public void OpenPanel(string path)
    {
        EnsureCanReplaceDocument();
        var root = RequireProject();
        var resolved = ResolveManifest(path, root);
        if (string.Equals(resolved, _panelPath, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)) return;
        StorePanelState();
        if (_panelStates.TryGetValue(resolved, out var state))
        {
            _panelPath = resolved;
            RestorePanelState(state);
            Changed?.Invoke();
            return;
        }
        var content = File.ReadAllText(resolved);
        var parsed = ReadCurrentDocument(content);
        _panelPath = resolved;
        _document = parsed;
        _diskHash = Hash(content);
        _savedContent = Payload(parsed);
        _selectedId = null;
        _selectedIds.Clear();
        ResetHistory(parsed);
        Changed?.Invoke();
    }

    public string CreatePanel(string name)
    {
        EnsureCanReplaceDocument();
        var root = RequireProject();
        var safeName = SanitizeSegment(name);
        var relative = Path.Combine("Assets", "Panel2D", safeName, PanelManifest);
        var manifestPath = WorkspacePathPolicy.ResolveConfined(root, relative);
        if (File.Exists(manifestPath) || Directory.Exists(Path.GetDirectoryName(manifestPath)!))
            throw new IOException($"A Panel2D package named '{safeName}' already exists.");
        Directory.CreateDirectory(Path.GetDirectoryName(manifestPath)!);
        var document = new Panel2DDocumentFile
        {
            SchemaVersion = Panel2DDocumentStorage.CurrentSchemaVersion,
            Title = safeName,
            Summary = string.Empty,
            SavedAtUtc = DateTime.UtcNow
        };
        var content = JsonSerializer.Serialize(document, new JsonSerializerOptions { WriteIndented = true });
        WriteAtomic(manifestPath, content);
        StorePanelState();
        _panelPath = manifestPath;
        _document = document;
        _diskHash = Hash(content);
        _savedContent = Payload(document);
        _selectedId = null;
        _selectedIds.Clear();
        ResetHistory(document);
        Changed?.Invoke();
        return manifestPath;
    }

    public void Select(string? id)
    {
        if (id is not null && !(_document?.Elements ?? []).Any(element => element.ObjectId == id))
            throw new KeyNotFoundException($"Panel element '{id}' was not found.");
        if (_selectedIds.Count == (id is null ? 0 : 1) && (id is null || _selectedIds[0] == id)) return;
        _selectedId = id;
        _selectedIds = id is null ? [] : [id];
        StorePanelState();
        Changed?.Invoke();
    }

    public bool UpdateElement(string id, string? name = null, double? x = null, double? y = null,
        double? width = null, double? height = null, bool? visible = null, bool? locked = null)
    {
        var document = RequireDocument();
        var index = Array.FindIndex(document.Elements, element => element.ObjectId == id);
        if (index < 0) throw new KeyNotFoundException($"Panel element '{id}' was not found.");
        if (name is not null && string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Element name cannot be blank.", nameof(name));
        var current = document.Elements[index];
        ValidateGeometry(x, y, width, height);
        var updated = current with
        {
            Name = name is null ? current.Name : name.Trim(),
            X = x ?? current.X, Y = y ?? current.Y, Width = width ?? current.Width, Height = height ?? current.Height,
            IsVisible = visible ?? current.IsVisible,
            LockTransform = locked ?? current.LockTransform
        };
        if (updated == current) return false;
        var geometryChanged = updated.X != current.X || updated.Y != current.Y
            || updated.Width != current.Width || updated.Height != current.Height;
        if (geometryChanged && current.LockTransform) throw new InvalidOperationException("This element has locked geometry.");
        var elements = (PanelElementFile[])document.Elements.Clone();
        elements[index] = updated;
        Commit(document with { Elements = elements });
        return true;
    }

    public string AddRectangle()
    {
        var document = RequireDocument();
        var id = Guid.NewGuid().ToString("N");
        var rectangle = new PanelElementFile
        {
            ObjectId = id, Name = "Rectangle", Kind = "rectangle", X = 24, Y = 24, Width = 120, Height = 80,
            IsVisible = true
        };
        Commit(document with { Elements = [.. document.Elements, rectangle] }, notify: false);
        _selectedId = id;
        _selectedIds = [id];
        StorePanelState();
        Changed?.Invoke();
        return id;
    }

    public void Undo()
    {
        if (!CanUndo) return;
        _document = _history[--_historyIndex];
        KeepSelection();
        Changed?.Invoke();
    }

    public void Redo()
    {
        if (!CanRedo) return;
        _document = _history[++_historyIndex];
        KeepSelection();
        Changed?.Invoke();
    }

    public void Save()
    {
        var path = _panelPath ?? throw new InvalidOperationException("Open or create a Panel2D document before saving.");
        var document = RequireDocument() with { SavedAtUtc = DateTime.UtcNow };
        ValidateDocumentForSave(document);
        var projectRoot = RequireProject();
        var relative = Path.GetRelativePath(projectRoot, path);
        var securePath = WorkspacePathPolicy.ResolveConfined(projectRoot, relative, requireExists: true);
        EnsurePanelManifestLocation(projectRoot, relative);
        var currentHash = HashFile(path);
        if (!string.Equals(currentHash, _diskHash, StringComparison.Ordinal))
            throw new IOException("The Panel2D file changed outside this session. Reopen it before saving your edits.");
        var content = JsonSerializer.Serialize(document, new JsonSerializerOptions { WriteIndented = true });
        WriteAtomic(securePath, content);
        _document = document;
        _diskHash = Hash(content);
        _savedContent = Payload(document);
        _history[_historyIndex] = document;
        StorePanelState();
        Changed?.Invoke();
    }

    public string ResolveAsset(string projectRelativePath)
    {
        var root = RequireProject();
        var resolved = WorkspacePathPolicy.ResolveConfined(root, projectRelativePath, requireExists: true);
        WorkspacePathPolicy.RejectReparsePoints(root, projectRelativePath, requireExists: true);
        return resolved;
    }

    private void Commit(Panel2DDocumentFile updated, bool notify = true)
    {
        if (Payload(updated) == Payload(RequireDocument())) return;
        _document = updated;
        if (_historyIndex + 1 < _history.Count) _history.RemoveRange(_historyIndex + 1, _history.Count - _historyIndex - 1);
        _history.Add(updated);
        _historyIndex = _history.Count - 1;
        StorePanelState();
        if (notify) Changed?.Invoke();
    }

    private void EnsureCanReplaceDocument()
    {
        if (!RetainDocuments && IsDirty) throw new InvalidOperationException("Save or undo the current Panel2D edits before replacing the open document.");
    }

    private string RequireProject() => _projectDirectory ?? throw new InvalidOperationException("Open an Oasis project first.");
    private Panel2DDocumentFile RequireDocument() => _document ?? throw new InvalidOperationException("Open or create a Panel2D document first.");

    private string ResolveManifest(string path, string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var normalized = path.Replace('\\', '/');
        var full = Path.IsPathRooted(path) ? Path.GetFullPath(path) : WorkspacePathPolicy.ResolveConfined(root, normalized);
        var relative = Path.GetRelativePath(root, full);
        var confined = WorkspacePathPolicy.ResolveConfined(root, relative, requireExists: true);
        EnsurePanelManifestLocation(root, relative);
        WorkspacePathPolicy.RejectReparsePoints(root, relative, requireExists: true);
        return confined;
    }

    private static void EnsurePanelManifestLocation(string root, string relative)
    {
        var segments = WorkspacePathPolicy.NormalizeRelativePath(relative).Split(Path.DirectorySeparatorChar);
        if (segments.Length != 4 || !string.Equals(segments[0], "Assets", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(segments[1], "Panel2D", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(segments[2])
            || !string.Equals(segments[3], PanelManifest, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Choose a Panel2D manifest at Assets/Panel2D/<name>/asset.panel2d.");
    }

    private static Panel2DDocumentFile ReadCurrentDocument(string content)
    {
        Panel2DDocumentFile source;
        try
        {
            source = JsonSerializer.Deserialize<Panel2DDocumentFile>(content, new JsonSerializerOptions
            {
                UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow
            }) ?? throw new InvalidDataException("The Panel2D manifest is empty.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("The Panel2D manifest is malformed or contains fields unsupported by this schema.", exception);
        }
        if (source.SchemaVersion != Panel2DDocumentStorage.CurrentSchemaVersion)
            throw new InvalidDataException($"Panel2D schema {source.SchemaVersion} is unsupported; schema {Panel2DDocumentStorage.CurrentSchemaVersion} is required.");
        if (source.Elements is null || source.FaceSourceShapes is null)
            throw new InvalidDataException("The Panel2D manifest contains a null element or face-shape collection.");
        foreach (var element in source.Elements)
        {
            if (element is null)
                throw new InvalidDataException("The Panel2D manifest contains a null element.");
            ValidateGeometryValues(element.X, element.Y, element.Width, element.Height);
            if (element.Width <= 0 || element.Height <= 0)
                throw new InvalidDataException($"Panel element '{element.ObjectId}' must have positive width and height.");
            if (element.ReelLamps?.Any(lamp => lamp is null) == true)
                throw new InvalidDataException($"Panel element '{element.ObjectId}' has a null reel-lamp entry.");
        }
        foreach (var shape in source.FaceSourceShapes)
        {
            if (shape is null || shape.Points is null || shape.Points.Length != 4)
                throw new InvalidDataException("Each face source shape must contain four points.");
            if (shape.Points.Any(point => !double.IsFinite(point.X) || !double.IsFinite(point.Y)))
                throw new InvalidDataException($"Face source shape '{shape.Id}' has invalid coordinates.");
        }
        if (!Panel2DDocumentStorage.TryValidateAndNormalize(source, out var document, out var error))
            throw new InvalidDataException(error);
        return document;
    }

    private static string ReadProjectName(string projectFile)
    {
        try
        {
            using var json = JsonDocument.Parse(File.ReadAllText(projectFile));
            var metadata = json.RootElement;
            if (metadata.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException("The Oasis project metadata must be a JSON object.");
            if (!metadata.TryGetProperty("version", out var versionValue)
                || versionValue.ValueKind != JsonValueKind.Number
                || !versionValue.TryGetInt32(out var version) || version != 9)
                throw new InvalidDataException("The project must use schema 9.");
            if (!metadata.TryGetProperty("layout", out var layout) || layout.ValueKind != JsonValueKind.Object
                || !layout.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.String || assets.GetString() != "Assets"
                || !layout.TryGetProperty("generated", out var generated) || generated.ValueKind != JsonValueKind.String || generated.GetString() != "Generated")
                throw new InvalidDataException("The project must use the standard Assets and Generated folders.");
            if (!metadata.TryGetProperty("name", out var name) || name.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(name.GetString()))
                throw new InvalidDataException("The Oasis project name is required.");
            return name.GetString()!.Trim();
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("The Oasis project metadata is malformed.", exception);
        }
    }

    private static void ValidateGeometryValues(double x, double y, double width, double height)
    {
        if (!double.IsFinite(x) || !double.IsFinite(y) || !double.IsFinite(width) || !double.IsFinite(height))
            throw new InvalidDataException("Panel geometry values must be finite.");
    }

    private static ElementSnapshot Project(PanelElementFile element) => new(
        element.ObjectId, Panel2DDocumentStorage.SerializeElementKind(element.ElementKind), element.Name,
        element.X, element.Y, element.Width, element.Height, element.AssetPath, element.SecondaryAssetPath,
        element.OnColorHex ?? element.Native?.OnColorHex, element.OffColorHex ?? element.Native?.OffColorHex,
        element.TextColorHex ?? element.Native?.TextColorHex, element.Native?.DisplayColorHex,
        element.DisplayText ?? element.Native?.Text, element.IsVisible ?? true, element.LockTransform);

    private static string FindProjectFile(string directory)
    {
        var files = Directory.EnumerateFiles(directory, "*.oasisproj", SearchOption.TopDirectoryOnly).ToArray();
        return files.Length switch
        {
            1 => files[0],
            0 => throw new FileNotFoundException("The folder does not contain an Oasis project file.", directory),
            _ => throw new InvalidDataException("The folder contains more than one Oasis project file.")
        };
    }

    private static string SanitizeSegment(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var invalid = Path.GetInvalidFileNameChars().Concat("<>:\"/\\|?*".ToCharArray()).ToHashSet();
        var result = new string(name.Trim().Select(ch => invalid.Contains(ch) || char.IsControl(ch) ? '-' : ch).ToArray()).Trim('.', ' ');
        if (result.Length == 0 || result is "." or "..") throw new ArgumentException("Panel name does not contain any safe path characters.", nameof(name));
        return result;
    }

    private static void ValidateGeometry(double? x, double? y, double? width, double? height)
    {
        foreach (var value in new[] { x, y, width, height })
            if (value.HasValue && !double.IsFinite(value.Value)) throw new ArgumentOutOfRangeException(nameof(x), "Geometry values must be finite.");
        if (width.HasValue && width.Value <= 0 || height.HasValue && height.Value <= 0)
            throw new ArgumentOutOfRangeException(nameof(width), "Element dimensions must be greater than zero.");
    }

    private static void ValidateDocumentForSave(Panel2DDocumentFile document)
    {
        if (document.Elements is null || document.FaceSourceShapes is null || document.Elements.Any(element => element is null))
            throw new InvalidDataException("The Panel2D document contains a null collection or element.");
        foreach (var element in document.Elements)
        {
            ValidateGeometryValues(element.X, element.Y, element.Width, element.Height);
            if (element.Width <= 0 || element.Height <= 0)
                throw new InvalidDataException($"Panel element '{element.ObjectId}' must have positive width and height.");
        }
        if (!Panel2DDocumentStorage.TryValidateAndNormalize(document, out _, out var error))
            throw new InvalidDataException(error);
    }

    private static string Payload(Panel2DDocumentFile document)
        => JsonSerializer.Serialize(document with { SavedAtUtc = default });
    private static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    private static string HashFile(string path) => File.Exists(path) ? Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))) : string.Empty;

    private static void WriteAtomic(string path, string content)
    {
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temp, content, new UTF8Encoding(false));
            File.Move(temp, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
    }

    private void ResetHistory(Panel2DDocumentFile document)
    {
        _history.Clear();
        _history.Add(document);
        _historyIndex = 0;
    }
    private void ClearHistory() { _history.Clear(); _historyIndex = 0; }
    private void StorePanelState()
    {
        if (_panelPath is null || _document is null) return;
        _panelStates[_panelPath] = new PanelState(_history.ToArray(), _historyIndex, _savedContent, _diskHash, _selectedId, _selectedIds.ToArray());
    }
    private void RestorePanelState(PanelState state)
    {
        _history.Clear();
        _history.AddRange(state.History);
        _historyIndex = state.HistoryIndex;
        _document = _history[_historyIndex];
        _savedContent = state.SavedContent;
        _diskHash = state.DiskHash;
        _selectedId = state.SelectedId;
        _selectedIds = state.SelectedIds.ToList();
        KeepSelection();
    }

    private sealed record PanelState(Panel2DDocumentFile[] History, int HistoryIndex, string SavedContent, string? DiskHash, string? SelectedId, string[] SelectedIds);
    private void KeepSelection()
    {
        var existing = (_document?.Elements ?? []).Select(element => element.ObjectId).ToHashSet(StringComparer.Ordinal);
        _selectedIds = _selectedIds.Where(existing.Contains).Distinct(StringComparer.Ordinal).ToList();
        _selectedId = _selectedIds.FirstOrDefault();
    }
}
