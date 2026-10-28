using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using System.Globalization;

namespace OasisEditor.Avalonia;

public sealed record AvaloniaPlayerPreferences
{
    public string ExecutablePath { get; init; } = string.Empty;
    public bool Fullscreen { get; init; }
    public int PreviewWidth { get; init; } = OasisPlayerLaunchService.DefaultPreviewWidth;
    public int PreviewHeight { get; init; } = OasisPlayerLaunchService.DefaultPreviewHeight;
}

public sealed record AvaloniaEditorPreferences
{
    public ProcessingPreferences Processing { get; init; } = new();
    public AvaloniaPlayerPreferences Player { get; init; } = new();

    public AvaloniaEditorPreferences Normalize()
    {
        ArgumentNullException.ThrowIfNull(Processing);
        ArgumentNullException.ThrowIfNull(Player);
        if (!Enum.IsDefined(Processing.CpuMode)) throw new ArgumentOutOfRangeException(nameof(Processing), "CPU mode is unsupported.");
        if (Player.PreviewWidth is < 1 or > 32768 || Player.PreviewHeight is < 1 or > 32768)
            throw new ArgumentOutOfRangeException(nameof(Player), "Player preview width and height must be from 1 to 32768 pixels.");
        var playerPath = Player.ExecutablePath?.Trim() ?? string.Empty;
        return this with
        {
            Processing = new ProcessingPreferences
            {
                CpuMode = Processing.CpuMode,
                CustomMaximumWorkers = Math.Clamp(Processing.CustomMaximumWorkers, 1, Math.Max(1, Environment.ProcessorCount))
            },
            Player = Player with { ExecutablePath = playerPath }
        };
    }
}

public sealed class AvaloniaEditorPreferencesStore
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, MaxDepth = 16, UnmappedMemberHandling=System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow };
    private readonly string _path;
    private string? _savedHash;

    public AvaloniaEditorPreferencesStore(string? path = null)
        => _path = Path.GetFullPath(path ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OasisEditor", "avalonia-preferences.json"));

    public AvaloniaEditorPreferences Load()
    {
        VerifyPath();
        if (!File.Exists(_path)) { _savedHash = null; return new AvaloniaEditorPreferences(); }
        if (new FileInfo(_path).Length > 512 * 1024) throw new InvalidDataException("Preferences file exceeds the 512 KiB limit.");
        var bytes = File.ReadAllBytes(_path);
        var json = System.Text.Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF');
        AvaloniaEditorPreferences value;
        try { value = JsonSerializer.Deserialize<AvaloniaEditorPreferences>(json, Json) ?? throw new InvalidDataException("Preferences file is empty."); }
        catch (JsonException error) { throw new InvalidDataException("Preferences file is malformed.", error); }
        try { var normalized=value.Normalize(); _savedHash = Hash(bytes); return normalized; }
        catch (ArgumentException error) { throw new InvalidDataException("Preferences file contains unsupported values.", error); }
    }

    public void Save(AvaloniaEditorPreferences preferences)
    {
        var normalized = (preferences ?? throw new ArgumentNullException(nameof(preferences))).Normalize();
        var directory = Path.GetDirectoryName(_path)!;
        VerifyPath();
        Directory.CreateDirectory(directory);
        VerifyPath();
        if (FileHash(_path) != _savedHash) throw new IOException("Preferences changed externally; reload before saving.");
        var temporary = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            var json = JsonSerializer.Serialize(normalized, Json);
            File.WriteAllText(temporary, json, new System.Text.UTF8Encoding(false));
            VerifyPath();
            if(FileHash(_path)!=_savedHash)throw new IOException("Preferences changed externally; reload before saving.");
            File.Move(temporary, _path, overwrite: true);
            _savedHash = Hash(System.Text.Encoding.UTF8.GetBytes(json));
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private void VerifyPath()
    {
        var file = new FileInfo(_path);
        if (file.LinkTarget is not null || file.Exists && file.Attributes.HasFlag(FileAttributes.ReparsePoint))
            throw new InvalidDataException("Preferences cannot be stored in a link.");
        if (file.Exists && file.Length > 512 * 1024) throw new InvalidDataException("Preferences file exceeds the 512 KiB limit.");
        var parent = Path.GetDirectoryName(_path)!;
        WorkspacePathPolicy.RejectRootReparsePoints(parent, requireExists: false);
    }

    private static string? FileHash(string path) => File.Exists(path) ? Hash(File.ReadAllBytes(path)) : null;
    private static string Hash(byte[] bytes) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes));
}

public sealed partial class WorkspaceView
{
    private AvaloniaEditorPreferencesStore? _preferencesStore;
    private AvaloniaEditorPreferences Preferences { get; set; } = new();
    public AvaloniaPlayerPreferences PlayerPreferences => Preferences.Player;
    public int AvailableLogicalProcessors => Math.Max(1, Environment.ProcessorCount);
    public int EffectiveProcessingWorkers => ImageProcessingExecutionPolicy.Resolve(Preferences.Processing, AvailableLogicalProcessors).MaxDegreeOfParallelism;

    private void InitializePreferences(MenuItem fileMenu)
    {
        if (!Program.IsHeadless && !_isAssetWindow)
        {
            _preferencesStore = new AvaloniaEditorPreferencesStore();
            try { Preferences = _preferencesStore.Load().Normalize(); }
            catch(Exception error) when(error is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException) { SetStatus("Preferences could not be loaded: "+error.Message); }
        }
        if(!_isAssetWindow)ImageProcessingExecutionPolicy.Configure(Preferences.Processing);
        var item = new MenuItem { Header = "Preferences…" };
        item.Click += async (_, _) => await ShowPreferencesAsync();
        fileMenu.Items.Add(item);
    }

    public AvaloniaEditorPreferences GetPreferences() => Preferences with
    {
        Processing = new ProcessingPreferences { CpuMode = Preferences.Processing.CpuMode, CustomMaximumWorkers = Preferences.Processing.CustomMaximumWorkers },
        Player = Preferences.Player with { }
    };

    /// <summary>Opt in to a preferences file inside the active workspace (used by explicit MCP requests).</summary>
    public void ConfigurePreferencesPersistence(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fullPath = Path.GetFullPath(path, _session.WorkspacePath);
        var relative = Path.GetRelativePath(_session.WorkspacePath, fullPath);
        var confined = WorkspacePathPolicy.ResolveConfined(_session.WorkspacePath, relative);
        WorkspacePathPolicy.RejectRootReparsePoints(Path.GetDirectoryName(confined)!, requireExists: false);
        var store = new AvaloniaEditorPreferencesStore(confined);
        var loaded = store.Load().Normalize();
        _preferencesStore = store;
        Preferences = loaded;
        ImageProcessingExecutionPolicy.Configure(Preferences.Processing);
        StateChanged?.Invoke();
    }

    public void UpdatePreferences(AvaloniaEditorPreferences preferences)
    {
        if(_documentOwner is {} owner){owner.UpdatePreferences(preferences);return;}
        var normalized = (preferences ?? throw new ArgumentNullException(nameof(preferences))).Normalize();
        _preferencesStore?.Save(normalized);
        Preferences = normalized;
        foreach(var entry in _nativeDocuments.Values)entry.Editor.Preferences=normalized;
        ImageProcessingExecutionPolicy.Configure(Preferences.Processing);
        StateChanged?.Invoke();
    }

    public void SetPlayerPreferences(AvaloniaPlayerPreferences player)
        => UpdatePreferences(Preferences with { Player = player ?? throw new ArgumentNullException(nameof(player)) });

    private async Task ShowPreferencesAsync()
    {
        if(_documentOwner is {} host){await host.ShowPreferencesAsync();return;}
        var current = Preferences;
        var mode = new ComboBox { ItemsSource = new[] { "Auto (Recommended)", "Maximum", "Custom" }, SelectedIndex = (int)current.Processing.CpuMode, MinWidth = 190 };
        var workers = new TextBox { Text = current.Processing.CustomMaximumWorkers.ToString(CultureInfo.InvariantCulture), MinWidth = 90 };
        var playerPath = new TextBox { Text = current.Player.ExecutablePath };
        var fullscreen = new CheckBox { Content = "Fullscreen", IsChecked = current.Player.Fullscreen };
        var width = new TextBox { Text = current.Player.PreviewWidth.ToString(CultureInfo.InvariantCulture), Width = 105 };
        var height = new TextBox { Text = current.Player.PreviewHeight.ToString(CultureInfo.InvariantCulture), Width = 105 };
        var custom = new StackPanel { Orientation = global::Avalonia.Layout.Orientation.Horizontal, Spacing = 8 };
        custom.Children.Add(new TextBlock { Text = "Maximum workers", VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center }); custom.Children.Add(workers);
        var playerBrowse = new Button { Content = "Browse" };
        playerBrowse.Click += async (_, _) => await BrowsePlayerExecutableAsync(playerPath);
        var playerRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 8 };
        playerRow.Children.Add(playerPath); Grid.SetColumn(playerBrowse, 1); playerRow.Children.Add(playerBrowse);
        var size = new StackPanel { Orientation = global::Avalonia.Layout.Orientation.Horizontal, Spacing = 8 };
        size.Children.Add(new TextBlock { Text = "Width", VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center }); size.Children.Add(width);
        size.Children.Add(new TextBlock { Text = "Height", VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center }); size.Children.Add(height);
        var error = new TextBlock { TextWrapping = global::Avalonia.Media.TextWrapping.Wrap };
        var body = new StackPanel { Margin = new Thickness(18), Spacing = 10 };
        body.Children.Add(Section("Processing"));
        body.Children.Add(new TextBlock { Text = "CPU image processing mode" }); body.Children.Add(mode); body.Children.Add(custom);
        body.Children.Add(new TextBlock { Text = $"Available logical processors: {AvailableLogicalProcessors}" });
        body.Children.Add(new TextBlock { Text = "Auto reserves CPU capacity for system responsiveness. Maximum uses all available processors." });
        body.Children.Add(Section("Player"));
        body.Children.Add(new TextBlock { Text = "Oasis Player executable path" }); body.Children.Add(playerRow); body.Children.Add(fullscreen); body.Children.Add(size);
        body.Children.Add(new TextBlock { Text = "Windowed preview defaults to 1280 × 800. Fullscreen passes the fullscreen launch option." });
        body.Children.Add(error);
        var apply = new Button { Content = "Apply", HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Right, MinWidth = 100 };
        body.Children.Add(apply);
        mode.SelectionChanged += (_, _) => custom.IsEnabled = mode.SelectedIndex == (int)CpuImageProcessingMode.Custom;
        custom.IsEnabled = mode.SelectedIndex == (int)CpuImageProcessingMode.Custom;
        var dialog = new Window { Title = "Oasis Editor Preferences", Width = 600, Height = 560, Content = new ScrollViewer { Content = body }, CanResize = true };
        if (TopLevel.GetTopLevel(this) is not Window owner) return;
        apply.Click += (_, _) =>
        {
            try
            {
                var cpuMode = mode.SelectedIndex switch { 1 => CpuImageProcessingMode.Maximum, 2 => CpuImageProcessingMode.Custom, _ => CpuImageProcessingMode.Auto };
                var workerCount = int.TryParse(workers.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedWorkers)
                    ? parsedWorkers : throw new ArgumentException("Maximum workers must be a whole number.");
                var previewWidth = ReadPositive(width, "Preview width");
                var previewHeight = ReadPositive(height, "Preview height");
                UpdatePreferences(new AvaloniaEditorPreferences
                {
                    Processing = new ProcessingPreferences { CpuMode = cpuMode, CustomMaximumWorkers = workerCount },
                    Player = new AvaloniaPlayerPreferences { ExecutablePath = playerPath.Text ?? string.Empty, Fullscreen = fullscreen.IsChecked == true, PreviewWidth = previewWidth, PreviewHeight = previewHeight }
                });
                SetStatus($"Preferences saved • {EffectiveProcessingWorkers} processing workers.");
                dialog.Close();
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or IOException or UnauthorizedAccessException)
            { error.Text = exception.Message; }
        };
        await dialog.ShowDialog(owner);

        static TextBlock Section(string text) => new() { Text = text.ToUpperInvariant(), FontWeight = global::Avalonia.Media.FontWeight.SemiBold, Margin = new Thickness(0, 8, 0, 0) };
        static int ReadPositive(TextBox box, string label)
            => int.TryParse(box.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value > 0 && value <= 32768
                ? value : throw new ArgumentException($"{label} must be a whole number from 1 to 32768.");
    }

    private async Task BrowsePlayerExecutableAsync(TextBox target)
    {
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { CanOpen: true } storage) return;
        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select Oasis Player executable",
            FileTypeFilter = [new FilePickerFileType("Oasis Player executable") { Patterns = OperatingSystem.IsWindows() ? ["*.exe"] : ["*"] }]
        });
        if (files.FirstOrDefault()?.TryGetLocalPath() is { } path) target.Text = path;
    }
}
