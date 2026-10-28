using Avalonia.Controls;
using Avalonia.Layout;

namespace OasisEditor.Avalonia;

public sealed partial class WorkspaceView
{
    private readonly MachineCompositionOverviewBuilder _compositionOverviewBuilder = new();
    private MachineCompositionGraphView _compositionGraphView = null!;
    private Button _compositionGraphToggle = null!;
    private bool _machineGraphActive;
    public bool IsMachineGraphActive => CommandView._machineGraphActive;

    private void InitializeCompositionGraph(Grid scene, StackPanel toolbar)
    {
        _compositionGraphView = new MachineCompositionGraphView { IsVisible = false };
        Grid.SetRow(_compositionGraphView, 1);
        scene.Children.Add(_compositionGraphView);
        _compositionGraphView.NodeActivated += node => _sceneTask = SceneAction(() => ActivateMachineCompositionNodeAsync(node.Id));
        _compositionGraphToggle = (Button)ActionButton("Composition graph", ToggleMachineGraph);
        toolbar.Children.Insert(3, _compositionGraphToggle);
        _machines.Changed += RefreshCompositionGraph;
        _faces.Changed += RefreshCompositionGraph;
        _cabinets.Changed += RefreshCompositionGraph;
        _prefabs.Changed += RefreshCompositionGraph;
        _reels.Changed += RefreshCompositionGraph;
        RefreshCompositionGraph();
    }

    public MachineOverview InspectMachineCompositionGraph()
    {
        if(!ReferenceEquals(CommandView,this))return CommandView.InspectMachineCompositionGraph();
        var machine = _machines.Document ?? throw new InvalidOperationException("Open a Machine before inspecting its composition graph.");
        return _compositionOverviewBuilder.Build(machine, _session.RequireEditorProject(), SceneLibraryRoot,
            _faces.FacePath, _faces.Document);
    }

    public void ShowMachineCompositionGraph(bool visible = true)
    {
        if(!ReferenceEquals(CommandView,this)){CommandView.ShowMachineCompositionGraph(visible);return;}
        if (visible && (_sceneKind != "Machine" || _machines.Document is null))
            throw new InvalidOperationException("Open a Machine scene before showing its composition graph.");
        _machineGraphActive = visible;
        _compositionGraphToggle.Content = visible ? "3D scene" : "Composition graph";
        RefreshCompositionGraph();
        UpdateSceneModeUI();
    }

    public async Task ActivateMachineCompositionNodeAsync(string id)
    {
        if(!ReferenceEquals(CommandView,this)){await CommandView.ActivateMachineCompositionNodeAsync(id);return;}
        var node = InspectMachineCompositionGraph().Nodes.FirstOrDefault(item => item.Id == id)
            ?? throw new KeyNotFoundException($"Composition node '{id}' was not found.");
        if (node.ManifestPath is null)
        {
            if (node.Kind is "Machine" or "Runtime" or "Anchor") ShowMachine();
            else throw new InvalidOperationException("This composition node has no saved asset to open.");
            return;
        }

        if(_documentOwner is {} owner)
        {
            if(IsInLibrary(node.ManifestPath)){await owner.OpenLibraryAssetAsync(Path.GetRelativePath(SceneLibraryRoot,node.ManifestPath));return;}
            await owner.OpenNativeAssetAsync(node.Kind=="Cabinet"?"Cabinet3D":node.Kind,node.ManifestPath);return;
        }
        if (IsInLibrary(node.ManifestPath))
        {
            await OpenLibraryAssetAsync(Path.GetRelativePath(SceneLibraryRoot, node.ManifestPath));
            return;
        }
        switch (node.Kind)
        {
            case "Machine": ShowMachine(); break;
            case "Cabinet": await OpenCabinet3DAsync(node.ManifestPath); break;
            case "Prefab": await OpenPrefab3DAsync(node.ManifestPath); break;
            case "Reel": await OpenReelAsync(node.ManifestPath); break;
            case "Face": _faces.OpenFace(node.ManifestPath); ShowFace(); break;
            case "Panel2D": _session.OpenPanel(node.ManifestPath); ShowPanel(); break;
            default: throw new InvalidOperationException($"Composition node type '{node.Kind}' is not navigable.");
        }
    }

    private bool IsInLibrary(string path) => !string.IsNullOrWhiteSpace(SceneLibraryRoot)
        && path.StartsWith(Path.TrimEndingDirectorySeparator(SceneLibraryRoot) + Path.DirectorySeparatorChar,
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    private void ToggleMachineGraph() => ShowMachineCompositionGraph(!_machineGraphActive);

    private void SetCompositionGraphVisible(bool visible)
    {
        if (_compositionGraphView is not null) _compositionGraphView.IsVisible = visible;
        if (_compositionGraphToggle is not null)
        { _compositionGraphToggle.IsEnabled = _sceneKind == "Machine"; _compositionGraphToggle.Content=_machineGraphActive?"3D scene":"Composition graph"; }
    }

    private void RefreshCompositionGraph()
    {
        if (_compositionGraphView is null || _machines.Document is not { } machine || _session.ProjectFilePath is null) return;
        try
        {
            _compositionGraphView.SetGraph(_compositionOverviewBuilder.Build(machine, _session.RequireEditorProject(),
                SceneLibraryRoot, _faces.FacePath, _faces.Document));
        }
        catch (Exception error) when (error is IOException or InvalidDataException or InvalidOperationException or ArgumentException)
        { SetStatus(error.Message); }
    }
}
