using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using OasisEditor.Features.CabinetEditor.Services;
using OasisEditor.Features.MachineComposition.ViewModels;

namespace OasisEditor;

/// <summary>Transient source preview. GLB nodes remain private render geometry, selected as their authored owner.</summary>
public sealed class PrefabPreviewViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly DocumentTabViewModel _document;
    private readonly ICabinetModelLoader _loader;
    private readonly Dictionary<string, Model3DGroup> _imports = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Model3DGroup> _nodeVisuals = new(StringComparer.Ordinal);
    private readonly Dictionary<GeometryModel3D, string> _hitOwners = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<string, List<Model3D>> _ownedVisuals = new(StringComparer.Ordinal);
    private readonly Dictionary<string, PrefabNode> _renderedNodes = new(StringComparer.Ordinal);
    private CancellationTokenSource _cancellation = new();
    private bool _disposed, _showModels = true, _showColliders = true;
    private int _version;
    private Model3DGroup _visual = new();
    private Point3D _cameraPosition = new(3, 2, 4);
    private Vector3D _cameraLookDirection = new(-3, -2, -4);
    public PrefabPreviewViewModel(DocumentTabViewModel document, ICabinetModelLoader loader)
    {
        _document = document; _loader = loader;
        ResetCameraCommand = new RelayCommand(ResetCamera);
        Refresh();
    }
    public event PropertyChangedEventHandler? PropertyChanged;
    public ObservableCollection<string> Diagnostics { get; } = [];
    public Model3DGroup Visual => _visual;
    public Rect3D SelectedBounds
    {
        get
        {
            var node = _document.SelectedPrefabNode;
            var bounds = _nodeVisuals.GetValueOrDefault(node.Id)?.Bounds ?? Rect3D.Empty;
            var root = _document.GetObject3DDocument().Root;
            while (PrefabAuthoringService.Parent(root, node.Id) is { } parent)
            {
                bounds = MachineCompositionTransform.Create(parent.Component(PrefabComponentKind.Transform)!.Transform!, new("", 1, "Y")).TransformBounds(bounds);
                node = parent;
            }
            return bounds;
        }
    }
    public bool ShowModels { get => _showModels; set { if (_showModels == value) return; _showModels = value; Notify(nameof(ShowModels)); _renderedNodes.Clear(); Refresh(); } }
    public bool ShowColliders { get => _showColliders; set { if (_showColliders == value) return; _showColliders = value; Notify(nameof(ShowColliders)); _renderedNodes.Clear(); Refresh(); } }
    public Point3D CameraPosition { get => _cameraPosition; set { _cameraPosition = value; Notify(nameof(CameraPosition)); } }
    public Vector3D CameraLookDirection { get => _cameraLookDirection; set { _cameraLookDirection = value; Notify(nameof(CameraLookDirection)); } }
    public ICommand ResetCameraCommand { get; }
    public bool SelectGeometry(GeometryModel3D geometry)
    {
        if (!_hitOwners.TryGetValue(geometry, out var id)) return false;
        _document.SelectionState.Replace(new EditorSelectionItem(EditorSelectionDomain.PrefabNode, id)); return true;
    }
    public void NotifySelection() => Notify(nameof(SelectedBounds));
    public void Refresh() => RefreshTask = RefreshAsync();
    internal Task RefreshTask { get; private set; } = Task.CompletedTask;
    private async Task RefreshAsync()
    {
        if (_disposed) return;
        var version = ++_version;
        _cancellation.Cancel(); _cancellation.Dispose(); _cancellation = new();
        var token = _cancellation.Token;
        var document = _document.GetObject3DDocument();
        var nodes = Object3DValidationService.Descendants(document.Root).ToArray();
        var diagnostics = new List<string>();
        try
        {
            foreach (var node in nodes)
            {
                if (node.Component(PrefabComponentKind.Model)?.Model is not { } model) continue;
                var path = ModelPath(node.Id, model.Path);
                if (path is null) { diagnostics.Add($"Prefab '{document.Id}', node '{node.Id}', Model.path: import a GLB or save into a package."); continue; }
                if (_imports.ContainsKey(path)) continue;
                var loaded = await _loader.LoadAsync(path, token);
                if (token.IsCancellationRequested || _disposed || version != _version) return;
                if (loaded.Succeeded && loaded.Model is not null) { _imports[path] = loaded.Model; _renderedNodes.Remove(node.Id); }
                else diagnostics.Add($"Prefab '{document.Id}', node '{node.Id}', Model.path: {loaded.ErrorMessage}");
            }
        }
        catch (OperationCanceledException) { return; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
        { diagnostics.Add($"Prefab '{document.Id}', preview: {ex.Message}"); }
        if (token.IsCancellationRequested || _disposed || version != _version) return;
        Diagnostics.Clear(); foreach (var diagnostic in diagnostics) Diagnostics.Add(diagnostic);
        var activePaths = nodes.Select(n => n.Component(PrefabComponentKind.Model)?.Model is { } m ? ModelPath(n.Id, m.Path) : null).OfType<string>().ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var stale in _imports.Keys.Where(k => !activePaths.Contains(k)).ToArray()) _imports.Remove(stale);
        foreach (var stale in _nodeVisuals.Keys.Where(k => nodes.All(n => n.Id != k)).ToArray()) { _nodeVisuals.Remove(stale); _renderedNodes.Remove(stale); _ownedVisuals.Remove(stale); }
        var rootVisual = Build(document.Root);
        if (_visual.Children.Count == 0 || !ReferenceEquals(_visual.Children[0], rootVisual)) { _visual.Children.Clear(); _visual.Children.Add(rootVisual); }
        _hitOwners.Clear(); MapOwners(document.Root);
        Notify(nameof(Visual)); NotifySelection();
    }
    private string? ModelPath(string nodeId, string path)
    {
        if (_document.PendingPrefabModelSources.TryGetValue(nodeId, out var source)) return source;
        return _document.Document.IsUntitled ? null : Path.GetFullPath(Path.Combine(Path.GetDirectoryName(_document.FilePath)!, path));
    }
    private Model3DGroup Build(PrefabNode node)
    {
        if (!_nodeVisuals.TryGetValue(node.Id, out var visual)) _nodeVisuals[node.Id] = visual = new();
        var previous = _renderedNodes.GetValueOrDefault(node.Id);
        var modelComponent = node.Component(PrefabComponentKind.Model);
        var colliderComponent = node.Component(PrefabComponentKind.Collider);
        if (previous is null || previous.Component(PrefabComponentKind.Model) != modelComponent || previous.Component(PrefabComponentKind.Collider) != colliderComponent)
        {
            var own = new List<Model3D>();
            var model = modelComponent?.Model;
            Model3DGroup? imported = null;
            if (model is not null && ModelPath(node.Id, model.Path) is { } path) _imports.TryGetValue(path, out imported);
            if (ShowModels && model is not null && imported is not null)
            {
                var rendering = imported.Clone();
                rendering.Transform = MachineCompositionTransform.Create(MachineObjectTransform.Identity, model);
                own.Add(rendering);
            }
            if (ShowColliders && colliderComponent is { Collider: { Kind: not Object3DColliderKind.None } collider } component)
            {
                var preview = new Model3DGroup();
                if (collider.Kind == Object3DColliderKind.Mesh && imported is not null) preview.Children.Add(Tint(imported, component.IsTrigger == true));
                else if (collider.Kind != Object3DColliderKind.Mesh) preview.Children.Add(PrefabColliderPreview.Create(collider, component.IsTrigger == true));
                if (component.SourceFrame == PrefabColliderSourceFrame.ModelLocal && model is not null) preview.Transform = MachineCompositionTransform.Create(MachineObjectTransform.Identity, model);
                own.Add(preview);
            }
            _ownedVisuals[node.Id] = own;
        }
        var desired = _ownedVisuals[node.Id].Concat(node.Children.Select(Build)).ToArray();
        for (var i = visual.Children.Count - 1; i >= 0; i--) if (!desired.Contains(visual.Children[i])) visual.Children.RemoveAt(i);
        for (var i = 0; i < desired.Length; i++)
        {
            var index = visual.Children.IndexOf(desired[i]);
            if (index < 0) visual.Children.Insert(i, desired[i]);
            else if (index != i) { visual.Children.RemoveAt(index); visual.Children.Insert(i, desired[i]); }
        }
        if (previous?.Component(PrefabComponentKind.Transform) != node.Component(PrefabComponentKind.Transform))
            visual.Transform = MachineCompositionTransform.Create(node.Component(PrefabComponentKind.Transform)!.Transform!, new("", 1, "Y"));
        _renderedNodes[node.Id] = node;
        return visual;
    }
    private void MapOwners(PrefabNode node)
    {
        if (_nodeVisuals.TryGetValue(node.Id, out var group)) foreach (var model in group.Children)
            if (!node.Children.Any(n => _nodeVisuals.GetValueOrDefault(n.Id) == model)) MapGeometry(model, node.Id);
        foreach (var child in node.Children) MapOwners(child);
    }
    private void MapGeometry(Model3D model, string id)
    {
        if (model is GeometryModel3D geometry) _hitOwners[geometry] = id;
        if (model is Model3DGroup group) foreach (var child in group.Children) MapGeometry(child, id);
    }
    private static Model3DGroup Tint(Model3DGroup imported, bool trigger)
    {
        var group = imported.Clone();
        void Apply(Model3D model)
        {
            if (model is GeometryModel3D g) g.Material = g.BackMaterial = PrefabColliderPreview.Material(trigger);
            else if (model is Model3DGroup children) foreach (var child in children.Children) Apply(child);
        }
        Apply(group); return group;
    }
    private void ResetCamera()
    {
        var b = Visual.Bounds;
        var center = b.IsEmpty ? new Point3D() : new Point3D(b.X + b.SizeX / 2, b.Y + b.SizeY / 2, b.Z + b.SizeZ / 2);
        var distance = b.IsEmpty ? 4 : Math.Max(.25, Math.Max(b.SizeX, Math.Max(b.SizeY, b.SizeZ)) * 2);
        var offset = new Vector3D(distance, distance * .6, distance);
        CameraPosition = center + offset; CameraLookDirection = -offset;
    }
    private void Notify(string name) => PropertyChanged?.Invoke(this, new(name));
    public void Dispose() { if (_disposed) return; _disposed = true; _version++; _cancellation.Cancel(); _cancellation.Dispose(); _imports.Clear(); _hitOwners.Clear(); }
}

internal static class PrefabColliderPreview
{
    // Diagnostic geometry colours match the existing collider/trigger projection palette.
    public static Material Material(bool trigger) => new DiffuseMaterial(new SolidColorBrush(trigger ? Color.FromArgb(105, 255, 90, 185) : Color.FromArgb(105, 40, 170, 255)));
    public static GeometryModel3D Create(Object3DColliderDefinition collider, bool trigger)
    {
        var mesh = new MeshGeometry3D();
        var center = collider.Center ?? [0,0,0];
        Point3D Point(double x, double y, double z) => new(x + center[0], y + center[1], z + center[2]);
        if (collider.Kind == Object3DColliderKind.Box)
        {
            var s = collider.Size!;
            for (var i = 0; i < 8; i++) mesh.Positions.Add(Point(((i & 1) == 0 ? -.5 : .5) * s[0], ((i & 2) == 0 ? -.5 : .5) * s[1], ((i & 4) == 0 ? -.5 : .5) * s[2]));
            foreach (var i in new[] {0,2,3,0,3,1,4,5,7,4,7,6,0,1,5,0,5,4,2,6,7,2,7,3,0,4,6,0,6,2,1,3,7,1,7,5}) mesh.TriangleIndices.Add(i);
        }
        else
        {
            const int rings = 16, segments = 24;
            var radius = collider.Radius!.Value;
            var halfCylinder = collider.Kind == Object3DColliderKind.Capsule ? collider.Height!.Value / 2 - radius : 0;
            var totalRings = halfCylinder > 0 ? rings + 1 : rings;
            for (var ring = 0; ring <= totalRings; ring++)
            {
                var sphereRing = halfCylinder > 0 && ring > rings / 2 ? ring - 1 : ring;
                var phi = Math.PI * sphereRing / rings;
                var y = radius * Math.Cos(phi) + (ring <= rings / 2 ? halfCylinder : -halfCylinder);
                for (var segment = 0; segment <= segments; segment++)
                {
                    var theta = Math.PI * 2 * segment / segments;
                    var x = radius * Math.Sin(phi) * Math.Cos(theta); var z = radius * Math.Sin(phi) * Math.Sin(theta);
                    mesh.Positions.Add(collider.Axis == Object3DCapsuleAxis.X ? Point(y,x,z) : collider.Axis == Object3DCapsuleAxis.Z ? Point(x,z,y) : Point(x,y,z));
                }
            }
            for (var r = 0; r < totalRings; r++) for (var c = 0; c < segments; c++)
            {
                var a = r * (segments + 1) + c; var b = a + segments + 1;
                foreach (var i in new[] {a,b,a+1,a+1,b,b+1}) mesh.TriangleIndices.Add(i);
            }
        }
        var material = Material(trigger);
        return new(mesh, material) { BackMaterial = material };
    }
}
