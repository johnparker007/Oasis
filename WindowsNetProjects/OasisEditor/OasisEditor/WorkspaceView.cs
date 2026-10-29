using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Dock.Avalonia.Controls;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.Mvvm;
using Dock.Model.Mvvm.Controls;
using DockOrientation = Dock.Model.Core.Orientation;
using Orientation = Avalonia.Layout.Orientation;

namespace OasisEditor.Avalonia;

public sealed partial class WorkspaceView : UserControl, IDisposable
{
    private readonly AuthoringSession _session;
    public OutputLogService Output { get; }
    private static OutputLogService CreateOutputLog(bool assetWindow)
    {
        if (Program.IsHeadless || assetWindow) return new();
        try { return new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Oasis", "AvaloniaEditor", "Logs", $"editor-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.log")); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return new(); }
    }
    private OutputPaneControl? _outputPane;
    private readonly FaceAuthoringSession _faces;
    public FaceAuthoringSession Faces => !_isDocumentEditor && _nativeDocuments.Count>0 ? GetDocumentEditor("Face")._faces : _faces;
    private readonly MachineAuthoringSession _machines;
    public MachineAuthoringSession Machines => !_isDocumentEditor && _nativeDocuments.Count>0 ? GetDocumentEditor("Machine")._machines : _machines;
    private bool _faceActive;
    private bool _machineActive;
    private bool HasLocalUnsavedChanges => _session.HasRetainedChanges || _faces.HasRetainedChanges || _machines.HasRetainedChanges || _cabinets?.HasRetainedChanges == true || _prefabs?.HasRetainedChanges == true || _reels?.HasRetainedChanges == true;
    public bool HasUnsavedChanges => _nativeDocuments.Values.Any(d=>d.Asset.IsDirty || d.Editor.HasLocalUnsavedChanges) || HasLocalUnsavedChanges;
    public string ActiveTitle => !ReferenceEquals(CommandView,this) ? CommandView.ActiveTitle : _sceneKind == "Reel" ? _reels.Document.DisplayName : _sceneKind == "Cabinet3D" ? Path.GetFileName(Path.GetDirectoryName(_cabinets.FilePath)) ?? "Cabinet" : _sceneKind == "Prefab" ? _prefabs.Document.DisplayName : _machineActive ? _machines.MachineTitle : _faceActive ? _faces.FaceTitle : _session.PanelTitle;
    public event Action? StateChanged;
    private readonly PanelViewport _viewport;
    private readonly ListBox _hierarchy = new() { SelectionMode = SelectionMode.Multiple };
    private readonly ListBox _sources = new() { MaxHeight = 125 };
    private readonly ObservableCollection<ElementSnapshot> _rows = [];
    private readonly ListBox _assets = new();
    private readonly TextBox _assetSearch = new() { PlaceholderText = "Search assets", Margin = new Thickness(8, 4) };
    private readonly StackPanel _inspector = new() { Margin = new Thickness(12), Spacing = 9 };
    private readonly TextBlock _status = new() { Margin = new Thickness(10, 5), TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly TextBlock _project = new() { Margin = new Thickness(10, 7), FontWeight = FontWeight.SemiBold };
    private readonly TextBlock _mode = new() { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0), FontSize = 11, Opacity = .65 };
    private readonly Button _undo, _redo, _save;
    private Document _document;
    private readonly WorkspaceDockFactory _factory;
    private readonly DockControl _dock;
    private readonly Dictionary<string, TextBox> _fields = [];
    private CheckBox? _visible, _locked;
    private bool _refreshing;
    private string? _lastDocument, _selectedSource;
    private object? _inspected;

    partial void AddMachineWorkflowMenus(MenuItem fileMenu, MenuItem createMenu);
    partial void AddFaceWorkflowMenus(MenuItem fileMenu, MenuItem createMenu);

    public WorkspaceView(AuthoringSession session, FaceAuthoringSession? faces = null, NativePreviewSession? preview = null, bool assetWindow = false, bool documentEditor = false, AssetEditorSessions? editorSessions = null, OutputLogService? outputLog = null)
    {
        _isAssetWindow=assetWindow; _isDocumentEditor=documentEditor; _assetSessionBundle=editorSessions;
        Output = outputLog ?? CreateOutputLog(assetWindow);
        Preview = preview ?? new NativePreviewSession();
        _session = session; _faces = faces ?? new FaceAuthoringSession(session); _machines = editorSessions?.Machines ?? new MachineAuthoringSession(session);
        _viewport = new PanelViewport(session, Faces, () => _faceActive);
        _viewport.CommandFailed += SetStatus;
        _viewport.SelectedSourceRegion=()=>_selectedSource;
        _viewport.SourceRegionSelected+=id=>{_selectedSource=id;_session.Select(null);Refresh();};
        _hierarchy.ItemsSource = _rows;
        InitializeHierarchyBackground();
        _hierarchy.PointerPressed += (_, args) =>
        {
            if (!args.GetCurrentPoint(_hierarchy).Properties.IsLeftButtonPressed || args.Source is not Visual source) return;
            var ancestors = source.GetVisualAncestors().Prepend(source).ToArray();
            if (ancestors.Any(v => v is ListBoxItem or global::Avalonia.Controls.Primitives.ScrollBar)) return;
            _selectedSource = null;
            Try(() => SelectActive([]));
            _hierarchy.Focusable = true;
            _hierarchy.Focus();
        };
        this.Bind(BackgroundProperty, this.GetResourceObservable("WorkspaceBrush"));
        _hierarchy.SelectionChanged += (_, _) =>
        {
            if (_refreshing || _movingDocument) return;
            _selectedSource = null;
            Try(() => SelectActive(_hierarchy.SelectedItems?.OfType<ElementSnapshot>().Select(e => e.Id).ToArray() ?? []));
        };
        _hierarchy.KeyDown+=(_,args)=>
        {
            if(args.Source is TextBox)return;
            if(args.Key==Key.F2 && _hierarchy.SelectedItem is ElementSnapshot rename)
            {BeginHierarchyRename(rename.Id);args.Handled=true;return;}
            if(_faceActive||_hierarchy.SelectedItem is not ElementSnapshot item)return;
            if(args.Key==Key.Right&&item.Kind=="group"){SetPanelGroupExpanded(item.Id,true);args.Handled=true;}
            else if(args.Key==Key.Left)
            {
                if(item.Kind=="group"&&_expandedPanelGroups.Contains(item.Id))SetPanelGroupExpanded(item.Id,false);
                else if(item.ParentId is not null)_session.Select(item.ParentId);
                args.Handled=true;
            }
        };
        _hierarchy.ItemTemplate = new FuncDataTemplate<ElementSnapshot>((e, _) => PanelHierarchyRow(e));
        _sources.ItemTemplate = new FuncDataTemplate<FaceSourceShapeSnapshot>((s, _) => new TextBlock { Text = "▱  " + s?.Name });
        _sources.SelectionChanged += (_, _) =>
        {
            if (_refreshing || _sources.SelectedItem is not FaceSourceShapeSnapshot shape) return;
            _selectedSource = shape.Id; _session.Select(null); Refresh();
        };
        var hierarchy = new Grid { RowDefinitions = new RowDefinitions("*,Auto,Auto") };
        hierarchy.Children.Add(_hierarchy);
        var sourceLabel = new TextBlock { Text = "FACE SOURCE REGIONS", FontSize = 10, Opacity = .65, Margin = new Thickness(9, 7) };
        Grid.SetRow(sourceLabel, 1); hierarchy.Children.Add(sourceLabel);
        Grid.SetRow(_sources, 2); hierarchy.Children.Add(_sources);

        _assets.ItemTemplate = new FuncDataTemplate<AssetSummary>((a, _) =>
            EditorIcons.Label(EditorIcons.ForComponent(a?.Type), a is null ? "" : $"{a.Type}   /   {a.Name}"));
        _assets.DoubleTapped += (_, _) => { if (_assets.SelectedItem is AssetSummary asset) _ = ScheduleAssetWindowAction(() => OpenAssetFromBrowserAsync(asset)); };
        _assetSearch.TextChanged += (_, _) => RefreshAssets();
        InitializeProjectAssetContextActions();
        var projectPanel = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,*,Auto") };
        projectPanel.Children.Add(_project); Grid.SetRow(_assetSearch, 1); projectPanel.Children.Add(_assetSearch);
        Grid.SetRow(_assets, 2); projectPanel.Children.Add(_assets);
        var creation = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(8), Spacing = 5 };
        var assetName = new TextBox { PlaceholderText = "Asset name", Width = 150 };
        creation.Children.Add(assetName);
        creation.Children.Add(ActionButton("New panel", () => Try(() => { _session.CreatePanel(assetName.Text ?? ""); ShowPanel(); })));
        creation.Children.Add(ActionButton("New Face", () => Try(() => { _faces.CreateFace(assetName.Text ?? ""); ShowFace(); })));
        creation.Children.Add(ActionButton("New Machine", () => Try(() => { _machines.CreateMachine(assetName.Text ?? ""); ShowMachine(); })));
        Grid.SetRow(creation, 3); projectPanel.Children.Add(creation);

        var scene = new Grid { RowDefinitions = new RowDefinitions("Auto,*") };
        var tools = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5, Margin = new Thickness(7, 5) };
        var create = new MenuItem { Header = "Create ▾" };
        foreach (var kind in new[] { "rectangle", "anchor", "zone", "label" })
        {
            var item = new MenuItem { Header = char.ToUpperInvariant(kind[0]) + kind[1..] };
            item.Click += (_, _) => Try(() => { RequirePanel(); session.AddElement(kind, item.Header!.ToString()!, 24, 24, 120, 80); });
            create.Items.Add(item);
        }
        var image = new MenuItem { Header = "Image…" }; image.Click += async (_, _) => await ImportImage(); create.Items.Add(image);
        var source = new MenuItem { Header = "Face source region" };
        source.Click += (_, _) => Try(() => { RequirePanel(); _selectedSource = session.AddSourceShape("Face source region"); session.Select(null); Refresh(); }); create.Items.Add(source);
        var lamp = new MenuItem { Header = "Lamp window" }; lamp.Click += (_, _) => Try(() => { RequireFace(); _faces.AddLamp("Lamp window", 24, 24, 120, 80); }); create.Items.Add(lamp);
        tools.Children.Add(new Menu { ItemsSource = new[] { create } });
        tools.Children.Add(ActionButton("Duplicate", () => Try(() => { RequirePanel(); session.DuplicateSelected(); })));
        tools.Children.Add(ActionButton("Delete", () => Try(DeleteActive)));
        tools.Children.Add(ActionButton("Fit", _viewport.Fit));
        tools.Children.Add(_mode);
        scene.Children.Add(Toolbar(tools)); Grid.SetRow(_viewport, 1); scene.Children.Add(_viewport);
        _document = new Document { Id = "Authoring", Title = "Panel 2D", Context = scene, CanClose = false };
        _editorHierarchy = hierarchy; _editorInspector = new ScrollViewer { Content = _inspector };
        _factory = new WorkspaceDockFactory(hierarchy, projectPanel, _editorInspector, () => _document,_isAssetWindow, _isAssetWindow ? null : (_outputPane = new OutputPaneControl(Output)));
        var layout = _factory.CreateLayout(); _factory.InitLayout(layout);
        _document = (Document)WorkspaceLayoutState.LeafMap(layout)["Authoring"];
        _dock = new DockControl { Layout = layout };

        var root = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,*,Auto") };
        var file = new MenuItem { Header = "_File" };
        MenuAction(file, "New project…", async () => await NewProjectDialog());
        MenuAction(file, "Open project…", async () => await OpenProjectDialog());
        MenuAction(file, "Save", () => Try(SaveActive));
        MenuAction(file, "Import FML layout…", async () => await ImportFml());
        AddFaceWorkflowMenus(file, create);
        AddMachineWorkflowMenus(file, create);
        Initialize3DAuthoring(hierarchy, scene, tools, file);
        InitializeReels(create);
        InitializeLibrary(projectPanel,file);
        InitializeFaceReels(create);
        InitializeFaceComponents(create);
        InitializePanelComponents(create);
        InitializeCompositionGraph(scene,_sceneTools);
        InitializePreview(file);
        InitializeLifecycleMenus(file);
        InitializePreferences(file);
        MenuAction(file,"Face generation settings…",async()=>{if(!_faceActive){SetStatus("Open a Face asset first.");return;}if(TopLevel.GetTopLevel(this) is Window owner && await FaceGenerationSettingsDialog.ShowAsync(owner,Faces)) Refresh();});
        InitializeDocumentTabs(scene,file);
        var edit = new MenuItem { Header = "_Edit" };
        MenuAction(edit, "Undo", () => Try(UndoActive)); MenuAction(edit, "Redo", () => Try(RedoActive));
        MenuAction(edit, "Duplicate", () => Try(() => { RequirePanel(); session.DuplicateSelected(); }));
        MenuAction(edit, "Delete", () => Try(DeleteActive));
        MenuAction(edit, "Bring forward", () => Try(() => { RequirePanel(); session.MoveSelectionInOrder(true); }));
        MenuAction(edit, "Send backward", () => Try(() => { RequirePanel(); session.MoveSelectionInOrder(false); }));
        InitializeClipboardUi(edit, tools);
        var windowMenu = new MenuItem { Header = "_Window" };
        MenuAction(windowMenu, "Save workspace", () => Try(SaveWorkspace));
        MenuAction(windowMenu, "Restore workspace", () => Try(RestoreWorkspace));
        MenuAction(windowMenu, "Reset layout", () => Try(ResetLayout));
        if (!_isAssetWindow) MenuAction(windowMenu, "Output", () => Try(ShowOutputPane));
        foreach (var theme in new[] { "System", "Dark", "Light" }) MenuAction(windowMenu, theme + " theme", () =>
        { Application.Current!.RequestedThemeVariant = theme == "Dark" ? ThemeVariant.Dark : theme == "Light" ? ThemeVariant.Light : ThemeVariant.Default; _viewport.InvalidateVisual(); });
        _menuSlot = new ContentControl { Content = BuildMainMenu(file, edit, windowMenu, create) };
        root.Children.Add(_menuSlot);
        var toolbar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Margin = new Thickness(8, 5) };
        toolbar.Children.Add(ActionButton("New project…", async () => await NewProjectDialog()));
        toolbar.Children.Add(ActionButton("Open project…", async () => await OpenProjectDialog()));
        _save = ActionButton("Save", () => Try(SaveActive)); toolbar.Children.Add(_save);
        _undo = ActionButton("Undo", () => Try(UndoActive)); toolbar.Children.Add(_undo);
        _redo = ActionButton("Redo", () => Try(RedoActive)); toolbar.Children.Add(_redo);
        toolbar.Children.Add(CreatePreviewToolbar());
        toolbar.Children.Add(new TextBlock { Text = "OASIS  /  AUTHORING", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0), FontWeight = FontWeight.SemiBold });
        var bar = Toolbar(toolbar); _editorToolbar = bar; Grid.SetRow(bar, 1); root.Children.Add(bar);
        Grid.SetRow(_dock, 2); root.Children.Add(_dock);
        var status = Toolbar(_status); _editorStatus = status; Grid.SetRow(status, 3); root.Children.Add(status); Content = root;
        if (_isDocumentEditor)
        {
            _editorMenu = (Menu)_menuSlot.Content!; _menuSlot.Content=null;
            root.Children.Remove(bar); root.Children.Remove(status); Content=scene;
        }
        else InitializeNativeDocuments(root);
        KeyDown += (_, e) =>
        {
            if (IsProjectLauncherVisible) return;
            var command = (e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Meta)) != 0;
            if (e.Handled) return;
            if(command && e.Key==Key.S && e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            { _=SaveAllFromUiAsync(); e.Handled=true; return; }
            if (!_isDocumentEditor && !ReferenceEquals(CommandView,this)) { CommandView.HandleDocumentKey(e); return; }
            if (command && e.Key == Key.S) { Try(SaveActive); e.Handled = true; }
            else if (e.Source is not TextBox)
            {
                if (command && e.Key == Key.Z) { Try((e.KeyModifiers & KeyModifiers.Shift) != 0 ? RedoActive : UndoActive); e.Handled = true; }
                else if (command && e.Key == Key.Y) { Try(RedoActive); e.Handled = true; }
                else if (command && e.Key == Key.D) { Try(() => { RequirePanel(); session.DuplicateSelected(); }); e.Handled = true; }
                else if (command && e.Key == Key.A) { Try(() => SelectActive(ActiveElements.Select(e => e.Id).ToArray())); e.Handled = true; }
                else if (e.Key == Key.Delete) { Try(DeleteActive); e.Handled = true; }
            }
        };
        session.Changed += Refresh; _faces.Changed += Refresh; _machines.Changed += Refresh; Refresh();
    }

    internal IReadOnlyList<ElementSnapshot> ActiveElements => _faceActive ? _faces.Elements.Select(ToViewportElement).ToArray() : _session.HierarchyElements;
    internal static ElementSnapshot ToViewportElement(FaceElementSnapshot e) => new(e.Id, e.Kind, e.Name, e.X, e.Y, e.Width, e.Height,
        e.AssetPath, null, e.OnColorHex, e.OffColorHex, null, null, null, e.Visible, e.Locked);
    private IReadOnlyList<string> SelectedIds => _faceActive ? _faces.SelectedId is { } id ? [id] : [] : _session.SelectedIds;
    private void SelectActive(string[] ids)
    { if (_faceActive) _faces.Select(ids.FirstOrDefault()); else _session.SelectElements(ids); }
    private void RequirePanel() { if (_faceActive || _machineActive || Is3DActive) throw new InvalidOperationException("Choose a Panel2D asset for this command."); }
    private void RequireFace() { if (!_faceActive) throw new InvalidOperationException("Choose a Face asset for this command."); }
    private void SaveActive() { if (_sceneKind == "Reel") _reels.Save(); else if (_sceneKind == "Cabinet3D") _cabinets.Save(); else if (_sceneKind == "Prefab") _prefabs.Save(); else if (_machineActive) _machines.Save(); else if (_faceActive) _faces.Save(); else _session.Save(); }
    private void UndoActive() { if (_sceneKind == "Reel") _reels.Undo(); else if (_sceneKind == "Cabinet3D") _cabinets.Undo(); else if (_sceneKind == "Prefab") _prefabs.Undo(); else if (_machineActive) _machines.Undo(); else if (_faceActive) _faces.Undo(); else _session.Undo(); }
    private void RedoActive() { if (_sceneKind == "Reel") _reels.Redo(); else if (_sceneKind == "Cabinet3D") _cabinets.Redo(); else if (_sceneKind == "Prefab") _prefabs.Redo(); else if (_machineActive) _machines.Redo(); else if (_faceActive) _faces.Redo(); else _session.Redo(); }
    private void DeleteActive()
    { if (Is3DActive) DeleteSceneSelection(); else if (_faceActive) _faces.DeleteSelected(); else if (_selectedSource is { } id) { _selectedSource = null; _session.DeleteSourceShape(id); } else _session.DeleteSelected(); }

    public void OpenAsset(AssetSummary asset)
    {
        if(!_isDocumentEditor || _documentOwner is not null) { var host=DocumentHost; host._sceneTask=host.SceneAction(()=>host.OpenNativeAssetAsync(asset.Type=="Object3D"?"Prefab":asset.Type,asset.ManifestPath)); return; }
        if(Path.IsPathRooted(asset.ManifestPath))asset=asset with {ManifestPath=Path.GetRelativePath(_session.RequireProjectDirectory(),Path.GetFullPath(asset.ManifestPath))};
        if (asset.Type == "Reel") { _sceneTask = SceneAction(() => OpenReelAsync(asset.ManifestPath)); return; }
        if (asset.Type == "Cabinet3D") { _sceneTask = SceneAction(() => OpenCabinet3DAsync(asset.ManifestPath)); return; }
        if (asset.Type == "Object3D") { _sceneTask = SceneAction(() => OpenPrefab3DAsync(asset.ManifestPath)); return; }
        if (!_machines.RetainDocuments && _machineActive && _machines.IsDirty && asset.Type != "Machine")
            throw new InvalidOperationException("Save or undo Machine edits before changing the active document.");
        var path = _session.ResolveAsset(asset.ManifestPath);
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (asset.Type == "Panel2D")
        { if (!string.Equals(_session.PanelPath, path, comparison)) _session.OpenPanel(asset.ManifestPath); ShowPanel(); }
        else if (asset.Type == "Face")
        { if (!string.Equals(_faces.FacePath, path, comparison)) _faces.OpenFace(asset.ManifestPath); ShowFace(); }
        else if (asset.Type == "Machine")
        { if (!_machines.IsDirty || !string.Equals(_machines.MachinePath, path, comparison)) _machines.OpenMachine(asset.ManifestPath); ShowMachine(); }
        else { SetStatus($"{asset.Type} / {asset.Name} — this asset type does not have a supported editor."); return; }
        _selectedSource = null; _inspected = null; Refresh();
    }
    public void ShowPanel() { if(!_isDocumentEditor && _nativeDocuments.Count>0){var editor=GetDocumentEditor("Panel2D"); editor.ShowPanel(); FocusEditorIfOwned(editor,"Panel2D");return;} _sceneView.SuspendRendering(); _sceneKind = null; _faceActive = false; _machineActive = false; _sceneCancellation.Cancel(); Refresh(); }
    public void ShowFace() { if(!_isDocumentEditor && _nativeDocuments.Count>0){var editor=GetDocumentEditor("Face"); editor.ShowFace(); FocusEditorIfOwned(editor,"Face");return;} _sceneView.SuspendRendering(); _sceneKind = null; _faceActive = true; _machineActive = false; _selectedSource = null; _sceneCancellation.Cancel(); Refresh(); }
    public void ShowMachine() { if(!_isDocumentEditor && _nativeDocuments.Count>0){var editor=GetDocumentEditor("Machine"); editor.ShowMachine(); FocusEditorIfOwned(editor,"Machine");return;} _sceneKind = "Machine"; _faceActive = false; _machineActive = true; Refresh(); QueueSceneReload(); }
    public void OpenProject(string path)
    { EnsureAssetWindowsIdle(); OpenProjectCore(path); }
    private void OpenProjectCore(string path, bool discardApproved = false)
    { if (HasUnsavedChanges && !discardApproved) throw new InvalidOperationException("Save or undo document edits before changing project."); if(_isAssetWindow)throw new InvalidOperationException("Change projects in the main workspace."); var probe=new AuthoringSession(_session.WorkspacePath); probe.OpenProject(path); DisposeNativeDocuments(); _faces.CloseAllDocuments(); _session.OpenProject(path); Clear3DForProjectChange(); _documentViews.Clear(); _machines.OpenProject(path); _machineActive = false; Refresh(); RecordCurrentProject(); }
    public void CreateProject(string name)
    { EnsureAssetWindowsIdle(); CreateProjectCore(name); }
    private void CreateProjectCore(string name, bool discardApproved = false)
    { if (HasUnsavedChanges && !discardApproved) throw new InvalidOperationException("Save or undo document edits before changing project."); if(_isAssetWindow)throw new InvalidOperationException("Create projects in the main workspace."); var probe=new AuthoringSession(_session.WorkspacePath); var path=probe.CreateProject(name); DisposeNativeDocuments(); _faces.CloseAllDocuments(); _session.OpenProject(path); Clear3DForProjectChange(); _documentViews.Clear(); _machines.OpenProject(_session.ProjectFilePath!); _machineActive = false; Refresh(); RecordCurrentProject(); }
    public void SetStatus(string text) { _status.Text = text; RecordOutput(text, OutputSeverity.Info); }
    private void RecordOutput(string text, OutputSeverity severity)
    {
        Output.Add(_documentOwner is null?text:$"[{ActiveTitle}] {text}", severity);
    }
    public void ShowOutputPane()
    {
        var pane = WorkspaceLayoutState.LeafMap(_dock.Layout!)["Output"];
        if (pane.Owner is IDock owner) owner.ActiveDockable = pane;
    }
    public void Try(Action action)
    {
        try { action(); }
        catch (Exception e) when (e is IOException or InvalidDataException or ArgumentException or InvalidOperationException or KeyNotFoundException or System.Text.Json.JsonException or UnauthorizedAccessException)
        { _status.Text = e.Message; RecordOutput(e.Message, OutputSeverity.Error); }
    }

    private async Task OpenProjectDialog()
    {
        if(_documentOwner is {} host){await host.OpenProjectDialog();return;}
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { CanOpen: true } storage) return;
        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        { Title = "Open Oasis project", FileTypeFilter = [new FilePickerFileType("Oasis project") { Patterns = ["*.oasisproj"] }] });
        if (files.Count > 0 && files[0].TryGetLocalPath() is string path)
            try { await RequestOpenProjectAsync(path); } catch(Exception error) when(error is not OutOfMemoryException){await ShowCloseErrorAsync(error);}
    }
    private async Task NewProjectDialog()
    {
        if(_documentOwner is {} host){await host.NewProjectDialog();return;}
        if (TopLevel.GetTopLevel(this) is not Window owner) return;
        var name = new TextBox { PlaceholderText = "Project name" };
        var message = new TextBlock { Text = "Location: " + _session.WorkspacePath, TextWrapping = TextWrapping.Wrap };
        var panel = new StackPanel { Margin = new Thickness(20), Spacing = 12 };
        panel.Children.Add(message); panel.Children.Add(name);
        var dialog = new Window { Title = "New Oasis project", Width = 440, Height = 220, Content = panel, CanResize = false };
        panel.Children.Add(TextEntryDialog.Actions(dialog, name, ActionButton("Create project", async () =>
        {
            try { if(await RequestCreateProjectAsync(name.Text ?? ""))dialog.Close(); }
            catch (Exception e) when (e is IOException or ArgumentException or InvalidOperationException or UnauthorizedAccessException) { message.Text = e.Message; }
        })));
        await dialog.ShowDialog(owner);
    }
    private async Task ImportImage()
    {
        if (_faceActive) { SetStatus("Select a Panel2D asset to import artwork."); return; }
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { CanOpen: true } storage) return;
        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        { Title = "Import image from workspace", FileTypeFilter = [new FilePickerFileType("Images") { Patterns = ["*.png", "*.jpg", "*.jpeg", "*.bmp", "*.webp"] }] });
        if (files.Count > 0 && files[0].TryGetLocalPath() is string path) Try(() => WithSelectedImportFile(path, p => _session.ImportImage(p)));
    }

    private async Task ImportFml()
    {
        if (_faceActive || _session.PanelPath is null) { SetStatus("Open a Panel2D asset before importing a layout."); return; }
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { CanOpen: true } storage) return;
        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        { Title = "Import FML layout", FileTypeFilter = [new FilePickerFileType("FML layout") { Patterns = ["*.fml"] }] });
        if (files.Count > 0 && files[0].TryGetLocalPath() is string path) Try(() => WithSelectedImportFile(path, p =>
        {
            var result = _session.ImportFml(p);
            SetStatus(result.Succeeded ? $"Imported {result.ImportedElementCount} elements; {result.Warnings.Count} warnings; {result.InputDefinitions.Count} recovered input definitions available for Machine setup."
                : string.Join("; ", result.Errors));
        }));
    }

    // Only a file explicitly chosen in the native picker reaches this adapter. MCP remains workspace-confined.
    private void WithSelectedImportFile(string selectedPath, Action<string> import)
    {
        var full = Path.GetFullPath(selectedPath);
        var relative = Path.GetRelativePath(_session.WorkspacePath, full);
        if (!Path.IsPathRooted(relative) && relative != ".." && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)) { import(full); return; }
        var root = Path.GetDirectoryName(_session.ProjectFilePath ?? throw new InvalidOperationException("Open a project first."))!;
        var stage = WorkspacePathPolicy.ResolveConfined(root, "Generated/ImportSources/" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stage);
        var staged = WorkspacePathPolicy.ResolveConfined(root, Path.GetRelativePath(root, Path.Combine(stage, Path.GetFileName(full))));
        try { File.Copy(full, staged, overwrite: false); import(staged); }
        finally { if (File.Exists(staged)) File.Delete(staged); Directory.Delete(stage, recursive: false); }
    }

    private void RefreshAssets()
    {
        _project.Text=_browseLibrary?"Oasis Library":_session.ProjectName??"Open or create an Oasis project";
        ToolTip.SetTip(_project,_browseLibrary?SceneLibraryRoot:null);
        IReadOnlyList<AssetSummary> assets = _browseLibrary ? LibraryCatalog.Assets.Select(a=>new AssetSummary(a.Type.ToString(),a.DisplayName+" · "+a.Reference.Path,a.Reference.Path)).ToArray() : _session.ProjectFilePath is null ? [] : _session.ListAssets();
        var query = _assetSearch.Text?.Trim() ?? "";
        var filtered = assets.Where(a => (a.Name + " " + a.Type).Contains(query, StringComparison.OrdinalIgnoreCase)).ToArray();
        var selected=(_assets.SelectedItem as AssetSummary)?.ManifestPath;
        if (_assets.ItemsSource is not AssetSummary[] old || !old.SequenceEqual(filtered)) { _assets.ItemsSource = filtered;_assets.SelectedItem=filtered.FirstOrDefault(a=>a.ManifestPath==selected); }
    }
    private void Refresh()
    {
        if(_movingDocument || _disposed)return;
        UpdateCleanupLease();
        RefreshWelcomePage();
        if (_refreshing) return;
        _refreshing = true;
        try
        {
            if (!_faces.IsProjectCurrent) { _faceActive = false; _faces.Close(); }
            RefreshDocumentTabs();
            UpdateSceneModeUI();
            if (Is3DActive) { Refresh3DWorkspaceChrome(); return; }
            var elements = _machineActive ? [] : VisibleHierarchyRows();
            if (!_rows.Select(e => e.Id).SequenceEqual(elements.Select(e => e.Id))) { _rows.Clear(); foreach (var e in elements) _rows.Add(e); }
            else for (var i = 0; i < elements.Count; i++) if (_rows[i] != elements[i]) _rows[i] = elements[i];
            // Keep the ListBox's range anchor and keyboard focus when it originated the selection.
            var selectedRows = _rows.Where(e => SelectedIds.Contains(e.Id)).ToArray();
            if (!_hierarchy.SelectedItems!.OfType<ElementSnapshot>().ToHashSet().SetEquals(selectedRows))
            {
                _hierarchy.SelectedItems.Clear();
                foreach (var e in selectedRows) _hierarchy.SelectedItems.Add(e);
            }
            var shapes = _faceActive ? [] : _session.SourceShapes;
            _sources.ItemsSource = shapes; _sources.SelectedItem = shapes.FirstOrDefault(s => s.Id == _selectedSource);
            RefreshAssets(); _project.Text = _browseLibrary ? "Oasis Library · "+SceneLibraryRoot : _session.ProjectName ?? "Open or create an Oasis project";
            var title = _machineActive ? _machines.MachineTitle : _faceActive ? _faces.FaceTitle : _session.PanelTitle;
            var path = _machineActive ? _machines.MachinePath : _faceActive ? _faces.FacePath : _session.PanelPath;
            var dirty = _machineActive ? _machines.IsDirty : _faceActive ? _faces.IsDirty : _session.IsDirty;
            _mode.Text = _machineActive ? "Machine settings" : _faceActive ? "Artwork + bounds" : "Panel 2D";
            _document.Title = string.IsNullOrEmpty(title) ? "Authoring" : title + (dirty ? " *" : "");
            _save.IsEnabled = dirty; _undo.IsEnabled = _machineActive ? _machines.CanUndo : _faceActive ? _faces.CanUndo : _session.CanUndo; _redo.IsEnabled = _machineActive ? _machines.CanRedo : _faceActive ? _faces.CanRedo : _session.CanRedo;
            if (_machineActive) RefreshMachineInspector(); else RefreshInspector();
            SetStatus(path is null ? "Open or create an Oasis asset."
                : $"{(_machineActive ? "Machine" : _faceActive ? "Face" : "Panel2D")}  •  {(dirty ? "Unsaved changes" : "Saved")}  •  {path}");
            if (_lastDocument != path) { _lastDocument = path; _viewport.ClearImages(); _viewport.RequestFit(); }
            _viewport.InvalidateVisual();
        }
        finally { _refreshing = false; RefreshMenuState(); if(!_isDocumentEditor)QueueAssetDocumentSync(); }
        StateChanged?.Invoke();
    }

    private void RefreshInspector()
    {
        var shape = !_faceActive ? _session.SourceShapes.FirstOrDefault(s => s.Id == _selectedSource) : null;
        var selected = ActiveElements.FirstOrDefault(e => SelectedIds.Contains(e.Id));
        object key = shape is not null ? (object)shape : SelectedIds.Count > 1 ? string.Join("|", SelectedIds)
            : _faceActive ? (_faces.Elements.FirstOrDefault(e => e.Id == selected?.Id),_faces.RenderRevision) : selected?.Kind=="group" ? (_session.Groups.First(g=>g.Id==selected.Id),_session.RenderRevision) : (object?)selected ?? "none";
        if (Equals(_inspected, key)) return;
        _inspected = key; _inspector.Children.Clear(); _fields.Clear();
        if (shape is not null)
        {
            Heading("FACE SOURCE REGION"); Field("Name", shape.Name);
            for (var i = 0; i < 4; i++) { Field($"P{i + 1} X", shape.Points[i].X); Field($"P{i + 1} Y", shape.Points[i].Y); }
            _inspector.Children.Add(ActionButton("Apply region", () => Try(() => _session.UpdateSourceShape(shape.Id, _fields["Name"].Text ?? "",
                Enumerable.Range(1, 4).Select(i => new PointSnapshot(Number($"P{i} X"), Number($"P{i} Y"))).ToArray()))));
            return;
        }
        if (SelectedIds.Count > 1)
        {
            Heading($"{SelectedIds.Count} ELEMENTS"); Field("Move X", 0); Field("Move Y", 0);
            _inspector.Children.Add(ActionButton("Move selection", () => Try(() => _session.MoveSelected(Number("Move X"), Number("Move Y")))));
            return;
        }
        if (selected is null) { if (_faceActive && _faces.FacePath is not null) { AppendFaceBuildInspector(); return; } Heading("Select an element"); _inspector.Children.Add(new TextBlock { Text = "Choose an item in the Hierarchy or viewport, or a Face source region.", TextWrapping = TextWrapping.Wrap }); return; }
        if(!_faceActive&&selected.Kind=="group"){AppendGroupInspector(selected.Id);return;}
        Heading(selected.Kind.ToUpperInvariant());
        Field("Name", selected.Name); Field("X", selected.X); Field("Y", selected.Y); Field("Width", selected.Width); Field("Height", selected.Height);
        var lampIndex = _faceActive ? _faces.Elements.FirstOrDefault(e => e.Id == selected.Id)?.LampId : null;
        if (lampIndex.HasValue) Field("Lamp ID", lampIndex.Value);
        _visible = new CheckBox { Content = "Visible", IsChecked = selected.Visible }; _locked = new CheckBox { Content = "Lock transform", IsChecked = selected.Locked };
        _inspector.Children.Add(_visible); _inspector.Children.Add(_locked);
        _inspector.Children.Add(ActionButton("Apply", () => Try(() =>
        {
            if (_faceActive) _faces.UpdateElement(selected.Id, _fields["Name"].Text, Number("X"), Number("Y"), Number("Width"), Number("Height"), _visible.IsChecked == true, _locked.IsChecked == true,
                _fields.ContainsKey("Lamp ID") ? Integer("Lamp ID") : null);
            else _session.UpdateElement(selected.Id, _fields["Name"].Text, Number("X"), Number("Y"), Number("Width"), Number("Height"), _visible.IsChecked == true, _locked.IsChecked == true);
        })));
        if (selected.AssetPath is not null) _inspector.Children.Add(new TextBlock { Text = selected.AssetPath, TextWrapping = TextWrapping.Wrap, Opacity = .65 });
        if(_faceActive) { AppendFaceReelInspector(selected.Id); AppendFaceComponentInspector(selected.Id); }
        else { AppendPanelComponentInspector(selected.Id); AppendPanelInspectorParity(selected.Id); }
    }
    private void Heading(string text) => _inspector.Children.Add(new TextBlock { Text = text, FontWeight = FontWeight.SemiBold });
    private double Number(string name) => double.TryParse(_fields[name].Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && double.IsFinite(value)
        ? value : throw new ArgumentException($"{name} must be a finite number.");
    private int Integer(string name) => int.TryParse(_fields[name].Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
        ? value : throw new ArgumentException($"{name} must be a whole number.");
    private void Field(string name, object value)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("70,*") };
        row.Children.Add(new TextBlock { Text = name, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap });
        var box = new TextBox { Text = Convert.ToString(value, CultureInfo.InvariantCulture) }; _fields[name] = box;
        Grid.SetColumn(box, 1); row.Children.Add(box); _inspector.Children.Add(row);
    }
    private static Button ActionButton(string text, Action action) { var button = new Button { Content = text }; button.Click += (_, _) => action(); return button; }
    private static void MenuAction(MenuItem parent, string text, Action action) { var item = new MenuItem { Header = text }; item.Click += (_, _) => action(); parent.Items.Add(item); }
    private static Border Toolbar(Control child)
    {
        var border = new Border { Child = child, BorderThickness = new Thickness(0, 0, 0, 1) };
        border.Bind(BackgroundProperty, border.GetResourceObservable("ToolbarBrush")); border.Bind(Border.BorderBrushProperty, border.GetResourceObservable("DividerBrush")); return border;
    }
    public void Dispose()
    {
        if(_disposed)return;_disposed=true;
        _cleanupLease?.Dispose(); _cleanupLease = null;
        DisposeTabPresentation();
        DisposeNativeDocuments();
        DisposeLibrary();
        Dispose3D();
        DisposePreview();
        _session.Changed -= Refresh; _faces.Changed -= Refresh; _machines.Changed -= Refresh;
        if (_dock.Layout is IRootDock root) root.ExitWindows.Execute(null);
        _viewport.Dispose();
        _outputPane?.Dispose(); if(!_isDocumentEditor)Output.Dispose();
    }
}

internal sealed class WorkspaceDockFactory(Control hierarchy, Control project, Control inspector, Func<Document> document,bool assetWindow=false, Control? output=null) : Factory
{
    public event Action<Window>? HostWindowCreated;

    public override void InitDockWindow(IDockWindow window, IDockable owner, IHostWindow host)
    {
        // Wire gestures before the first native input, including restored hosts.
        if (host is Window nativeWindow) HostWindowCreated?.Invoke(nativeWindow);
        base.InitDockWindow(window, owner, host);
        if (host is Window floating && window.Layout is {} layout)
        {
            // A short dock allocation must not become a floating window smaller
            // than its tool group's minimum (which can clip the title offscreen).
            foreach (var pane in WorkspaceLayoutState.LeafMap(layout).Values)
                if (pane.Owner is IToolDock dock && double.IsFinite(dock.MinHeight))
                    floating.MinHeight = Math.Max(floating.MinHeight, dock.MinHeight);
        }
    }

    public override IRootDock CreateLayout()
    {
        var source = document();
        var authoring = new Document { Id = source.Id, Title = source.Title, Context = source.Context, CanClose = false };
        ToolDock Pane(string id, Control content, double proportion)
        {
            var tool = new Tool { Id = id, Title = id, Context = content, CanClose = true };
            return new ToolDock { Proportion = proportion, ActiveDockable = tool, VisibleDockables = CreateList<IDockable>(tool), CanClose = true, CanCloseLastDockable = true };
        }
        ToolDock ProjectPane()
        {
            var projectTool = new Tool { Id = "Project", Title = "Project", Context = project, CanClose = true };
            var dock = new ToolDock { Proportion = .25, MinHeight = 280, ActiveDockable = projectTool, VisibleDockables = CreateList<IDockable>(projectTool), CanClose = true, CanCloseLastDockable = true };
            if (output is not null) dock.VisibleDockables.Add(new Tool { Id = "Output", Title = "Output", Context = output, CanClose = true });
            return dock;
        }
        var documents=new DocumentDock {IsCollapsable=false,ActiveDockable=authoring,VisibleDockables=CreateList<IDockable>(authoring),Proportion=assetWindow ? .52 : .75};
        IDockable center=assetWindow?documents:new ProportionalDock { Orientation = DockOrientation.Vertical, Proportion = .60,
            VisibleDockables = CreateList<IDockable>(documents, new ProportionalDockSplitter(), ProjectPane()) };
        var main = new ProportionalDock { Orientation = DockOrientation.Horizontal,
            VisibleDockables = CreateList<IDockable>(Pane("Hierarchy", hierarchy, .18), new ProportionalDockSplitter(), center, new ProportionalDockSplitter(), Pane("Inspector", inspector, assetWindow ? .30 : .22)) };
        return new RootDock { Id = "Root", ActiveDockable = main, DefaultDockable = main, VisibleDockables = CreateList<IDockable>(main) };
    }
    public override void InitLayout(IDockable layout)
    { HostWindowLocator = new Dictionary<string, Func<IHostWindow?>> { [nameof(IDockWindow)] = () => new HostWindow() }; base.InitLayout(layout); }
}
