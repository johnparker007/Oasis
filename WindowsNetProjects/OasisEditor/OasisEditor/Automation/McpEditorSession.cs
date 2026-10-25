using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using OasisEditor.Features.LayoutImport;

namespace OasisEditor.Automation;

/// <summary>A single isolated automation session. Call only from its owning STA dispatcher.</summary>
public sealed partial class McpEditorSession
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
        _panels.Values.Select(p => Describe(p, 0, 0)).ToArray(), _faces.Values.Select(f => DescribeFace(f, 0, 0)).ToArray(),
        _machines.Select(m => new MachineDocumentReference(m.Key, m.Value.Path)).ToArray());

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

    public PanelElementSnapshot InspectElement(Guid documentId, string elementId)
    {
        var panel = Get(documentId).Document;
        return DescribeElement(panel, FindElement(panel, elementId));
    }

    public PanelSnapshot SelectElements(Guid documentId, IReadOnlyList<string> elementIds)
    {
        ArgumentNullException.ThrowIfNull(elementIds);
        if (elementIds.Count > 200) throw new ArgumentException("Select at most 200 elements per call.");
        var panel = Get(documentId).Document;
        var selected = elementIds.Distinct(StringComparer.Ordinal).Select(id =>
            new EditorSelectionItem(EditorSelectionDomain.PanelElement, FindElement(panel, id).ObjectId)).ToArray();
        // Validate every ID before replacing selection. Selection is transient, not an authored edit.
        panel.SelectionState.Replace(selected);
        return Describe(Get(documentId));
    }

    public ElementEditSnapshot UpdateElement(Guid documentId, string elementId, PanelElementEdit edit)
    {
        ArgumentNullException.ThrowIfNull(edit);
        var panel = Get(documentId).Document;
        var original = FindElement(panel, elementId);
        if (edit.Name is not null && string.IsNullOrWhiteSpace(edit.Name)) throw new ArgumentException("Element name must not be blank.");
        var updated = PanelElementModelCloner.Clone(original, name: edit.Name, x: edit.X, y: edit.Y,
            width: edit.Width, height: edit.Height, isVisible: edit.IsVisible, isTransformLocked: edit.IsTransformLocked);
        if (!PanelElementValidation.IsValidForInspectorUpdate(updated)) throw new ArgumentException("Position must be finite and dimensions must be positive and finite.");
        if (original.IsTransformLocked && (original.X != updated.X || original.Y != updated.Y || original.Width != updated.Width || original.Height != updated.Height))
            throw new InvalidOperationException("Element transform is locked. Unlock it in a separate edit before changing geometry.");
        panel.CommandService.Execute(CanvasMutationCommands.CreateUpdateElementCommand(documentId, panel, elementId, updated, "Edit panel element via MCP"));
        return new(Describe(Get(documentId)), DescribeElement(panel, FindElement(panel, elementId)));
    }

    public PanelViewSnapshot SetPanelView(Guid documentId, double zoom, double panX = 0, double panY = 0)
    {
        if (!double.IsFinite(zoom) || zoom < EditorViewportTransform.MinZoom || zoom > EditorViewportTransform.MaxZoom
            || !double.IsFinite(panX) || !double.IsFinite(panY)) throw new ArgumentException("Use finite pan coordinates and zoom between 0.01 and 64 (1 = 100%).");
        var panel = Get(documentId).Document;
        panel.PanelZoom = zoom;
        panel.PanelPanX = panX;
        panel.PanelPanY = panY;
        return DescribeView(panel);
    }

    private static PanelElementModel FindElement(DocumentTabViewModel panel, string id)
    {
        if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Element ID is required.");
        return panel.GetPanelElements().FirstOrDefault(e => string.Equals(e.ObjectId, id, StringComparison.Ordinal))
            ?? throw new ArgumentException("Element ID does not exist in this document.");
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

    public byte[] CapturePanel(Guid documentId, int width, int height, ThemePreference theme, string framing = "current", string? elementId = null)
    {
        var panel = Get(documentId).Document;
        if (framing is not ("current" or "fit" or "element")) throw new ArgumentException("Framing must be current, fit or element.");
        if (framing != "element" && elementId is not null) throw new ArgumentException("elementId requires element framing.");
        var target = framing == "element" ? FindElement(panel, elementId ?? "") : null;
        if (target is not null && !PanelElementValidation.IsValidForInspectorUpdate(target)) throw new InvalidOperationException("Target element has invalid geometry.");
        ValidatePanelAssets(panel);
        return McpPanelCapture.Capture(panel, ProjectDirectory, width, height, theme, framing,
            target is null ? null : new System.Windows.Rect(target.X, target.Y, target.Width, target.Height));
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
            elements.Skip(offset).Take(limit).Select(e => DescribeElement(entry.Document, e)).ToArray(),
            entry.Document.SelectionState.Items.Where(i => i.Domain == EditorSelectionDomain.PanelElement).Select(i => i.ObjectId).ToArray(), DescribeView(entry.Document));
    }

    private static PanelElementSnapshot DescribeElement(DocumentTabViewModel document, PanelElementModel element) =>
        new(element.ObjectId, element.Name, element.Kind.ToString(), element.X, element.Y, element.Width, element.Height,
            element.IsVisible, element.IsTransformLocked, document.SelectionState.Items.Any(i => i.Domain == EditorSelectionDomain.PanelElement && i.ObjectId == element.ObjectId));

    private static PanelViewSnapshot DescribeView(DocumentTabViewModel panel) => new(panel.PanelZoom, panel.PanelPanX, panel.PanelPanY);

    private sealed class OpenPanel(DocumentTabViewModel document, string path)
    {
        public DocumentTabViewModel Document { get; } = document;
        public string Path { get; } = path;
        public string? SavedHash { get; set; }
    }
}

public sealed record ProjectSnapshot(string ProjectFile, IReadOnlyList<PanelSnapshot> OpenPanels, IReadOnlyList<FaceSnapshot> OpenFaces, IReadOnlyList<MachineDocumentReference> OpenMachines);
public sealed record MachineDocumentReference(Guid DocumentId, string Path);
public sealed record PanelSnapshot(Guid DocumentId, string Name, string Path, bool IsDirty, bool CanUndo, bool CanRedo,
    int ElementCount, int Offset, IReadOnlyList<PanelElementSnapshot> Elements, IReadOnlyList<string> SelectedElementIds, PanelViewSnapshot View);
public sealed record PanelElementSnapshot(string Id, string Name, string Kind, double X, double Y, double Width, double Height, bool IsVisible, bool IsTransformLocked, bool IsSelected);
public sealed record PanelElementEdit(string? Name = null, double? X = null, double? Y = null, double? Width = null, double? Height = null,
    bool? IsVisible = null, bool? IsTransformLocked = null);
public sealed record ElementEditSnapshot(PanelSnapshot Panel, PanelElementSnapshot Element);
public sealed record PanelViewSnapshot(double Zoom, double PanX, double PanY);
public sealed record ImportSnapshot(bool Succeeded, PanelSnapshot Panel, int ImportedCount,
    IReadOnlyList<string> Warnings, IReadOnlyList<string> Errors, int CopiedAssetCount, IReadOnlyList<string> CopiedAssetPaths);
