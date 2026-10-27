using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Threading;

namespace OasisEditor.Avalonia;

public sealed record NativePreviewSnapshot(string State, string? Machine, bool Audible, bool Throttled,
    string? Error, long AudioFrames, long NonZeroSamples);

public sealed partial class WorkspaceView
{
    public NativePreviewSession Preview { get; } = new();
    public NativeEmulationPreferences NativePreviewPreferences { get; private set; } = new();
    private readonly DispatcherTimer _previewTimer = new() { Interval = TimeSpan.FromMilliseconds(33) };
    private readonly Dictionary<Key, string[]> _previewKeys = [];
    private string? _previewSource;
    private volatile bool _previewSourceCurrent;
    private bool _previewStopping;
    private Window? _previewInputs;
    partial void ConnectPreviewRendering(IEmulationBackend backend, MachineDocument machine);
    partial void ClearPreviewRendering();
    partial void RefreshPreviewRendering();

    private void InitializePreview(MenuItem file)
    {
        var menu = new MenuItem { Header = "Emulation preview" }; file.Items.Add(menu);
        MenuAction(menu, "Configure native libraries…", async () => await PreviewConfigurationDialog());
        MenuAction(menu, "Play", async () => await PreviewAction(() => StartNativePreviewAsync()));
        foreach (var action in new[] { "pause", "resume", "reset", "stop", "throttle", "unthrottle" })
            MenuAction(menu, char.ToUpperInvariant(action[0]) + action[1..], async () => await PreviewAction(() => Preview.ControlAsync(action)));
        MenuAction(menu, "Machine inputs…", ShowPreviewInputs);
        Preview.BackendCreated += (backend, machine) => ConnectPreviewRendering(backend, machine);
        Preview.Cleared += () => Dispatcher.UIThread.Post(() => { ClearPreviewRendering(); _previewKeys.Clear(); });
        _session.Changed += CheckPreviewSource; Faces.Changed += CheckPreviewSource; Machines.Changed += CheckPreviewSource;
        _previewTimer.Tick += async (_, _) =>
        {
            if (Preview.Backend is null) return;
            if (!_previewSourceCurrent && !_previewStopping)
            {
                _previewStopping = true;
                try { await PreviewAction(() => Preview.ControlAsync("stop")); SetStatus("Preview stopped because its authored source changed."); }
                finally { _previewStopping = false; }
            }
            else RefreshPreviewRendering();
        };
        _previewTimer.Start();
        AddHandler(KeyDownEvent, PreviewKeyDown, RoutingStrategies.Tunnel);
        AddHandler(KeyUpEvent, PreviewKeyUp, RoutingStrategies.Tunnel);
        LostFocus += async (_, _) => await PreviewAction(Preview.ReleaseInputsAsync, report: false);
        AttachedToVisualTree += (_, _) => { if (TopLevel.GetTopLevel(this) is Window window) window.Deactivated += PreviewWindowDeactivated; };
    }

    public void ConfigureNativePreview(NativeEmulationPreferences preferences)
    {
        if (Preview.Backend is not null) throw new InvalidOperationException("Stop preview before changing native libraries.");
        if (preferences.AudioBufferLengthMilliseconds is < 10 or > 2000) throw new ArgumentOutOfRangeException(nameof(preferences));
        NativePreviewPreferences = preferences;
    }
    public async Task<NativePreviewSnapshot> StartNativePreviewAsync(bool audible = false, CancellationToken cancellationToken = default)
    {
        if (Program.IsHeadless && audible) throw new InvalidOperationException("Headless preview consumes diagnostic PCM silently.");
        if (HasUnsavedChanges || !Machines.IsProjectCurrent) throw new InvalidOperationException("Save the current project documents before previewing.");
        var machine = Machines.Document ?? throw new InvalidOperationException("Open a configured Machine first.");
        _previewSource = PreviewSource(); _previewSourceCurrent = true;
        await Preview.StartAsync(machine, NativePreviewPreferences, audible, () => _previewSourceCurrent, cancellationToken);
        SetStatus($"{machine.DisplayName}: {Preview.State} ({(audible ? "audio enabled" : "silent diagnostic audio")}).");
        return InspectNativePreview();
    }
    public NativePreviewSnapshot InspectNativePreview()
    {
        var audio = (Preview.Backend as FabricEmulationBackend)?.AudioSink as DiagnosticAudioSink;
        return new(Preview.State.ToString(), Preview.Machine?.DisplayName, Preview.Audible,
            Preview.Backend?.IsThrottleEnabled ?? true, Preview.LastError, audio?.Frames ?? 0, audio?.NonZeroSamples ?? 0);
    }
    private string? PreviewSource() => !Machines.IsProjectCurrent || Machines.Document is not { } machine ? null
        : _session.ProjectFilePath + "\n" + Machines.MachinePath + "\n" + MachineDocumentStorage.Serialize(machine);
    private void CheckPreviewSource()
    {
        if (Preview.Backend is not null)
            _previewSourceCurrent = !HasUnsavedChanges && _previewSource == PreviewSource();
    }
    private async Task PreviewAction(Func<Task> action, bool report = true)
    {
        try { await action(); if (report) SetStatus("Preview: " + Preview.State); }
        catch (Exception error) { SetStatus(error.Message); }
    }
    private async void PreviewWindowDeactivated(object? sender, EventArgs e) => await PreviewAction(Preview.ReleaseInputsAsync, report: false);
    private async void PreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled || e.Source is TextBox || Preview.State != EmulationBackendState.Running) return;
        if (_previewKeys.ContainsKey(e.Key)) { e.Handled = true; return; }
        var ids = Preview.Machine!.InputDefinitions.Where(input => Matches(input.KeyboardShortcut, e)).Select(input => input.Id).ToArray();
        if (ids.Length == 0) return;
        e.Handled = true; _previewKeys[e.Key] = ids;
        foreach (var id in ids) await PreviewAction(() => Preview.SetInputAsync(id, true), report: false);
    }
    private async void PreviewKeyUp(object? sender, KeyEventArgs e)
    {
        if (!_previewKeys.Remove(e.Key, out var ids)) return;
        e.Handled = true;
        foreach (var id in ids) await PreviewAction(() => Preview.SetInputAsync(id, false), report: false);
    }
    private static bool Matches(string shortcut, KeyEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(shortcut)) return false;
        try { var gesture = KeyGesture.Parse(shortcut); return gesture.Key == e.Key && gesture.KeyModifiers == e.KeyModifiers; }
        catch (FormatException) { return false; }
        catch (ArgumentException) { return false; }
    }
    private void ShowPreviewInputs()
    {
        if (Preview.Machine is not { } machine) { SetStatus("Start preview to use the Machine's authored inputs."); return; }
        if (_previewInputs is not null) { _previewInputs.Activate(); return; }
        if (TopLevel.GetTopLevel(this) is not Window owner) return;
        var items = new StackPanel { Spacing = 8, Margin = new Thickness(15) };
        foreach (var input in machine.InputDefinitions)
        {
            var button = new Button { Content = input.Name + (input.CoinInput ? " (coin)" : "") + "   " + input.KeyboardShortcut, HorizontalAlignment = HorizontalAlignment.Stretch };
            button.PointerPressed += async (_, e) => { e.Handled = true; e.Pointer.Capture(button); await PreviewAction(() => Preview.SetInputAsync(input.Id, true), false); };
            button.PointerReleased += async (_, e) => { e.Pointer.Capture(null); await PreviewAction(() => Preview.SetInputAsync(input.Id, false), false); };
            button.PointerCaptureLost += async (_, _) => await PreviewAction(() => Preview.SetInputAsync(input.Id, false), false);
            items.Children.Add(button);
        }
        if (machine.InputDefinitions.Count == 0) items.Children.Add(new TextBlock { Text = "Author Machine inputs and shortcuts in Machine settings first." });
        _previewInputs = new Window { Title = "Preview inputs — " + machine.DisplayName, Width = 380, Height = 500, Content = new ScrollViewer { Content = items } };
        _previewInputs.Deactivated += PreviewWindowDeactivated;
        _previewInputs.Closed += async (_, _) => { _previewInputs = null; await PreviewAction(Preview.ReleaseInputsAsync, false); };
        _previewInputs.Show(owner);
    }
    private async Task PreviewConfigurationDialog()
    {
        if (TopLevel.GetTopLevel(this) is not Window owner) return;
        var fields = typeof(NativeEmulationPreferences).GetProperties().Where(p => p.PropertyType == typeof(string)).ToArray();
        var content = new StackPanel { Margin = new Thickness(16), Spacing = 8 };
        var inputs = new Dictionary<string, TextBox>();
        foreach (var field in fields)
        {
            content.Children.Add(new TextBlock { Text = field.Name.Replace("LibraryPath", " library") });
            var input = new TextBox { Text = (string?)field.GetValue(NativePreviewPreferences), Watermark = "Absolute native library path" };
            inputs[field.Name] = input; content.Children.Add(input);
        }
        var audible = new CheckBox { Content = "Enable audio for Play (Windows)", IsChecked = false };
        content.Children.Add(audible);
        var dialog = new Window { Title = "Fabric emulation", Width = 650, Height = 640 };
        var apply = new Button { Content = "Apply and play" }; content.Children.Add(apply);
        apply.Click += async (_, _) =>
        {
            var preferences = new NativeEmulationPreferences();
            foreach (var field in fields) field.SetValue(preferences, inputs[field.Name].Text?.Trim() ?? "");
            try { ConfigureNativePreview(preferences); await StartNativePreviewAsync(audible.IsChecked == true); dialog.Close(); }
            catch (Exception error) { SetStatus(error.Message); }
        };
        dialog.Content = new ScrollViewer { Content = content }; await dialog.ShowDialog(owner);
    }
    private void DisposePreview()
    {
        _previewTimer.Stop();
        _session.Changed -= CheckPreviewSource; Faces.Changed -= CheckPreviewSource; Machines.Changed -= CheckPreviewSource;
        if (TopLevel.GetTopLevel(this) is Window window) window.Deactivated -= PreviewWindowDeactivated;
        _previewInputs?.Close();
        Preview.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
}
