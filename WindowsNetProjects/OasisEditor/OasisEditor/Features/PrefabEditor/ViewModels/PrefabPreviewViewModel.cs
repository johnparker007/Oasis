using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
using OasisEditor.Features.CabinetEditor.Services;
using OasisEditor.Features.CabinetEditor.ViewModels;
using OasisEditor.Features.MachineComposition.ViewModels;

namespace OasisEditor.Features.PrefabEditor.ViewModels;

/// <summary>Transient model and collider projection for a single authored Prefab.</summary>
public sealed class PrefabPreviewViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly DocumentTabViewModel _document;
    private readonly ICabinetModelLoader _loader = new SharpGltfWpfModelLoader();
    private readonly Dictionary<ModelCacheKey, Model3DGroup> _modelCache = [];
    private CancellationTokenSource _refreshToken = new();
    private int _revision;
    private bool _refreshQueued;
    private Task _refreshTask = Task.CompletedTask;
    private bool _disposed;
    private bool _hasFramed;
    private Model3DGroup _visualModel = new();
    private Model3DGroup _colliderPreviewModel = new();
    private string _status = "No model is assigned to this Prefab.";

    public PrefabPreviewViewModel(DocumentTabViewModel document)
    {
        _document = document;
        Viewport = new CabinetViewportViewModel();
        ResetCameraCommand = Viewport.ResetCameraCommand;
        _document.PropertyChanged += OnDocumentPropertyChanged;
        _refreshTask = RefreshAsync();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public ObservableCollection<string> Diagnostics { get; } = [];
    public CabinetViewportViewModel Viewport { get; }
    public System.Windows.Input.ICommand ResetCameraCommand { get; }
    public Model3DGroup VisualModel { get => _visualModel; private set { _visualModel = value; Notify(); } }
    public Model3DGroup ColliderPreviewModel { get => _colliderPreviewModel; private set { _colliderPreviewModel = value; Notify(); } }
    public string Status { get => _status; private set { if (_status == value) return; _status = value; Notify(); } }
    public Task WaitForRefreshAsync() => _refreshTask;
    public string[] NodeLabels { get; private set; } = [];
    public double GridSize => Math.Max(.1, Math.Max(Viewport.ModelBounds.SizeX, Math.Max(Viewport.ModelBounds.SizeY, Viewport.ModelBounds.SizeZ)) * 3);
    public double GridSpacing => GridSize / 10;
    public double GridThickness => GridSize / 500;

    public async Task RefreshAsync()
    {
        if (_disposed) return;
        var revision = Interlocked.Increment(ref _revision);
        var old = _refreshToken;
        _refreshToken = new CancellationTokenSource();
        old.Cancel(); old.Dispose();
        var token = _refreshToken.Token;
        var document = _document.GetObject3DDocument();
        var visual = new Model3DGroup();
        var colliders = new Model3DGroup();
        var diagnostics = new List<string>();
        try
        {
            await BuildNodeAsync(document.Root, visual, colliders, diagnostics, token);
            token.ThrowIfCancellationRequested();
            if (_disposed || revision != _revision) return;
            Freeze(visual); Freeze(colliders);
            VisualModel = visual;
            ColliderPreviewModel = colliders;
            Diagnostics.Clear(); foreach (var diagnostic in diagnostics) Diagnostics.Add(diagnostic);
            var bounds = visual.Bounds;
            if (!colliders.Bounds.IsEmpty) bounds.Union(colliders.Bounds);
            var position = Viewport.CameraPosition;
            var direction = Viewport.CameraLookDirection;
            Viewport.SetLoadedModels(visual, colliders, null, bounds);
            Notify(nameof(GridSize)); Notify(nameof(GridSpacing)); Notify(nameof(GridThickness));
            if (_hasFramed)
            {
                Viewport.CameraPosition = position;
                Viewport.CameraLookDirection = direction;
            }
            if (!bounds.IsEmpty) _hasFramed = true;
            Status = diagnostics.Count == 0 ? "Prefab preview is up to date." : diagnostics.Count + " preview diagnostic(s).";
        }
        catch (OperationCanceledException) { }
        catch (Exception exception)
        {
            if (_disposed || revision != _revision) return;
            Diagnostics.Clear(); Diagnostics.Add(exception.Message);
            Status = "Preview could not be built.";
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _document.PropertyChanged -= OnDocumentPropertyChanged;
        _refreshToken.Cancel(); _refreshToken.Dispose();
        _modelCache.Clear();
    }

    private async Task BuildNodeAsync(Object3DNodeDefinition node, Model3DGroup visual, Model3DGroup colliders, List<string> diagnostics, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var model = node.Components.FirstOrDefault(component => component.Kind == Object3DComponentDefinition.ModelKind)?.Model;
        var nodeTransform = node.Transform;
        var transformedVisual = new Model3DGroup { Transform = NodeTransform(nodeTransform) };
        var transformedColliders = new Model3DGroup { Transform = NodeTransform(nodeTransform) };
        if (model is not null)
        {
            var package = Path.GetDirectoryName(_document.FilePath) ?? string.Empty;
            var fullPath = Path.GetFullPath(Path.Combine(package, model.Path.Replace('/', Path.DirectorySeparatorChar)));
            var file = new FileInfo(fullPath);
            var key = new ModelCacheKey(fullPath, file.Exists ? file.LastWriteTimeUtc.Ticks : 0, file.Exists ? file.Length : 0);
            if (!_modelCache.TryGetValue(key, out var loaded))
            {
                var result = await _loader.LoadAsync(fullPath, token);
                token.ThrowIfCancellationRequested();
                if (!result.Succeeded || result.Model is null) diagnostics.Add($"Node '{node.Name}': {result.ErrorMessage ?? "model failed to load."}");
                else
                {
                    loaded = result.Model;
                    Freeze(loaded);
                    _modelCache[key] = loaded;
                }
            }
            if (loaded is not null)
            {
                var modelVisual = loaded.CloneCurrentValue();
                modelVisual.Transform = IntrinsicTransform(model);
                transformedVisual.Children.Add(modelVisual);
            }
        }
        var collider = node.Components.FirstOrDefault(component => component.Kind == Object3DComponentDefinition.ColliderKind)?.Collider;
        if (collider is { Kind: not Object3DColliderKind.None })
        {
            var shape = CreateColliderModel(collider, node.Name, diagnostics);
            if (shape is not null)
            {
                var corrected = new Model3DGroup();
                if (collider.SourceFrame == Object3DColliderSourceFrame.ModelLocal && model is not null) corrected.Transform = IntrinsicTransform(model);
                corrected.Children.Add(shape);
                transformedColliders.Children.Add(corrected);
            }
        }
        visual.Children.Add(transformedVisual);
        colliders.Children.Add(transformedColliders);
        foreach (var child in node.Children) await BuildNodeAsync(child, transformedVisual, transformedColliders, diagnostics, token);
    }

    private static GeometryModel3D? CreateColliderModel(Object3DColliderDefinition collider, string nodeName, List<string> diagnostics)
    {
        System.Windows.Media.Media3D.MeshGeometry3D mesh;
        try
        {
            var center = new Point3D(collider.Center?.ElementAtOrDefault(0) ?? 0, collider.Center?.ElementAtOrDefault(1) ?? 0, collider.Center?.ElementAtOrDefault(2) ?? 0);
            switch (collider.Kind)
            {
                case Object3DColliderKind.Sphere: mesh = CreateSphere(center, collider.Radius ?? .5); break;
                case Object3DColliderKind.Box:
                    var size = collider.Size ?? [1, 1, 1];
                    mesh = CreateBox(center, size.ElementAtOrDefault(0), size.ElementAtOrDefault(1), size.ElementAtOrDefault(2));
                    break;
                case Object3DColliderKind.Capsule:
                    diagnostics.Add($"Node '{nodeName}': capsule collider preview is not available yet; no approximate shape is shown.");
                    return null;
                case Object3DColliderKind.Mesh:
                    diagnostics.Add($"Node '{nodeName}': mesh collider shape preview is unavailable; model is shown in the viewport.");
                    return null;
                default: return null;
            }
        }
        catch (Exception exception)
        {
            diagnostics.Add($"Node '{nodeName}': collider preview failed: {exception.Message}");
            return null;
        }
        var themeBrush = Application.Current?.TryFindResource(collider.IsTrigger ? "BorderStrongBrush" : "SelectionBrush") as SolidColorBrush;
        var brush = themeBrush is null ? Brushes.Transparent : new SolidColorBrush(Color.FromArgb(105, themeBrush.Color.R, themeBrush.Color.G, themeBrush.Color.B));
        if (brush.CanFreeze) brush.Freeze();
        var material = new DiffuseMaterial(brush);
        if (material.CanFreeze) material.Freeze();
        var geometry = new GeometryModel3D(mesh, material) { BackMaterial = material };
        if (geometry.CanFreeze) geometry.Freeze();
        return geometry;
    }

    private static Transform3D NodeTransform(MachineObjectTransform transform)
    {
        var group = new Transform3DGroup();
        group.Children.Add(new ScaleTransform3D(transform.Scale.X, transform.Scale.Y, transform.Scale.Z));
        group.Children.Add(new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(0, 0, 1), transform.Rotation.Z)));
        group.Children.Add(new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(1, 0, 0), transform.Rotation.X)));
        group.Children.Add(new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(0, 1, 0), transform.Rotation.Y)));
        group.Children.Add(new TranslateTransform3D(transform.Position.X, transform.Position.Y, transform.Position.Z));
        return group;
    }
    private static System.Windows.Media.Media3D.MeshGeometry3D CreateSphere(Point3D center, double radius)
    {
        const int latitudeSteps = 12;
        const int longitudeSteps = 20;
        var mesh = new System.Windows.Media.Media3D.MeshGeometry3D();
        for (var latitude = 0; latitude <= latitudeSteps; latitude++)
        {
            var phi = Math.PI * latitude / latitudeSteps;
            var y = Math.Cos(phi);
            var ringRadius = Math.Sin(phi);
            for (var longitude = 0; longitude <= longitudeSteps; longitude++)
            {
                var theta = 2 * Math.PI * longitude / longitudeSteps;
                mesh.Positions.Add(new Point3D(center.X + radius * ringRadius * Math.Cos(theta), center.Y + radius * y, center.Z + radius * ringRadius * Math.Sin(theta)));
            }
        }
        for (var latitude = 0; latitude < latitudeSteps; latitude++)
            for (var longitude = 0; longitude < longitudeSteps; longitude++)
            {
                var a = latitude * (longitudeSteps + 1) + longitude;
                var b = a + longitudeSteps + 1;
                mesh.TriangleIndices.Add(a); mesh.TriangleIndices.Add(b); mesh.TriangleIndices.Add(a + 1);
                mesh.TriangleIndices.Add(a + 1); mesh.TriangleIndices.Add(b); mesh.TriangleIndices.Add(b + 1);
            }
        return mesh;
    }
    private static System.Windows.Media.Media3D.MeshGeometry3D CreateBox(Point3D center, double sizeX, double sizeY, double sizeZ)
    {
        var mesh = new System.Windows.Media.Media3D.MeshGeometry3D();
        var x = sizeX / 2; var y = sizeY / 2; var z = sizeZ / 2;
        mesh.Positions.Add(new(center.X - x, center.Y - y, center.Z - z)); mesh.Positions.Add(new(center.X + x, center.Y - y, center.Z - z));
        mesh.Positions.Add(new(center.X + x, center.Y + y, center.Z - z)); mesh.Positions.Add(new(center.X - x, center.Y + y, center.Z - z));
        mesh.Positions.Add(new(center.X - x, center.Y - y, center.Z + z)); mesh.Positions.Add(new(center.X + x, center.Y - y, center.Z + z));
        mesh.Positions.Add(new(center.X + x, center.Y + y, center.Z + z)); mesh.Positions.Add(new(center.X - x, center.Y + y, center.Z + z));
        int[] triangles = [0,2,1,0,3,2,4,5,6,4,6,7,0,1,5,0,5,4,3,7,6,3,6,2,0,4,7,0,7,3,1,2,6,1,6,5];
        foreach (var index in triangles) mesh.TriangleIndices.Add(index);
        return mesh;
    }
    private static Transform3D IntrinsicTransform(Object3DModelDefinition model)
    {
        var group = new Transform3DGroup();
        group.Children.Add(new ScaleTransform3D(model.Scale, model.Scale, model.Scale));
        if (model.UpAxis == "Z") group.Children.Add(new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(1, 0, 0), -90)));
        else if (model.UpAxis == "X") group.Children.Add(new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(0, 0, 1), 90)));
        return group;
    }
    private void OnDocumentPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(DocumentTabViewModel.Object3DDisplayName) or nameof(DocumentTabViewModel.Object3DModelPath)) QueueRefresh();
    }
    private void QueueRefresh()
    {
        if (_disposed) return;
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null) { _refreshTask = RefreshAsync(); return; }
        if (_refreshQueued) return;
        _refreshQueued = true;
        _refreshTask = dispatcher.InvokeAsync(async () =>
        {
            _refreshQueued = false;
            await RefreshAsync();
        }, DispatcherPriority.Background).Task.Unwrap();
    }
    private static void Freeze(Freezable value) { if (value.CanFreeze) value.Freeze(); }
    private void Notify([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    private readonly record struct ModelCacheKey(string Path, long ModifiedTicks, long Length);
}
