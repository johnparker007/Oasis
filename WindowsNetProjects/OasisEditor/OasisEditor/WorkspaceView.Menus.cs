using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Controls.Templates;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;

namespace OasisEditor.Avalonia;

public sealed record EditorMenuEntry(string Header, bool Enabled, bool Checked, string? Gesture, EditorMenuEntry[] Items);

public sealed partial class WorkspaceView
{
    private Menu? _mainMenu;
    private readonly Dictionary<MenuItem, Func<bool>> _menuConditions = [];
    private MenuItem? _pauseMenu, _unthrottledMenu;
    private bool HasPanelSelection => !Is3DActive && !_faceActive && _session.PanelPath is not null && _session.SelectedIds.Count > 0;

    // The feature initializers register existing actions before the menu tree is composed.
    // Move those same controls (and their handlers) into the recovered WPF hierarchy.
    private Menu BuildMainMenu(MenuItem registrations, MenuItem editRegistrations, MenuItem windowRegistrations, MenuItem elementCreate)
    {
        MenuItem Take(MenuItem source, string label, string? header = null, Func<bool>? enabled = null)
        {
            var item = source.Items.OfType<MenuItem>().Single(m => Equals(m.Header, label));
            source.Items.Remove(item);
            item.Header = header ?? label;
            if (enabled is not null) _menuConditions[item] = enabled;
            return item;
        }
        MenuItem Group(string header, params object[] items)
        {
            var group = new MenuItem { Header = header };
            foreach (var item in items) group.Items.Add(item);
            group.SubmenuOpened += (_, _) => RefreshMenuState();
            return group;
        }
        MenuItem Action(string header, Action execute, Func<bool>? enabled = null, string? gesture = null)
        {
            var item = new MenuItem { Header = header };
            item.Click += (_, e) => { e.Handled = true; if (item.IsEnabled) execute(); };
            if (enabled is not null) _menuConditions[item] = enabled;
            if (gesture is not null) item.InputGesture = KeyGesture.Parse(gesture);
            return item;
        }
        bool Project() => _session.ProjectFilePath is not null;
        bool MainProject() => Project();
        bool Face() => _faceActive && _faces.Document is not null;
        bool Panel() => !Is3DActive && !_faceActive && _session.PanelPath is not null;
        bool Machine() => _machineActive && _machines.Document is not null;

        var assetCreate = Group("_Create");
        foreach (var kind in new[] { "Panel2D", "Face", "Cabinet3D", "Machine", "Reel", "Prefab" })
            assetCreate.Items.Add(Action(kind, async () => await CreateAssetFromMenuAsync(kind), MainProject));
        var import = Group("_Import",
            Take(registrations, "Import FML layout…", "MFME ._FML…", Panel),
            Take(registrations, "Import Cabinet GLB…", "_GLB Model…", MainProject));
        var file = Group("_File",
            Take(registrations, "New project…"),
            Take(registrations, "Open project…"),
            assetCreate, new Separator(), import,
            Group("_Open asset", Take(registrations, "Open Machine…", "_Machine…", MainProject)),
            new Separator(),
            Take(registrations, "Build Player machine…", "Build Oasis _Player Machine", Machine),
            Take(registrations, "Launch in Oasis Player…", "Preview in Oasis _Player", Machine),
            Group("_Export", Take(registrations, "Export portable Player package…", enabled: Machine)),
            new Separator(),
            Take(registrations, "Save", "_Save Asset", () => _save.IsEnabled),
            Take(registrations, "Save All", "Save _All", enabled: Project),
            Take(registrations, "Close asset", enabled: () => ActiveDocumentPath is not null),
            Take(registrations, "Close project", "_Close Project", MainProject),
            Action("Delete unreferenced assets…", async () => await ShowUnreferencedAssetsAsync(), MainProject),
            new Separator(), Action("E_xit", async () => await ExitFromMenuAsync()));
        file.Items.OfType<MenuItem>().Single(item=>Equals(item.Header,"Save _All")).InputGesture=KeyGesture.Parse("Ctrl+Shift+S");
        // Asset creation is now reachable through File > Create rather than a mixed File list.
        registrations.Items.Remove(registrations.Items.OfType<MenuItem>().Single(m => Equals(m.Header, "Create Machine…")));
        registrations.Items.Remove(registrations.Items.OfType<MenuItem>().Single(m => Equals(m.Header, "Create Prefab…")));

        var face = Group("_Face",
            Action("Add Panel2D Face Source _Shape", () => Try(() => { RequirePanel(); _selectedSource = _session.AddSourceShape("Face source region"); _session.Select(null); Refresh(); }), Panel),
            Action("Create Face from Selected Panel2D Source _Shape", async () => await GenerateFaceFromSelectedShape(), () => Panel() && _selectedSource is not null),
            Take(registrations, "Validate Face", "_Validate Face", Face), new Separator(),
            Group("_Artwork",
                Take(registrations, "Import Face artwork…", enabled: Face),
                Take(registrations, "Artwork registration and processing…", enabled: Face),
                Take(registrations, "Use Panel2D artwork source", enabled: Face),
                Take(registrations, "Rebuild Face artwork", enabled: Face),
                Group("_Override", new[] { "Import artwork override…", "Create artwork override from Base", "Enable artwork override", "Disable artwork override", "Remove artwork override" }
                    .Select(label => (object)Take(registrations, label, enabled: Face)).ToArray())),
            Group("_Illumination", Take(registrations, "Import Face lamp mask…", enabled: Face), Take(registrations, "Use Panel2D lamp mask", enabled: Face)),
            Group("_Build", Take(registrations, "Build Face", enabled: Face), Take(registrations, "Build Face runtime assets", enabled: Face), Take(registrations, "Rebuild Face components from source…", enabled: Face)),
            Take(registrations, "Open source Panel2D", enabled: Face),
            Take(registrations, "Face generation settings…", enabled: Face));

        var edit = Group("_Edit");
        foreach (var label in new[] { "Undo", "Redo" })
        {
            var item = Take(editRegistrations, label, enabled: () => label == "Undo" ? _undo.IsEnabled : _redo.IsEnabled);
            item.InputGesture = KeyGesture.Parse(label == "Undo" ? "Ctrl+Z" : "Ctrl+Shift+Z"); edit.Items.Add(item);
        }
        edit.Items.Add(new Separator());
        foreach (var (label, gesture) in new[] { ("Cut", "Ctrl+X"), ("Copy", "Ctrl+C"), ("Paste", "Ctrl+V"), ("Duplicate", "Ctrl+D"), ("Delete", "Delete") })
        {
            var item = Take(editRegistrations, label, enabled: () => label == "Paste" ? Panel() && _session.CanPasteClipboard : label == "Delete" ? SelectedIds.Count > 0 && !Is3DActive : HasPanelSelection);
            item.InputGesture = KeyGesture.Parse(gesture); edit.Items.Add(item);
        }
        edit.Items.Add(new Separator());
        edit.Items.Add(Group("_Arrange", new[] { "Bring to front", "Bring forward", "Send backward", "Send to back" }
            .Select(label => (object)Take(editRegistrations, label, enabled: () => HasPanelSelection)).ToArray()));
        edit.Items.Add(Group("_Object",
            Action("_Lock Transform", () => Try(() => SetSelectionObjectState(locked: true)), () => !Is3DActive && SelectedIds.Count > 0),
            Action("_Unlock Transform", () => Try(() => SetSelectionObjectState(locked: false)), () => !Is3DActive && SelectedIds.Count > 0), new Separator(),
            Action("_Hide", () => Try(() => SetSelectionObjectState(visible: false)), () => !Is3DActive && SelectedIds.Count > 0),
            Action("_Show", () => Try(() => SetSelectionObjectState(visible: true)), () => !Is3DActive && SelectedIds.Count > 0)));
        edit.Items.Add(new Separator());
        edit.Items.Add(Action("Create Group",()=>Try(()=>CreatePanelGroupFromUi(false)),Panel));
        edit.Items.Add(Action("Group Selection",()=>Try(()=>CreatePanelGroupFromUi()),()=>HasPanelSelection));
        edit.Items.Add(Action("Ungroup",()=>Try(_session.UngroupSelection),()=>Panel()&&_session.Groups.Any(g=>SelectedIds.Contains(g.Id)&&g.Control is null)));
        edit.Items.Add(new Separator());
        edit.Items.Add(Take(registrations, "Preferences…", "_Preferences"));
        edit.Items.Add(Action("_Project Settings", async () => await ShowProjectSettingsFromMenuAsync(), MainProject));

        var window = Group("_Window");
        foreach (var pane in new[] { "Assets", "Hierarchy", "Inspector", "Output" })
            window.Items.Add(Action("_" + pane, () => Try(() => ShowWorkspacePane(pane))));
        window.Items.Add(Action("_Play View", ShowPlayViewFromMenu, Machine));
        window.Items.Add(Action("_Input Map", ShowInputMapFromMenu, Machine));
        window.Items.Add(new Separator());
        window.Items.Add(Take(registrations, "Float active asset", enabled: () => ActiveDocumentPath is not null));
        foreach (var label in new[] { "Save workspace", "Restore workspace", "Reset layout" })
        {
            var item = windowRegistrations.Items.OfType<MenuItem>().FirstOrDefault(m => Equals(m.Header, label));
            if (item is not null) { windowRegistrations.Items.Remove(item); window.Items.Add(item); }
        }
        var themes = Group("_Theme");
        foreach (var item in windowRegistrations.Items.OfType<MenuItem>().Where(m => m.Header?.ToString()?.EndsWith(" theme") == true).ToArray())
        { windowRegistrations.Items.Remove(item); themes.Items.Add(item); }
        window.Items.Add(themes);
        window.Items.Add(Group("Oasis _Library", Take(registrations, "Set Oasis Library folder…", enabled: MainProject), Take(registrations, "Refresh Oasis Library", enabled: MainProject)));

        var preview = Take(registrations, "Emulation preview");
        var emulation = Group("_Emulation", Take(preview, "Play", "_Start", () => Machine() && _machines.Document!.Runtime is EmulationRuntimeDefinition && Preview.State is EmulationBackendState.Stopped or EmulationBackendState.Failed), Take(preview, "Stop", "_Stop", () => Preview.Backend is not null));
        _pauseMenu = Action("_Pause", async () => await PreviewAction(() => Preview.ControlAsync(Preview.State == EmulationBackendState.Paused ? "resume" : "pause")), () => Preview.State is EmulationBackendState.Running or EmulationBackendState.Paused);
        _unthrottledMenu = Action("_Unthrottled", async () => await PreviewAction(() => Preview.ControlAsync(Preview.Backend?.IsThrottleEnabled == true ? "unthrottle" : "throttle")), () => Preview.Backend is not null);
        _pauseMenu.ToggleType = _unthrottledMenu.ToggleType = MenuItemToggleType.CheckBox;
        emulation.Items.Add(_pauseMenu); emulation.Items.Add(_unthrottledMenu); emulation.Items.Add(new Separator());
        emulation.Items.Add(Take(preview, "Reset", "_Reset", () => Preview.Backend is not null));
        emulation.Items.Add(new Separator()); emulation.Items.Add(Take(preview, "Configure native libraries…", enabled: () => Preview.Backend is null));
        if (registrations.Items.Count != 0 || editRegistrations.Items.Count != 0)
            throw new InvalidOperationException("A registered Editor action has no menu location. Add it to the main menu hierarchy.");
        // Window > Input Map retains authored diagnostics even before native preview starts.
        _mainMenu = new Menu { ItemsSource = new[] { file, face, edit, window, emulation } };
        return _mainMenu;
    }

    private void RefreshMenuState()
    {
        if (_mainMenu is null) return;
        foreach (var (item, enabled) in _menuConditions) item.IsEnabled = enabled();
        if (_pauseMenu is not null) _pauseMenu.IsChecked = Preview.State == EmulationBackendState.Paused;
        if (_unthrottledMenu is not null) _unthrottledMenu.IsChecked = Preview.Backend?.IsThrottleEnabled == false;
    }

    public EditorMenuEntry[] InspectMenus()
    {
        if(!ReferenceEquals(CommandView,this))return CommandView.InspectMenus();
        RefreshMenuState();
        EditorMenuEntry Entry(object? value) => value is MenuItem item
            ? new(item.Header?.ToString()?.Replace("_", "") ?? "", item.IsEnabled, item.IsChecked, item.InputGesture?.ToString(), item.Items.Select(Entry).ToArray())
            : new("---", false, false, null, []);
        return _mainMenu!.Items.Select(Entry).ToArray();
    }

    internal MenuItem FindMenu(params string[] path)
    {
        if(!ReferenceEquals(CommandView,this))return CommandView.FindMenu(path);
        IEnumerable<object?> items = _mainMenu!.Items;
        MenuItem? found = null;
        foreach (var header in path)
        {
            found = items.OfType<MenuItem>().Single(m => m.Header?.ToString()?.Replace("_", "") == header);
            items = found.Items;
        }
        return found ?? throw new ArgumentException("Choose a menu path.", nameof(path));
    }

    internal void OpenMenuForCapture(string[] path)
    {
        if (!Program.IsHeadless) throw new InvalidOperationException("Menu capture requires the headless host.");
        RefreshMenuState();
        for (var index = 1; index <= path.Length; index++)
        {
            var item = FindMenu(path.Take(index).ToArray());
            if (item.Items.Count == 0) throw new ArgumentException("Choose a submenu, not an action.");
            item.ApplyTemplate();
            foreach (var popup in item.GetTemplateChildren().OfType<Popup>()) popup.ShouldUseOverlayLayer = true;
            item.IsSubMenuOpen = true;
            UpdateLayout(); Dispatcher.UIThread.RunJobs();
        }
    }

    internal void CloseMenus()
    {
        if(!ReferenceEquals(CommandView,this)){CommandView.CloseMenus();return;}
        foreach (var root in _mainMenu!.Items.OfType<MenuItem>()) root.IsSubMenuOpen = false;
    }
}
