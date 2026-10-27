using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace OasisEditor.Avalonia;

public sealed record WorkspaceDocumentTab(string Kind,string Path,string Scope,bool IsDirty,bool IsActive,string? LibraryRoot=null);

public sealed partial class WorkspaceView
{
    private readonly StackPanel _documentTabs = new() { Orientation=Orientation.Horizontal,Spacing=2 };
    private sealed record DocumentViewState(WorkspaceViewportState View,string? Source,WorkspaceSceneCameraState Camera,string? SceneNode);
    private readonly Dictionary<string,DocumentViewState> _documentViews = new();
    private string? _activeTabKey;
    private bool _tabsReady;
    private void InitializeDocumentTabs(Grid scene, MenuItem file)
    {
        _session.EnableDocumentRetention();Faces.EnableDocumentRetention();Machines.EnableDocumentRetention();
        Cabinets.EnableDocumentRetention();Prefabs.EnableDocumentRetention();Reels.EnableDocumentRetention();
        scene.RowDefinitions=new RowDefinitions("Auto,Auto,*");
        foreach(var child in scene.Children) Grid.SetRow(child,Grid.GetRow(child)+1);
        var tabs=new ScrollViewer { Content=_documentTabs,HorizontalScrollBarVisibility=global::Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,VerticalScrollBarVisibility=global::Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
        scene.Children.Add(tabs);
        MenuAction(file,"Close asset",async()=>await SceneAction(CloseActiveDocumentAsync));
        MenuAction(file,"Save all assets",async()=>await SceneAction(SaveAllDocumentsAsync));
        _tabsReady=true;
    }
    public IReadOnlyList<WorkspaceDocumentTab> InspectOpenDocuments()
    {
        var result=new List<WorkspaceDocumentTab>();
        var activePath=ActiveDocumentPath;
        void Add(string kind,IReadOnlyList<OpenAuthoringDocument> docs)
        {
            foreach(var d in docs)
            {
                var scope=d.LibraryRoot is null?"Project":"Library";
                result.Add(new(kind,d.Path,scope,d.IsDirty,kind==ActiveDocumentKind && SameScenePath(activePath,d.Path),d.LibraryRoot));
            }
        }
        if(_session.ProjectFilePath is null)return result;
        Add("Panel2D",_session.OpenDocuments);Add("Face",Faces.OpenDocuments);Add("Machine",Machines.OpenDocuments);
        Add("Cabinet3D",Cabinets.OpenDocuments);Add("Prefab",Prefabs.OpenDocuments);Add("Reel",Reels.OpenDocuments);
        return result;
    }
    private string ActiveDocumentKind=>_sceneKind??(_faceActive?"Face":_machineActive?"Machine":"Panel2D");
    private string? ActiveDocumentPath=>ActiveDocumentKind switch {"Face"=>Faces.FacePath,"Machine"=>Machines.MachinePath,"Cabinet3D"=>Cabinets.FilePath,"Prefab"=>Prefabs.FilePath,"Reel"=>Reels.FilePath,_=>_session.PanelPath};
    private void RefreshDocumentTabs()
    {
        if(!_tabsReady)return;
        var key=ActiveDocumentPath is {} path ? ActiveDocumentKind+":"+path:null;
        if(_activeTabKey!=key)
        {
            if(_activeTabKey is {} previous)_documentViews[previous]=new(_viewport.CaptureViewState(),_selectedSource,new(SceneView.Camera.Target.X,SceneView.Camera.Target.Y,SceneView.Camera.Target.Z,SceneView.Camera.Distance,SceneView.Camera.Yaw,SceneView.Camera.Pitch,SceneView.Camera.FieldOfView),_selectedSceneNodeId);
            _activeTabKey=key;
            if(key is {} current && _documentViews.TryGetValue(current,out var saved))
            { _viewport.RestoreViewState(saved.View);_selectedSource=saved.Source;_lastDocument=ActiveDocumentPath; }
        }
        _documentTabs.Children.Clear();
        foreach(var tab in InspectOpenDocuments())
        {
            var item=new StackPanel{Orientation=Orientation.Horizontal,Spacing=1};
            var title=Path.GetFileName(Path.GetDirectoryName(tab.Path))??tab.Kind;
            var button=ActionButton($"{title} · {tab.Kind}{(tab.Scope=="Library"?" · Library":"")}{(tab.IsDirty?" *":"")}",async()=>await SceneAction(()=>ActivateDocumentAsync(tab.Kind,tab.Path)));
            button.FontWeight=tab.IsActive?FontWeight.Bold:FontWeight.Normal;
            item.Children.Add(button);
            item.Children.Add(ActionButton("×",async()=>await SceneAction(()=>CloseDocumentAsync(tab.Kind,tab.Path))));
            _documentTabs.Children.Add(item);
        }
    }
    private void RestoreDocumentCamera()
    {
        if(!Is3DActive || ActiveDocumentPath is not {} path || !_documentViews.TryGetValue(ActiveDocumentKind+":"+path,out var view))return;
        ApplySceneCamera(view.Camera);
        if(view.SceneNode is {} node && _sceneRows.Any(r=>r.Id==node))SelectSceneNode(node);
    }
    public async Task ActivateDocumentAsync(string kind,string path)
    {
        var tab=InspectOpenDocuments().FirstOrDefault(t=>t.Kind==kind && SameScenePath(t.Path,path))
            ??throw new KeyNotFoundException("This document is not open.");
        if(tab.Scope=="Library")
        {
            var root=tab.LibraryRoot!;
            var relative=Path.GetRelativePath(root,path);
            switch(kind)
            {
                case "Reel":Reels.OpenLibrary(root,relative);_sceneKind="Reel";break;
                case "Cabinet3D":Cabinets.OpenLibrary(root,relative);_sceneKind="Cabinet3D";break;
                case "Prefab":Prefabs.OpenLibrary(root,relative);_sceneKind="Prefab";break;
            }
            _faceActive=_machineActive=false;await ReloadSceneAsync();RestoreDocumentCamera();Refresh();return;
        }
        switch(kind)
        {
            case "Panel2D":_session.OpenPanel(path);ShowPanel();break;
            case "Face":Faces.OpenFace(path);ShowFace();break;
            case "Machine":Machines.OpenMachine(path);ShowMachine();break;
            case "Cabinet3D":await OpenCabinet3DAsync(path);break;
            case "Prefab":await OpenPrefab3DAsync(path);break;
            case "Reel":await OpenReelAsync(path);break;
            default:throw new ArgumentException("Unknown document kind.",nameof(kind));
        }
        Refresh();
    }
    public async Task CloseDocumentAsync(string kind,string path)
    {
        var before=InspectOpenDocuments();
        var target=before.FirstOrDefault(t=>t.Kind==kind&&SameScenePath(t.Path,path))??throw new KeyNotFoundException("This document is not open.");
        if(target.IsDirty)throw new InvalidOperationException("Save or undo this asset's edits before closing it.");
        var previous=before.FirstOrDefault(t=>t.IsActive);
        switch(kind)
        {
            case "Panel2D":_session.CloseRetainedDocument(path);break;
            case "Face":Faces.CloseRetainedDocument(path);break;
            case "Machine":Machines.CloseRetainedDocument(path);break;
            case "Cabinet3D":Cabinets.CloseRetainedDocument(path);break;
            case "Prefab":Prefabs.CloseRetainedDocument(path);break;
            case "Reel":Reels.CloseRetainedDocument(path);break;
        }
        var next=previous is not null&&!target.IsActive?previous:InspectOpenDocuments().LastOrDefault();
        _documentViews.Remove(kind+":"+path);
        if(!target.IsActive){Refresh();return;}
        if(next is not null)await ActivateDocumentAsync(next.Kind,next.Path);
        else{_sceneKind=null;_faceActive=_machineActive=false;SceneView.SuspendRendering();Refresh();}
    }
    public Task CloseActiveDocumentAsync()=>ActiveDocumentPath is {} path?CloseDocumentAsync(ActiveDocumentKind,path):Task.CompletedTask;
    public async Task SaveAllDocumentsAsync()
    {
        var tabs=InspectOpenDocuments();var active=tabs.FirstOrDefault(t=>t.IsActive);
        try{foreach(var tab in tabs.Where(t=>t.IsDirty)){await ActivateDocumentAsync(tab.Kind,tab.Path);SaveActive();}}
        finally{if(active is not null)await ActivateDocumentAsync(active.Kind,active.Path);}
    }
}
