using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace OasisEditor.Avalonia;

public sealed record WorkspaceDocumentTab(string Kind,string Path,string Scope,bool IsDirty,bool IsActive,string? LibraryRoot=null,bool IsFloating=false);

public sealed partial class WorkspaceView
{
    private sealed record DocumentViewState(WorkspaceViewportState View,string? Source,WorkspaceSceneCameraState Camera,string? SceneNode);
    private readonly Dictionary<string,DocumentViewState> _documentViews = new();
    private string? _activeTabKey;
    private bool _tabsReady;
    private void InitializeDocumentTabs(Grid scene, MenuItem file)
    {
        _session.EnableDocumentRetention();_faces.EnableDocumentRetention();_machines.EnableDocumentRetention();
        _cabinets.EnableDocumentRetention();_prefabs.EnableDocumentRetention();_reels.EnableDocumentRetention();
        MenuAction(file,"Close asset",async()=>await SceneAction(CloseActiveDocumentAsync));
        MenuAction(file,"Float active asset",async()=>{if(ActiveDocumentPath is {} path)await ScheduleAssetWindowAction(()=>FloatDocumentAsync(ActiveDocumentKind,path));});
        MenuAction(file,"Save All",async()=>await SaveAllFromUiAsync());
        _tabsReady=true;
    }
    private IReadOnlyList<WorkspaceDocumentTab> InspectDockedDocuments()
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
        Add("Panel2D",_session.OpenDocuments);Add("Face",_faces.OpenDocuments);Add("Machine",_machines.OpenDocuments);
        Add("Cabinet3D",_cabinets.OpenDocuments);Add("Prefab",_prefabs.OpenDocuments);Add("Reel",_reels.OpenDocuments);
        return result;
    }
    public IReadOnlyList<WorkspaceDocumentTab> InspectOpenDocuments()=>_nativeDocuments.Count>0 ? InspectNativeDocuments() : InspectDockedDocuments();
    private string ActiveDocumentKind=>_sceneKind??(_faceActive?"Face":_machineActive?"Machine":"Panel2D");
    private string? ActiveDocumentPath=>ActiveDocumentKind switch {"Face"=>_faces.FacePath,"Machine"=>_machines.MachinePath,"Cabinet3D"=>_cabinets.FilePath,"Prefab"=>_prefabs.FilePath,"Reel"=>_reels.FilePath,_=>_session.PanelPath};
    private void RefreshDocumentTabs()
    {
        if(!_tabsReady)return;
        var key=ActiveDocumentPath is {} path ? ActiveDocumentKind+":"+path:null;
        if(_activeTabKey!=key)
        {
            if(_activeTabKey is {} previous)_documentViews[previous]=new(_viewport.CaptureViewState(),_selectedSource,new(_sceneView.Camera.Target.X,_sceneView.Camera.Target.Y,_sceneView.Camera.Target.Z,_sceneView.Camera.Distance,_sceneView.Camera.Yaw,_sceneView.Camera.Pitch,_sceneView.Camera.FieldOfView),_selectedSceneNodeId);
            _activeTabKey=key;
            if(key is {} current && _documentViews.TryGetValue(current,out var saved))
            { _viewport.RestoreViewState(saved.View);_selectedSource=saved.Source;_lastDocument=ActiveDocumentPath; }
        }
        if (!_isDocumentEditor) QueueAssetDocumentSync();
    }

    private void RestoreDocumentCamera()
    {
        if(!Is3DActive || ActiveDocumentPath is not {} path || !_documentViews.TryGetValue(ActiveDocumentKind+":"+path,out var view))return;
        ApplySceneCamera(view.Camera);
        if(view.SceneNode is {} node && _sceneRows.Any(r=>r.Id==node))SelectSceneNode(node);
    }
    public async Task ActivateDocumentAsync(string kind,string path)
    {
        if(_documentOwner is {} host){await host.ActivateDocumentAsync(kind,path);return;}
        if(!_isDocumentEditor && _nativeDocuments.Count>0){await ActivateNativeDocumentAsync(kind,path);return;}
        await ActivateLocalDocumentAsync(kind,path);
    }
    private async Task ActivateLocalDocumentAsync(string kind,string path)
    {
        path=Path.GetFullPath(path,_session.RequireProjectDirectory());
        var tab=InspectDockedDocuments().FirstOrDefault(t=>t.Kind==kind && SameScenePath(t.Path,path))
            ??throw new KeyNotFoundException("This document is not open.");
        if(tab.Scope=="Library")
        {
            var root=tab.LibraryRoot!;
            var relative=Path.GetRelativePath(root,path);
            switch(kind)
            {
                case "Reel":_reels.OpenLibrary(root,relative);_sceneKind="Reel";break;
                case "Cabinet3D":_cabinets.OpenLibrary(root,relative);_sceneKind="Cabinet3D";break;
                case "Prefab":_prefabs.OpenLibrary(root,relative);_sceneKind="Prefab";break;
            }
            _faceActive=_machineActive=false;await ReloadSceneAsync();RestoreDocumentCamera();Refresh();return;
        }
        switch(kind)
        {
            case "Panel2D":_session.OpenPanel(path);ShowPanel();break;
            case "Face":_faces.OpenFace(path);ShowFace();break;
            case "Machine":_machines.OpenMachine(path);ShowMachine();break;
            case "Cabinet3D":_cabinets.Open(path);_sceneKind="Cabinet3D";_faceActive=_machineActive=false;await ReloadSceneAsync();break;
            case "Prefab":_prefabs.Open(path);_sceneKind="Prefab";_faceActive=_machineActive=false;await ReloadSceneAsync();break;
            case "Reel":_reels.Open(path);_sceneKind="Reel";_faceActive=_machineActive=false;await ReloadSceneAsync();break;
            default:throw new ArgumentException("Unknown document kind.",nameof(kind));
        }
        Refresh();
    }
    public async Task CloseDocumentAsync(string kind,string path)
    {
        if(_documentOwner is {} host){await host.CloseDocumentAsync(kind,path);return;}
        if(!_isDocumentEditor && _nativeDocuments.Count>0){EnsureAssetWindowsIdle();CloseNativeDocument(kind,path);return;}
        path=Path.GetFullPath(path,_session.RequireProjectDirectory());
        EnsureAssetWindowsIdle();
        var before=InspectDockedDocuments();
        var target=before.FirstOrDefault(t=>t.Kind==kind&&SameScenePath(t.Path,path))??throw new KeyNotFoundException("This document is not open.");
        if(target.IsDirty)throw new InvalidOperationException("Save or undo this asset's edits before closing it.");
        var previous=before.FirstOrDefault(t=>t.IsActive);
        if(target.IsActive){_sceneKind=null;_faceActive=_machineActive=false;}
        switch(kind)
        {
            case "Panel2D":_session.CloseRetainedDocument(path);break;
            case "Face":_faces.CloseRetainedDocument(path);break;
            case "Machine":_machines.CloseRetainedDocument(path);break;
            case "Cabinet3D":_cabinets.CloseRetainedDocument(path);break;
            case "Prefab":_prefabs.CloseRetainedDocument(path);break;
            case "Reel":_reels.CloseRetainedDocument(path);break;
        }
        var next=previous is not null&&!target.IsActive?previous:InspectOpenDocuments().LastOrDefault();
        _documentViews.Remove(kind+":"+path);
        if(!target.IsActive){Refresh();return;}
        if(next is not null)await ActivateDocumentAsync(next.Kind,next.Path);
        else{_sceneKind=null;_faceActive=_machineActive=false;_sceneView.SuspendRendering();Refresh();}
    }
    public async Task CloseActiveDocumentAsync()
    { var view=CommandView; if(view.ActiveDocumentPath is {} path)await DocumentHost.RequestCloseDocumentAsync(view.ActiveDocumentKind,path); }
    public async Task SaveAllDocumentsAsync()
    {
        if(_documentOwner is {} host){await host.SaveAllDocumentsAsync();return;}
        if(!_isDocumentEditor)await SynchronizeAssetDocumentsAsync();
        EnsureAssetWindowsIdle();
        if(_nativeDocuments.Count>0){foreach(var d in _nativeDocuments.Values.Where(d=>d.Asset.IsDirty).ToArray())SaveNativeAsset(d); RefreshNativeDocumentTitles();return;}
        EnsureAssetWindowsIdle();
        var tabs=InspectOpenDocuments();var active=tabs.FirstOrDefault(t=>t.IsActive);
        try{foreach(var tab in tabs.Where(t=>t.IsDirty)){if(tab.IsFloating)SaveFloatingDocument(tab.Kind,tab.Path);else{await ActivateDocumentAsync(tab.Kind,tab.Path);SaveActive();}}}
        finally{if(active is not null)await ActivateDocumentAsync(active.Kind,active.Path);}
    }
}
