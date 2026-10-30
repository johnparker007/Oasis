using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace OasisEditor.Avalonia;

internal sealed record ApplicationWindowPlacements(WorkspaceWindowState? Launcher = null, WorkspaceWindowState? Editor = null);

/// <summary>Separate launcher/editor placements for the shared desktop host.</summary>
internal sealed class WindowPlacementPersistence : IDisposable
{
    private readonly Window _window;
    private readonly string _path;
    private readonly Action<string> _report;
    private ApplicationWindowPlacements _saved;
    private readonly WorkspaceWindowState _defaults;
    private WorkspaceWindowState _normal;
    private WindowState _state;
    private bool _launcher, _restoring, _queued, _disposed, _canSave = true;
    private readonly DispatcherTimer _saveTimer = new() { Interval = TimeSpan.FromMilliseconds(500) };

    public WindowPlacementPersistence(Window window, string path, bool launcher, Action<string> report, WorkspaceWindowState? legacyEditor = null)
    {
        _window = window; _path = Path.GetFullPath(path); _launcher = launcher; _report = report;
        _saveTimer.Tick += (_, _) => { _saveTimer.Stop(); Save(); };
        _normal = _defaults = NormalBounds();
        try
        {
            _saved = Read();
            if (_saved.Editor is null && legacyEditor is not null)
            { WorkspaceLayoutState.ValidateBounds(legacyEditor); _saved = _saved with { Editor = legacyEditor }; }
        }
        catch (Exception error) when (error is IOException or ArgumentException or JsonException or UnauthorizedAccessException)
        { _saved = new(); _canSave = false; report("Window placement could not be restored: " + error.Message); }
        Restore();
        window.PositionChanged += PositionChanged;
        window.Resized += Resized;
        window.PropertyChanged += PropertyChanged;
        window.Closing += Closing;
    }

    private WorkspaceWindowState NormalBounds() => new(_window.Position.X, _window.Position.Y,
        Math.Max(_window.MinWidth, double.IsFinite(_window.Width) ? _window.Width : Math.Max(100, _window.ClientSize.Width)),
        Math.Max(_window.MinHeight, double.IsFinite(_window.Height) ? _window.Height : Math.Max(100, _window.ClientSize.Height)));

    private void Capture()
    {
        if (_restoring || _disposed || !_window.IsVisible || _window.WindowState == WindowState.Minimized) return;
        _state = _window.WindowState;
        if (_state == WindowState.Normal) _normal = NormalBounds();
    }
    private void QueueCapture()
    {
        if (_restoring || _disposed || _queued) return;
        _queued = true;
        // Native maximize/restore notifications can deliver size before state.
        Dispatcher.UIThread.Post(() =>
        {
            _queued = false; Capture();
            if (!_disposed && !_restoring) { _saveTimer.Stop(); _saveTimer.Start(); }
        }, DispatcherPriority.Background);
    }
    private void PositionChanged(object? sender, PixelPointEventArgs args) => QueueCapture();
    private void Resized(object? sender, WindowResizedEventArgs args) => QueueCapture();
    private void PropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs args)
    {
        if (args.Property != Window.WindowStateProperty || _restoring) return;
        if (_window.WindowState != WindowState.Minimized) _state = _window.WindowState;
        QueueCapture();
    }
    private void Closing(object? sender, WindowClosingEventArgs args) => Save();

    public void Switch(bool launcher)
    {
        if (_launcher == launcher || _disposed) return;
        Save(); _launcher = launcher; Restore();
    }
    private void Restore()
    {
        _restoring = true;
        _saveTimer.Stop();
        try
        {
            var bounds = (_launcher ? _saved.Launcher : _saved.Editor) ?? _defaults;
            _window.WindowState = WindowState.Normal;
            Apply(_window, bounds with { State = "Normal" });
            _normal = NormalBounds();
            // Establish native restore bounds before requesting maximization.
            _window.WindowState = bounds.State switch { "Maximized" => WindowState.Maximized, "FullScreen" => WindowState.FullScreen, _ => WindowState.Normal };
            _state = _window.WindowState;
        }
        finally { _restoring = false; }
    }
    private void Save()
    {
        if (_disposed || !_canSave) return;
        Capture();
        var placement = _normal with { State = _state.ToString() };
        try
        {
            // Merge the other profile in case another editor instance saved it.
            var latest = Read();
            _saved = _launcher ? latest with { Launcher = placement, Editor = latest.Editor ?? _saved.Editor }
                : latest with { Editor = placement, Launcher = latest.Launcher ?? _saved.Launcher };
            var parent = Path.GetDirectoryName(_path)!; Directory.CreateDirectory(parent);
            var temp = Path.Combine(parent, ".window-placement-" + Guid.NewGuid().ToString("N") + ".tmp");
            try { File.WriteAllText(temp, JsonSerializer.Serialize(_saved)); File.Move(temp, _path, overwrite: true); }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
        catch (Exception error) when (error is IOException or ArgumentException or JsonException or UnauthorizedAccessException)
        { _report("Window placement could not be saved: " + error.Message); }
    }
    private ApplicationWindowPlacements Read()
    {
        WorkspacePathPolicy.RejectRootReparsePoints(Path.GetDirectoryName(_path)!, requireExists: false);
        var file = new FileInfo(_path);
        if (file.LinkTarget is not null || (file.Exists && file.Attributes.HasFlag(FileAttributes.ReparsePoint)))
            throw new IOException("Window placement settings cannot be a link.");
        if (!file.Exists) return new();
        if (file.Length > 16 * 1024) throw new InvalidDataException("Window placement settings exceed the size limit.");
        var saved = JsonSerializer.Deserialize<ApplicationWindowPlacements>(File.ReadAllText(_path)) ?? throw new InvalidDataException("Window placement is missing.");
        if (saved.Launcher is not null) WorkspaceLayoutState.ValidateBounds(saved.Launcher);
        if (saved.Editor is not null) WorkspaceLayoutState.ValidateBounds(saved.Editor);
        return saved;
    }

    internal static void Apply(Window window, WorkspaceWindowState bounds)
    {
        WorkspaceLayoutState.ValidateBounds(bounds);
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Width = Math.Max(window.MinWidth, bounds.Width); window.Height = Math.Max(window.MinHeight, bounds.Height);
        var x = (int)bounds.X; var y = (int)bounds.Y;
        if (window.Screens.All.Count > 0)
        {
            var screen = window.Screens.All.FirstOrDefault(s => s.WorkingArea.Contains(new PixelPoint(x, y))) ?? window.Screens.Primary ?? window.Screens.All[0];
            var area = screen.WorkingArea;
            window.Width = Math.Min(window.Width, Math.Max(window.MinWidth, area.Width / screen.Scaling));
            window.Height = Math.Min(window.Height, Math.Max(window.MinHeight, area.Height / screen.Scaling));
            x = Math.Clamp(x, area.X, Math.Max(area.X, area.Right - (int)Math.Ceiling(window.Width * screen.Scaling)));
            y = Math.Clamp(y, area.Y, Math.Max(area.Y, area.Bottom - (int)Math.Ceiling(window.Height * screen.Scaling)));
        }
        window.Position = new PixelPoint(x, y);
        window.WindowState = bounds.State switch { "Maximized" => WindowState.Maximized, "FullScreen" => WindowState.FullScreen, _ => WindowState.Normal };
    }
    public void Dispose()
    {
        if (_disposed) return;
        _saveTimer.Stop(); Save(); _disposed = true;
        _window.PositionChanged -= PositionChanged; _window.Resized -= Resized;
        _window.PropertyChanged -= PropertyChanged; _window.Closing -= Closing;
    }
}
