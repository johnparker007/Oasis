using System.Security.Cryptography;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Styling;
using Avalonia.Threading;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.Mvvm.Controls;
using OasisEditor.Avalonia.Rendering3D;

namespace OasisEditor.Avalonia;

public sealed partial class WorkspaceView
{
    private string? _workspaceSettingsPath;
    private string? _workspaceSettingsHash;
    public bool CanAutoSaveWorkspace { get; private set; }
    public IReadOnlyList<Window> FloatingWorkspaceWindows => (_dock.Layout as IRootDock)?.Windows?.Select(w => w.Host).OfType<Window>().ToArray() ?? [];
    public WorkspaceState InspectWorkspaceState()
    {
        var projectRoot = _session.ProjectFilePath is null ? null : Path.GetDirectoryName(_session.ProjectFilePath)!;
        string? Relative(string? path) => projectRoot is null || path is null ? null : Path.GetRelativePath(projectRoot, path).Replace('\\', '/');
        string? AssetRelative(string? path) => path is null ? null : IsLibraryScene && ActiveLibraryRoot is { } library ? Path.GetRelativePath(library,path).Replace('\\','/') : Relative(path);
        var window = TopLevel.GetTopLevel(this) as Window;
        var bounds = window is null ? null : new WorkspaceWindowState(window.Position.X, window.Position.Y,
            Math.Max(100, window.ClientSize.Width), Math.Max(100, window.ClientSize.Height), window.WindowState.ToString());
        var theme = Application.Current!.RequestedThemeVariant;
        return new(1, theme == ThemeVariant.Dark ? "Dark" : theme == ThemeVariant.Light ? "Light" : "System",
            WorkspaceLayoutState.Capture(_dock.Layout ?? throw new InvalidOperationException("Workspace has no layout.")),
            _session.ProjectFilePath is null ? null : Path.GetRelativePath(_session.WorkspacePath, _session.ProjectFilePath).Replace('\\', '/'),
            Relative(_session.PanelPath), Relative(Faces.FacePath), _machineActive ? "Machine" : _faceActive ? "Face" : "Panel2D",
            _session.SelectedIds.ToArray(), Faces.SelectedId, _assetSearch.Text, bounds, _selectedSource, _viewport.CaptureViewState(),
            Machines.IsProjectCurrent ? Relative(Machines.MachinePath) : null, NativePreviewPreferences,
            new WorkspaceSceneState(_sceneKind,
                _sceneKind == "Cabinet3D" && Cabinets.IsProjectCurrent ? AssetRelative(Cabinets.FilePath) : null,
                _sceneKind == "Prefab" && Prefabs.IsProjectCurrent ? AssetRelative(Prefabs.FilePath) : null,
                _sceneKind is null ? null : _selectedSceneNodeId,
                new WorkspaceSceneCameraState(SceneView.Camera.Target.X, SceneView.Camera.Target.Y, SceneView.Camera.Target.Z,
                    SceneView.Camera.Distance, SceneView.Camera.Yaw, SceneView.Camera.Pitch, SceneView.Camera.FieldOfView),
                SceneView.ShowVisuals, SceneView.ShowColliders, SceneView.ShowTriggers,
                _sceneKind == "Reel" && Reels.IsProjectCurrent ? AssetRelative(Reels.FilePath) : null,IsLibraryScene?"Library":"Project"),
                string.IsNullOrWhiteSpace(SceneLibraryRoot)?null:SceneLibraryRoot,_browseLibrary,CaptureDocumentList());
    }

    public void ConfigureWorkspacePersistence(string path, bool restore = true)
    {
        var fullPath = Path.GetFullPath(path);
        WorkspacePathPolicy.RejectRootReparsePoints(Path.GetDirectoryName(fullPath)!, requireExists: false);
        VerifySettingsFile(fullPath);
        if (string.Equals(fullPath, _workspaceSettingsPath, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
        {
            if (restore && File.Exists(fullPath)) RestoreWorkspace();
            return;
        }
        _workspaceSettingsPath = fullPath;
        _workspaceSettingsHash = FileHash(_workspaceSettingsPath);
        CanAutoSaveWorkspace = false;
        if (restore && File.Exists(_workspaceSettingsPath)) RestoreWorkspace();
        CanAutoSaveWorkspace = true;
    }

    public void SaveWorkspace()
    {
        var path = _workspaceSettingsPath ?? throw new InvalidOperationException("Choose a workspace settings file first.");
        var parent = Path.GetDirectoryName(path)!;
        WorkspacePathPolicy.RejectRootReparsePoints(parent, requireExists: false);
        if (File.Exists(path) && new FileInfo(path).Attributes.HasFlag(FileAttributes.ReparsePoint)) throw new IOException("Workspace settings cannot be a link.");
        if (FileHash(path) != _workspaceSettingsHash) throw new IOException("Workspace settings changed externally; reload before saving.");
        var json = WorkspaceLayoutState.Serialize(InspectWorkspaceState());
        Directory.CreateDirectory(parent);
        var temp = Path.Combine(parent, ".workspace-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            File.WriteAllText(temp, json, new UTF8Encoding(false));
            File.Move(temp, path, overwrite: true);
            _workspaceSettingsHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
            CanAutoSaveWorkspace = true;
            SetStatus("Workspace saved.");
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

    public void RestoreWorkspace()
    {
        var path = _workspaceSettingsPath ?? throw new InvalidOperationException("Choose a workspace settings file first.");
        if (HasUnsavedChanges) throw new InvalidOperationException("Save or undo all document changes before restoring a workspace.");
        WorkspacePathPolicy.RejectRootReparsePoints(Path.GetDirectoryName(path)!, requireExists: true);
        if (!File.Exists(path)) throw new FileNotFoundException("No workspace settings have been saved.", path);
        if (new FileInfo(path).Attributes.HasFlag(FileAttributes.ReparsePoint)) throw new InvalidDataException("Workspace settings cannot be a link.");
        if (new FileInfo(path).Length > 512 * 1024) throw new InvalidDataException("Workspace settings exceed the size limit.");
        var bytes = File.ReadAllBytes(path);
        var json = Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF');
        var state = WorkspaceLayoutState.Deserialize(json);
        ApplyWorkspaceState(state);
        _workspaceSettingsHash = Convert.ToHexString(SHA256.HashData(bytes));
        CanAutoSaveWorkspace = true;
        SetStatus("Workspace restored.");
    }

    public void ApplyWorkspaceState(WorkspaceState state)
    {
        if (Preview.Backend is not null) throw new InvalidOperationException("Stop preview before restoring a workspace.");
        if (state?.Layout is null) throw new InvalidDataException("Workspace layout is missing.");
        if (HasUnsavedChanges) throw new InvalidOperationException("Save or undo all document changes before restoring a workspace.");
        if (state.Version != 1 || state.Theme is not ("System" or "Dark" or "Light") || state.ActiveDocument is not ("Panel2D" or "Face" or "Machine"))
            throw new InvalidDataException("Unsupported workspace settings version, theme or document type.");
        if (state.MainWindow is not null) WorkspaceLayoutState.ValidateBounds(state.MainWindow);
        state.Viewport?.Validate();
        state.Scene3D?.Validate();
        if(state.LibraryRoot is { } libraryRoot)WorkspacePathPolicy.RejectRootReparsePoints(Path.GetFullPath(libraryRoot),requireExists:state.Scene3D?.Scope=="Library");
        if(state.Scene3D?.Scope=="Library"&&(string.IsNullOrWhiteSpace(state.LibraryRoot)||!Directory.Exists(state.LibraryRoot)))throw new InvalidDataException("The saved Library asset requires its Library folder.");
        var sceneMode = state.Scene3D?.Mode ?? (state.ActiveDocument == "Machine" ? "Machine" : null);
        if (sceneMode == "Machine" && (state.Machine is null || state.ActiveDocument != "Machine"))
            throw new InvalidDataException("A saved Machine scene requires the active Machine document.");
        if ((sceneMode == "Cabinet3D" && state.Scene3D?.Cabinet is null) || (sceneMode == "Prefab" && state.Scene3D?.Prefab is null))
            throw new InvalidDataException("A saved 3D asset scene requires its asset path.");
        if ((sceneMode is "Cabinet3D" or "Prefab" or "Reel") && state.Project is null)
            throw new InvalidDataException("Saved 3D assets require a project.");
        if (state.Emulation?.AudioBufferLengthMilliseconds is < 10 or > 2000) throw new InvalidDataException("Invalid emulation audio buffer.");
        if ((state.AssetSearch?.Length ?? 0) > 256 || (state.PanelSelection?.Length ?? 0) > 10000)
            throw new InvalidDataException("Workspace document state exceeds the limit.");
        var fresh = _factory.CreateLayout();
        var known = WorkspaceLayoutState.LeafMap(fresh);
        var validation = known.ToDictionary(pair => pair.Key, pair => pair.Value is ITool
            ? (IDockable)new Tool { Id = pair.Key } : new Document { Id = pair.Key }, StringComparer.Ordinal);
        _ = WorkspaceLayoutState.Restore(state.Layout, validation);
        // Preflight all saved content before replacing the visible workspace or its clean documents.
        if (state.Project is not null)
        {
            var project = WorkspacePathPolicy.ResolveConfined(_session.WorkspacePath, state.Project, requireExists: true);
            var probe = new AuthoringSession(_session.WorkspacePath); probe.OpenProject(project);
            PreflightDocumentList(state,probe);
            if (state.Panel is not null) probe.OpenPanel(state.Panel);
            if (state.Face is not null) new FaceAuthoringSession(probe).OpenFace(state.Face);
            if (state.Machine is not null) new MachineAuthoringSession(probe).OpenMachine(state.Machine);
            if (sceneMode == "Cabinet3D") {var asset=new CabinetAuthoringSession(probe);if(state.Scene3D!.Scope=="Library")asset.OpenLibrary(state.LibraryRoot!,state.Scene3D.Cabinet!);else asset.Open(state.Scene3D.Cabinet!);}
            if (sceneMode == "Prefab") {var asset=new PrefabAuthoringSession(probe);if(state.Scene3D!.Scope=="Library")asset.OpenLibrary(state.LibraryRoot!,state.Scene3D.Prefab!);else asset.Open(state.Scene3D.Prefab!);}
            if (sceneMode == "Reel") {var asset=new ReelAuthoringSession(probe);if(state.Scene3D!.Scope=="Library")asset.OpenLibrary(state.LibraryRoot!,state.Scene3D.Reel!);else asset.Open(state.Scene3D.Reel!);}
        }
        else if (state.Panel is not null || state.Face is not null || state.Machine is not null || sceneMode is not null || state.Documents?.Length > 0)
            throw new InvalidDataException("Saved documents require a project.");
        if (state.Project is not null)
        {
            Faces.CloseAllDocuments();
            OpenProject(state.Project);
            RestoreDocumentList(state);
            if (state.Machine is not null) Machines.OpenMachine(state.Machine);
            if (state.Panel is not null)
            {
                _session.OpenPanel(state.Panel);
                var available = _session.Elements.Select(e => e.Id).ToHashSet(StringComparer.Ordinal);
                _session.SelectElements((state.PanelSelection ?? []).Where(available.Contains).ToArray());
            }
            if (state.Face is not null)
            {
                Faces.OpenFace(state.Face);
                Faces.Select(Faces.Elements.Any(e => e.Id == state.FaceSelection) ? state.FaceSelection : null);
            }
        }
        else { Faces.CloseAllDocuments(); _session.CloseProject(); Machines.CloseProject(); Clear3DForProjectChange(); }
        SceneLibraryRoot=state.LibraryRoot??string.Empty;RestartLibraryWatcher();_libraryTask=RefreshLibraryAsync();SetAssetBrowserSource(state.LibraryBrowser);
        var restored = WorkspaceLayoutState.Restore(state.Layout, known);
        ReplaceLayout(restored);
        _faceActive = state.ActiveDocument == "Face" && Faces.FacePath is not null;
        _machineActive = state.ActiveDocument == "Machine" && Machines.IsProjectCurrent && Machines.MachinePath is not null;
        ConfigureNativePreview(state.Emulation ?? new());
        _selectedSource = _session.SourceShapes.Any(s => s.Id == state.SourceSelection) ? state.SourceSelection : null;
        _assetSearch.Text = state.AssetSearch;
        Application.Current!.RequestedThemeVariant = state.Theme == "Dark" ? ThemeVariant.Dark : state.Theme == "Light" ? ThemeVariant.Light : ThemeVariant.Default;
        if (state.MainWindow is not null && TopLevel.GetTopLevel(this) is Window window) RestoreWindow(window, state.MainWindow);
        _inspected = null; Refresh();
        if (state.Viewport is not null) _viewport.RestoreViewState(state.Viewport);
        if (state.Scene3D is { } sceneState)
        {
            SceneView.ShowVisuals = sceneState.ShowVisuals;
            SceneView.ShowColliders = sceneState.ShowColliders;
            SceneView.ShowTriggers = sceneState.ShowTriggers;
        }
        if (sceneMode is null)
        {
            if (_faceActive) ShowFace(); else ShowPanel();
            if (state.Scene3D?.Camera is { } camera) ApplySceneCamera(camera);
        }
        else _sceneTask = RestoreScene3DAsync(sceneMode, state.Scene3D);
    }

    private async Task RestoreScene3DAsync(string mode, WorkspaceSceneState? saved)
    {
        if(saved?.Scope=="Library")
        {
            await OpenLibraryAssetAsync(mode=="Cabinet3D"?saved.Cabinet!:mode=="Prefab"?saved.Prefab!:saved.Reel!);
            if(saved.Selection is null || _sceneRows.Any(row=>row.Id==saved.Selection))SelectScene3DNode(saved.Selection);
            if(saved.Camera is { } libraryCamera)ApplySceneCamera(libraryCamera);
            return;
        }
        switch (mode)
        {
            case "Machine":
                ShowMachine();
                await WaitForSceneAsync();
                break;
            case "Cabinet3D":
                await OpenCabinet3DAsync(saved!.Cabinet!);
                break;
            case "Prefab":
                await OpenPrefab3DAsync(saved!.Prefab!);
                break;
            case "Reel":
                await OpenReelAsync(saved!.Reel!);
                break;
            default:
                throw new InvalidDataException("Workspace 3D mode is invalid.");
        }

        if (saved?.Selection is null) SelectScene3DNode(null);
        else if (_sceneRows.Any(row => row.Id == saved.Selection)) SelectScene3DNode(saved.Selection);
        if (saved?.Camera is { } camera) ApplySceneCamera(camera);
    }

    private void ApplySceneCamera(WorkspaceSceneCameraState camera)
    {
        SceneView.SetCamera(new SceneCameraState(
            new((float)camera.TargetX, (float)camera.TargetY, (float)camera.TargetZ),
            (float)camera.Distance, (float)camera.Yaw, (float)camera.Pitch, (float)camera.FieldOfView));
    }

    public void ResetLayout()
    {
        ReplaceLayout(_factory.CreateLayout());
        SetStatus("Default layout restored. Documents and unsaved changes are retained.");
    }

    private void ReplaceLayout(IRootDock layout)
    {
        var contexts = _dock.Layout is null ? new Dictionary<string, object?>()
            : WorkspaceLayoutState.LeafMap(_dock.Layout).ToDictionary(pair => pair.Key, pair => pair.Value.Context);
        if (_dock.Layout is IRootDock previous)
        {
            foreach (var pane in WorkspaceLayoutState.LeafMap(previous).Values) pane.Context = null;
            previous.ExitWindows.Execute(null);
        }
        _dock.Layout = null;
        Dispatcher.UIThread.RunJobs();
        foreach (var pane in WorkspaceLayoutState.LeafMap(layout).Values)
            if (pane.Context is null && contexts.TryGetValue(pane.Id, out var context)) pane.Context = context;
        _factory.InitLayout(layout);
        foreach (var floating in layout.Windows ?? []) _factory.InitDockWindow(floating, layout, new Dock.Avalonia.Controls.HostWindow());
        _dock.Layout = layout;
        _document = (Document)WorkspaceLayoutState.LeafMap(layout)["Authoring"];
        if (TopLevel.GetTopLevel(this) is Window owner)
        {
            foreach (var floating in layout.Windows ?? [])
            {
                var screen = owner.Screens.All.FirstOrDefault(s => s.WorkingArea.Contains(new PixelPoint((int)floating.X, (int)floating.Y))) ?? owner.Screens.Primary;
                if (screen is null) continue;
                var area = screen.WorkingArea;
                floating.X = Math.Clamp(floating.X, area.X, Math.Max(area.X, area.Right - 120));
                floating.Y = Math.Clamp(floating.Y, area.Y, Math.Max(area.Y, area.Bottom - 80));
                floating.Width = Math.Min(floating.Width, Math.Max(200, area.Width / screen.Scaling));
                floating.Height = Math.Min(floating.Height, Math.Max(150, area.Height / screen.Scaling));
            }
        }
        layout.ShowWindows.Execute(null);
    }

    private static void RestoreWindow(Window window, WorkspaceWindowState bounds)
    {
        window.Width = Math.Max(window.MinWidth, bounds.Width); window.Height = Math.Max(window.MinHeight, bounds.Height);
        var x = (int)bounds.X; var y = (int)bounds.Y;
        var screens = window.Screens.All;
        if (screens.Count > 0)
        {
            var screen = screens.FirstOrDefault(s => s.WorkingArea.Contains(new PixelPoint(x, y))) ?? window.Screens.Primary ?? screens[0];
            var area = screen.WorkingArea;
            x = Math.Clamp(x, area.X, Math.Max(area.X, area.Right - 120));
            y = Math.Clamp(y, area.Y, Math.Max(area.Y, area.Bottom - 80));
            window.Width = Math.Min(window.Width, Math.Max(window.MinWidth, area.Width / screen.Scaling));
            window.Height = Math.Min(window.Height, Math.Max(window.MinHeight, area.Height / screen.Scaling));
        }
        window.Position = new PixelPoint(x, y);
        // Reopening a minimized workspace should leave it usable.
        window.WindowState = bounds.State switch { "Maximized" => WindowState.Maximized, "FullScreen" => WindowState.FullScreen, _ => WindowState.Normal };
    }
    private static string? FileHash(string path) => File.Exists(path) ? Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))) : null;
    private static void VerifySettingsFile(string path)
    {
        var file = new FileInfo(path);
        if (file.LinkTarget is not null || (file.Exists && file.Attributes.HasFlag(FileAttributes.ReparsePoint))) throw new InvalidDataException("Workspace settings cannot be a link.");
        if (file.Exists && file.Length > 512 * 1024) throw new InvalidDataException("Workspace settings exceed the size limit.");
    }
}
