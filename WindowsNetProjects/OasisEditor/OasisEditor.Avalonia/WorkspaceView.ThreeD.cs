using System.Collections.ObjectModel;
using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Oasis.Rendering3D;
using OasisEditor.Avalonia.Rendering3D;
using OasisEditor.Features.CabinetEditor.Models;

namespace OasisEditor.Avalonia;

public sealed record SceneHierarchyRow(string Id, string Name, string Kind, int Depth);

public sealed partial class WorkspaceView
{
    public CabinetAuthoringSession Cabinets { get; private set; } = null!;
    public PrefabAuthoringSession Prefabs { get; private set; } = null!;
    public SceneViewport SceneView { get; } = new();
    public string? SceneKind => _sceneKind;
    public string SceneLibraryRoot { get; private set; } = string.Empty;
    private string? _sceneKind, _selectedSceneNodeId, _sceneIdentity;
    private readonly ListBox _sceneHierarchy = new();
    private readonly ObservableCollection<SceneHierarchyRow> _sceneRows = [];
    private readonly Dictionary<string, string> _meshNodeIds = [];
    private readonly StackPanel _sceneTools = new() { Orientation = Orientation.Horizontal, Spacing = 5, Margin = new Thickness(7, 5), IsVisible = false };
    private readonly Portable3DCompositionService _sceneComposer = new();
    private Portable3DCompositionResult? _composition;
    private SceneRenderData _baseScene = SceneRenderData.Empty;
    private CancellationTokenSource _sceneCancellation = new();
    private Task _sceneTask = Task.CompletedTask;
    private bool _sceneHierarchyUpdating, _sceneDisposed;
    private Control? _twoDToolbar, _sourceRegionHeading;
    private Border? _sceneToolbarBorder;
    private bool Is3DActive => _sceneKind is not null;

    private void Initialize3DAuthoring(Grid hierarchy, Grid scene, StackPanel twoDTools, MenuItem file)
    {
        Cabinets = new(_session); Prefabs = new(_session);
        _twoDToolbar=twoDTools.Parent as Control;
        _sourceRegionHeading=hierarchy.Children.OfType<TextBlock>().FirstOrDefault();
        if (!Program.IsHeadless) SceneLibraryRoot = GetLibraryRoot();
        _sceneHierarchy.ItemsSource = _sceneRows; _sceneHierarchy.IsVisible = false;
        _sceneHierarchy.ItemTemplate = new FuncDataTemplate<SceneHierarchyRow>((row, _) => new TextBlock
        { Text = row is null ? "" : new string(' ', row.Depth * 2) + "◇  " + row.Name, TextTrimming = TextTrimming.CharacterEllipsis });
        _sceneHierarchy.SelectionChanged += (_, _) =>
        { if (!_sceneHierarchyUpdating) SelectSceneNode((_sceneHierarchy.SelectedItem as SceneHierarchyRow)?.Id); };
        hierarchy.Children.Add(_sceneHierarchy);
        Grid.SetRow(SceneView, 1); SceneView.IsVisible = false; scene.Children.Add(SceneView);
        SceneView.Failed += SetStatus; SceneView.Picked += hit => SelectSceneNode(hit is null ? null : _meshNodeIds.GetValueOrDefault(hit.Id));
        _sceneTools.Children.Add(ActionButton("Frame all", SceneView.FrameAll));
        _sceneTools.Children.Add(ActionButton("Frame selection", FrameSceneSelection));
        _sceneTools.Children.Add(ActionButton("Reload", async () => await SceneAction(() => ReloadSceneAsync())));
        foreach (var label in new[] { "Visual meshes", "Colliders", "Triggers" })
        {
            var check = new CheckBox { Content = label, IsChecked = label == "Visual meshes", VerticalAlignment = VerticalAlignment.Center };
            check.IsCheckedChanged += (_, _) => { if (label == "Visual meshes") SceneView.ShowVisuals = check.IsChecked == true;
                else if (label == "Colliders") SceneView.ShowColliders = check.IsChecked == true; else SceneView.ShowTriggers = check.IsChecked == true; };
            _sceneTools.Children.Add(check);
            SceneView.VisibilityChanged += () => check.IsChecked = label == "Visual meshes" ? SceneView.ShowVisuals : label == "Colliders" ? SceneView.ShowColliders : SceneView.ShowTriggers;
        }
        _sceneToolbarBorder=Toolbar(_sceneTools); scene.Children.Add(_sceneToolbarBorder);
        MenuAction(file, "Import Cabinet GLB…", async () => await Create3DAssetDialog(false));
        MenuAction(file, "Create Prefab…", async () => await Create3DAssetDialog(true));
        Cabinets.Changed += On3DAssetChanged; Prefabs.Changed += On3DAssetChanged;
        Machines.Changed += OnMachineSceneChanged;
        Faces.Changed += OnMachineSceneChanged;
    }
    public async Task OpenCabinet3DAsync(string path)
    {
        if (Cabinets.FilePath is null || !SameScenePath(Cabinets.FilePath, Path.GetFullPath(path, _session.RequireProjectDirectory())) || !Cabinets.IsDirty) Cabinets.Open(path);
        _faceActive = _machineActive = false; _sceneKind = "Cabinet3D"; _selectedSceneNodeId = "cabinet";
        await ReloadSceneAsync(); Refresh();
    }
    public async Task OpenPrefab3DAsync(string path)
    {
        if (Prefabs.FilePath is null || !SameScenePath(Prefabs.FilePath, Path.GetFullPath(path, _session.RequireProjectDirectory())) || !Prefabs.IsDirty) Prefabs.Open(path);
        _faceActive = _machineActive = false; _sceneKind = "Prefab"; _selectedSceneNodeId = Prefabs.Document.Root.Id;
        await ReloadSceneAsync(); Refresh();
    }
    public Task WaitForSceneAsync() => _sceneTask;
    public void SelectScene3DNode(string? id)
    {
        if (id is not null && !_sceneRows.Any(row => row.Id == id)) throw new ArgumentException("Scene node does not exist: " + id, nameof(id));
        SelectSceneNode(id);
    }
    public object? PickScene3D(float normalizedX, float normalizedY)
    {
        var hit = SceneView.Pick(normalizedX, normalizedY);
        SelectSceneNode(hit is null ? null : _meshNodeIds.GetValueOrDefault(hit.Id));
        return hit is null ? null : new { hit.Id, hit.Name, position = new[] { hit.Position.X, hit.Position.Y, hit.Position.Z },
            uv = new[] { hit.TextureCoordinate.X, hit.TextureCoordinate.Y }, hit.Distance, nodeId = _selectedSceneNodeId };
    }
    public object InspectScene3D()
    {
        var camera = SceneView.Camera; var frame = SceneView.LastFrame;
        return new { kind = _sceneKind, selectedNodeId = _selectedSceneNodeId, hierarchy = _sceneRows.ToArray(),
            camera = new { target = new[] { camera.Target.X, camera.Target.Y, camera.Target.Z }, camera.Distance, camera.Yaw, camera.Pitch, camera.FieldOfView },
            visibility = new { SceneView.ShowVisuals, SceneView.ShowColliders, SceneView.ShowTriggers },
            bounds = new { minimum = new[] { _baseScene.Minimum.X, _baseScene.Minimum.Y, _baseScene.Minimum.Z }, maximum = new[] { _baseScene.Maximum.X, _baseScene.Maximum.Y, _baseScene.Maximum.Z } },
            meshes = _baseScene.Meshes.Count, diagnostics = _baseScene.Diagnostics, SceneView.RenderingError,
            mountedFaces = _faceOutputKeys.Select(pair => new { id = pair.Key, outputFingerprint = pair.Value }).ToArray(),
            faceTargets = _composition?.CabinetFaceTargets.Select(target => new { target.Id, target.DisplayName, target.IsValid, target.ErrorMessage,
                corners = target.Corners.Select(p => new[] {p.X,p.Y,p.Z}).ToArray(), normal = new[] {target.Normal.X,target.Normal.Y,target.Normal.Z} }).ToArray(),
            reflectionReceivers = _composition?.ReflectionTargets,
            reflectionBindings = _baseScene.Meshes.Count(mesh => mesh.Reflection is not null),
            gpu = frame is null ? null : new { frame.Width, frame.Height, frame.Device, frame.DrawCalls, frame.Triangles } };
    }
    public void SetSceneLibraryRoot(string path)
    {
        var full = Path.GetFullPath(path);
        WorkspacePathPolicy.RejectRootReparsePoints(full, requireExists: true);
        SceneLibraryRoot = full;
        if (Is3DActive) QueueSceneReload();
    }
    private void QueueSceneReload() => _sceneTask = SceneAction(() => ReloadSceneAsync());
    private async Task SceneAction(Func<Task> action)
    { try { await action(); } catch (OperationCanceledException) { } catch (Exception e) when (e is not OutOfMemoryException) { SetStatus(e.Message); } }
    private void On3DAssetChanged() { CheckPreviewSource(); if (Is3DActive) { Refresh(); QueueSceneReload(); } }
    private void OnMachineSceneChanged() { if (_sceneKind is "Machine" or "Cabinet3D") QueueSceneReload(); }
    public async Task ReloadSceneAsync()
    {
        if (!Is3DActive || _sceneDisposed) return;
        if (_sceneKind == "Reel") { _sceneCancellation.Cancel(); ReloadReelScene(); return; }
        var old = _sceneCancellation; _sceneCancellation = new(); old.Cancel(); old.Dispose();
        var token = _sceneCancellation.Token;
        var project = _session.RequireEditorProject(); var kind = _sceneKind;
        var identity = kind + ":" + (kind == "Cabinet3D" ? Cabinets.FilePath : kind == "Prefab" ? Prefabs.FilePath : Machines.MachinePath);
        var result = kind switch
        {
            "Cabinet3D" => await _sceneComposer.ComposeCabinetAsync(project, Cabinets.Document, Cabinets.FilePath!, MachineForCabinet(project), token),
            "Prefab" => await _sceneComposer.ComposePrefabAsync(project, Prefabs.Document, Prefabs.FilePath!, token),
            "Machine" when Machines.Document is { } machine => await _sceneComposer.ComposeMachineAsync(project, machine, SceneLibraryRoot, token),
            _ => new Portable3DCompositionResult([], [], [], [], BoundingBox3.Empty, ["Open a Machine before viewing its composition."])
        };
        token.ThrowIfCancellationRequested();
        if (_sceneDisposed || _sceneKind != kind || !SameScenePath(project.ProjectFilePath, _session.ProjectFilePath)) return;
        _composition = result;
        _meshNodeIds.Clear();
        var meshes = new List<SceneRenderMesh>();
        foreach (var part in result.Meshes)
        {
            var instance = new GlbMeshInstance(0, part.NodePath.Split('/').Last(), part.NodePath, part.WorldTransform, part.SemanticKind, part.SemanticId, part.Primitives);
            var owner = part.OwnerId + "/" + part.NodeId;
            var converted = SceneRenderAdapter.ConvertMeshes([instance], result.Materials, Matrix4x4.Identity, CurrentScenePalette(), owner);
            var nodePath = part.NodeId.Contains('/') ? part.NodeId[..part.NodeId.LastIndexOf('/')] : part.NodeId;
            foreach (var mesh in converted) _meshNodeIds[mesh.Id] = kind == "Prefab" ? nodePath.Split('/').Last() : kind == "Cabinet3D" ? part.OwnerId + ":" + part.NodeId : part.OwnerId;
            meshes.AddRange(converted);
        }
        var faceDiagnostics = AddFacePlaneMeshes(result, meshes);
        var reflectionDiagnostics = AttachSceneReflections(result, meshes);
        AddAnchorMeshes(result, meshes);
        var bounds = result.Bounds;
        var diagnostics = result.Diagnostics.Concat(faceDiagnostics).Concat(reflectionDiagnostics).ToArray();
        _baseScene = new(meshes, bounds.IsEmpty ? Vector3.Zero : bounds.Minimum, bounds.IsEmpty ? Vector3.Zero : bounds.Maximum, diagnostics);
        RebuildSceneHierarchy(result); UpdateSceneDisplay(identity != _sceneIdentity); _sceneIdentity = identity;
        Refresh3DInspector();
        SetStatus($"{kind}: {meshes.Count} mesh primitives" + (diagnostics.Length > 0 ? " • " + string.Join(" • ", diagnostics) : ""));
    }
    private MachineDocument? MachineForCabinet(EditorProject project)
    {
        if (!Machines.IsProjectCurrent || Machines.Document is not { CabinetAsset: { } reference } machine) return null;
        try { return SameScenePath(new AssetReferenceResolver().Resolve(project, SceneLibraryRoot, reference), Cabinets.FilePath) ? machine : null; }
        catch (Exception e) when (e is IOException or ArgumentException or InvalidOperationException) { return null; }
    }
    private void RebuildSceneHierarchy(Portable3DCompositionResult result)
    {
        _sceneHierarchyUpdating = true;
        try
        {
            var rows = new List<SceneHierarchyRow>();
            if (_sceneKind == "Prefab") AddPrefab(Prefabs.Document.Root, 0);
            else
            {
                if (result.CabinetDocument is not null) rows.Add(new("cabinet", "Cabinet", "Cabinet", 0));
                foreach (var target in result.CabinetFaceTargets) rows.Add(new("target:" + target.Id, target.DisplayName, "Face target", 1));
                if (_sceneKind == "Machine" && Machines.Document is { } machine)
                {
                    var visited = new HashSet<string>();
                    foreach (var item in machine.ObjectInstances.Where(i => i.ParentId is null)) AddInstance(item, 0);
                    foreach (var item in machine.ObjectInstances.Where(i => !visited.Contains(i.Id))) AddInstance(item, 0);
                    foreach (var anchor in machine.Anchors) rows.Add(new("anchor:" + anchor.Id, anchor.DisplayName, "Anchor", 0));
                    void AddInstance(MachineObject3DInstance item, int depth)
                    { if (!visited.Add(item.Id)) return; rows.Add(new(item.Id, item.DisplayName + (item.Active ? "" : " (inactive)"), "Object", depth)); foreach (var child in machine.ObjectInstances.Where(i => i.ParentId == item.Id)) AddInstance(child, depth + 1); }
                }
                else foreach (var mesh in result.Meshes) rows.Add(new(mesh.OwnerId + ":" + mesh.NodeId, mesh.NodePath, mesh.SemanticKind.ToString(), 1));
            }
            if (!_sceneRows.SequenceEqual(rows)) { _sceneRows.Clear(); foreach (var row in rows) _sceneRows.Add(row); }
            if (!_sceneRows.Any(row => row.Id == _selectedSceneNodeId)) _selectedSceneNodeId = null;
            _sceneHierarchy.SelectedItem = _sceneRows.FirstOrDefault(r => r.Id == _selectedSceneNodeId);
            void AddPrefab(Object3DNodeDefinition node, int depth) { rows.Add(new(node.Id, node.Name, "Prefab node", depth)); foreach (var child in node.Children) AddPrefab(child, depth + 1); }
        }
        finally { _sceneHierarchyUpdating = false; }
    }
    private void SelectSceneNode(string? id)
    {
        _selectedSceneNodeId = id;
        _sceneHierarchyUpdating = true;
        try { _sceneHierarchy.SelectedItem = _sceneRows.FirstOrDefault(row => row.Id == id); }
        finally { _sceneHierarchyUpdating = false; }
        UpdateSceneDisplay(); Refresh3DInspector();
    }
    private void UpdateSceneDisplay(bool frame = false)
    {
        var palette = CurrentScenePalette(); var meshes = _baseScene.Meshes.ToList();
        meshes.Add(SceneRenderAdapter.Grid(_baseScene.Minimum, _baseScene.Maximum, palette.Grid));
        if (SelectedSceneBounds() is { } bounds) meshes.Add(SceneRenderAdapter.Bounds("selection", bounds.Min, bounds.Max, palette.Selection));
        SceneView.SetScene(_baseScene with { Meshes = meshes }, frame);
    }
    private (Vector3 Min, Vector3 Max)? SelectedSceneBounds()
    {
        var min = new Vector3(float.PositiveInfinity); var max = new Vector3(float.NegativeInfinity); var any = false;
        foreach (var mesh in _baseScene.Meshes.Where(m => _meshNodeIds.GetValueOrDefault(m.Id) == _selectedSceneNodeId || _selectedSceneNodeId == "cabinet" && _meshNodeIds.GetValueOrDefault(m.Id)?.StartsWith("cabinet:") == true))
            for (var i = 0; i < mesh.Vertices.Length; i += 8)
            { var p = Vector3.Transform(new Vector3(mesh.Vertices[i],mesh.Vertices[i+1],mesh.Vertices[i+2]),mesh.Transform); min = Vector3.Min(min,p); max=Vector3.Max(max,p); any=true; }
        return any ? (min,max) : null;
    }
    private void FrameSceneSelection()
    { if (SelectedSceneBounds() is { } bounds) SceneView.SetCamera(SceneCameraState.Frame(bounds.Min,bounds.Max)); else SceneView.FrameAll(); }
    private ScenePalette CurrentScenePalette()
    {
        Vector4 Get(string key)
        {
            if (!this.TryFindResource(key, ActualThemeVariant, out var value) || value is not Color c) throw new InvalidOperationException("Missing scene theme color: " + key);
            return new(c.R/255f,c.G/255f,c.B/255f,c.A/255f);
        }
        return new(Get("ViewportGrid"),Get("SelectionColor"),Get("SceneColliderColor"),Get("SceneTriggerColor"));
    }
    private void Refresh3DInspector()
    { if (_sceneKind == "Reel") RefreshReelInspector(); else if (_sceneKind == "Prefab") RefreshPrefab3DInspector(); else if (_sceneKind == "Cabinet3D") RefreshCabinet3DInspector(); else RefreshMachine3DInspector(); }
    private static bool SameScenePath(string? left,string? right) => left is not null && right is not null && string.Equals(Path.GetFullPath(left),Path.GetFullPath(right),OperatingSystem.IsWindows()?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal);
    private async Task Create3DAssetDialog(bool prefab)
    {
        if (TopLevel.GetTopLevel(this) is not Window owner) return;
        var name = new TextBox { PlaceholderText = "Asset name" }; var error = new TextBlock { TextWrapping=TextWrapping.Wrap };
        var content=new StackPanel { Margin=new Thickness(16),Spacing=10,Children={name,error} };
        var dialog=new Window { Title=prefab?"Create Prefab":"Import Cabinet GLB", Width=420,Height=200,Content=content };
        content.Children.Add(ActionButton(prefab?"Create Prefab":"Choose GLB…",async()=>
        {
            try
            {
                if(prefab) { var path=Prefabs.Create(name.Text??""); await OpenPrefab3DAsync(path); }
                else { var files=await owner.StorageProvider.OpenFilePickerAsync(new(){Title="Choose Cabinet GLB",AllowMultiple=false,FileTypeFilter=[new("GLB models"){Patterns=["*.glb"]}]});
                    if(files.FirstOrDefault()?.TryGetLocalPath() is not { } path)return;
                    WithSelectedModelFile(path,source=>Cabinets.Create(name.Text??"",source)); await OpenCabinet3DAsync(Cabinets.FilePath!); }
                dialog.Close();
            }
            catch(Exception e) when(e is not OutOfMemoryException) {error.Text=e.Message;}
        }));
        await dialog.ShowDialog(owner);
    }
    private void WithSelectedModelFile(string path,Action<string> import)
    {
        var root=_session.RequireProjectDirectory(); var full=Path.GetFullPath(path); var relative=Path.GetRelativePath(root,full);
        if(!Path.IsPathRooted(relative)&&relative!=".."&&!relative.StartsWith(".."+Path.DirectorySeparatorChar,StringComparison.Ordinal)) { import(relative);return; }
        var staging=WorkspacePathPolicy.ResolveConfined(root,"Generated/ModelImportSources/"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(staging);
        var file=Path.Combine(staging,Path.GetFileName(full));
        try {File.Copy(full,file,overwrite:false);import(Path.GetRelativePath(root,file));}
        finally {if(File.Exists(file))File.Delete(file);Directory.Delete(staging);}
    }
    private async Task ImportPrefabModelDialogAsync(string nodeId)
    {
        if(TopLevel.GetTopLevel(this)?.StorageProvider is not {CanOpen:true} storage)return;
        var files=await storage.OpenFilePickerAsync(new(){Title="Import Prefab GLB",AllowMultiple=false,FileTypeFilter=[new("GLB models"){Patterns=["*.glb"]}]});
        if(files.FirstOrDefault()?.TryGetLocalPath() is { } path) Try(()=>WithSelectedModelFile(path,source=>Prefabs.ImportModel(nodeId,source)));
    }
    private void Clear3DForProjectChange()
    {
        _sceneKind=null; _sceneCancellation.Cancel(); Cabinets.Close(); Prefabs.Close(); Reels.Close(); _composition=null; _sceneRows.Clear(); _baseScene=SceneRenderData.Empty; SceneView.SetScene(SceneRenderData.Empty);
    }
    private void Dispose3D()
    {
        _sceneDisposed=true; _sceneKind=null; _sceneCancellation.Cancel(); _sceneCancellation.Dispose();
        Reels.Changed-=On3DAssetChanged; Cabinets.Changed-=On3DAssetChanged; Prefabs.Changed-=On3DAssetChanged; Machines.Changed-=OnMachineSceneChanged; Faces.Changed-=OnMachineSceneChanged; SceneView.Dispose();
    }
    private void UpdateSceneModeUI()
    {
        var active=Is3DActive;
        _viewport.IsVisible=!active;SceneView.IsVisible=active;_hierarchy.IsVisible=!active;_sceneHierarchy.IsVisible=active;
        _sources.IsVisible=!active;if(_sourceRegionHeading is not null)_sourceRegionHeading.IsVisible=!active;
        _sceneTools.IsVisible=active;if(_sceneToolbarBorder is not null)_sceneToolbarBorder.IsVisible=active;if(_twoDToolbar is not null)_twoDToolbar.IsVisible=!active;
    }
    private void Refresh3DWorkspaceChrome()
    {
        RefreshAssets();_project.Text=_session.ProjectName??"Open or create an Oasis project";
        var dirty=_sceneKind=="Reel"?Reels.IsDirty:_sceneKind=="Cabinet3D"?Cabinets.IsDirty:_sceneKind=="Prefab"?Prefabs.IsDirty:Machines.IsDirty;
        _document.Title=ActiveTitle+(dirty?" *":"");_mode.Text=_sceneKind;_save.IsEnabled=dirty;
        _undo.IsEnabled=_sceneKind=="Reel"?Reels.CanUndo:_sceneKind=="Cabinet3D"?Cabinets.CanUndo:_sceneKind=="Prefab"?Prefabs.CanUndo:Machines.CanUndo;
        _redo.IsEnabled=_sceneKind=="Reel"?Reels.CanRedo:_sceneKind=="Cabinet3D"?Cabinets.CanRedo:_sceneKind=="Prefab"?Prefabs.CanRedo:Machines.CanRedo;
        Refresh3DInspector();StateChanged?.Invoke();
    }
}
