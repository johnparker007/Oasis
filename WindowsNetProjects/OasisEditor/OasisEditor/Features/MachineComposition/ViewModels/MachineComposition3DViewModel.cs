using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using OasisEditor.Features.CabinetEditor.Models;
using OasisEditor.Features.CabinetEditor.Services;

namespace OasisEditor.Features.MachineComposition.ViewModels;

/// <summary>A transient, derived projection of Machine -> Cabinet -> Object3D instances.</summary>
public sealed class MachineComposition3DViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly DocumentTabViewModel _document;
    private readonly ICabinetModelLoader _loader;
    private readonly AssetReferenceResolver _resolver = new();
    private readonly Dictionary<AssetReference, LoadedObjectDefinition> _definitionCache = [];
    private readonly Dictionary<AssetReference, string> _definitionDiagnostics = [];
    private readonly Dictionary<string, string> _instanceDiagnostics = new(StringComparer.Ordinal);
    private CancellationTokenSource _refreshCancellation = new();
    private Task _refreshTask = Task.CompletedTask;
    private string? _cabinetDiagnostic;
    private int _refreshVersion;
    private bool _disposed;
    private Model3DGroup? _cabinetVisual;
    private Model3DGroup? _cabinetColliders;
    private Model3DGroup? _cabinetTriggers;
    private Model3DGroup _objects = CreateFrozenGroup();
    private Model3DGroup _anchors = CreateFrozenGroup();
    private MachineObjectInstanceRow? _selectedObject;
    private bool _showCabinetVisual = true;
    private bool _showObjects = true;
    private bool _showAnchors = true;
    private MachineAnchorRow? _selectedAnchor;
    private bool _showCabinetCollision;
    private bool _showColliders = true;
    private bool _showTriggers = true;
    private Rect3D _bounds = Rect3D.Empty;
    private Point3D _cameraPosition;
    private Vector3D _cameraLookDirection;

    public MachineComposition3DViewModel(DocumentTabViewModel document, ICabinetModelLoader loader)
    {
        _document = document;
        _loader = loader;
        ResetCameraCommand = new RelayCommand(ResetCamera);
        RemoveSelectedCommand = new RelayCommand(() => { if (SelectedObject is not null) _document.RemoveMachineObjectInstance(SelectedObject.Id); else if (SelectedAnchor is not null) _document.RemoveMachineAnchor(SelectedAnchor.Id); });
        ResetCamera();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public ObservableCollection<string> Diagnostics { get; } = [];
    public ObservableCollection<MachineObjectInstanceRow> Objects { get; } = [];
    public ObservableCollection<MachineAnchorRow> Anchors { get; } = [];
    public ICommand ResetCameraCommand { get; }
    public ICommand RemoveSelectedCommand { get; }
    public ICommand AddCommand => _document.AddMachineObjectInstanceCommand;
    public string CabinetLabel => _document.GetMachineDocument().CabinetAsset is null ? "Cabinet (none assigned)" : "Cabinet";
    public bool HasDiagnostics => Diagnostics.Count > 0;
    public Model3DGroup? CabinetVisual => ShowCabinetVisual ? _cabinetVisual : null;
    public Model3DGroup? CabinetColliderPreview => ShowCabinetCollision && ShowColliders ? _cabinetColliders : null;
    public Model3DGroup? CabinetTriggerPreview => ShowCabinetCollision && ShowTriggers ? _cabinetTriggers : null;
    public Model3DGroup? ObjectVisuals => ShowObjects ? _objects : null;
    public Model3DGroup? AnchorVisuals => ShowAnchors ? _anchors : null;
    public bool ShowCabinetVisual { get => _showCabinetVisual; set => SetFilter(ref _showCabinetVisual, value, nameof(CabinetVisual)); }
    public bool ShowObjects { get => _showObjects; set => SetFilter(ref _showObjects, value, nameof(ObjectVisuals)); }
    public bool ShowAnchors { get => _showAnchors; set => SetFilter(ref _showAnchors, value, nameof(AnchorVisuals)); }
    public bool ShowCabinetCollision { get => _showCabinetCollision; set { if (_showCabinetCollision == value) return; _showCabinetCollision = value; Notify(); Notify(nameof(CabinetColliderPreview)); Notify(nameof(CabinetTriggerPreview)); RecalculateBounds(); } }
    public bool ShowColliders { get => _showColliders; set => SetFilter(ref _showColliders, value, nameof(CabinetColliderPreview)); }
    public bool ShowTriggers { get => _showTriggers; set => SetFilter(ref _showTriggers, value, nameof(CabinetTriggerPreview)); }
    public MachineObjectInstanceRow? SelectedObject { get => _selectedObject; set { if (ReferenceEquals(_selectedObject, value)) return; _selectedObject = value; if (value is not null) { _selectedAnchor = null; Notify(nameof(SelectedAnchor)); } Notify(); Notify(nameof(HasSelection)); Notify(nameof(SelectedBounds)); } }
    public MachineAnchorRow? SelectedAnchor { get => _selectedAnchor; set { if (ReferenceEquals(_selectedAnchor, value)) return; _selectedAnchor = value; if (value is not null) { _selectedObject = null; Notify(nameof(SelectedObject)); } Notify(); Notify(nameof(HasSelection)); Notify(nameof(SelectedBounds)); RebuildAnchors(); } }
    public bool HasSelection => SelectedObject is not null || SelectedAnchor is not null;
    public Rect3D SelectedBounds
    {
        get
        {
            if (SelectedAnchor is not null)
            {
                var anchor = _document.GetMachineDocument().Anchors?.FirstOrDefault(item => item.Id == SelectedAnchor.Id);
                return anchor is null ? Rect3D.Empty : new Rect3D(anchor.Position.X - .08, anchor.Position.Y - .08, anchor.Position.Z - .08, .16, .16, .16);
            }
            if (SelectedObject is null) return Rect3D.Empty;
            var instance = _document.GetMachineDocument().ObjectInstances.FirstOrDefault(item => item.Id == SelectedObject.Id);
            if (instance?.ObjectAsset is null || !_definitionCache.TryGetValue(instance.ObjectAsset, out var loaded)) return Rect3D.Empty;
            return MachineCompositionTransform.Create(instance.Transform, loaded.Definition.Model).TransformBounds(loaded.Model.Bounds);
        }
    }
    public double GridSize => Bounds.IsEmpty ? 10 : Math.Max(1, Math.Ceiling(Math.Max(Bounds.SizeX, Math.Max(Bounds.SizeY, Bounds.SizeZ)) * 2));
    public double GridSpacing => Math.Max(.1, GridSize / 20);
    public double AxisLength => Math.Max(1, GridSize / 4);
    public Point3D XAxisEnd => new(AxisLength, 0, 0);
    public Point3D YAxisEnd => new(0, AxisLength, 0);
    public Point3D ZAxisEnd => new(0, 0, AxisLength);
    public Rect3D Bounds { get => _bounds; private set { _bounds = value; Notify(); Notify(nameof(GridSize)); Notify(nameof(GridSpacing)); Notify(nameof(AxisLength)); Notify(nameof(XAxisEnd)); Notify(nameof(YAxisEnd)); Notify(nameof(ZAxisEnd)); } }
    public Point3D CameraPosition { get => _cameraPosition; set { _cameraPosition = value; Notify(); } }
    public Vector3D CameraLookDirection { get => _cameraLookDirection; set { _cameraLookDirection = value; Notify(); } }
    public Vector3D CameraUpDirection { get; set; } = new(0, 1, 0);
    public double CameraFieldOfView { get; set; } = 45;

    public void RefreshDefinitions() => _refreshTask = RebuildAsync(clearCache: true);
    internal Task RefreshDefinitionsAsync() { RefreshDefinitions(); return _refreshTask; }
    internal Task WaitForRefreshAsync() => _refreshTask;

    internal void Refresh(MachineDocument machine, bool cabinetChanged, bool objectInstancesChanged)
    {
        var selectedId = SelectedObject?.Id;
        var selectedAnchorId = SelectedAnchor?.Id;
        Objects.Clear();
        foreach (var row in _document.MachineObjectInstanceRows) Objects.Add(row);
        SelectedObject = Objects.FirstOrDefault(row => row.Id == selectedId);
        Anchors.Clear();
        foreach (var row in _document.MachineAnchorRows) Anchors.Add(row);
        SelectedAnchor = Anchors.FirstOrDefault(row => row.Id == selectedAnchorId);
        Notify(nameof(CabinetLabel));
        if (cabinetChanged || DefinitionsChanged(machine)) _refreshTask = RebuildAsync(clearCache: false);
        else if (objectInstancesChanged) { RebuildObjectInstances(); RebuildAnchors(); }
    }

    private bool DefinitionsChanged(MachineDocument machine) => machine.ObjectInstances.Any(instance => instance.ObjectAsset is not null && !_definitionCache.ContainsKey(instance.ObjectAsset));

    private async Task RebuildAsync(bool clearCache)
    {
        if (_disposed) return;
        _refreshCancellation.Cancel();
        _refreshCancellation.Dispose();
        _refreshCancellation = new CancellationTokenSource();
        var token = _refreshCancellation.Token;
        var version = ++_refreshVersion;
        if (clearCache) _definitionCache.Clear();
        _definitionDiagnostics.Clear();
        _instanceDiagnostics.Clear();
        _cabinetDiagnostic = null;
        RefreshDiagnostics();
        var project = _document.CurrentProject;
        if (project is null) { _cabinetDiagnostic = "Open this Machine in a project to resolve composition assets."; RefreshDiagnostics(); return; }
        var machine = _document.GetMachineDocument();
        await LoadCabinetAsync(project, machine.CabinetAsset, version, token);
        foreach (var reference in machine.ObjectInstances.Select(i => i.ObjectAsset).OfType<AssetReference>().Distinct())
            if (!_definitionCache.ContainsKey(reference)) await LoadObjectAsync(project, reference, version, token);
        if (!IsCurrent(version, token)) return;
        Refresh(machine, false, false);
        RebuildObjectInstances();
        RebuildAnchors();
        ResetCamera();
    }

    private async Task LoadCabinetAsync(EditorProject project, AssetReference? reference, int version, CancellationToken token)
    {
        if (!IsCurrent(version, token)) return;
        _cabinetVisual = _cabinetColliders = _cabinetTriggers = null;
        if (reference is null) { _cabinetDiagnostic = "No Cabinet is assigned; Object3D instances remain available."; RefreshDiagnostics(); NotifyCabinetModels(); return; }
        try
        {
            var manifest = _resolver.Resolve(project, _document.CurrentLibraryRoot, reference);
            if (!File.Exists(manifest) || !CabinetDocumentStorage.TryRead(await File.ReadAllTextAsync(manifest, token), out var cabinet)) throw new InvalidOperationException($"Cabinet manifest could not be read: {reference.Path}");
            var modelPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(manifest)!, cabinet.Model.Path));
            var result = await _loader.LoadAsync(modelPath, token);
            if (!result.Succeeded) throw new InvalidOperationException(result.ErrorMessage);
            if (!IsCurrent(version, token)) return;
            _cabinetVisual = result.Model; _cabinetColliders = result.ColliderModel; _cabinetTriggers = result.TriggerModel;
            _cabinetDiagnostic = null;
        }
        catch (OperationCanceledException) { return; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
        { if (IsCurrent(version, token)) _cabinetDiagnostic = $"Cabinet cannot render: {ex.Message}"; }
        if (!IsCurrent(version, token)) return;
        RefreshDiagnostics();
        NotifyCabinetModels();
    }

    private async Task LoadObjectAsync(EditorProject project, AssetReference reference, int version, CancellationToken token)
    {
        try
        {
            var manifest = _resolver.Resolve(project, _document.CurrentLibraryRoot, reference);
            if (!File.Exists(manifest)) throw new InvalidOperationException($"manifest not found: {reference.Path}");
            if (!Object3DDocumentStorage.TryRead(await File.ReadAllTextAsync(manifest, token), out var definition, out var error)) throw new InvalidOperationException(error);
            var modelPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(manifest)!, definition.Model.Path));
            var result = await _loader.LoadAsync(modelPath, token);
            if (!result.Succeeded || result.Model is null) throw new InvalidOperationException(result.ErrorMessage);
            if (!IsCurrent(version, token)) return;
            _definitionCache[reference] = new(definition, result.Model);
            _definitionDiagnostics.Remove(reference);
        }
        catch (OperationCanceledException) { return; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
        { if (IsCurrent(version, token)) _definitionDiagnostics[reference] = $"Object3D '{reference.Path}' cannot render: {ex.Message}"; }
        if (IsCurrent(version, token)) RefreshDiagnostics();
    }

    private void RebuildObjectInstances()
    {
        var group = new Model3DGroup();
        _instanceDiagnostics.Clear();
        var instances = _document.GetMachineDocument().ObjectInstances;
        var referencedAssets = instances.Select(instance => instance.ObjectAsset).OfType<AssetReference>().ToHashSet();
        foreach (var staleReference in _definitionDiagnostics.Keys.Where(reference => !referencedAssets.Contains(reference)).ToArray())
            _definitionDiagnostics.Remove(staleReference);
        foreach (var instance in instances)
        {
            if (instance.ObjectAsset is null) { _instanceDiagnostics[instance.Id] = $"{instance.Id}: no Object3D asset assigned."; continue; }
            if (!_definitionCache.TryGetValue(instance.ObjectAsset, out var loaded)) { _instanceDiagnostics[instance.Id] = $"{instance.Id}: Object3D reference is unresolved ({instance.ObjectAsset.Path})."; continue; }
            group.Children.Add(new Model3DGroup { Children = { loaded.Model }, Transform = MachineCompositionTransform.Create(instance.Transform, loaded.Definition.Model) });
        }
        // Composition refresh can complete on a worker thread in headless tests, and the
        // viewport may subsequently read this WPF Freezable on the UI thread. Production
        // source models are frozen by the loader; freeze the derived group for the same
        // cross-thread contract.
        group.Freeze();
        _objects = group;
        Notify(nameof(ObjectVisuals));
        Notify(nameof(SelectedBounds));
        RecalculateBounds();
        RefreshDiagnostics();
    }

    private void RecalculateBounds()
    {
        var bounds = Rect3D.Empty;
        if (ShowCabinetVisual && _cabinetVisual is not null) bounds.Union(_cabinetVisual.Bounds);
        if (ShowObjects) bounds.Union(_objects.Bounds);
        if (ShowAnchors) bounds.Union(_anchors.Bounds);
        if (ShowCabinetCollision && ShowColliders && _cabinetColliders is not null) bounds.Union(_cabinetColliders.Bounds);
        if (ShowCabinetCollision && ShowTriggers && _cabinetTriggers is not null) bounds.Union(_cabinetTriggers.Bounds);
        Bounds = bounds;
    }

    private void RebuildAnchors()
    {
        var group = new Model3DGroup();
        foreach (var anchor in _document.GetMachineDocument().Anchors ?? [])
        {
            var selected = SelectedAnchor?.Id == anchor.Id;
            var size = selected ? .08 : .05;
            var mesh = new MeshGeometry3D
            {
                Positions = new Point3DCollection { new(-size, 0, 0), new(size, 0, 0), new(0, -size, 0), new(0, size, 0), new(0, 0, 0), new(0, 0, size * 2) },
                TriangleIndices = new Int32Collection { 0, 2, 4, 1, 3, 4, 0, 3, 5, 1, 2, 5 }
            };
            var material = new DiffuseMaterial(new SolidColorBrush(selected ? Colors.Yellow : Colors.Orange));
            var transform = new Transform3DGroup();
            transform.Children.Add(new RotateTransform3D(new AxisAngleRotation3D(new(0, 0, 1), anchor.Rotation.Z)));
            transform.Children.Add(new RotateTransform3D(new AxisAngleRotation3D(new(1, 0, 0), anchor.Rotation.X)));
            transform.Children.Add(new RotateTransform3D(new AxisAngleRotation3D(new(0, 1, 0), anchor.Rotation.Y)));
            transform.Children.Add(new TranslateTransform3D(anchor.Position.X, anchor.Position.Y, anchor.Position.Z));
            group.Children.Add(new GeometryModel3D(mesh, material) { BackMaterial = material, Transform = transform });
        }
        group.Freeze(); _anchors = group; Notify(nameof(AnchorVisuals)); Notify(nameof(SelectedBounds)); RecalculateBounds();
    }

    private void ResetCamera()
    {
        RecalculateBounds();
        var center = Bounds.IsEmpty ? new Point3D() : new Point3D(Bounds.X + Bounds.SizeX / 2, Bounds.Y + Bounds.SizeY / 2, Bounds.Z + Bounds.SizeZ / 2);
        var radius = Bounds.IsEmpty ? 5 : Math.Max(Bounds.SizeX, Math.Max(Bounds.SizeY, Bounds.SizeZ));
        if (radius <= 0) radius = 5;
        var distance = radius * 2.5;
        CameraPosition = new(center.X + distance, center.Y + distance * .65, center.Z + distance);
        CameraLookDirection = center - CameraPosition;
        Notify(nameof(CameraUpDirection)); Notify(nameof(CameraFieldOfView));
    }

    private void SetFilter(ref bool field, bool value, string modelProperty) { if (field == value) return; field = value; Notify(); Notify(modelProperty); RecalculateBounds(); }
    private void NotifyCabinetModels() { Notify(nameof(CabinetVisual)); Notify(nameof(CabinetColliderPreview)); Notify(nameof(CabinetTriggerPreview)); RecalculateBounds(); }
    private bool IsCurrent(int version, CancellationToken token) => !_disposed && !token.IsCancellationRequested && version == _refreshVersion;
    private void RefreshDiagnostics()
    {
        var current = new List<string>();
        if (_cabinetDiagnostic is not null) current.Add(_cabinetDiagnostic);
        current.AddRange(_definitionDiagnostics.OrderBy(item => item.Key.Scope).ThenBy(item => item.Key.Path, StringComparer.Ordinal).Select(item => item.Value));
        current.AddRange(_instanceDiagnostics.OrderBy(item => item.Key, StringComparer.Ordinal).Select(item => item.Value));
        Diagnostics.Clear();
        foreach (var message in current.Distinct(StringComparer.Ordinal)) Diagnostics.Add(message);
        Notify(nameof(HasDiagnostics));
    }
    private void Notify([CallerMemberName] string? property = null) => PropertyChanged?.Invoke(this, new(property));
    public void Dispose() { if (_disposed) return; _disposed = true; _refreshVersion++; _refreshCancellation.Cancel(); _refreshCancellation.Dispose(); }

    private static Model3DGroup CreateFrozenGroup()
    {
        var group = new Model3DGroup();
        group.Freeze();
        return group;
    }

    private sealed record LoadedObjectDefinition(Object3DDocument Definition, Model3DGroup Model);
}

public static class MachineCompositionTransform
{
    /// <summary>Matches Player: placement uses Unity Z-X-Y Euler order; intrinsic correction is below placement.</summary>
    public static Transform3D Create(MachineObjectTransform placement, Object3DModelDefinition model)
    {
        var transforms = new Transform3DGroup();
        transforms.Children.Add(new ScaleTransform3D(model.Scale, model.Scale, model.Scale));
        if (model.UpAxis == "Z") transforms.Children.Add(new RotateTransform3D(new AxisAngleRotation3D(new(1, 0, 0), -90)));
        else if (model.UpAxis == "X") transforms.Children.Add(new RotateTransform3D(new AxisAngleRotation3D(new(0, 0, 1), 90)));
        transforms.Children.Add(new ScaleTransform3D(placement.Scale.X, placement.Scale.Y, placement.Scale.Z));
        transforms.Children.Add(new RotateTransform3D(new AxisAngleRotation3D(new(0, 0, 1), placement.Rotation.Z)));
        transforms.Children.Add(new RotateTransform3D(new AxisAngleRotation3D(new(1, 0, 0), placement.Rotation.X)));
        transforms.Children.Add(new RotateTransform3D(new AxisAngleRotation3D(new(0, 1, 0), placement.Rotation.Y)));
        transforms.Children.Add(new TranslateTransform3D(placement.Position.X, placement.Position.Y, placement.Position.Z));
        return transforms;
    }
}
