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
    public FaceAuthoringSession Faces { get; }
    public MachineAuthoringSession Machines { get; }
    private bool _faceActive;
    private bool _machineActive;
    public bool HasUnsavedChanges => _session.IsDirty || Faces.IsDirty || Machines.IsDirty;
    public string ActiveTitle => _machineActive ? Machines.MachineTitle : _faceActive ? Faces.FaceTitle : _session.PanelTitle;
    public event Action? StateChanged;
    private readonly PanelViewport _viewport;
    private readonly ListBox _hierarchy = new() { SelectionMode = SelectionMode.Multiple | SelectionMode.Toggle };
    private readonly ListBox _sources = new() { MaxHeight = 125 };
    private readonly ObservableCollection<ElementSnapshot> _rows = [];
    private readonly ListBox _assets = new();
    private readonly TextBox _assetSearch = new() { PlaceholderText = "Search assets", Margin = new Thickness(8, 4) };
    private readonly StackPanel _inspector = new() { Margin = new Thickness(12), Spacing = 9 };
    private readonly TextBlock _status = new() { Margin = new Thickness(10, 5), TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly TextBlock _project = new() { Margin = new Thickness(10, 7), FontWeight = FontWeight.SemiBold };
    private readonly TextBlock _mode = new() { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0), FontSize = 11, Opacity = .65 };
    private readonly Button _undo, _redo, _save;
    private readonly Document _document;
    private readonly Dictionary<string, TextBox> _fields = [];
    private CheckBox? _visible, _locked;
    private bool _refreshing;
    private string? _lastDocument, _selectedSource;
    private object? _inspected;

    partial void AddMachineWorkflowMenus(MenuItem fileMenu, MenuItem createMenu);

    public WorkspaceView(AuthoringSession session, FaceAuthoringSession? faces = null)
    {
        _session = session; Faces = faces ?? new FaceAuthoringSession(session); Machines = new MachineAuthoringSession(session);
        _viewport = new PanelViewport(session, Faces, () => _faceActive);
        _viewport.CommandFailed += SetStatus;
        _hierarchy.ItemsSource = _rows;
        this.Bind(BackgroundProperty, this.GetResourceObservable("WorkspaceBrush"));
        _hierarchy.SelectionChanged += (_, _) =>
        {
            if (_refreshing) return;
            _selectedSource = null;
            Try(() => SelectActive(_hierarchy.SelectedItems?.OfType<ElementSnapshot>().Select(e => e.Id).ToArray() ?? []));
        };
        _hierarchy.ItemTemplate = new FuncDataTemplate<ElementSnapshot>((e, _) => new TextBlock
        { Text = e is null ? "" : $"{(e.Locked ? "▣" : "◇")}  {e.Name}", Opacity = e?.Visible == false ? .45 : 1, TextTrimming = TextTrimming.CharacterEllipsis });
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

        _assets.ItemTemplate = new FuncDataTemplate<AssetSummary>((a, _) => new TextBlock
        { Text = a is null ? "" : $"{a.Type}   /   {a.Name}", TextTrimming = TextTrimming.CharacterEllipsis });
        _assets.DoubleTapped += (_, _) => { if (_assets.SelectedItem is AssetSummary asset) Try(() => OpenAsset(asset)); };
        _assetSearch.TextChanged += (_, _) => RefreshAssets();
        var projectPanel = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,*,Auto") };
        projectPanel.Children.Add(_project); Grid.SetRow(_assetSearch, 1); projectPanel.Children.Add(_assetSearch);
        Grid.SetRow(_assets, 2); projectPanel.Children.Add(_assets);
        var creation = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(8), Spacing = 5 };
        var assetName = new TextBox { PlaceholderText = "Asset name", Width = 150 };
        creation.Children.Add(assetName);
        creation.Children.Add(ActionButton("New panel", () => Try(() => { _session.CreatePanel(assetName.Text ?? ""); _faceActive = false; Refresh(); })));
        creation.Children.Add(ActionButton("New Face", () => Try(() => { Faces.CreateFace(assetName.Text ?? ""); _faceActive = true; Refresh(); })));
        creation.Children.Add(ActionButton("New Machine", () => Try(() => { Machines.CreateMachine(assetName.Text ?? ""); _faceActive = false; _machineActive = true; Refresh(); })));
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
        var lamp = new MenuItem { Header = "Lamp window" }; lamp.Click += (_, _) => Try(() => { RequireFace(); Faces.AddLamp("Lamp window", 24, 24, 120, 80); }); create.Items.Add(lamp);
        tools.Children.Add(new Menu { ItemsSource = new[] { create } });
        tools.Children.Add(ActionButton("Duplicate", () => Try(() => { RequirePanel(); session.DuplicateSelected(); })));
        tools.Children.Add(ActionButton("Delete", () => Try(DeleteActive)));
        tools.Children.Add(ActionButton("Fit", _viewport.Fit));
        tools.Children.Add(_mode);
        scene.Children.Add(Toolbar(tools)); Grid.SetRow(_viewport, 1); scene.Children.Add(_viewport);
        _document = new Document { Id = "Authoring", Title = "Panel 2D", Context = scene, CanClose = false };
        var factory = new WorkspaceDockFactory(hierarchy, projectPanel, new ScrollViewer { Content = _inspector }, _document);
        var layout = factory.CreateLayout(); factory.InitLayout(layout);
        var dock = new DockControl { Layout = layout };

        var root = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,*,Auto") };
        var file = new MenuItem { Header = "_File" };
        MenuAction(file, "New project…", async () => await NewProjectDialog());
        MenuAction(file, "Open project…", async () => await OpenProjectDialog());
        MenuAction(file, "Save", () => Try(SaveActive));
        MenuAction(file, "Import FML layout…", async () => await ImportFml());
        AddMachineWorkflowMenus(file, create);
        var edit = new MenuItem { Header = "_Edit" };
        MenuAction(edit, "Undo", () => Try(UndoActive)); MenuAction(edit, "Redo", () => Try(RedoActive));
        MenuAction(edit, "Duplicate", () => Try(() => { RequirePanel(); session.DuplicateSelected(); }));
        MenuAction(edit, "Delete", () => Try(DeleteActive));
        MenuAction(edit, "Bring forward", () => Try(() => { RequirePanel(); session.MoveSelectionInOrder(true); }));
        MenuAction(edit, "Send backward", () => Try(() => { RequirePanel(); session.MoveSelectionInOrder(false); }));
        var windowMenu = new MenuItem { Header = "_Window" };
        foreach (var theme in new[] { "System", "Dark", "Light" }) MenuAction(windowMenu, theme + " theme", () =>
        { Application.Current!.RequestedThemeVariant = theme == "Dark" ? ThemeVariant.Dark : theme == "Light" ? ThemeVariant.Light : ThemeVariant.Default; _viewport.InvalidateVisual(); });
        root.Children.Add(new Menu { ItemsSource = new[] { file, edit, windowMenu } });
        var toolbar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Margin = new Thickness(8, 5) };
        toolbar.Children.Add(ActionButton("New project…", async () => await NewProjectDialog()));
        toolbar.Children.Add(ActionButton("Open project…", async () => await OpenProjectDialog()));
        _save = ActionButton("Save", () => Try(SaveActive)); toolbar.Children.Add(_save);
        _undo = ActionButton("Undo", () => Try(UndoActive)); toolbar.Children.Add(_undo);
        _redo = ActionButton("Redo", () => Try(RedoActive)); toolbar.Children.Add(_redo);
        toolbar.Children.Add(new TextBlock { Text = "OASIS  /  AUTHORING", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0), FontWeight = FontWeight.SemiBold });
        var bar = Toolbar(toolbar); Grid.SetRow(bar, 1); root.Children.Add(bar);
        Grid.SetRow(dock, 2); root.Children.Add(dock);
        var status = Toolbar(_status); Grid.SetRow(status, 3); root.Children.Add(status); Content = root;
        KeyDown += (_, e) =>
        {
            var command = (e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Meta)) != 0;
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
        session.Changed += Refresh; Faces.Changed += Refresh; Machines.Changed += Refresh; Refresh();
    }

    internal IReadOnlyList<ElementSnapshot> ActiveElements => _faceActive ? Faces.Elements.Select(ToViewportElement).ToArray() : _session.Elements;
    internal static ElementSnapshot ToViewportElement(FaceElementSnapshot e) => new(e.Id, e.Kind, e.Name, e.X, e.Y, e.Width, e.Height,
        e.AssetPath, null, e.OnColorHex, e.OffColorHex, null, null, null, e.Visible, e.Locked);
    private IReadOnlyList<string> SelectedIds => _faceActive ? Faces.SelectedId is { } id ? [id] : [] : _session.SelectedIds;
    private void SelectActive(string[] ids)
    { if (_faceActive) Faces.Select(ids.FirstOrDefault()); else _session.SelectElements(ids); }
    private void RequirePanel() { if (_faceActive) throw new InvalidOperationException("Choose a Panel2D asset for this command."); }
    private void RequireFace() { if (!_faceActive) throw new InvalidOperationException("Choose a Face asset for this command."); }
    private void SaveActive() { if (_machineActive) Machines.Save(); else if (_faceActive) Faces.Save(); else _session.Save(); }
    private void UndoActive() { if (_machineActive) Machines.Undo(); else if (_faceActive) Faces.Undo(); else _session.Undo(); }
    private void RedoActive() { if (_machineActive) Machines.Redo(); else if (_faceActive) Faces.Redo(); else _session.Redo(); }
    private void DeleteActive()
    { if (_faceActive) Faces.DeleteSelected(); else if (_selectedSource is { } id) { _selectedSource = null; _session.DeleteSourceShape(id); } else _session.DeleteSelected(); }

    public void OpenAsset(AssetSummary asset)
    {
        if (_machineActive && Machines.IsDirty && asset.Type != "Machine")
            throw new InvalidOperationException("Save or undo Machine edits before changing the active document.");
        var path = _session.ResolveAsset(asset.ManifestPath);
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (asset.Type == "Panel2D")
        { if (!string.Equals(_session.PanelPath, path, comparison)) _session.OpenPanel(asset.ManifestPath); _faceActive = false; _machineActive = false; }
        else if (asset.Type == "Face")
        { if (!string.Equals(Faces.FacePath, path, comparison)) Faces.OpenFace(asset.ManifestPath); _faceActive = true; _machineActive = false; }
        else if (asset.Type == "Machine")
        { if (!string.Equals(Machines.MachinePath, path, comparison)) Machines.OpenMachine(asset.ManifestPath); _faceActive = false; _machineActive = true; }
        else { SetStatus($"{asset.Type} / {asset.Name} — authoring for this asset type is still available in the WPF Editor."); return; }
        _selectedSource = null; _inspected = null; Refresh();
    }
    public void ShowPanel() { _faceActive = false; _machineActive = false; Refresh(); }
    public void ShowFace() { _faceActive = true; _machineActive = false; _selectedSource = null; Refresh(); }
    public void ShowMachine() { _faceActive = false; _machineActive = true; Refresh(); }
    public void OpenProject(string path)
    { if (Faces.IsDirty || Machines.IsDirty) throw new InvalidOperationException("Save or undo document edits before changing project."); _session.OpenProject(path); Machines.OpenProject(path); _machineActive = false; }
    public void CreateProject(string name)
    { if (Faces.IsDirty || Machines.IsDirty) throw new InvalidOperationException("Save or undo document edits before changing project."); _session.CreateProject(name); Machines.OpenProject(_session.ProjectFilePath!); _machineActive = false; }
    public void SetStatus(string text) => _status.Text = text;
    public void Try(Action action)
    {
        try { action(); }
        catch (Exception e) when (e is IOException or ArgumentException or InvalidOperationException or KeyNotFoundException or System.Text.Json.JsonException or UnauthorizedAccessException)
        { SetStatus(e.Message); }
    }

    private async Task OpenProjectDialog()
    {
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { CanOpen: true } storage) return;
        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        { Title = "Open Oasis project", FileTypeFilter = [new FilePickerFileType("Oasis project") { Patterns = ["*.oasisproj"] }] });
        if (files.Count > 0 && files[0].TryGetLocalPath() is string path) Try(() => OpenProject(path));
    }
    private async Task NewProjectDialog()
    {
        if (TopLevel.GetTopLevel(this) is not Window owner) return;
        var name = new TextBox { PlaceholderText = "Project name" };
        var message = new TextBlock { Text = "Location: " + _session.WorkspacePath, TextWrapping = TextWrapping.Wrap };
        var panel = new StackPanel { Margin = new Thickness(20), Spacing = 12 };
        panel.Children.Add(message); panel.Children.Add(name);
        var dialog = new Window { Title = "New Oasis project", Width = 440, Height = 220, Content = panel, CanResize = false };
        panel.Children.Add(ActionButton("Create project", () =>
        {
            try { CreateProject(name.Text ?? ""); dialog.Close(); }
            catch (Exception e) when (e is IOException or ArgumentException or InvalidOperationException or UnauthorizedAccessException) { message.Text = e.Message; }
        }));
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
        var assets = _session.ProjectFilePath is null ? [] : _session.ListAssets();
        var query = _assetSearch.Text?.Trim() ?? "";
        var filtered = assets.Where(a => (a.Name + " " + a.Type).Contains(query, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (_assets.ItemsSource is not AssetSummary[] old || !old.SequenceEqual(filtered)) _assets.ItemsSource = filtered;
    }
    private void Refresh()
    {
        if (_refreshing) return;
        _refreshing = true;
        try
        {
            if (!Faces.IsProjectCurrent) { _faceActive = false; Faces.Close(); }
            var elements = _machineActive ? [] : ActiveElements;
            if (!_rows.Select(e => e.Id).SequenceEqual(elements.Select(e => e.Id))) { _rows.Clear(); foreach (var e in elements) _rows.Add(e); }
            else for (var i = 0; i < elements.Count; i++) if (_rows[i] != elements[i]) _rows[i] = elements[i];
            _hierarchy.SelectedItems?.Clear();
            foreach (var e in _rows.Where(e => SelectedIds.Contains(e.Id))) _hierarchy.SelectedItems?.Add(e);
            var shapes = _faceActive ? [] : _session.SourceShapes;
            _sources.ItemsSource = shapes; _sources.SelectedItem = shapes.FirstOrDefault(s => s.Id == _selectedSource);
            RefreshAssets(); _project.Text = _session.ProjectName ?? "Open or create an Oasis project";
            var title = _machineActive ? Machines.MachineTitle : _faceActive ? Faces.FaceTitle : _session.PanelTitle;
            var path = _machineActive ? Machines.MachinePath : _faceActive ? Faces.FacePath : _session.PanelPath;
            var dirty = _machineActive ? Machines.IsDirty : _faceActive ? Faces.IsDirty : _session.IsDirty;
            _mode.Text = _machineActive ? "Machine settings" : _faceActive ? "Artwork + bounds" : "Panel 2D";
            _document.Title = string.IsNullOrEmpty(title) ? "Authoring" : title + (dirty ? " *" : "");
            _save.IsEnabled = dirty; _undo.IsEnabled = _machineActive ? Machines.CanUndo : _faceActive ? Faces.CanUndo : _session.CanUndo; _redo.IsEnabled = _machineActive ? Machines.CanRedo : _faceActive ? Faces.CanRedo : _session.CanRedo;
            if (_machineActive) RefreshMachineInspector(); else RefreshInspector();
            SetStatus(path is null ? "Open or create a Panel2D or Face asset.  •  3D authoring is deferred."
                : $"{(_machineActive ? "Machine" : _faceActive ? "Face" : "Panel2D")}  •  {(dirty ? "Unsaved changes" : "Saved")}  •  {path}");
            if (_lastDocument != path) { _lastDocument = path; _viewport.ClearImages(); _viewport.RequestFit(); }
            _viewport.InvalidateVisual();
        }
        finally { _refreshing = false; }
        StateChanged?.Invoke();
    }

    private void RefreshInspector()
    {
        var shape = !_faceActive ? _session.SourceShapes.FirstOrDefault(s => s.Id == _selectedSource) : null;
        var selected = ActiveElements.FirstOrDefault(e => SelectedIds.Contains(e.Id));
        object key = shape is not null ? (object)shape : SelectedIds.Count > 1 ? string.Join("|", SelectedIds)
            : _faceActive ? (object?)Faces.Elements.FirstOrDefault(e => e.Id == selected?.Id) ?? "none" : (object?)selected ?? "none";
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
        if (selected is null) { Heading("Select an element"); _inspector.Children.Add(new TextBlock { Text = "Choose an item in the Hierarchy or viewport, or a Face source region.", TextWrapping = TextWrapping.Wrap }); return; }
        Heading(selected.Kind.ToUpperInvariant());
        Field("Name", selected.Name); Field("X", selected.X); Field("Y", selected.Y); Field("Width", selected.Width); Field("Height", selected.Height);
        var lampIndex = _faceActive ? Faces.Elements.FirstOrDefault(e => e.Id == selected.Id)?.LampId : null;
        if (lampIndex.HasValue) Field("Lamp ID", lampIndex.Value);
        _visible = new CheckBox { Content = "Visible", IsChecked = selected.Visible }; _locked = new CheckBox { Content = "Lock transform", IsChecked = selected.Locked };
        _inspector.Children.Add(_visible); _inspector.Children.Add(_locked);
        _inspector.Children.Add(ActionButton("Apply", () => Try(() =>
        {
            if (_faceActive) Faces.UpdateElement(selected.Id, _fields["Name"].Text, Number("X"), Number("Y"), Number("Width"), Number("Height"), _visible.IsChecked == true, _locked.IsChecked == true,
                _fields.ContainsKey("Lamp ID") ? Integer("Lamp ID") : null);
            else _session.UpdateElement(selected.Id, _fields["Name"].Text, Number("X"), Number("Y"), Number("Width"), Number("Height"), _visible.IsChecked == true, _locked.IsChecked == true);
        })));
        if (selected.AssetPath is not null) _inspector.Children.Add(new TextBlock { Text = selected.AssetPath, TextWrapping = TextWrapping.Wrap, Opacity = .65 });
    }
    private void Heading(string text) => _inspector.Children.Add(new TextBlock { Text = text, FontWeight = FontWeight.SemiBold });
    private double Number(string name) => double.TryParse(_fields[name].Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && double.IsFinite(value)
        ? value : throw new ArgumentException($"{name} must be a finite number.");
    private int Integer(string name) => int.TryParse(_fields[name].Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
        ? value : throw new ArgumentException($"{name} must be a whole number.");
    private void Field(string name, object value)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("70,*") };
        row.Children.Add(new TextBlock { Text = name, VerticalAlignment = VerticalAlignment.Center });
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
    public void Dispose() { _session.Changed -= Refresh; Faces.Changed -= Refresh; Machines.Changed -= Refresh; _viewport.Dispose(); }
}

internal sealed class WorkspaceDockFactory(Control hierarchy, Control project, Control inspector, Document document) : Factory
{
    public override IRootDock CreateLayout()
    {
        ToolDock Pane(string id, Control content, double proportion)
        {
            var tool = new Tool { Id = id, Title = id, Context = content, CanClose = false };
            return new ToolDock { Proportion = proportion, ActiveDockable = tool, VisibleDockables = CreateList<IDockable>(tool), CanClose = false };
        }
        var center = new ProportionalDock { Orientation = DockOrientation.Vertical, Proportion = .60,
            VisibleDockables = CreateList<IDockable>(new DocumentDock { ActiveDockable = document, VisibleDockables = CreateList<IDockable>(document), Proportion = .75 }, new ProportionalDockSplitter(), Pane("Project", project, .25)) };
        var main = new ProportionalDock { Orientation = DockOrientation.Horizontal,
            VisibleDockables = CreateList<IDockable>(Pane("Hierarchy", hierarchy, .18), new ProportionalDockSplitter(), center, new ProportionalDockSplitter(), Pane("Inspector", inspector, .22)) };
        return new RootDock { Id = "Root", ActiveDockable = main, DefaultDockable = main, VisibleDockables = CreateList<IDockable>(main) };
    }
    public override void InitLayout(IDockable layout)
    { HostWindowLocator = new Dictionary<string, Func<IHostWindow?>> { [nameof(IDockWindow)] = () => new HostWindow() }; base.InitLayout(layout); }
}
