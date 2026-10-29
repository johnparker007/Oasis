using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using OasisEditor.Authoring.Assets;

namespace OasisEditor.Avalonia;

public sealed record ReusableAssetChoice(string Label, AssetReference? Reference)
{ public override string ToString()=>Label; }

public sealed partial class WorkspaceView
{
    public LibraryDiscoveryResult LibraryCatalog { get; private set; } = new([],[]);
    public bool BrowsingLibrary => _browseLibrary;
    private bool _browseLibrary, _libraryDisposed;
    private readonly ComboBox _librarySource = new(){ItemsSource=new[]{"Project Assets","Oasis Library"},SelectedIndex=0,MinWidth=130};
    private FileSystemWatcher? _libraryWatcher;
    private readonly DispatcherTimer _libraryDebounce = new(){Interval=TimeSpan.FromMilliseconds(350)};
    private int _libraryVersion;
    private Task _libraryTask=Task.CompletedTask;
    private readonly SemaphoreSlim _libraryRefreshGate=new(1,1);
    private void InitializeLibrary(Grid projectPanel,MenuItem file)
    {
        foreach(var child in projectPanel.Children)if(Grid.GetRow(child)>=1)Grid.SetRow(child,Grid.GetRow(child)+1);
        projectPanel.RowDefinitions=new("Auto,Auto,Auto,*,Auto");
        var tools=new WrapPanel{Orientation=Orientation.Horizontal,Margin=new(8,0,8,3)};
        tools.Children.Add(_librarySource);
        tools.Children.Add(ActionButton("Refresh",async()=>{await RefreshLibraryAsync();Refresh();}));
        tools.Children.Add(ActionButton("Library folder…",async()=>await ChooseLibraryRootAsync()));
        tools.Children.Add(ActionButton("Copy to Library…",async()=>await CopyLibraryDialogAsync(false)));
        tools.Children.Add(ActionButton("Import to project…",async()=>await CopyLibraryDialogAsync(true)));
        Grid.SetRow(tools,1);projectPanel.Children.Add(tools);
        _librarySource.SelectionChanged+=(_,_)=>{_browseLibrary=_librarySource.SelectedIndex==1;_assets.SelectedItem=null;RefreshAssets();};
        _libraryDebounce.Tick+=async(_,_)=>{_libraryDebounce.Stop();await RefreshLibraryAsync();};
        MenuAction(file,"Refresh Oasis Library",async()=>await RefreshLibraryAsync());
        RestartLibraryWatcher();_libraryTask=RefreshLibraryAsync();
    }
    public void SetAssetBrowserSource(bool library)
    { _browseLibrary=library;_librarySource.SelectedIndex=library?1:0;RefreshAssets(); }
    public async Task RefreshLibraryAsync()
    {
        await _libraryRefreshGate.WaitAsync();
        try
        {
            if(_libraryDisposed)return;var root=SceneLibraryRoot;
            var catalog=string.IsNullOrWhiteSpace(root)?new LibraryDiscoveryResult([],[]):await Task.Run(()=>LibraryAssetService.Discover(root));
            if(_libraryDisposed||root!=SceneLibraryRoot)return;
            LibraryCatalog=catalog;RefreshAssets();
        }
        finally{_libraryRefreshGate.Release();}
    }
    private void RestartLibraryWatcher()
    {
        _libraryWatcher?.Dispose();_libraryWatcher=null;
        if(_libraryDisposed||string.IsNullOrWhiteSpace(SceneLibraryRoot)||!Directory.Exists(SceneLibraryRoot))return;
        _libraryWatcher=new(SceneLibraryRoot){IncludeSubdirectories=true,NotifyFilter=NotifyFilters.FileName|NotifyFilters.DirectoryName|NotifyFilters.LastWrite};
        _libraryWatcher.Changed+=LibraryFileChanged;_libraryWatcher.Created+=LibraryFileChanged;_libraryWatcher.Deleted+=LibraryFileChanged;_libraryWatcher.Renamed+=LibraryFileChanged;
        _libraryWatcher.Error+=(_,_)=>Dispatcher.UIThread.Post(()=>{if(!_libraryDisposed)_libraryDebounce.Start();});
        _libraryWatcher.EnableRaisingEvents=true;
    }
    private void LibraryFileChanged(object? sender,FileSystemEventArgs e)
        =>Dispatcher.UIThread.Post(()=>{if(!_libraryDisposed){_libraryDebounce.Stop();_libraryDebounce.Start();}});
    private void DisposeLibrary()
    {_libraryDisposed=true;_libraryVersion++;_libraryDebounce.Stop();_libraryWatcher?.Dispose();_libraryWatcher=null;}
    public async Task OpenLibraryAssetAsync(string libraryRelativeManifest)
    {
        _=_session.RequireEditorProject();
        var reference=AssetReference.Library(libraryRelativeManifest);
        await RefreshLibraryAsync();
        var asset=LibraryCatalog.Assets.FirstOrDefault(a=>a.Reference==reference)??throw new InvalidDataException("Library asset is missing or invalid. Refresh the Library and inspect its diagnostics.");
        var kind=asset.Type switch{EditorAssetType.Object3D=>"Prefab",EditorAssetType.Cabinet3D=>"Cabinet3D",_=>asset.Type.ToString()};
        if(!_isDocumentEditor || _documentOwner is not null){await DocumentHost.OpenNativeAssetAsync(kind,asset.ManifestPath,SceneLibraryRoot);return;}
        switch(asset.Type)
        {
            case EditorAssetType.Reel:if(!SameScenePath(_reels.FilePath,asset.ManifestPath)||!_reels.IsDirty)_reels.OpenLibrary(SceneLibraryRoot,reference.Path);_sceneKind="Reel";_selectedSceneNodeId=_reels.Document.Id;break;
            case EditorAssetType.Cabinet3D:if(!SameScenePath(_cabinets.FilePath,asset.ManifestPath)||!_cabinets.IsDirty)_cabinets.OpenLibrary(SceneLibraryRoot,reference.Path);_sceneKind="Cabinet3D";_selectedSceneNodeId="cabinet";break;
            case EditorAssetType.Object3D:if(!SameScenePath(_prefabs.FilePath,asset.ManifestPath)||!_prefabs.IsDirty)_prefabs.OpenLibrary(SceneLibraryRoot,reference.Path);_sceneKind="Prefab";_selectedSceneNodeId=_prefabs.Document.Root.Id;break;
            default:throw new InvalidDataException("Unsupported Library asset type.");
        }
        _faceActive=_machineActive=false;await ReloadSceneAsync();RestoreDocumentCamera();Refresh();
    }
    public LibraryCopyResult CopyProjectAssetToLibrary(string projectManifest,string? packageName=null)
    {
        if(HasUnsavedChanges)throw new InvalidOperationException("Save or undo open document edits before copying a package to the Library.");
        if(string.IsNullOrWhiteSpace(SceneLibraryRoot))throw new InvalidOperationException("Choose an Oasis Library folder first.");
        return LibraryAssetService.CopyToLibrary(_session.RequireEditorProject(),projectManifest,SceneLibraryRoot,packageName);
    }
    public LibraryCopyResult ImportLibraryAsset(string libraryRelativeManifest,string? packageName=null)
    {
        if(string.IsNullOrWhiteSpace(SceneLibraryRoot))throw new InvalidOperationException("Choose an Oasis Library folder first.");
        var result=LibraryAssetService.ImportToProject(_session.RequireEditorProject(),AssetReference.Library(libraryRelativeManifest),SceneLibraryRoot,packageName);
        RefreshAssets();return result;
    }
    public IReadOnlyList<ReusableAssetChoice> GetReusableAssetChoices(EditorAssetType type,AssetReference? current=null)
    {
        if(type is not(EditorAssetType.Reel or EditorAssetType.Cabinet3D or EditorAssetType.Object3D))throw new ArgumentException("Choose a Cabinet, Reel or Prefab asset type.");
        var result=new List<ReusableAssetChoice>{new("(None)",null)};
        if(_session.ProjectFilePath is not null)
            foreach(var asset in _session.ListAssets().Where(a=>a.Type==type.ToString()))
            {
                var label=asset.Name;
                try{var json=File.ReadAllText(_session.ResolveAsset(asset.ManifestPath));
                    if(type==EditorAssetType.Reel){if(!ReelDocumentStorage.TryRead(json,out var reel,out _))continue;label=reel.DisplayName;}
                    else if(type==EditorAssetType.Object3D){if(!Object3DDocumentStorage.TryRead(json,out var prefab,out _))continue;label=prefab.DisplayName;}}
                catch(Exception e)when(e is IOException or InvalidDataException or UnauthorizedAccessException){continue;}
                result.Add(new($"{label} [Project] · {asset.ManifestPath}",AssetReference.Project(asset.ManifestPath)));
            }
        result.AddRange(LibraryCatalog.Assets.Where(a=>a.Type==type).Select(a=>new ReusableAssetChoice($"{a.DisplayName} [Library] · {a.Reference.Path}",a.Reference)));
        if(current is not null&&!result.Any(c=>c.Reference==current))result.Add(new($"Missing: {current.Path} [{current.Scope}]",current));
        return result;
    }
    public void AssignMachineReel(MachineObjectReference reference,AssetReference? asset)
    {
        if(!_isDocumentEditor && _nativeDocuments.Count>0){GetDocumentEditor("Machine").AssignMachineReel(reference,asset);return;}
        if(reference.Kind!=MachineObjectKind.Reel||reference.IsEmpty)throw new ArgumentException("A logical Reel reference is required.");
        if(asset is not null)
        {
            var path=new AssetReferenceResolver().Resolve(_session.RequireEditorProject(),SceneLibraryRoot,asset);
            var root=asset.Scope==AssetReferenceScope.Library?SceneLibraryRoot:_session.RequireProjectDirectory();
            path=WorkspacePathPolicy.ResolveConfined(root,Path.GetRelativePath(root,path),requireExists:true);
            if(Path.GetFileName(path)!=ProjectAssetPathService.ReelManifestFileName||!ReelDocumentStorage.TryRead(File.ReadAllText(path),out _,out var error))
                throw new InvalidDataException("The selected asset must be a valid native Reel package.");
        }
        _machines.UpdateDocument(machine=>machine with{ReelAssignments=asset is null?machine.ReelAssignments.Where(a=>a.MachineReelReference!=reference).ToArray():machine.ReelAssignments.Any(a=>a.MachineReelReference==reference)
            ?machine.ReelAssignments.Select(a=>a.MachineReelReference==reference?new MachineReelAssignment(reference,asset):a).ToArray():[..machine.ReelAssignments,new(reference,asset)]});
    }
    private bool IsLibraryScene => _sceneKind switch{"Reel"=>_reels.IsLibraryAsset,"Cabinet3D"=>_cabinets.IsLibraryAsset,"Prefab"=>_prefabs.IsLibraryAsset,_=>false};
    private string? ActiveLibraryRoot => _sceneKind switch{"Reel"=>_reels.LibraryRoot,"Cabinet3D"=>_cabinets.LibraryRoot,"Prefab"=>_prefabs.LibraryRoot,_=>null};
    private async Task CopyLibraryDialogAsync(bool import)
    {
        if(_assets.SelectedItem is not AssetSummary selected||_browseLibrary!=import){SetStatus(import?"Select a Library package to import.":"Select a project Cabinet, Reel or Prefab package to copy.");return;}
        if(TopLevel.GetTopLevel(this) is not Window owner)return;
        var name=new TextBox{Text=Path.GetFileName(Path.GetDirectoryName(selected.ManifestPath))};var error=new TextBlock{TextWrapping=TextWrapping.Wrap};
        var content=new StackPanel{Margin=new(16),Spacing=10,Children={new TextBlock{Text="Destination package name"},name,error}};
        var dialog=new Window{Title=import?"Import Library package":"Copy package to Library",Width=430,Height=210,Content=content};
        content.Children.Add(TextEntryDialog.Actions(dialog, name, ActionButton(import?"Import":"Copy",async()=>{try{var result=import?ImportLibraryAsset(selected.ManifestPath,name.Text):CopyProjectAssetToLibrary(selected.ManifestPath,name.Text);
            if(!result.Success){error.Text=result.Error;return;}await RefreshLibraryAsync();SetStatus("Package copied to "+result.ManifestPath);dialog.Close();}catch(Exception e)when(e is not OutOfMemoryException){error.Text=e.Message;}})));
        await dialog.ShowDialog(owner);
    }
}
