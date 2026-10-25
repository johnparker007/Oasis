using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using OasisEditor.Features.LayoutImport;

namespace OasisEditor.Automation;

/// <summary>A single isolated automation session. Call only from its owning STA dispatcher.</summary>
public sealed class McpEditorSession
{
    private readonly string _workspace;
    private readonly IFmlAutomationImportService _importer;
    private readonly Dictionary<Guid, OpenPanel> _panels = [];
    private string? _projectFile;
    private string ProjectDirectory => Path.GetDirectoryName(_projectFile ?? throw new InvalidOperationException("Create or open a project first."))!;

    public McpEditorSession(string workspace) : this(workspace, new FmlAutomationImportService()) { }

    internal McpEditorSession(string workspace, IFmlAutomationImportService importer)
    {
        _workspace = Path.GetFullPath(workspace);
        if (!Directory.Exists(_workspace)) throw new DirectoryNotFoundException("The explicit workspace directory must already exist.");
        _importer = importer;
        Confine(_workspace);
    }

    public ProjectSnapshot CreateProject(string name)
    {
        RequireNoProject();
        ValidateName(name);
        Confine(Path.Combine(_workspace, name));
        var directory = new ProjectContainerCreationService().CreateProjectContainer(name, _workspace);
        _projectFile = Path.Combine(directory, name + ".oasisproj");
        return InspectProject();
    }

    public ProjectSnapshot OpenProject(string path)
    {
        RequireNoProject();
        var file = Confine(path);
        if (!string.Equals(Path.GetExtension(file), ".oasisproj", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Expected an .oasisproj file.");
        using var json = JsonDocument.Parse(File.ReadAllText(file));
        var root = json.RootElement;
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("version", out var version)
            || version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out var schema) || schema != EditorProject.CurrentSchemaVersion)
            throw new InvalidDataException("Missing or unsupported project schema version.");
        if (!root.TryGetProperty("name", out var projectName) || projectName.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(projectName.GetString())) throw new InvalidDataException("Project name is missing.");
        // The first MCP slice deliberately supports only the standard current layout.
        if (!root.TryGetProperty("layout", out var layout) || layout.ValueKind != JsonValueKind.Object
            || !layout.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.String || assets.GetString() != "Assets"
            || !layout.TryGetProperty("generated", out var generated) || generated.ValueKind != JsonValueKind.String || generated.GetString() != "Generated")
            throw new InvalidDataException("MCP currently requires the standard Assets/Generated layout.");
        Confine(Path.Combine(Path.GetDirectoryName(file)!, "Assets"));
        Confine(Path.Combine(Path.GetDirectoryName(file)!, "Generated"));
        _projectFile = file;
        return InspectProject();
    }

    public ProjectSnapshot InspectProject() => new(_projectFile ?? throw new InvalidOperationException("No project open."),
        _panels.Values.Select(p => Describe(p, 0, 0)).ToArray());

    public PanelSnapshot CreatePanel(string name)
    {
        ValidateName(name);
        var directory = Confine(Path.Combine(ProjectDirectory, "Assets", "Panel2D", name));
        if (Directory.Exists(directory)) throw new IOException("Panel package already exists; refusing to overwrite it.");
        var panel = new Panel2DDocumentCreationService().CreatePanel2DStubDocument(name, _panels.Count + 1);
        var entry = new OpenPanel(panel, Path.Combine(directory, ProjectAssetPathService.Panel2DManifestFileName));
        Directory.CreateDirectory(directory);
        _panels.Add(panel.DocumentId, entry);
        return SavePanel(panel.DocumentId);
    }

    public PanelSnapshot OpenPanelDocument(string path)
    {
        var file = Confine(path);
        var panelRoot = Path.Combine(ProjectDirectory, "Assets", "Panel2D") + Path.DirectorySeparatorChar;
        if (!file.StartsWith(panelRoot, StringComparison.OrdinalIgnoreCase) || Path.GetFileName(file) != ProjectAssetPathService.Panel2DManifestFileName
            || !string.Equals(Path.GetDirectoryName(Path.GetDirectoryName(file)), panelRoot.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Expected Assets/Panel2D/<Name>/asset.panel2d in the open project.");
        if (_panels.Values.Any(p => string.Equals(p.Path, file, StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("Panel already open; use its document ID.");
        var content = File.ReadAllText(file);
        var opened = DocumentWorkspaceViewModel.BuildOpenDocumentData(file, content);
        if (opened.PanelLayoutJson is null) throw new InvalidDataException(opened.Summary);
        var panel = new DocumentTabViewModel(EditorDocument.CreateFromFile(file, opened.Summary), opened.PanelLayoutJson);
        ValidatePanelAssets(panel);
        var entry = new OpenPanel(panel, file) { SavedHash = Hash(file) };
        _panels.Add(panel.DocumentId, entry);
        return Describe(entry);
    }

    public ImportSnapshot ImportFml(Guid documentId, string source)
    {
        var entry = Get(documentId);
        var file = Confine(source);
        if (!File.Exists(file) || !string.Equals(Path.GetExtension(file), ".fml", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Source must be an existing .fml inside the workspace.");
        ValidateImportDestination();
        var result = _importer.ImportFromFml(file, ProjectDirectory, Confine(Path.Combine(ProjectDirectory, "Assets")), true);
        if (result.Succeeded)
            entry.Document.CommandService.Execute(new ImportPanelElementsCommand(documentId, entry.Document, result.ImportedElements));
        return new(result.Succeeded, Describe(entry), result.ImportedElements.Count,
            result.Warnings.Take(100).Select(w => w.ToString() ?? "").ToArray(), result.Errors.Take(100).ToArray(),
            result.CopiedAssetRelativePaths.Count, result.CopiedAssetRelativePaths.Take(100).ToArray());
    }

    public PanelSnapshot SavePanel(Guid documentId)
    {
        var entry = Get(documentId);
        Confine(entry.Path);
        if (entry.SavedHash is null ? File.Exists(entry.Path) : !File.Exists(entry.Path) || Hash(entry.Path) != entry.SavedHash)
            throw new IOException("Panel changed on disk; refusing to overwrite external changes.");
        new DocumentSaveService().SaveDocument(entry.Document, entry.Path).ApplyTo(entry.Document);
        entry.SavedHash = Hash(entry.Path);
        return Describe(entry);
    }

    public PanelSnapshot InspectPanel(Guid documentId, int offset = 0, int limit = 50)
    {
        if (offset < 0 || limit < 1 || limit > 200) throw new ArgumentOutOfRangeException(nameof(limit), "Use offset >= 0 and limit 1..200.");
        return Describe(Get(documentId), offset, limit);
    }

    public PanelSnapshot Undo(Guid documentId)
    {
        var entry = Get(documentId);
        entry.Document.CommandService.TryUndo();
        return Describe(entry);
    }

    public PanelSnapshot Redo(Guid documentId)
    {
        var entry = Get(documentId);
        entry.Document.CommandService.TryRedo();
        return Describe(entry);
    }

    public byte[] CapturePanel(Guid documentId, int width, int height, ThemePreference theme)
    {
        var panel = Get(documentId).Document;
        ValidatePanelAssets(panel);
        return McpPanelCapture.Capture(panel, ProjectDirectory, width, height, theme);
    }

    private void ValidatePanelAssets(DocumentTabViewModel panel)
    {
        foreach (var element in panel.GetPanelElements())
            foreach (var path in new[] { element.AssetPath, element.SecondaryAssetPath, element.SourceOffImageAssetPath, element.ReelLampTransmissionMaskAssetPath })
                if (!string.IsNullOrWhiteSpace(path)) Confine(Path.Combine(ProjectDirectory, path));
    }

    private void ValidateImportDestination()
    {
        var root = Confine(Path.Combine(ProjectDirectory, "Assets", "FmlImport"));
        if (!Directory.Exists(root)) return;
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.TryPop(out var directory))
            foreach (var child in Directory.EnumerateFileSystemEntries(directory))
            {
                Confine(child);
                if (Directory.Exists(child)) pending.Push(child);
            }
    }

    private OpenPanel Get(Guid id) => _panels.TryGetValue(id, out var panel) ? panel : throw new ArgumentException("Unknown document ID for this session.");
    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    private void RequireNoProject() { if (_projectFile is not null) throw new InvalidOperationException("One project per MCP process; start a new session to switch projects."); }
    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name != name.Trim() || name is "." or ".." || name.EndsWith('.') || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            throw new ArgumentException("Use a nonblank Windows file name without path separators, trailing spaces or dots.");
    }

    private string Confine(string path)
    {
        var full = Path.GetFullPath(path, _workspace);
        if (!string.Equals(full, _workspace, StringComparison.OrdinalIgnoreCase) && !full.StartsWith(_workspace.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("Path must remain inside the explicitly configured workspace.");
        for (var current = full; current is not null; current = Path.GetDirectoryName(current))
            if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new UnauthorizedAccessException("Junctions and symbolic links are not supported in MCP workspace paths.");
        return full;
    }

    private static PanelSnapshot Describe(OpenPanel entry, int offset = 0, int limit = 50)
    {
        var elements = entry.Document.GetPanelElements();
        return new(entry.Document.DocumentId, entry.Document.Document.Title, entry.Path, entry.Document.IsDirty,
            entry.Document.CommandService.CanUndo, entry.Document.CommandService.CanRedo, elements.Count, offset,
            elements.Skip(offset).Take(limit).Select(e => new PanelElementSnapshot(e.ObjectId, e.Name, e.Kind.ToString(), e.X, e.Y, e.Width, e.Height)).ToArray());
    }

    private sealed class OpenPanel(DocumentTabViewModel document, string path)
    {
        public DocumentTabViewModel Document { get; } = document;
        public string Path { get; } = path;
        public string? SavedHash { get; set; }
    }
}

public sealed record ProjectSnapshot(string ProjectFile, IReadOnlyList<PanelSnapshot> OpenPanels);
public sealed record PanelSnapshot(Guid DocumentId, string Name, string Path, bool IsDirty, bool CanUndo, bool CanRedo,
    int ElementCount, int Offset, IReadOnlyList<PanelElementSnapshot> Elements);
public sealed record PanelElementSnapshot(string Id, string Name, string Kind, double X, double Y, double Width, double Height);
public sealed record ImportSnapshot(bool Succeeded, PanelSnapshot Panel, int ImportedCount,
    IReadOnlyList<string> Warnings, IReadOnlyList<string> Errors, int CopiedAssetCount, IReadOnlyList<string> CopiedAssetPaths);
