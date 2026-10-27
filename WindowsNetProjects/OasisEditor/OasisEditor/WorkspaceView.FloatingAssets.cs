using Avalonia;
using Avalonia.Controls;
using OasisEditor.Features.CabinetEditor.Models;

namespace OasisEditor.Avalonia;

public sealed record FloatingAssetState(string Kind,string Path,string? LibraryRoot,WorkspaceWindowState Bounds,
    WorkspaceViewportState? Viewport=null,string? SourceSelection=null,string[]? PanelSelection=null,string? FaceSelection=null,
    WorkspaceSceneState? Scene=null,bool MachineGraph=false,WorkspaceDockNode? Layout=null);

public sealed partial class WorkspaceView
{
    private sealed record AssetWindow(string Kind,string Path,WorkspaceView View,Window Window);
    private readonly Dictionary<string,AssetWindow> _assetWindows = new(OperatingSystem.IsWindows()?StringComparer.OrdinalIgnoreCase:StringComparer.Ordinal);
    private readonly bool _isAssetWindow;
    private bool _movingDocument,_releasingAssetWindows,_disposed,_assetWindowOperation,_restoringAssetWindows;
    private Task _assetWindowTask=Task.CompletedTask;
    private Func<Task>? _redockAction;
    private WorkspaceView? _assetWindowOwner;
    private string AssetWindowKey(string kind,string path)=>kind+":"+(Path.IsPathRooted(path)?Path.GetFullPath(path):Path.GetFullPath(path,_session.RequireProjectDirectory()));
    private AssetWindow? FindAssetWindow(string kind,string path)=>_assetWindows.Count==0?null:_assetWindows.GetValueOrDefault(AssetWindowKey(kind,path));
    public WorkspaceView GetFloatingDocumentView(string kind,string path)=>FindAssetWindow(kind,path)?.View
        ??throw new KeyNotFoundException("This asset is not floating.");

    private void ConfigureAssetWindowChrome(MenuItem file,MenuItem create,MenuItem windowMenu,StackPanel toolbar)
    {
        var allowed=new HashSet<string>(StringComparer.Ordinal){"Save","Import FML layout…","Import Face artwork…","Import artwork override…",
            "Use Panel2D artwork source","Use Panel2D lamp mask","Create artwork override from Base","Import Face lamp mask…","Build Face","Build Face runtime assets","Rebuild Face components from source…","Enable artwork override","Disable artwork override","Remove artwork override",
            "Artwork registration and processing…","Rebuild Face artwork","Face generation settings…","Validate Face",
            "Build Player machine…","Export portable Player package…","Launch in Oasis Player…","Emulation preview"};
        foreach(var item in file.Items.OfType<MenuItem>().Where(m=>!allowed.Contains(m.Header?.ToString()??"")).ToArray())file.Items.Remove(item);
        foreach(var item in create.Items.OfType<MenuItem>().Where(m=>m.Header?.ToString() is "Reel…" or "Generate Face from selected source…").ToArray())create.Items.Remove(item);
        foreach(var item in windowMenu.Items.OfType<MenuItem>().Where(m=>m.Header?.ToString() is "Save workspace" or "Restore workspace").ToArray())windowMenu.Items.Remove(item);
        if(_sceneToolbarBorder is {} sceneBar && sceneBar.Child is not ScrollViewer)
        {
            var sceneTools=sceneBar.Child;sceneBar.Child=null;
            sceneBar.Child=new ScrollViewer{Content=sceneTools,HorizontalScrollBarVisibility=global::Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility=global::Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled};
        }
        toolbar.Children.RemoveAt(0);toolbar.Children.RemoveAt(0);
        toolbar.Children.Insert(0,ActionButton("Dock asset",async()=>await SceneAction(()=>_redockAction?.Invoke()??Task.CompletedTask)));
    }

    public bool IsMovingAssetWindow=>_assetWindowOperation||_restoringAssetWindows;
    private void EnsureAssetWindowsIdle()
    {if(IsMovingAssetWindow)throw new InvalidOperationException("Wait for the current asset-window move or restore to finish.");}
    private Task ScheduleAssetWindowAction(Func<Task> action)=>_assetWindowTask=SceneAction(action);
    public async Task FloatDocumentAsync(string kind,string path,WorkspaceWindowState? bounds=null)
    {
        EnsureAssetWindowsIdle();_assetWindowOperation=true;
        try{await FloatDocumentCoreAsync(kind,path,bounds);}finally{_assetWindowOperation=false;}
    }
    private async Task FloatDocumentCoreAsync(string kind,string path,WorkspaceWindowState? bounds=null)
    {
        if(_isAssetWindow)throw new InvalidOperationException("Dock this asset before moving it to another window.");
        path=Path.GetFullPath(path,_session.RequireProjectDirectory());
        if(FindAssetWindow(kind,path) is {} existing){ShowOwnedAssetWindow(existing.Window);return;}
        var tab=InspectDockedDocuments().FirstOrDefault(t=>t.Kind==kind&&SameScenePath(t.Path,path))
            ??throw new KeyNotFoundException("Open the asset before floating its tab.");
        bounds??=new WorkspaceWindowState(80+_assetWindows.Count*24,80+_assetWindows.Count*24,1200,800);
        WorkspaceLayoutState.ValidateBounds(bounds);
        await _sceneTask; // Capture the completed source scene camera, including a just-opened Machine.
        tab=InspectDockedDocuments().Single(t=>t.Kind==kind&&SameScenePath(t.Path,path));
        if(Preview.Backend is not null)await Preview.ControlAsync("stop");
        var project=new AuthoringSession(_session.WorkspacePath);project.OpenProject(_session.ProjectFilePath!);
        var child=new WorkspaceView(project,assetWindow:true);
        var window=new Window{Content=child,Width=bounds.Width,Height=bounds.Height,MinWidth=800,MinHeight=560};
        var entry=new AssetWindow(kind,tab.Path,child,window);
        var previous=InspectDockedDocuments().FirstOrDefault(t=>t.IsActive);
        var viewState=CaptureAssetView(tab);
        var moved=false;
        try
        {
            child.SceneLibraryRoot=SceneLibraryRoot;child.ConfigureNativePreview(NativePreviewPreferences);child.UpdatePreferences(GetPreferences());
            _movingDocument=child._movingDocument=true;
            MoveAssetTo(kind,tab.Path,child);moved=true;
            _assetWindows.Add(AssetWindowKey(kind,tab.Path),entry);
            if(previous is {IsActive:true} && previous.Kind==kind && SameScenePath(previous.Path,tab.Path)){_sceneKind=null;_faceActive=_machineActive=false;_activeTabKey=null;_lastDocument=null;_inspected=null;}
            _movingDocument=child._movingDocument=false;
            child._assetWindowOwner=this;
            child._redockAction=()=>ScheduleAssetWindowAction(()=>DockDocumentAsync(kind,tab.Path));
            SubscribeFloatingAsset(child,true);
            window.Closing+=(_,e)=>
            {
                if(_releasingAssetWindows)return;
                e.Cancel=true;
                _assetWindowTask=SceneAction(async()=>{await Task.Yield();await DockDocumentAsync(kind,tab.Path);});
            };
            await child.DisplayTransferredAssetAsync(kind,viewState);
            UpdateAssetWindowTitle(entry);
            RestoreWindow(window,bounds);
            window.Show();
            if(previous?.IsActive==true && previous.Kind==kind&&SameScenePath(previous.Path,tab.Path))await SelectRemainingDockedDocumentAsync();
            Refresh();StateChanged?.Invoke();
        }
        catch
        {
            _movingDocument=child._movingDocument=true;
            if(moved)child.MoveAssetTo(kind,tab.Path,this);
            _movingDocument=child._movingDocument=false;
            _assetWindows.Remove(AssetWindowKey(kind,tab.Path));
            ReleaseAssetWindow(entry);
            if(moved)await DisplayTransferredAssetAsync(kind,viewState);
            throw;
        }
        finally{_movingDocument=child._movingDocument=false;}
    }

    public async Task DockDocumentAsync(string kind,string path)
    {
        EnsureAssetWindowsIdle();_assetWindowOperation=true;
        try{await DockDocumentCoreAsync(kind,path);}finally{_assetWindowOperation=false;}
    }
    private async Task DockDocumentCoreAsync(string kind,string path)
    {
        var entry=FindAssetWindow(kind,path)??throw new KeyNotFoundException("This asset is not floating.");
        if(entry.View.Preview.Backend is not null)await entry.View.Preview.ControlAsync("stop");
        var tab=entry.View.InspectDockedDocuments().Single();
        var state=entry.View.CaptureAssetView(tab);
        try
        {
            _movingDocument=entry.View._movingDocument=true;
            entry.View.MoveAssetTo(kind,entry.Path,this);
            _assetWindows.Remove(AssetWindowKey(kind,path));
        }
        finally{_movingDocument=entry.View._movingDocument=false;}
        ReleaseAssetWindow(entry);
        await DisplayTransferredAssetAsync(kind,state);
        Refresh();StateChanged?.Invoke();
    }

    private void MoveAssetTo(string kind,string path,WorkspaceView destination)
    {
        switch(kind)
        {
            case "Panel2D":_session.MoveDocumentTo(path,destination._session);break;
            case "Face":Faces.MoveDocumentTo(path,destination.Faces);break;
            case "Machine":Machines.MoveDocumentTo(path,destination.Machines);break;
            case "Cabinet3D":Cabinets.MoveDocumentTo(path,destination.Cabinets);break;
            case "Prefab":Prefabs.MoveDocumentTo(path,destination.Prefabs);break;
            case "Reel":Reels.MoveDocumentTo(path,destination.Reels);break;
            default:throw new ArgumentException("Unsupported asset kind.",nameof(kind));
        }
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
            if(state.Scene is {} scene){SceneView.ShowVisuals=scene.ShowVisuals;SceneView.ShowColliders=scene.ShowColliders;SceneView.ShowTriggers=scene.ShowTriggers;}
        }
        else SceneView.SuspendRendering();
        _machineGraphActive=kind=="Machine"&&state.MachineGraph;
        _selectedSource=state.SourceSelection;
        if(ActiveDocumentPath is {} activePath)
            _documentViews[ActiveDocumentKind+":"+activePath]=new(state.Viewport??_viewport.CaptureViewState(),state.SourceSelection,
                state.Scene?.Camera??new(SceneView.Camera.Target.X,SceneView.Camera.Target.Y,SceneView.Camera.Target.Z,SceneView.Camera.Distance,SceneView.Camera.Yaw,SceneView.Camera.Pitch,SceneView.Camera.FieldOfView),state.Scene?.Selection);
        Refresh();
        if(state.Viewport is {} viewport)_viewport.RestoreViewState(viewport);
    }

    private async Task SelectRemainingDockedDocumentAsync()
    {
        var next=InspectDockedDocuments().LastOrDefault();
        _sceneKind=null;_faceActive=_machineActive=false;_activeTabKey=null;_lastDocument=null;_inspected=null;
        if(next is not null)await ActivateDocumentAsync(next.Kind,next.Path);
        else{SceneView.SuspendRendering();Refresh();}
    }
    private void SubscribeFloatingAsset(WorkspaceView view,bool add)
    {
        if(add){view.StateChanged+=FloatingAssetChanged;view._session.Changed+=FloatingAssetChanged;view.Faces.Changed+=FloatingAssetChanged;
            view.Machines.Changed+=FloatingAssetChanged;view.Reels.Changed+=FloatingAssetChanged;view.Cabinets.Changed+=FloatingAssetChanged;view.Prefabs.Changed+=FloatingAssetChanged;}
        else{view.StateChanged-=FloatingAssetChanged;view._session.Changed-=FloatingAssetChanged;view.Faces.Changed-=FloatingAssetChanged;
            view.Machines.Changed-=FloatingAssetChanged;view.Reels.Changed-=FloatingAssetChanged;view.Cabinets.Changed-=FloatingAssetChanged;view.Prefabs.Changed-=FloatingAssetChanged;}
    }
    private void FloatingAssetChanged()
    {
        if(_movingDocument||_releasingAssetWindows||_disposed)return;
        CheckPreviewSource();
        foreach(var entry in _assetWindows.Values)UpdateAssetWindowTitle(entry);
        Refresh();StateChanged?.Invoke();
    }
    private static void UpdateAssetWindowTitle(AssetWindow entry)
        =>entry.Window.Title=$"{entry.View.ActiveTitle}{(entry.View.HasUnsavedChanges?" *":"")} · {entry.Kind}{(entry.View.IsLibraryScene?" · Library":"")} — Oasis Editor";
    private static void ShowOwnedAssetWindow(Window window){if(!Program.IsHeadless){if(window.WindowState==WindowState.Minimized)window.WindowState=WindowState.Normal;window.Activate();}}
    private void ReleaseAssetWindow(AssetWindow entry)
    {
        var prior=_releasingAssetWindows;_releasingAssetWindows=true;
        try{SubscribeFloatingAsset(entry.View,false);entry.View.Dispose();entry.Window.Close();}
        finally{_releasingAssetWindows=prior;}
    }
    private void CloseAllAssetWindows()
    {
        foreach(var entry in _assetWindows.Values.ToArray())ReleaseAssetWindow(entry);
        _assetWindows.Clear();
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
        var camera=current?new WorkspaceSceneCameraState(SceneView.Camera.Target.X,SceneView.Camera.Target.Y,SceneView.Camera.Target.Z,SceneView.Camera.Distance,SceneView.Camera.Yaw,SceneView.Camera.Pitch,SceneView.Camera.FieldOfView):retained?.Camera;
        var node=current?_selectedSceneNodeId:retained?.SceneNode;
        var relative=Path.GetRelativePath(tab.LibraryRoot??_session.RequireProjectDirectory(),tab.Path).Replace('\\','/');
        return new(tab.Kind,relative,tab.LibraryRoot,new(80,80,1200,800),current?_viewport.CaptureViewState():retained?.View,
            current?_selectedSource:retained?.Source,null,null,
            tab.Kind is "Panel2D" or "Face"?null:new(tab.Kind,tab.Kind=="Cabinet3D"?relative:null,tab.Kind=="Prefab"?relative:null,node,camera,
                SceneView.ShowVisuals,SceneView.ShowColliders,SceneView.ShowTriggers,tab.Kind=="Reel"?relative:null,tab.Scope),
            tab.Kind=="Machine"&&_machineGraphActive);
    }
    public IReadOnlyList<FloatingAssetState> InspectFloatingDocuments()=>_assetWindows.Values.Select(entry=>
    {
        var state=entry.View.CaptureAssetView(entry.View.InspectDockedDocuments().Single());var window=entry.Window;
        return state with{Bounds=new(window.Position.X,window.Position.Y,Math.Max(100,window.ClientSize.Width),Math.Max(100,window.ClientSize.Height),window.WindowState.ToString()),
            PanelSelection=entry.View._session.SelectedIds.ToArray(),FaceSelection=entry.View.Faces.SelectedId,Layout=WorkspaceLayoutState.Capture(entry.View._dock.Layout!)};
    }).ToArray();

    private void PreflightFloatingDocuments(WorkspaceState state,AuthoringSession project)
    {
        var docs=state.FloatingDocuments??[];
        if(docs.Length>64)throw new InvalidDataException("Too many floating asset windows.");
        var seen=new HashSet<string>(OperatingSystem.IsWindows()?StringComparer.OrdinalIgnoreCase:StringComparer.Ordinal);
        foreach(var d in docs)
        {
            if(d is null||d.Bounds is null||string.IsNullOrWhiteSpace(d.Path)||!seen.Add(AssetWindowKey(d.Kind,Path.GetFullPath(d.Path,d.LibraryRoot??project.RequireProjectDirectory()))))throw new InvalidDataException("Invalid or duplicate floating document.");
            WorkspaceLayoutState.ValidateBounds(d.Bounds);d.Viewport?.Validate();d.Scene?.Validate();
            if(d.Layout is {} layout)
            {
                var known=new Dictionary<string,global::Dock.Model.Core.IDockable>{["Authoring"]=new global::Dock.Model.Mvvm.Controls.Document{Id="Authoring"},
                    ["Hierarchy"]=new global::Dock.Model.Mvvm.Controls.Tool{Id="Hierarchy"},["Inspector"]=new global::Dock.Model.Mvvm.Controls.Tool{Id="Inspector"}};
                _=WorkspaceLayoutState.Restore(layout,known);
            }
            if(d.Scene is not null&&d.Scene.Mode!=d.Kind || d.MachineGraph&&d.Kind!="Machine" || d.PanelSelection?.Length>10000)
                throw new InvalidDataException("Invalid floating asset view.");
            if(!(state.Documents??[]).Any(t=>t.Kind==d.Kind&&t.Path==d.Path&&t.LibraryRoot==d.LibraryRoot))throw new InvalidDataException("A floating asset must belong to the saved document list.");
            // Use the native validators before changing the live workspace or creating any windows.
            ReadDocumentList(state with{Documents=[new(d.Kind,d.Path,d.LibraryRoot)]},project,new(project),new(project),new(project),new(project),new(project));
        }
    }
    private async Task RestoreFloatingDocumentsAsync(Task primary,FloatingAssetState[]? docs)
    {
        _restoringAssetWindows=true;
        try{
        await primary;
        foreach(var state in docs??[])
        {
            var path=Path.GetFullPath(state.Path,state.LibraryRoot??_session.RequireProjectDirectory());
            if(_disposed)return;
            _assetWindowOperation=true;
            try{await FloatDocumentCoreAsync(state.Kind,path,state.Bounds);}finally{_assetWindowOperation=false;}
            var view=GetFloatingDocumentView(state.Kind,path);
            if(state.Kind=="Panel2D")view._session.SelectElements((state.PanelSelection??[]).Where(id=>view._session.Elements.Any(e=>e.Id==id)).ToArray());
            if(state.Kind=="Face")view.Faces.Select(view.Faces.Elements.Any(e=>e.Id==state.FaceSelection)?state.FaceSelection:null);
            if(state.Layout is {} layout)view.ReplaceLayout(WorkspaceLayoutState.Restore(layout,WorkspaceLayoutState.LeafMap(view._factory.CreateLayout())));
            await view.DisplayTransferredAssetAsync(state.Kind,state);
        }
        }finally{_restoringAssetWindows=false;}
    }
}
