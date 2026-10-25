using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using OasisEditor.Features.PrefabEditor.Views;

namespace OasisEditor.Automation;

public sealed partial class McpEditorSession
{
    private sealed class OpenPrefab(DocumentTabViewModel document, string path, Dictionary<string, string> files)
    {
        public DocumentTabViewModel Document { get; } = document;
        public string Path { get; } = path;
        public Dictionary<string, string> Files { get; set; } = files;
    }
    private readonly Dictionary<Guid, OpenPrefab> _prefabs = [];

    public PrefabSnapshot OpenPrefabDocument(string path)
    {
        var file = Confine(path);
        if (Path.GetFileName(file) != ProjectAssetPathService.Object3DManifestFileName
            || !string.Equals(Path.GetDirectoryName(Path.GetDirectoryName(file)), Path.Combine(Project.AssetsDirectory, "Object3D"), StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Expected Assets/Object3D/<Name>/asset.object3d in this project.");
        if (_prefabs.Values.Any(p => string.Equals(p.Path, file, StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("Prefab already open; use its session document ID.");
        var content = File.ReadAllText(file);
        if (!Object3DDocumentStorage.TryRead(content, out var model, out var error)) throw new InvalidDataException(error);
        ValidatePrefabPaths(model, Path.GetDirectoryName(file)!);
        var document = new DocumentTabViewModel(EditorDocument.CreateFromFile(file, "Prefab opened through MCP."), object3DDocumentJson: content);
        document.SetProjectAccessor(() => Project);
        _prefabs.Add(document.DocumentId, new(document, file, FingerprintPackage(Path.GetDirectoryName(file)!)));
        return InspectPrefab(document.DocumentId);
    }

    public PrefabSnapshot CreatePrefab(string name, string? modelSource = null)
    {
        ValidateName(name);
        var directory = Confine(Path.Combine(Project.AssetsDirectory, "Object3D", name));
        ValidateTree(Confine(Path.Combine(Project.AssetsDirectory, "Object3D")));
        if (Directory.Exists(directory) || File.Exists(directory)) throw new IOException("Prefab package already exists.");
        // A grouping Prefab needs no model; add supported components explicitly.
        var initial = Object3DDocument.Create(name);
        if (modelSource is null)
            initial = initial with { Root = initial.Root with { Components = initial.Root.Components.Where(c => c.Kind == Object3DComponentDefinition.TransformKind).ToArray() } };
        else
        {
            modelSource = Confine(modelSource);
            if (!File.Exists(modelSource) || !string.Equals(Path.GetExtension(modelSource), ".glb", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Model source must be an existing workspace GLB.");
            initial = initial with { Model = initial.Model with { Path = Path.GetFileName(modelSource) } };
        }
        var manifest = Path.Combine(directory, ProjectAssetPathService.Object3DManifestFileName);
        Directory.CreateDirectory(directory);
        if (modelSource is not null) File.Copy(modelSource, Path.Combine(directory, initial.Model.Path), false);
        File.WriteAllText(manifest, Object3DDocumentStorage.Serialize(initial));
        return OpenPrefabDocument(manifest);
    }

    public PrefabSnapshot InspectPrefab(Guid documentId)
    {
        var entry = GetPrefab(documentId);
        var model = entry.Document.GetObject3DDocument();
        var representable = model.TryProjectSingleRoot(out _, out _, out var error);
        return new(documentId, entry.Path, model, entry.Document.IsDirty, entry.Document.CommandService.CanUndo, entry.Document.CommandService.CanRedo, representable, error);
    }

    public PrefabSnapshot UpdatePrefabNode(Guid documentId, string nodeId, string? name = null, MachineObjectTransform? transform = null,
        Object3DComponentDefinition? component = null, string? removeComponentKind = null)
    {
        var entry = GetPrefab(documentId);
        var model = entry.Document.GetObject3DDocument();
        var found = false;
        Object3DNodeDefinition Edit(Object3DNodeDefinition node)
        {
            if (node.Id != nodeId) return node with { Children = node.Children.Select(Edit).ToArray() };
            found = true;
            var next = name is null ? node : node with { Name = name.Trim() };
            if (transform is not null) next = next with { Transform = transform };
            if (component is not null) next = next.WithComponent(component);
            if (removeComponentKind is not null) next = next.WithoutComponent(removeComponentKind);
            return next;
        }
        var updated = model with { Root = Edit(model.Root) };
        if (!found) throw new ArgumentException("Unknown Prefab node ID.");
        ValidatePrefabPaths(updated, Path.GetDirectoryName(entry.Path)!);
        entry.Document.SetObject3DDocumentFromAuthoring(updated, "Edit Prefab node via MCP");
        return InspectPrefab(documentId);
    }

    public PrefabSnapshot AddPrefabChild(Guid documentId, string parentNodeId, string name)
    {
        var authoring = GetPrefab(documentId).Document.PrefabAuthoring!;
        if (authoring.AddChild(parentNodeId, name) is null) throw new InvalidOperationException(authoring.Diagnostic);
        return InspectPrefab(documentId);
    }

    public PrefabSnapshot ReparentPrefabNode(Guid documentId, string nodeId, string parentNodeId, int? index)
    {
        var authoring = GetPrefab(documentId).Document.PrefabAuthoring!;
        authoring.ReparentNode(nodeId, parentNodeId, index);
        if (authoring.HasDiagnostic) throw new InvalidOperationException(authoring.Diagnostic);
        return InspectPrefab(documentId);
    }

    public PrefabSnapshot RemovePrefabNode(Guid documentId, string nodeId)
    {
        var authoring = GetPrefab(documentId).Document.PrefabAuthoring!;
        if (!authoring.RemoveNode(nodeId)) throw new InvalidOperationException(authoring.Diagnostic);
        return InspectPrefab(documentId);
    }

    public PrefabSnapshot PrefabHistory(Guid documentId, bool redo)
    {
        var commands = GetPrefab(documentId).Document.CommandService;
        if (redo) commands.TryRedo(); else commands.TryUndo();
        return InspectPrefab(documentId);
    }

    public PrefabSnapshot SavePrefab(Guid documentId)
    {
        var entry = GetPrefab(documentId);
        var current = FingerprintPackage(Path.GetDirectoryName(entry.Path)!);
        if (current.Count != entry.Files.Count || current.Any(f => !entry.Files.TryGetValue(f.Key, out var hash) || hash != f.Value))
            throw new IOException("Prefab package changed externally; reopen in a fresh session.");
        ValidatePrefabPaths(entry.Document.GetObject3DDocument(), Path.GetDirectoryName(entry.Path)!);
        new DocumentSaveService().SaveDocument(entry.Document, entry.Path, Project).ApplyTo(entry.Document);
        entry.Files = FingerprintPackage(Path.GetDirectoryName(entry.Path)!);
        return InspectPrefab(documentId);
    }

    public async Task<byte[]> CapturePrefabAsync(Guid documentId, int width, int height, ThemePreference theme)
    {
        if (width is < 800 or > 1920 || height is < 600 or > 1080) throw new ArgumentException("Prefab capture requires 800..1920 by 600..1080 pixels.");
        var entry = GetPrefab(documentId);
        ValidatePrefabPaths(entry.Document.GetObject3DDocument(), Path.GetDirectoryName(entry.Path)!);
        var authoring = entry.Document.PrefabAuthoring!;
        await authoring.Preview.WaitForRefreshAsync();
        new ApplicationThemeService().ApplyTheme(Application.Current, theme);
        await authoring.Preview.RefreshAsync();
        var view = new PrefabEditorView { DataContext = authoring, Width = width, Height = height };
        using var source = new HwndSource(new HwndSourceParameters("Oasis MCP Prefab capture", width, height)
        { WindowStyle = unchecked((int)0x90000000), ExtendedWindowStyle = 0x08000080, PositionX = -32000, PositionY = -32000 });
        try
        {
            source.RootVisual = view;
            view.Measure(new Size(width, height)); view.Arrange(new Rect(0, 0, width, height)); view.UpdateLayout();
            var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32); bitmap.Render(view);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = new MemoryStream(); encoder.Save(stream); return stream.ToArray();
        }
        finally { source.RootVisual = null; view.DataContext = null; view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent)); }
    }

    private OpenPrefab GetPrefab(Guid id) => _prefabs.TryGetValue(id, out var entry) ? entry : throw new ArgumentException("Unknown Prefab session document ID.");
    private void ValidatePrefabPaths(Object3DDocument model, string package)
    {
        Object3DValidationService.Validate(model);
        ValidateTree(Confine(package));
        void ValidateNode(Object3DNodeDefinition node)
        {
            foreach (var component in node.Components) if (component.Model is not null) Confine(Path.Combine(package, component.Model.Path));
            foreach (var child in node.Children) ValidateNode(child);
        }
        ValidateNode(model.Root);
    }
}

public sealed record PrefabSnapshot(Guid DocumentId, string Path, Object3DDocument Prefab, bool IsDirty, bool CanUndo, bool CanRedo,
    bool CanBuildSingleRoot, string BuildDiagnostic);
