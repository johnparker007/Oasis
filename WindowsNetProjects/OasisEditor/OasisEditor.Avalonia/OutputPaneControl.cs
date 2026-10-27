using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Media;

namespace OasisEditor.Avalonia;

/// <summary>Dockable view for the shared Output log service.</summary>
public sealed class OutputPaneControl : UserControl, IDisposable
{
    private readonly OutputLogService _service;
    private readonly ListBox _entries = new() { SelectionMode = SelectionMode.Multiple };
    private readonly TextBox _search = new() { PlaceholderText = "Filter output", MinWidth = 180, HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly CheckBox _infoFilter, _warningFilter, _errorFilter;
    private bool _disposed, _syncingControls;

    public OutputPaneControl(OutputLogService service)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _search.TextChanged += (_, _) => { if (!_syncingControls) _service.SearchText = _search.Text ?? string.Empty; };
        var filters = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
        _infoFilter = Filter("Info", _service.ShowInfo, value => _service.ShowInfo = value);
        _warningFilter = Filter("Warnings", _service.ShowWarnings, value => _service.ShowWarnings = value);
        _errorFilter = Filter("Errors", _service.ShowErrors, value => _service.ShowErrors = value);
        filters.Children.Add(_infoFilter);
        filters.Children.Add(_warningFilter);
        filters.Children.Add(_errorFilter);
        var copy = new Button { Content = "Copy", MinWidth = 64 };
        copy.Click += async (_, _) => await CopySelectionAsync();
        var clear = new Button { Content = "Clear", MinWidth = 64 };
        clear.Click += (_, _) => _service.Clear();
        var toolbar = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto,Auto,Auto"), ColumnSpacing = 8, Margin = new Thickness(8, 6) };
        toolbar.Children.Add(_search);
        Grid.SetColumn(filters, 1); toolbar.Children.Add(filters);
        Grid.SetColumn(copy, 2); toolbar.Children.Add(copy);
        Grid.SetColumn(clear, 3); toolbar.Children.Add(clear);
        var logPath = new TextBlock { Text = service.DiskLogPath is null ? "Disk logging off" : service.DiskLogPath, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center, Opacity = .65 };
        Grid.SetColumn(logPath, 4); toolbar.Children.Add(logPath);

        _entries.ItemsSource = _service.FilteredEntries;
        _entries.ItemTemplate = new FuncDataTemplate<OutputLogItem>((item, _) => CreateRow(item));
        _service.PropertyChanged += OnServicePropertyChanged;
        var layout = new Grid { RowDefinitions = new RowDefinitions("Auto,*") };
        layout.Children.Add(toolbar);
        Grid.SetRow(_entries, 1); layout.Children.Add(_entries);
        Content = layout;
        KeyDown += OnKeyDown;
    }

    public OutputLogService Service => _service;

    private CheckBox Filter(string label, bool initial, Action<bool> changed)
    {
        var check = new CheckBox { Content = label, IsChecked = initial, VerticalAlignment = VerticalAlignment.Center };
        check.IsCheckedChanged += (_, _) => { if (!_syncingControls) changed(check.IsChecked == true); };
        return check;
    }

    private static Control CreateRow(OutputLogItem? item)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*"), ColumnSpacing = 8, Margin = new Thickness(6, 2) };
        grid.Children.Add(new TextBlock { Text = item?.FormattedTimestamp ?? string.Empty, Opacity = .65, FontFamily = new FontFamily("monospace") });
        var severity = Severity(item);
        Grid.SetColumn(severity, 1); grid.Children.Add(severity);
        var message = Message(item);
        Grid.SetColumn(message, 2); grid.Children.Add(message);
        return grid;
    }

    private static TextBlock Severity(OutputLogItem? item) => new()
    {
        Text = item?.Severity.ToString().ToUpperInvariant() ?? string.Empty,
        FontWeight = FontWeight.SemiBold,
        Opacity = .8
    };

    private static TextBlock Message(OutputLogItem? item) => new()
    {
        Text = item?.Message ?? string.Empty,
        TextWrapping = TextWrapping.Wrap
    };

    private async Task CopySelectionAsync()
    {
        var text = BuildCopyTextForSelection(_entries.SelectedItems?.OfType<OutputLogItem>());
        if (text.Length > 0 && TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
            await clipboard.SetTextAsync(text);
    }

    public string BuildCopyTextForSelection(IEnumerable<OutputLogItem>? selectedItems = null)
    {
        var selected = selectedItems?.ToArray() ?? _entries.SelectedItems?.OfType<OutputLogItem>().ToArray() ?? [];
        return _service.BuildText(selected.Length == 0 ? _service.FilteredEntries : selected);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if ((e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Meta)) != 0 && e.Key == Key.C)
        {
            e.Handled = true;
            _ = CopySelectionAsync();
        }
    }

    private void OnServicePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OutputLogService.FilteredEntries))
            _entries.ItemsSource = _service.FilteredEntries;
        if (e.PropertyName is nameof(OutputLogService.SearchText) or nameof(OutputLogService.ShowInfo)
            or nameof(OutputLogService.ShowWarnings) or nameof(OutputLogService.ShowErrors))
        {
            if (global::Avalonia.Threading.Dispatcher.UIThread.CheckAccess()) SyncFilterControls();
            else global::Avalonia.Threading.Dispatcher.UIThread.Post(SyncFilterControls);
        }
    }

    private void SyncFilterControls()
    {
        if (_disposed) return;
        _syncingControls = true;
        try
        {
            _search.Text = _service.SearchText;
            _infoFilter.IsChecked = _service.ShowInfo;
            _warningFilter.IsChecked = _service.ShowWarnings;
            _errorFilter.IsChecked = _service.ShowErrors;
        }
        finally { _syncingControls = false; }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _service.PropertyChanged -= OnServicePropertyChanged;
    }
}
