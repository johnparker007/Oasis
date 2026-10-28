using OasisEditor.Features.CabinetEditor.Models;
namespace OasisEditor.Avalonia;

public sealed record FloatingAssetState(string Kind,string Path,string? LibraryRoot,WorkspaceWindowState Bounds,
    WorkspaceViewportState? Viewport=null,string? SourceSelection=null,string[]? PanelSelection=null,string? FaceSelection=null,
    WorkspaceSceneState? Scene=null,bool MachineGraph=false,WorkspaceDockNode? Layout=null);

public sealed partial class WorkspaceView
{
    private readonly bool _isAssetWindow;
    private bool _movingDocument,_disposed;
    private Task _assetWindowTask=Task.CompletedTask;

    public WorkspaceView GetFloatingDocumentView(string kind,string path)
    {
        if(_documentOwner is {} host)return host.GetFloatingDocumentView(kind,path);
        return FindNativeEditor(kind,path).Editor;
    }

    public bool IsMovingAssetWindow=>_nativeSyncing||_nativeLayoutOperation||!_nativeRestoreTask.IsCompleted||!_assetWindowTask.IsCompleted;
    private void EnsureAssetWindowsIdle()
    {
        if(IsMovingAssetWindow)throw new InvalidOperationException("Wait for document docking or workspace restoration to finish.");
    }
    private Task ScheduleAssetWindowAction(Func<Task> action)=>_assetWindowTask=SceneAction(action);

    public async Task FloatDocumentAsync(string kind,string path,WorkspaceWindowState? bounds=null)
    {
        if(_documentOwner is {} host){await host.FloatDocumentAsync(kind,path,bounds);return;}
        if(_nativeLayoutOperation||!_nativeRestoreTask.IsCompleted)
            throw new InvalidOperationException("Wait for document docking or workspace restoration to finish.");
        await SynchronizeAssetDocumentsAsync();
        var entry=FindNativeEditor(kind,path);
        _nativeLayoutOperation=true;
        try
        {
            _factory.FloatDockable(entry.Document);
            global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            if(bounds is not null && NativeWindowFor(entry) is {} window)RestoreWindow(window,bounds);
            _factory.SetActiveDockable(entry.Document);FocusNativeEditor(entry);
        }
        finally{_nativeLayoutOperation=false;}
    }

    public async Task DockDocumentAsync(string kind,string path)
    {
        if(_documentOwner is {} host){await host.DockDocumentAsync(kind,path);return;}
        if(_nativeLayoutOperation||!_nativeRestoreTask.IsCompleted)
            throw new InvalidOperationException("Wait for document docking or workspace restoration to finish.");
        await SynchronizeAssetDocumentsAsync();
        RedockNativeEditor(FindNativeEditor(kind,path));
    }

    private async Task DisplayTransferredAssetAsync(string kind,FloatingAssetState state)
    {
        _sceneCancellation.Cancel();_sceneKind=null;_faceActive=_machineActive=false;
        _activeTabKey=null;_lastDocument=null;_inspected=null;
        if(kind=="Face")_faceActive=true;
        else if(kind is "Machine" or "Cabinet3D" or "Prefab" or "Reel")
        {
            _sceneKind=kind;_machineActive=kind=="Machine";
            _selectedSceneNodeId=state.Scene?.Selection;
            await ReloadSceneAsync();
            if(state.Scene?.Camera is {} camera)ApplySceneCamera(camera);
            if(state.Scene?.Selection is {} node&&_sceneRows.Any(r=>r.Id==node))SelectSceneNode(node);
            if(state.Scene is {} scene){_sceneView.ShowVisuals=scene.ShowVisuals;_sceneView.ShowColliders=scene.ShowColliders;_sceneView.ShowTriggers=scene.ShowTriggers;}
        }
        else _sceneView.SuspendRendering();
        _machineGraphActive=kind=="Machine"&&state.MachineGraph;
        _selectedSource=state.SourceSelection;
        if(ActiveDocumentPath is {} activePath)
            _documentViews[ActiveDocumentKind+":"+activePath]=new(state.Viewport??_viewport.CaptureViewState(),state.SourceSelection,
                state.Scene?.Camera??new(_sceneView.Camera.Target.X,_sceneView.Camera.Target.Y,_sceneView.Camera.Target.Z,_sceneView.Camera.Distance,_sceneView.Camera.Yaw,_sceneView.Camera.Pitch,_sceneView.Camera.FieldOfView),state.Scene?.Selection);
        Refresh();
        if(state.Viewport is {} viewport)_viewport.RestoreViewState(viewport);
    }

    public void SaveFloatingDocument(string kind,string path)=>GetFloatingDocumentView(kind,path).SaveActive();
    public void FloatingDocumentHistory(string kind,string path,bool redo=false)
    {var view=GetFloatingDocumentView(kind,path);if(redo)view.RedoActive();else view.UndoActive();}

    public object InspectFloatingDocument(string kind,string path)
    {
        var view=GetFloatingDocumentView(kind,path);
        var native=kind switch {"Face"=>FaceDocumentStorage.Serialize(view.Faces.Document!),"Machine"=>MachineDocumentStorage.Serialize(view.Machines.Document!),
            "Cabinet3D"=>CabinetDocumentStorage.Serialize(view.Cabinets.Document),"Prefab"=>Object3DDocumentStorage.Serialize(view.Prefabs.Document),
            "Reel"=>ReelDocumentStorage.Serialize(view.Reels.Document),_=>null};
        return new {kind,path,title=view.ActiveTitle,isDirty=view.HasUnsavedChanges,nativeJson=native,
            elements=view.ActiveElements,selectedIds=view.SelectedIds,sourceShapes=view._session.SourceShapes,
            scene=view.Is3DActive?view.InspectScene3D():null};
    }

    public void UpdateFloatingElement(string kind,string path,string id,string? name=null,double? x=null,double? y=null,
        double? width=null,double? height=null,bool? visible=null,bool? locked=null)
    {
        var view=GetFloatingDocumentView(kind,path);
        if(kind=="Panel2D")view._session.UpdateElement(id,name,x,y,width,height,visible,locked);
        else if(kind=="Face")view.Faces.UpdateElement(id,name,x,y,width,height,visible,locked);
        else throw new ArgumentException("Choose a floating Panel2D or Face.",nameof(kind));
    }

    private FloatingAssetState CaptureAssetView(WorkspaceDocumentTab tab)
    {
        var current=tab.IsActive;
        _documentViews.TryGetValue(tab.Kind+":"+tab.Path,out var retained);
        var camera=current?new WorkspaceSceneCameraState(_sceneView.Camera.Target.X,_sceneView.Camera.Target.Y,_sceneView.Camera.Target.Z,_sceneView.Camera.Distance,_sceneView.Camera.Yaw,_sceneView.Camera.Pitch,_sceneView.Camera.FieldOfView):retained?.Camera;
        var node=current?_selectedSceneNodeId:retained?.SceneNode;
        var relative=Path.GetRelativePath(tab.LibraryRoot??_session.RequireProjectDirectory(),tab.Path).Replace('\\','/');
        return new(tab.Kind,relative,tab.LibraryRoot,new(80,80,1200,800),current?_viewport.CaptureViewState():retained?.View,
            current?_selectedSource:retained?.Source,null,null,
            tab.Kind is "Panel2D" or "Face"?null:new(tab.Kind,tab.Kind=="Cabinet3D"?relative:null,tab.Kind=="Prefab"?relative:null,node,camera,
                _sceneView.ShowVisuals,_sceneView.ShowColliders,_sceneView.ShowTriggers,tab.Kind=="Reel"?relative:null,tab.Scope),
            tab.Kind=="Machine"&&_machineGraphActive);
    }

    public IReadOnlyList<FloatingAssetState> InspectFloatingDocuments()
        =>_documentOwner?.InspectFloatingDocuments()??InspectNativeFloatingDocuments();
}
