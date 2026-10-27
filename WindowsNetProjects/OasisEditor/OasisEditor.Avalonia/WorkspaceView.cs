using System.Globalization;
using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Dock.Avalonia.Controls;
using Dock.Model.Core;
using Dock.Model.Controls;
using Dock.Model.Mvvm;
using Dock.Model.Mvvm.Controls;
using DockOrientation = Dock.Model.Core.Orientation;
using Orientation = Avalonia.Layout.Orientation;

namespace OasisEditor.Avalonia;

public sealed class WorkspaceView : UserControl, IDisposable
{
    private readonly AuthoringSession _session;
    private readonly PanelViewport _viewport;
    private readonly ListBox _hierarchy = new();
    private readonly ObservableCollection<ElementSnapshot> _rows = [];
    private string[] _panelPaths = [];
    private readonly ListBox _assets = new();
    private readonly StackPanel _inspector = new() { Margin = new Thickness(12), Spacing = 10 };
    private readonly TextBlock _status = new() { Margin = new Thickness(10, 5), TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly TextBlock _project = new() { Margin = new Thickness(10, 7), FontWeight = FontWeight.SemiBold };
    private readonly Button _undo, _redo, _save;
    private readonly Document _document;
    private readonly WorkspaceDockFactory _factory;
    private readonly DockControl _dock;
    private bool _refreshing;
    private string? _lastPanel;
    private string? _inspectedId;
    private ElementSnapshot? _inspectedElement;
    private readonly Dictionary<string, TextBox> _fields = [];
    private CheckBox? _visible, _locked;

    public WorkspaceView(AuthoringSession session)
    {
        _session = session;
        _viewport = new PanelViewport(session);
        _hierarchy.ItemsSource = _rows;
        this.Bind(BackgroundProperty, this.GetResourceObservable("WorkspaceBrush"));
        _hierarchy.SelectionChanged += (_, _) => { if (!_refreshing) Try(() => session.Select((_hierarchy.SelectedItem as ElementSnapshot)?.Id)); };
        _hierarchy.ItemTemplate = new FuncDataTemplate<ElementSnapshot>((e, _) => new TextBlock
        {
            Text = e is null ? "" : $"{(e.Locked ? "▣" : "◇")}  {e.Name}", Opacity = e?.Visible == false ? .45 : 1,
            TextTrimming = TextTrimming.CharacterEllipsis
        });
        _assets.ItemTemplate = new FuncDataTemplate<string>((path, _) => new TextBlock
        {
            Text = "▤  " + Path.GetFileName(Path.GetDirectoryName(path)), TextTrimming = TextTrimming.CharacterEllipsis
        });
        _assets.DoubleTapped += (_, _) => { if (_assets.SelectedItem is string path) Try(() => session.OpenPanel(path)); };

        var projectPanel = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto") };
        projectPanel.Children.Add(_project);
        Grid.SetRow(_assets, 1); projectPanel.Children.Add(_assets);
        var newPanelBar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(8), Spacing = 5 };
        var panelName = new TextBox { PlaceholderText = "Panel name", Width = 140 };
        newPanelBar.Children.Add(panelName);
        newPanelBar.Children.Add(ActionButton("Create", () => Try(() => session.CreatePanel(panelName.Text ?? ""))));
        Grid.SetRow(newPanelBar, 2); projectPanel.Children.Add(newPanelBar);

        var scene = new Grid { RowDefinitions = new RowDefinitions("Auto,*") };
        var tools = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Margin = new Thickness(7, 5) };
        tools.Children.Add(ActionButton("+ Rectangle", () => Try(() => session.AddRectangle())));
        tools.Children.Add(ActionButton("Fit", _viewport.Fit));
        tools.Children.Add(new TextBlock { Text = "Wheel: zoom  ·  Middle drag: pan  ·  Drag: move", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0) });
        scene.Children.Add(Toolbar(tools)); Grid.SetRow(_viewport, 1); scene.Children.Add(_viewport);
        _document = new Document { Id = "Panel2D", Title = "Panel 2D", Context = scene, CanClose = false };
        _factory = new WorkspaceDockFactory(_hierarchy, projectPanel, new ScrollViewer { Content = _inspector }, _document);
        var layout = _factory.CreateLayout(); _factory.InitLayout(layout);
        _dock = new DockControl { Layout = layout };

        var root = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,*,Auto") };
        var file = new MenuItem { Header = "_File" };
        var open = new MenuItem { Header = "Open project…" }; open.Click += async (_, _) => await OpenProjectDialog();
        var save = new MenuItem { Header = "Save panel" }; save.Click += (_, _) => Try(session.Save);
        file.Items.Add(open); file.Items.Add(save);
        var edit = new MenuItem { Header = "_Edit" };
        var undo = new MenuItem { Header = "Undo" }; undo.Click += (_, _) => Try(session.Undo); edit.Items.Add(undo);
        var redo = new MenuItem { Header = "Redo" }; redo.Click += (_, _) => Try(session.Redo); edit.Items.Add(redo);
        var windowMenu = new MenuItem { Header = "_Window" };
        foreach (var theme in new[] { "System", "Dark", "Light" })
        {
            var item = new MenuItem { Header = theme + " theme" };
            item.Click += (_, _) => { Application.Current!.RequestedThemeVariant = theme == "Dark" ? ThemeVariant.Dark : theme == "Light" ? ThemeVariant.Light : ThemeVariant.Default; _viewport.InvalidateVisual(); };
            windowMenu.Items.Add(item);
        }
        root.Children.Add(new Menu { ItemsSource = new[] { file, edit, windowMenu } });
        var toolbar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Margin = new Thickness(8, 5) };
        toolbar.Children.Add(ActionButton("Open project…", async () => await OpenProjectDialog()));
        _save = ActionButton("Save", () => Try(session.Save)); toolbar.Children.Add(_save);
        _undo = ActionButton("Undo", () => Try(session.Undo)); toolbar.Children.Add(_undo);
        _redo = ActionButton("Redo", () => Try(session.Redo)); toolbar.Children.Add(_redo);
        toolbar.Children.Add(new TextBlock { Text = "OASIS  /  AUTHORING", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(14, 0), FontWeight = FontWeight.SemiBold });
        var toolbarBorder = Toolbar(toolbar); Grid.SetRow(toolbarBorder, 1); root.Children.Add(toolbarBorder);
        Grid.SetRow(_dock, 2); root.Children.Add(_dock);
        var statusBorder = Toolbar(_status); Grid.SetRow(statusBorder, 3); root.Children.Add(statusBorder);
        Content = root;
        KeyDown += (_, e) =>
        {
            var command = (e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Meta)) != 0;
            if (!command) return;
            if (e.Key == Key.S) { Try(session.Save); e.Handled = true; }
            else if (e.Source is not TextBox && e.Key == Key.Z)
            { Try((e.KeyModifiers & KeyModifiers.Shift) != 0 ? session.Redo : session.Undo); e.Handled = true; }
            else if (e.Source is not TextBox && e.Key == Key.Y) { Try(session.Redo); e.Handled = true; }
        };
        session.Changed += Refresh;
        Refresh();
    }

    public void SetStatus(string text) => _status.Text = text;
    public void Try(Action action)
    {
        try { action(); }
        catch (Exception e) when (e is IOException or ArgumentException or InvalidOperationException or KeyNotFoundException or System.Text.Json.JsonException)
        { SetStatus(e.Message); }
    }

    private async Task OpenProjectDialog()
    {
        var top = TopLevel.GetTopLevel(this);
        if (top?.StorageProvider is not { CanOpen: true } storage) { SetStatus("Open a project through MCP in this session."); return; }
        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open Oasis project", AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("Oasis project") { Patterns = ["*.oasisproj"] }]
        });
        if (files.Count > 0 && files[0].TryGetLocalPath() is string path) Try(() => _session.OpenProject(path));
    }

    private void Refresh()
    {
        _refreshing = true;
        var elements = _session.Elements;
        if (!_rows.Select(e => e.Id).SequenceEqual(elements.Select(e => e.Id)))
        {
            _rows.Clear(); foreach (var element in elements) _rows.Add(element);
        }
        else for (var i = 0; i < elements.Count; i++) if (_rows[i] != elements[i]) _rows[i] = elements[i];
        _hierarchy.SelectedItem = _rows.FirstOrDefault(e => e.Id == _session.SelectedId);
        var paths = _session.ProjectFilePath is null ? [] : _session.ListPanels().ToArray();
        if (!_panelPaths.SequenceEqual(paths)) { _panelPaths = paths; _assets.ItemsSource = paths; }
        _assets.SelectedItem = _session.PanelPath;
        _project.Text = _session.ProjectName ?? "Open an Oasis project";
        _document.Title = string.IsNullOrEmpty(_session.PanelTitle) ? "Panel 2D" : _session.PanelTitle + (_session.IsDirty ? " *" : "");
        _save.IsEnabled = _session.IsDirty; _undo.IsEnabled = _session.CanUndo; _redo.IsEnabled = _session.CanRedo;
        RefreshInspector(elements.FirstOrDefault(e => e.Id == _session.SelectedId));
        SetStatus(_session.PanelPath is null ? "Open a project and choose a Panel2D asset.  •  3D authoring is deferred."
            : $"{elements.Count} elements  •  {(_session.IsDirty ? "Unsaved changes" : "Saved")}  •  {_session.PanelPath}");
        if (_lastPanel != _session.PanelPath) { _lastPanel = _session.PanelPath; _viewport.ClearImages(); _viewport.RequestFit(); }
        _viewport.InvalidateVisual();
        _refreshing = false;
    }

    private void RefreshInspector(ElementSnapshot? selected)
    {
        if (selected == _inspectedElement) return;
        _inspectedElement = selected; _inspectedId = selected?.Id;
        _inspector.Children.Clear(); _fields.Clear();
        if (selected is null)
        {
            _inspector.Children.Add(new TextBlock { Text = "Select an element", FontWeight = FontWeight.SemiBold });
            _inspector.Children.Add(new TextBlock { Text = "Choose an item in the Hierarchy or the 2D view to edit its properties.", TextWrapping = TextWrapping.Wrap });
            return;
        }
        _inspector.Children.Add(new TextBlock { Text = selected.Kind.ToUpperInvariant(), FontWeight = FontWeight.SemiBold });
        _inspector.Children.Add(new TextBlock { Text = selected.Id, FontSize = 10, TextWrapping = TextWrapping.Wrap, Opacity = .55 });
        Field("Name", selected.Name);
        Field("X", selected.X); Field("Y", selected.Y); Field("Width", selected.Width); Field("Height", selected.Height);
        _visible = new CheckBox { Content = "Visible", IsChecked = selected.Visible };
        _locked = new CheckBox { Content = "Lock transform", IsChecked = selected.Locked };
        _inspector.Children.Add(_visible); _inspector.Children.Add(_locked);
        _inspector.Children.Add(ActionButton("Apply", () => Try(ApplyInspector)));
        if (selected.AssetPath is not null) _inspector.Children.Add(new TextBlock { Text = selected.AssetPath, TextWrapping = TextWrapping.Wrap, Opacity = .65 });
    }

    private void Field(string name, object value)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("70,*") };
        row.Children.Add(new TextBlock { Text = name, VerticalAlignment = VerticalAlignment.Center });
        var field = new TextBox { Text = Convert.ToString(value, CultureInfo.InvariantCulture) };
        _fields[name] = field; Grid.SetColumn(field, 1); row.Children.Add(field); _inspector.Children.Add(row);
    }

    private void ApplyInspector()
    {
        if (_inspectedId is null) return;
        double Number(string field) => double.TryParse(_fields[field].Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) && double.IsFinite(n)
            ? n : throw new ArgumentException($"{field} must be a finite number.");
        _session.UpdateElement(_inspectedId, _fields["Name"].Text, Number("X"), Number("Y"), Number("Width"), Number("Height"), _visible!.IsChecked == true, _locked!.IsChecked == true);
    }

    private static Button ActionButton(string text, Action action)
    {
        var button = new Button { Content = text }; button.Click += (_, _) => action(); return button;
    }
    private static Border Toolbar(Control child)
    {
        var border = new Border { Child = child, BorderThickness = new Thickness(0, 0, 0, 1) };
        border.Bind(BackgroundProperty, border.GetResourceObservable("ToolbarBrush"));
        border.Bind(Border.BorderBrushProperty, border.GetResourceObservable("DividerBrush"));
        return border;
    }
    public void Dispose() { _session.Changed -= Refresh; _viewport.Dispose(); }
}

internal sealed class WorkspaceDockFactory(Control hierarchy, Control project, Control inspector, Document document) : Factory
{
    public override IRootDock CreateLayout()
    {
        var left = new Tool { Id = "Hierarchy", Title = "Hierarchy", Context = hierarchy, CanClose = false };
        var bottom = new Tool { Id = "Project", Title = "Project", Context = project, CanClose = false };
        var right = new Tool { Id = "Inspector", Title = "Inspector", Context = inspector, CanClose = false };
        ToolDock Pane(Tool tool, double proportion) => new()
        {
            Proportion = proportion, ActiveDockable = tool, VisibleDockables = CreateList<IDockable>(tool), CanClose = false
        };
        var center = new ProportionalDock
        {
            Orientation = DockOrientation.Vertical, Proportion = .60,
            VisibleDockables = CreateList<IDockable>(new DocumentDock { ActiveDockable = document, VisibleDockables = CreateList<IDockable>(document), Proportion = .75 },
                new ProportionalDockSplitter(), Pane(bottom, .25))
        };
        var main = new ProportionalDock
        {
            Orientation = DockOrientation.Horizontal,
            VisibleDockables = CreateList<IDockable>(Pane(left, .18), new ProportionalDockSplitter(), center,
                new ProportionalDockSplitter(), Pane(right, .22))
        };
        return new RootDock { Id = "Root", ActiveDockable = main, DefaultDockable = main, VisibleDockables = CreateList<IDockable>(main) };
    }
    public override void InitLayout(IDockable layout)
    {
        HostWindowLocator = new Dictionary<string, Func<IHostWindow?>> { [nameof(IDockWindow)] = () => new HostWindow() };
        base.InitLayout(layout);
    }
}
