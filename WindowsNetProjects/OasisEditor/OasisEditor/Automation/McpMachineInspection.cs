using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using OasisEditor.Features.MachineComposition.Views;
using OasisEditor.Progress;

namespace OasisEditor.Automation;

public sealed partial class McpEditorSession
{
    private readonly Dictionary<Guid, (DocumentTabViewModel Document, string Path, string Hash, string? BehaviorHash)> _machines = [];

    public static IReadOnlyList<string> ListRecentProjects() => new RecentProjectsStore().Load();

    public IReadOnlyList<string> ListMachineAssets()
    {
        var directory = Confine(Path.Combine(ProjectDirectory, "Assets", "Machines"));
        if (!Directory.Exists(directory)) return [];
        ValidateTree(directory);
        return Directory.EnumerateDirectories(directory).Select(d => Path.Combine(d, "asset.machine")).Where(File.Exists).Take(200).ToArray();
    }

    public MachineSnapshot OpenMachine(string path)
    {
        var file = Confine(path);
        if (Path.GetFileName(file) != "asset.machine" || !string.Equals(Path.GetDirectoryName(Path.GetDirectoryName(file)),
            Path.Combine(ProjectDirectory, "Assets", "Machines"), StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Expected Assets/Machines/<Name>/asset.machine in this project.");
        if (_machines.Values.Any(m => string.Equals(m.Path, file, StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("Machine already open; use its session document ID.");
        var content = File.ReadAllText(file);
        if (!MachineDocumentStorage.TryRead(content, out var machine, out var error)) throw new InvalidDataException(error);
        ValidateMachineAssets(machine!);
        var behavior = Confine(Path.Combine(Path.GetDirectoryName(file)!, "behavior.oasis"));
        if (File.Exists(behavior) && new FileInfo(behavior).Length > 256 * 1024) throw new InvalidDataException("Behavior source exceeds 256 KiB inspection limit.");
        var document = new DocumentTabViewModel(EditorDocument.CreateFromFile(file, "Machine inspected through MCP."), machineDocumentJson: content);
        document.SetProjectAccessor(() => Project);
        _machines.Add(document.DocumentId, (document, file, Hash(file), HashOptional(behavior)));
        return InspectMachine(document.DocumentId);
    }

    public MachineSnapshot InspectMachine(Guid documentId)
    {
        var entry = GetMachine(documentId);
        var model = entry.Document.GetMachineDocument();
        return new(documentId, entry.Path, model, entry.Document.MachineBehaviorSource,
            entry.Document.MachineBehaviorDiagnostics.Select(d => d.ToString() ?? "").Take(100).ToArray(),
            entry.Document.IsDirty, entry.Document.CommandService.CanUndo, entry.Document.CommandService.CanRedo);
    }

    public MachineRuntimeBuildResult BuildMachine(Guid documentId, CancellationToken cancellationToken)
    {
        var entry = GetMachine(documentId);
        if (entry.Document.IsDirty) throw new InvalidOperationException("Machine has unsaved edits; save it before building.");
        var behaviorPath = Confine(Path.Combine(Path.GetDirectoryName(entry.Path)!, MachineBehaviorDefinition.CanonicalSourcePath));
        if (!File.Exists(entry.Path) || Hash(entry.Path) != entry.Hash || HashOptional(behaviorPath) != entry.BehaviorHash)
            throw new IOException("Machine package changed on disk; reopen in a fresh session.");
        // This first inspection/build slice deliberately accepts project-only dependencies.
        // Validate the tree before the existing build pipeline follows model/asset references.
        ValidateMachineAssets(entry.Document.GetMachineDocument());
        ValidateTree(Confine(Project.GeneratedDirectory));
        return new MachineRuntimeBuildService(libraryRoot: "").BuildFromMachineDocument(Project, entry.Path, NoOpEditorProgressReporter.Instance, cancellationToken);
    }

    private void ValidateMachineAssets(MachineDocument machine)
    {
        ValidateTree(Confine(Project.AssetsDirectory));
        var references = machine.ObjectInstances.Select(o => o.ObjectAsset).Concat(machine.PrefabDependencies.Select(dependency => dependency.PrefabAsset)).Append(machine.CabinetAsset)
            .Concat(machine.ReelAssignments.Select(r => r.ReelAsset)).OfType<AssetReference>();
        foreach (var reference in references)
        {
            if (reference.Scope != AssetReferenceScope.Project) throw new InvalidOperationException("MCP machine builds currently require project-local assets; copy Library assets into the disposable project first.");
            Confine(Path.Combine(ProjectDirectory, reference.Path));
        }
        foreach (var face in machine.SurfaceAssignments) Confine(Path.Combine(ProjectDirectory, face.FaceAssetPath));
        // Inspect every JSON asset path before any build writes. The workspace is an
        // operational boundary, not an OS sandbox; do not use untrusted project data.
        foreach (var manifest in Directory.EnumerateFiles(Project.AssetsDirectory, "asset.*", SearchOption.AllDirectories))
        {
            using var json = JsonDocument.Parse(File.ReadAllText(manifest));
            ValidatePaths(json.RootElement, Path.GetDirectoryName(manifest)!);
        }
    }

    private void ValidatePaths(JsonElement value, string package)
    {
        if (value.ValueKind == JsonValueKind.Array) foreach (var item in value.EnumerateArray()) ValidatePaths(item, package);
        else if (value.ValueKind == JsonValueKind.Object)
            foreach (var property in value.EnumerateObject())
            {
                if (property.Name.EndsWith("path", StringComparison.OrdinalIgnoreCase) && property.Value.ValueKind == JsonValueKind.String
                    && !string.IsNullOrWhiteSpace(property.Value.GetString()))
                {
                    var path = property.Value.GetString()!;
                    Confine(Path.Combine(path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) || path.StartsWith("Generated/", StringComparison.OrdinalIgnoreCase) ? ProjectDirectory : package, path));
                }
                ValidatePaths(property.Value, package);
            }
    }

    public async Task<byte[]> CaptureMachineAsync(Guid documentId, int width, int height, ThemePreference theme)
    {
        if (width is < 640 or > 1920 || height is < 480 or > 1080) throw new ArgumentException("Machine capture requires width 640..1920 and height 480..1080.");
        var document = GetMachine(documentId).Document;
        ValidateMachineAssets(document.GetMachineDocument());
        var composition = document.MachineComposition3D!;
        await composition.WaitForRefreshAsync();
        new ApplicationThemeService().ApplyTheme(Application.Current, theme);
        var view = new MachineComposition3DView { DataContext = document, Width = width, Height = height };
        using var source = new HwndSource(new HwndSourceParameters("Oasis MCP machine capture", width, height)
        { WindowStyle = unchecked((int)0x90000000), ExtendedWindowStyle = 0x08000080, PositionX = -32000, PositionY = -32000 });
        try
        {
            source.RootVisual = view;
            view.Measure(new Size(width, height)); view.Arrange(new Rect(0, 0, width, height)); view.UpdateLayout();
            var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32); bitmap.Render(view);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var output = new MemoryStream(); encoder.Save(output); return output.ToArray();
        }
        finally { source.RootVisual = null; view.DataContext = null; view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent)); }
    }

    private (DocumentTabViewModel Document, string Path, string Hash, string? BehaviorHash) GetMachine(Guid id) =>
        _machines.TryGetValue(id, out var machine) ? machine : throw new ArgumentException("Unknown Machine session document ID.");
}

public sealed record MachineSnapshot(Guid DocumentId, string Path, MachineDocument Machine, string BehaviorSource, IReadOnlyList<string> BehaviorDiagnostics,
    bool IsDirty, bool CanUndo, bool CanRedo);
