using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

namespace OasisEditor.Avalonia;

public sealed record NativePreviewSnapshot(string State, string? Machine, bool Audible, bool Throttled,
    string? Error, long AudioFrames, long NonZeroSamples, int Lamps, int Reels, int SegmentCells);

public sealed partial class WorkspaceView
{
    public NativePreviewSession Preview { get; }
    public NativeEmulationPreferences NativePreviewPreferences { get; private set; } = new();
    private readonly DispatcherTimer _previewTimer = new() { Interval = TimeSpan.FromMilliseconds(33) };
    private readonly Dictionary<Key, string[]> _previewKeys = [];
    private string? _previewSource;
    private volatile bool _previewSourceCurrent;
    private bool _previewStopping;
    private int _previewCheckTicks;
    private Window? _previewInputs;
    private readonly TextBlock _previewStatus = new() { Text = "Stopped", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0) };
    private bool _previewAudible;

    private void InitializePreview(MenuItem file)
    {
        var menu = new MenuItem { Header = "Emulation preview" }; file.Items.Add(menu);
        MenuAction(menu, "Configure native libraries…", async () => await PreviewConfigurationDialog());
        MenuAction(menu, "Play", async () => await PreviewAction(() => StartNativePreviewAsync(_previewAudible)));
        foreach (var action in new[] { "pause", "resume", "reset", "stop", "throttle", "unthrottle" })
            MenuAction(menu, char.ToUpperInvariant(action[0]) + action[1..], async () => await PreviewAction(() => Preview.ControlAsync(action)));
        MenuAction(menu, "Machine inputs…", ShowPreviewInputs);
        Preview.BackendCreated += (backend, machine) => ConnectPreviewRendering(backend, machine);
        Preview.Cleared += () => Dispatcher.UIThread.Post(() => { ClearPreviewRendering(); _previewKeys.Clear(); });
        _session.Changed += CheckPreviewSource; Faces.Changed += CheckPreviewSource; Machines.Changed += CheckPreviewSource;
        _previewTimer.Tick += async (_, _) =>
        {
            _previewStatus.Text = Preview.State.ToString();
            if (Preview.Backend is null) return;
            if (++_previewCheckTicks % 30 == 0)
            {
                try { Machines.EnsureSavedSourceUnchanged(); }
                catch (Exception error) when (error is IOException or InvalidOperationException or UnauthorizedAccessException)
                { _previewSourceCurrent = false; SetStatus(error.Message); }
            }
            if (!_previewSourceCurrent && !_previewStopping)
            {
                _previewStopping = true;
                try { await PreviewAction(() => Preview.ControlAsync("stop")); SetStatus("Preview stopped because its authored source changed."); }
                finally { _previewStopping = false; }
            }
            else RefreshPreviewRendering();
        };
        _previewTimer.Start();
        _viewport.PreviewIsActive = () => Preview.Backend is not null;
        _viewport.ResolvePreviewInput = ResolvePreviewPointerInput;
        _viewport.PreviewInputRequested += async (id, pressed) => await PreviewAction(() => Preview.SetInputAsync(id, pressed, source: "viewport"), false);
        SceneView.ResolvePreviewInput = ResolveScenePreviewInput;
        SceneView.PreviewInputRequested += async (id, pressed) => await PreviewAction(() => Preview.SetInputAsync(id, pressed, source: "scene"), false);
        AddHandler(KeyDownEvent, PreviewKeyDown, RoutingStrategies.Tunnel);
        AddHandler(KeyUpEvent, PreviewKeyUp, RoutingStrategies.Tunnel);
        LostFocus += async (_, _) => await PreviewAction(ReleasePreviewInputs, report: false);
        AttachedToVisualTree += (_, _) => { if (TopLevel.GetTopLevel(this) is Window window) window.Deactivated += PreviewWindowDeactivated; };
    }
    private Control CreatePreviewToolbar()
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Margin = new Thickness(10, 0, 0, 0) };
        panel.Children.Add(ActionButton("▶ Play", async () => await PreviewAction(() => StartNativePreviewAsync(_previewAudible))));
        panel.Children.Add(ActionButton("Ⅱ Pause", async () => await PreviewAction(() => Preview.ControlAsync(Preview.State == EmulationBackendState.Paused ? "resume" : "pause"))));
        panel.Children.Add(ActionButton("■ Stop", async () => await PreviewAction(() => Preview.ControlAsync("stop"))));
        panel.Children.Add(_previewStatus);
        return panel;
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
        Machines.EnsureSavedSourceUnchanged();
        if (machine.Runtime is EmulationRuntimeDefinition runtime && runtime.Platform != FruitMachinePlatformType.None)
            machine = machine with { Runtime = runtime with { PlatformSettings = FabricRomResourceResolver.ResolveSettingsPaths(
                _session.RequireEditorProject(), runtime.Platform, runtime.PlatformSettings) } };
        _previewSource = PreviewSource(); _previewSourceCurrent = true;
        await Preview.StartAsync(machine, NativePreviewPreferences, audible, () => _previewSourceCurrent, cancellationToken);
        SetStatus($"{machine.DisplayName}: {Preview.State} ({(audible ? "audio enabled" : "silent diagnostic audio")}).");
        return InspectNativePreview();
    }
    public NativePreviewSnapshot InspectNativePreview()
    {
        var audio = (Preview.Backend as FabricEmulationBackend)?.AudioSink as DiagnosticAudioSink;
        return new(Preview.State.ToString(), Preview.Machine?.DisplayName, Preview.Audible,
            Preview.Backend?.IsThrottleEnabled ?? true, Preview.LastError ?? (Preview.Backend as FabricEmulationBackend)?.LastFailure?.Message,
            audio?.Frames ?? 0, audio?.NonZeroSamples ?? 0, _previewOutputs?.LampCount ?? 0,
            _previewOutputs?.ReelCount ?? 0, _previewOutputs?.SegmentCount ?? 0);
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
    private Task ReleasePreviewInputs() { _previewKeys.Clear(); return Preview.ReleaseInputsAsync(); }
    private async void PreviewWindowDeactivated(object? sender, EventArgs e) => await PreviewAction(ReleasePreviewInputs, report: false);
    private async void PreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled || e.Source is TextBox || Preview.State != EmulationBackendState.Running) return;
        if (_previewKeys.ContainsKey(e.Key)) { e.Handled = true; return; }
        var ids = Preview.Machine!.InputDefinitions.Where(input => Matches(input.KeyboardShortcut, e)).Select(input => input.Id).ToArray();
        if (ids.Length == 0) return;
        e.Handled = true; _previewKeys[e.Key] = ids;
        foreach (var id in ids) await PreviewAction(() => Preview.SetInputAsync(id, true, source: "key:" + e.Key), report: false);
    }
    private async void PreviewKeyUp(object? sender, KeyEventArgs e)
    {
        if (!_previewKeys.Remove(e.Key, out var ids)) return;
        e.Handled = true;
        foreach (var id in ids) await PreviewAction(() => Preview.SetInputAsync(id, false, source: "key:" + e.Key), report: false);
    }
    private static bool Matches(string shortcut, KeyEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(shortcut)) return false;
        try { var gesture = KeyGesture.Parse(shortcut); return gesture.Key == e.Key && gesture.KeyModifiers == e.KeyModifiers; }
        catch (FormatException) { return false; }
        catch (ArgumentException) { return false; }
    }
    internal string? ResolvePreviewPointerInput(Point point)
    {
        var inputs = Preview.Machine?.InputDefinitions;
        if (inputs is null) return null;
        if (_faceActive && Faces.Document is { } face)
        {
            foreach (var button in face.Elements.OfType<FaceButtonElement>().Reverse())
                if (button.IsVisible && new Rect(button.X, button.Y, button.Width, button.Height).Contains(point)
                    && FaceInputTargetResolver.TryGetInputReference(button, out var reference))
                    return inputs.FirstOrDefault(i => MachineInputReference.FromInputId(i.Id).Id == reference.Id)?.Id;
        }
        else
        {
            var hit = _session.Elements.LastOrDefault(e => e.Visible && new Rect(e.X, e.Y, e.Width, e.Height).Contains(point));
            if (hit is not null && Guid.TryParse(hit.Id, out var id)) return inputs.FirstOrDefault(i => i.LinkedVisualElementId == id)?.Id;
        }
        return null;
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
            button.PointerPressed += async (_, e) => { e.Handled = true; e.Pointer.Capture(button); await PreviewAction(() => Preview.SetInputAsync(input.Id, true, source: "button"), false); };
            button.PointerReleased += async (_, e) => { e.Pointer.Capture(null); await PreviewAction(() => Preview.SetInputAsync(input.Id, false, source: "button"), false); };
            button.PointerCaptureLost += async (_, _) => await PreviewAction(() => Preview.SetInputAsync(input.Id, false, source: "button"), false);
            items.Children.Add(button);
        }
        if (machine.InputDefinitions.Count == 0) items.Children.Add(new TextBlock { Text = "Author Machine inputs and shortcuts in Machine settings first." });
        _previewInputs = new Window { Title = "Preview inputs — " + machine.DisplayName, Width = 380, Height = 500, Content = new ScrollViewer { Content = items } };
        _previewInputs.Deactivated += PreviewWindowDeactivated;
        _previewInputs.Closed += async (_, _) => { _previewInputs = null; await PreviewAction(ReleasePreviewInputs, false); };
        _previewInputs.Show(owner);
    }
    private async Task PreviewConfigurationDialog()
    {
        if (TopLevel.GetTopLevel(this) is not Window owner) return;
        var fields = typeof(NativeEmulationPreferences).GetProperties().Where(p => p.PropertyType == typeof(string)).ToArray();
        var labels = new[] { "Fabric runtime", "JPM Impact provider", "Barcrest MPU5 provider", "Maygay Epoch provider", "Barcrest MPU3 provider", "Maygay M1 provider", "Bell-Fruit Scorpion 4 provider" };
        var content = new StackPanel { Margin = new Thickness(16), Spacing = 8 };
        var inputs = new Dictionary<string, TextBox>();
        for (var index = 0; index < fields.Length; index++)
        {
            var field = fields[index];
            content.Children.Add(new TextBlock { Text = labels[index] });
            var input = new TextBox { Text = (string?)field.GetValue(NativePreviewPreferences), Watermark = "Absolute native library path" };
            inputs[field.Name] = input;
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 6 }; row.Children.Add(input);
            var browse = ActionButton("Browse…", async () =>
            {
                var files = await owner.StorageProvider.OpenFilePickerAsync(new()
                {
                    Title = "Choose native library", AllowMultiple = false,
                    FileTypeFilter = [new FilePickerFileType("Native libraries") { Patterns = ["*.dll", "*.so", "*.dylib"] }]
                });
                if (files.FirstOrDefault()?.TryGetLocalPath() is { } path) input.Text = path;
            });
            Grid.SetColumn(browse, 1); row.Children.Add(browse); content.Children.Add(row);
        }
        var audible = new CheckBox { Content = "Enable audio for Play (Windows)", IsChecked = false };
        content.Children.Add(audible);
        var dialog = new Window { Title = "Fabric emulation", Width = 650, Height = 640 };
        var apply = new Button { Content = "Apply and play" }; content.Children.Add(apply);
        apply.Click += async (_, _) =>
        {
            var preferences = new NativeEmulationPreferences();
            foreach (var field in fields) field.SetValue(preferences, inputs[field.Name].Text?.Trim() ?? "");
            try { ConfigureNativePreview(preferences); _previewAudible = audible.IsChecked == true; await StartNativePreviewAsync(_previewAudible); dialog.Close(); }
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
        ClearPreviewRendering();
    }
}
