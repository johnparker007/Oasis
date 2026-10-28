using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.Mvvm.Controls;

namespace OasisEditor.Avalonia;

public sealed partial class WorkspaceView
{
    private sealed class NativeAssetEditor(WorkspaceAssetDocument asset, WorkspaceView editor, Document document)
    {
        public WorkspaceAssetDocument Asset { get; } = asset;
        public WorkspaceView Editor { get; } = editor;
        public Document Document { get; } = document;
        public FloatingAssetState? ViewState;
    }
    private readonly bool _isDocumentEditor;
    private readonly AssetEditorSessions? _assetSessionBundle;
    private WorkspaceView? _documentOwner;
    private readonly Dictionary<string, NativeAssetEditor> _nativeDocuments = new(StringComparer.Ordinal);
    private AssetDocumentRegistry _assetRegistry = new();
    private NativeAssetEditor? _focusedAsset;
    private bool _nativeSyncQueued, _nativeSyncing, _nativeLayoutOperation, _nativeDisposing;
    private Task _nativeDocumentTask = Task.CompletedTask;
    private ContentControl _menuSlot = null!, _toolbarSlot = null!, _statusSlot = null!;
    private Control _editorHierarchy = null!, _editorInspector = null!, _editorToolbar = null!, _editorStatus = null!;
    private Menu _editorMenu = null!;
    private IDocumentDock? _mainAssetDock;
    public WorkspaceView CommandView => _focusedAsset?.Editor ?? this;
    public AuthoringSession CommandSession => CommandView._session;
    private WorkspaceView DocumentHost => _documentOwner ?? this;
    public WorkspaceView GetDocumentEditor(string kind)
    {
        var host = DocumentHost;
        if (host._focusedAsset?.Asset.Kind == kind) return host._focusedAsset.Editor;
        return host._nativeDocuments.Values.LastOrDefault(d => d.Asset.Kind == kind)?.Editor ?? host.CommandView;
    }
    private AssetEditorSessions LocalAssetSessions => new(_session, _faces, _machines, _cabinets, _prefabs, _reels);

    private void InitializeNativeDocuments(Grid root)
    {
        InitializeTabPresentation();
        _mainAssetDock = _document.Owner as IDocumentDock;
        _editorMenu = (Menu)_menuSlot.Content!;
        root.Children.Remove(_editorToolbar); root.Children.Remove(_editorStatus);
        _toolbarSlot = new ContentControl { Content = _editorToolbar }; Grid.SetRow(_toolbarSlot, 1); root.Children.Add(_toolbarSlot);
        _statusSlot = new ContentControl { Content = _editorStatus }; Grid.SetRow(_statusSlot, 3); root.Children.Add(_statusSlot);
        _factory.ActiveDockableChanged += (_, e) => FocusNativeDockable(e.Dockable);
        _factory.FocusedDockableChanged += (_, e) => FocusNativeDockable(e.Dockable);
        _factory.DockableClosing += (_, e) =>
        {
            if (_nativeDisposing || _nativeLayoutOperation) return;
            var entry = _nativeDocuments.Values.FirstOrDefault(d => ReferenceEquals(d.Document, e.Dockable));
            if (entry?.Asset.IsDirty == true) { e.Cancel = true; entry.Editor.SetStatus("Save or undo this asset's edits before closing it."); }
        };
        _factory.DockableClosed += (_, e) =>
        {
            if (_nativeDisposing || _nativeLayoutOperation) return;
            var entry = _nativeDocuments.Values.FirstOrDefault(d => ReferenceEquals(d.Document, e.Dockable));
            if (entry is not null) RemoveNativeEditor(entry);
        };
        _factory.WindowClosing += (_, e) =>
        {
            if (_nativeDisposing || _nativeLayoutOperation || e.Window.Layout is null) return;
            var ids = WorkspaceLayoutState.LeafMap(e.Window.Layout).Keys.ToHashSet(StringComparer.Ordinal);
            var entries = _nativeDocuments.Values.Where(d => ids.Contains(d.Document.Id)).ToArray();
            if (entries.Length == 0) return;
            e.Cancel = true;
            Dispatcher.UIThread.Post(() => Try(() => { foreach (var entry in entries) RedockNativeEditor(entry); }));
        };
    }

    private void FocusNativeDockable(IDockable? dockable)
    {
        if (_nativeSyncing || _nativeDisposing || _nativeLayoutOperation) return;
        var entry = _nativeDocuments.Values.FirstOrDefault(d => ReferenceEquals(d.Document, dockable));
        if (entry is not null) FocusNativeEditor(entry);
    }
    private void FocusEditorIfOwned(WorkspaceView editor,string kind)
    {
        if(_nativeDocuments.Values.FirstOrDefault(d=>ReferenceEquals(d.Editor,editor) && d.Asset.Kind==kind && SameScenePath(d.Asset.Path,editor.ActiveDocumentPath)) is {} entry)
        {_factory.SetActiveDockable(entry.Document);FocusNativeEditor(entry);}
    }
    private void FocusNativeEditor(NativeAssetEditor entry)
    {
        if (_nativeDisposing) return;
        if(_maximisedTab?.Pane is {} maximised && _nativeDocuments.Values.Any(d=>ReferenceEquals(d.Document,maximised)) && !ReferenceEquals(maximised,entry.Document))RestoreMaximisedTab();
        _focusedAsset = entry;
        _menuSlot.Content = entry.Editor._editorMenu;
        _toolbarSlot.Content = entry.Editor._editorToolbar;
        _statusSlot.Content = entry.Editor._editorStatus;
        var panes = WorkspaceLayoutState.LeafMap(_dock.Layout!);
        if (panes.TryGetValue("Hierarchy", out var hierarchy)) hierarchy.Context = entry.Editor._editorHierarchy;
        if (panes.TryGetValue("Inspector", out var inspector)) inspector.Context = entry.Editor._editorInspector;
        RefreshNativeDocumentTitles(); RefreshAssets(); StateChanged?.Invoke();
    }
    private void RefreshNativeDocumentTitles()
    {
        foreach (var entry in _nativeDocuments.Values)
            entry.Document.Title = $"{entry.Asset.Title} · {entry.Asset.Kind}{(entry.Asset.LibraryRoot is null ? "" : " · Library")}{(entry.Asset.IsDirty ? " *" : "")}";
    }
    private void QueueAssetDocumentSync()
    {
        if (_isDocumentEditor) { _documentOwner?.QueueAssetDocumentSync(); return; }
        if (_disposed || _nativeDisposing || _nativeSyncing || _nativeLayoutOperation || _nativeSyncQueued || _session.ProjectFilePath is null) return;
        _nativeSyncQueued = true;
        Dispatcher.UIThread.Post(async () =>
        {
            _nativeSyncQueued = false;
            if(_nativeLayoutOperation)return;
            try { await SynchronizeAssetDocumentsAsync(); }
            catch (Exception error) { SetStatus(error.Message); }
        }, DispatcherPriority.Send);
    }
    public Task SynchronizeAssetDocumentsAsync()
    {
        if (_documentOwner is {} owner) return owner.SynchronizeAssetDocumentsAsync();
        if (_nativeSyncing) return _nativeDocumentTask;
        _nativeDocumentTask = SynchronizeNativeCoreAsync();
        return _nativeDocumentTask;
    }
    private async Task SynchronizeNativeCoreAsync()
    {
        if (_isDocumentEditor || _nativeDisposing || _session.ProjectFilePath is null) return;
        var wantedKind=CommandView.ActiveDocumentKind; var wantedPath=CommandView.ActiveDocumentPath;
        _nativeSyncing = true;
        try
        {
            await Task.Yield();
            for (var pass = 0; pass < 4; pass++)
            {
                var changed = false;
                foreach (var source in new[] { this }.Concat(_nativeDocuments.Values.Select(d => d.Editor)).ToArray())
                {
                    await source._sceneTask;
                    foreach (var tab in source.InspectDockedDocuments().ToArray())
                    {
                        if (_nativeDocuments.Values.Any(d => ReferenceEquals(d.Editor, source) && d.Asset.Kind == tab.Kind && SameScenePath(d.Asset.Path, tab.Path))) continue;
                        var existing = _nativeDocuments.Values.FirstOrDefault(d => d.Asset.Kind == tab.Kind && SameScenePath(d.Asset.Path, tab.Path));
                        if (existing is not null)
                        {
                            if(tab.IsDirty)throw new InvalidOperationException("This asset is already open in another document. Its pending edits have been preserved; activate the existing document before editing it.");
                            source._movingDocument=true;
                            try
                            {
                                switch(tab.Kind)
                                {
                                    case "Panel2D":source._session.CloseRetainedDocument(tab.Path);break;
                                    case "Face":source._faces.CloseRetainedDocument(tab.Path);break;
                                    case "Machine":source._machines.CloseRetainedDocument(tab.Path);break;
                                    case "Cabinet3D":source._cabinets.CloseRetainedDocument(tab.Path);break;
                                    case "Prefab":source._prefabs.CloseRetainedDocument(tab.Path);break;
                                    case "Reel":source._reels.CloseRetainedDocument(tab.Path);break;
                                }
                            }
                            finally{source._movingDocument=false;}
                            if(_nativeDocuments.Values.FirstOrDefault(d=>ReferenceEquals(d.Editor,source)) is {} canonical)
                            {
                                canonical.Asset.Activate();
                                await source.ActivateLocalDocumentAsync(canonical.Asset.Kind,canonical.Asset.Path);
                                if(canonical.ViewState is {} previous)await source.DisplayTransferredAssetAsync(canonical.Asset.Kind,previous);
                            }
                            _factory.SetActiveDockable(existing.Document);FocusNativeEditor(existing);
                            continue;
                        }
                        var state = source.ActiveDocumentKind == tab.Kind && SameScenePath(source.ActiveDocumentPath, tab.Path)
                            ? source.CaptureAssetView(tab) : new FloatingAssetState(tab.Kind, tab.Path, tab.LibraryRoot, new(80, 80, 1200, 800));
                        WorkspaceAssetDocument asset;
                        source._movingDocument=true;
                        try { asset = _assetRegistry.Adopt(tab.Kind, tab.Path, tab.LibraryRoot, source.LocalAssetSessions); }
                        finally { source._movingDocument=false; }
                        var entry = await AddNativeEditorAsync(asset, state);
                        _factory.SetActiveDockable(entry.Document); FocusNativeEditor(entry);
                        var original = _nativeDocuments.Values.FirstOrDefault(d => ReferenceEquals(d.Editor, source));
                        if (original is not null)
                        {
                            original.Asset.Activate();
                            await source.ActivateLocalDocumentAsync(original.Asset.Kind, original.Asset.Path);
                            if (original.ViewState is {} previous)
                            {
                                if(source._documentViews.TryGetValue(original.Asset.Kind+":"+original.Asset.Path,out var retained))
                                    previous=previous with{Viewport=retained.View,SourceSelection=retained.Source,Scene=previous.Scene is {} scene?scene with{Camera=retained.Camera,Selection=retained.SceneNode}:null};
                                await source.DisplayTransferredAssetAsync(original.Asset.Kind, previous);
                            }
                        }
                        changed = true;
                    }
                }
                if (!changed) break;
            }
            _sceneKind=null; _faceActive=_machineActive=false; _sceneCancellation.Cancel(); _sceneView.SuspendRendering();
            if(_nativeDocuments.Values.FirstOrDefault(d=>d.Asset.Kind==wantedKind && SameScenePath(d.Asset.Path,wantedPath)) is {} wanted)
            {_factory.SetActiveDockable(wanted.Document);FocusNativeEditor(wanted);}
            RefreshNativeDocumentTitles();
        }
        finally { _nativeSyncing = false; }
    }

    private async Task<NativeAssetEditor> AddNativeEditorAsync(WorkspaceAssetDocument asset, FloatingAssetState? viewState = null)
    {
        _session.ShareClipboardWith(asset.Sessions.Project);
        var child = new WorkspaceView(asset.Sessions.Project, asset.Sessions.Faces, preview: Preview, assetWindow: true, documentEditor: true, editorSessions: asset.Sessions, outputLog: Output);
        child._documentOwner = this;
        child.SceneLibraryRoot = asset.LibraryRoot ?? SceneLibraryRoot; child.NativePreviewPreferences=NativePreviewPreferences; child.Preferences=GetPreferences();
        var document = new Document { Id = asset.Id, Title = asset.Title, Context = child, CanClose = true };
        var entry = new NativeAssetEditor(asset, child, document); _nativeDocuments.Add(asset.Id, entry);
        void ChildChanged()
        {
            if (_nativeDisposing || _nativeSyncing || _nativeLayoutOperation || !_nativeDocuments.ContainsKey(asset.Id)) return;
            if (child.ActiveDocumentKind == asset.Kind && SameScenePath(child.ActiveDocumentPath, asset.Path))
                entry.ViewState = child.CaptureAssetView(new(asset.Kind, asset.Path, asset.LibraryRoot is null ? "Project" : "Library", asset.IsDirty, true, asset.LibraryRoot));
            RefreshNativeDocumentTitles(); QueueAssetDocumentSync(); StateChanged?.Invoke();
        }
        child.StateChanged += ChildChanged; child._session.Changed += ChildChanged; child.Faces.Changed += ChildChanged; child.Machines.Changed += ChildChanged; child.Cabinets.Changed += ChildChanged; child.Prefabs.Changed += ChildChanged; child.Reels.Changed += ChildChanged;
        child.AddHandler(PointerPressedEvent, (_, _) => { FocusNativeEditor(entry); _factory.SetActiveDockable(document); }, RoutingStrategies.Tunnel, handledEventsToo: true);
        var target = _mainAssetDock;
        target ??= _document.Owner as IDocumentDock ?? throw new InvalidOperationException("No document dock is available.");
        var placeholder = target.VisibleDockables?.FirstOrDefault(d => d.Id == "Authoring");
        if (placeholder is not null) _factory.RemoveDockable(placeholder, false);
        _factory.AddDockable(target, document); _factory.InitDockable(document, target);
        _factory.SetActiveDockable(document); UpdateLayout(); Dispatcher.UIThread.RunJobs();
        asset.Activate();
        await child.DisplayTransferredAssetAsync(asset.Kind, viewState ?? new(asset.Kind, asset.Path, asset.LibraryRoot, new(80,80,1200,800)));
        entry.ViewState = child.CaptureAssetView(new(asset.Kind,asset.Path,asset.LibraryRoot is null ? "Project" : "Library",asset.IsDirty,true,asset.LibraryRoot));
        return entry;
    }
    private NativeAssetEditor FindNativeEditor(string kind, string path)
    {
        path = Path.GetFullPath(path, _session.RequireProjectDirectory());
        return _nativeDocuments.Values.SingleOrDefault(d => d.Asset.Kind == kind && SameScenePath(d.Asset.Path,path))
            ?? throw new KeyNotFoundException("This asset is not open.");
    }
    public async Task OpenNativeAssetAsync(string kind, string path, string? libraryRoot = null)
    {
        if (_documentOwner is {} host) { await host.OpenNativeAssetAsync(kind,path,libraryRoot); return; }
        if(_nativeLayoutOperation||!_nativeRestoreTask.IsCompleted)throw new InvalidOperationException("Wait for workspace restoration to finish before opening an asset.");
        await SynchronizeAssetDocumentsAsync();
        RestoreMaximisedTab();
        path=Path.GetFullPath(path,libraryRoot??_session.RequireProjectDirectory());
        var entry=_nativeDocuments.Values.FirstOrDefault(d=>d.Asset.Kind==kind && SameScenePath(d.Asset.Path,path));
        if(entry is null)
        {
            var asset=_assetRegistry.Open(_session.WorkspacePath,_session.ProjectFilePath!,kind,path,libraryRoot);
            entry=await AddNativeEditorAsync(asset);
        }
        _factory.SetActiveDockable(entry.Document);FocusNativeEditor(entry);
    }
    private static IDocumentDock? FindMainDocumentDock(IDockable layout)
    {
        if(layout is IDocumentDock documents)return documents;
        if(layout is IDock dock)foreach(var child in dock.VisibleDockables??[])
            if(FindMainDocumentDock(child) is {} found)return found;
        return null;
    }
    private IReadOnlyList<FloatingAssetState> InspectNativeFloatingDocuments()=>_nativeDocuments.Values
        .Where(d=>NativeWindowFor(d) is not null).Select(d=>
        {
            var window=NativeWindowFor(d)!;
            var tab=new WorkspaceDocumentTab(d.Asset.Kind,d.Asset.Path,d.Asset.LibraryRoot is null?"Project":"Library",d.Asset.IsDirty,true,d.Asset.LibraryRoot);
            return d.Editor.CaptureAssetView(tab) with { Bounds=new(window.Position.X,window.Position.Y,Math.Max(100,window.ClientSize.Width),Math.Max(100,window.ClientSize.Height),window.WindowState.ToString()),PanelSelection=d.Editor._session.SelectedIds.ToArray(),FaceSelection=d.Editor.Faces.SelectedId };
        }).ToArray();
    public async Task SplitDocumentAsync(string kind,string path,string targetKind,string targetPath,string side="right")
    {
        if(_documentOwner is {} host){await host.SplitDocumentAsync(kind,path,targetKind,targetPath,side);return;}
        await SynchronizeAssetDocumentsAsync();
        RestoreMaximisedTab();
        var source=FindNativeEditor(kind,path);var target=FindNativeEditor(targetKind,targetPath);
        if(ReferenceEquals(source,target))throw new ArgumentException("Choose a different document to split beside.");
        var operation=side.ToLowerInvariant() switch {"left"=>DockOperation.Left,"right"=>DockOperation.Right,"top"=>DockOperation.Top,"bottom"=>DockOperation.Bottom,_=>throw new ArgumentException("Use left, right, top or bottom.",nameof(side))};
        var targetDock=(IDock)target.Document.Owner!;
        _nativeLayoutOperation=true;
        try
        {
            _factory.RemoveDockable(source.Document,true);
            var group=_factory.CreateDocumentDock();
            group.Id="Documents-"+Guid.NewGuid().ToString("N");
            group.VisibleDockables=new System.Collections.ObjectModel.ObservableCollection<IDockable>{source.Document};
            group.ActiveDockable=group.DefaultDockable=source.Document;
            _factory.SplitToDock(targetDock,group,operation);
            _factory.InitDockable(group,group.Owner); _factory.InitDockable(source.Document,group);
            _factory.SetActiveDockable(source.Document);FocusNativeEditor(source);
        }
        finally{_nativeLayoutOperation=false;}
    }
    private async Task ActivateNativeDocumentAsync(string kind, string path)
    {
        await SynchronizeAssetDocumentsAsync();
        var entry = FindNativeEditor(kind,path); _factory.SetActiveDockable(entry.Document); FocusNativeEditor(entry);
    }
    private IReadOnlyList<WorkspaceDocumentTab> InspectNativeDocuments() => _nativeDocuments.Values.Select(d =>
        new WorkspaceDocumentTab(d.Asset.Kind,d.Asset.Path,d.Asset.LibraryRoot is null ? "Project" : "Library",d.Asset.IsDirty,ReferenceEquals(d,_focusedAsset),d.Asset.LibraryRoot,NativeWindowFor(d) is not null)).ToArray();
    private Window? NativeWindowFor(NativeAssetEditor entry) => (_dock.Layout as IRootDock)?.Windows?
        .FirstOrDefault(w => w.Layout is not null && WorkspaceLayoutState.LeafMap(w.Layout).ContainsKey(entry.Document.Id))?.Host as Window;
    private void CloseNativeDocument(string kind,string path)
    {
        var entry=FindNativeEditor(kind,path);
        if(ReferenceEquals(_maximisedTab?.Pane,entry.Document))throw new InvalidOperationException("Restore the tab before closing it.");
        if(entry.Asset.IsDirty)throw new InvalidOperationException("Save or undo this asset's edits before closing it.");
        _factory.CloseDockable(entry.Document);
    }
    private void RemoveNativeEditor(NativeAssetEditor entry)
    {
        _nativeDocuments.Remove(entry.Asset.Id); _assetRegistry.RemoveClean(entry.Asset.Kind,entry.Asset.Path,entry.Asset.LibraryRoot);
        if(ReferenceEquals(_focusedAsset,entry))
        {
            _focusedAsset=null;
            if(_nativeDocuments.Values.LastOrDefault() is {} next) { _factory.SetActiveDockable(next.Document); FocusNativeEditor(next); }
            else
            {
                _menuSlot.Content=_editorMenu; _toolbarSlot.Content=_editorToolbar; _statusSlot.Content=_editorStatus;
                var panes=WorkspaceLayoutState.LeafMap(_dock.Layout!);
                if(panes.TryGetValue("Hierarchy",out var hierarchy))hierarchy.Context=_editorHierarchy;
                if(panes.TryGetValue("Inspector",out var inspector))inspector.Context=_editorInspector;
            }
        }
        entry.Document.Context=null; entry.Editor.Dispose(); CloseEmptyNativeHosts();
    }
    private void RedockNativeEditor(NativeAssetEditor entry)
    {
        var target=WorkspaceLayoutState.LeafMap(_dock.Layout!).Values.FirstOrDefault(p => p is IDocument && !ReferenceEquals(p,entry.Document) && !IsFloatingDockable(p));
        var mainDock = target?.Owner as IDock ?? _mainAssetDock;
        if(mainDock is null) throw new InvalidOperationException("No main document group is available.");
        _nativeLayoutOperation=true;
        try { _factory.MoveDockable((IDock)entry.Document.Owner!,mainDock,entry.Document,target!); _factory.SetActiveDockable(entry.Document); FocusNativeEditor(entry); CloseEmptyNativeHosts(); }
        finally { _nativeLayoutOperation=false; }
    }
    private void CloseEmptyNativeHosts()
    {
        if(_dock.Layout is not IRootDock root)return;
        foreach(var window in (root.Windows??[]).Where(w=>w.Layout is null || WorkspaceLayoutState.LeafMap(w.Layout).Count==0).ToArray())
        {
            _factory.CloseWindow(window);
            if(root.Windows?.Contains(window)==true)_factory.RemoveWindow(window);
        }
    }
    private bool IsFloatingDockable(IDockable pane) => (_dock.Layout as IRootDock)?.Windows?.Any(w=>w.Layout is not null && WorkspaceLayoutState.LeafMap(w.Layout).ContainsKey(pane.Id))==true;
    private void DisposeNativeDocuments()
    {
        if(!_isDocumentEditor)RestoreMaximisedTab();
        if(_isDocumentEditor || _nativeDocuments.Count==0)return;
        _nativeDisposing=true;
        try
        {
            foreach(var entry in _nativeDocuments.Values.ToArray()){entry.Document.Context=null;entry.Editor.Dispose();}
            _nativeDocuments.Clear();_assetRegistry=new();_focusedAsset=null;
            if(_menuSlot is not null){_menuSlot.Content=_editorMenu;_toolbarSlot.Content=_editorToolbar;_statusSlot.Content=_editorStatus;}
            if(!_disposed)
            {
                if(_dock.Layout is IRootDock old)old.ExitWindows.Execute(null);
                _dock.Layout=null;
                var fresh=_factory.CreateLayout();_factory.InitLayout(fresh);_dock.Layout=fresh;
                _document=(Document)WorkspaceLayoutState.LeafMap(fresh)["Authoring"];
                _mainAssetDock=_document.Owner as IDocumentDock;
            }
        }
        finally{_nativeDisposing=false;}
    }
    private void ResetNativeLayout()
    {
        RestoreMaximisedTab();
        _nativeLayoutOperation=true;
        try
        {
            var layout=_factory.CreateLayout();_factory.InitLayout(layout);
            var placeholder=(Document)WorkspaceLayoutState.LeafMap(layout)["Authoring"];
            var dock=(IDocumentDock)placeholder.Owner!;
            // CreateLayout is not initialized yet, so establish the document list directly.
            dock.VisibleDockables=new System.Collections.ObjectModel.ObservableCollection<IDockable>(_nativeDocuments.Values.Select(d=>(IDockable)d.Document));
            dock.ActiveDockable=_focusedAsset?.Document??dock.VisibleDockables.FirstOrDefault();
            dock.DefaultDockable=dock.ActiveDockable;
            ReplaceLayout(layout);
            if(_focusedAsset is {} focused){_factory.SetActiveDockable(focused.Document);FocusNativeEditor(focused);}
        }
        finally{_nativeLayoutOperation=false;}
    }
    private void HandleDocumentKey(KeyEventArgs e)
    {
        var command=(e.KeyModifiers & (KeyModifiers.Control|KeyModifiers.Meta))!=0;
        if(command && e.Key==Key.S){Try(SaveActive);e.Handled=true;return;}
        if(e.Source is TextBox)return;
        if(command && e.Key==Key.Z){Try((e.KeyModifiers&KeyModifiers.Shift)!=0?RedoActive:UndoActive);e.Handled=true;}
        else if(command && e.Key==Key.Y){Try(RedoActive);e.Handled=true;}
        else if(command && e.Key==Key.C){CopyPanelSelection();e.Handled=true;}
        else if(command && e.Key==Key.X){CutPanelSelection();e.Handled=true;}
        else if(command && e.Key==Key.V){PastePanelSelection();e.Handled=true;}
        else if(command && e.Key==Key.D){Try(()=>{RequirePanel();_session.DuplicateSelected();});e.Handled=true;}
        else if(command && e.Key==Key.A){Try(()=>SelectActive(ActiveElements.Select(element=>element.Id).ToArray()));e.Handled=true;}
        else if(e.Key==Key.Delete){Try(DeleteActive);e.Handled=true;}
    }
}
