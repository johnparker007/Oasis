using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using OasisEditor.Progress;

namespace OasisEditor.Avalonia;

public sealed partial class WorkspaceView
{
    private void AddMachineBuildExportMenus(MenuItem fileMenu)
    {
        AddMenu(fileMenu, "Build Player machine…", async () => await BuildMachineAsync(launchAfterBuild: false));
        AddMenu(fileMenu, "Export portable Player package…", async () => await ExportMachineAsync());
        AddMenu(fileMenu, "Launch in Oasis Player…", async () => await BuildMachineAsync(launchAfterBuild: true));
        AddMenu(fileMenu, "Set Oasis Library folder…", async () => await ChooseLibraryRootAsync());
    }

    private async Task BuildMachineAsync(bool launchAfterBuild)
    {
        try
        {
            var (project, machinePath) = GetBuildInputs();
            string? executable = null;
            string? fabricRuntimeDll = null;
            string? amberImpactDll = null;
            if (launchAfterBuild)
            {
                var runtime = Machines.Document?.Runtime ?? throw new InvalidOperationException("Open a Machine before launching Oasis Player.");
                if (runtime is EmulationRuntimeDefinition emulation && emulation.Platform != FruitMachinePlatformType.Impact)
                    throw new InvalidOperationException($"Oasis Player preview currently supports Oasis behavior and JPM Impact. The authored {emulation.Platform} runtime has no Player backend yet.");
                executable = string.IsNullOrWhiteSpace(PlayerPreferences.ExecutablePath)?await ChoosePlayerExecutableAsync():PlayerPreferences.ExecutablePath;
                if (string.IsNullOrWhiteSpace(executable)) return;
                if(executable!=PlayerPreferences.ExecutablePath)SetPlayerPreferences(PlayerPreferences with {ExecutablePath=executable});
                if (runtime is EmulationRuntimeDefinition)
                {
                    fabricRuntimeDll = await ChooseNativeLibraryAsync("Choose the Fabric runtime DLL");
                    if (string.IsNullOrWhiteSpace(fabricRuntimeDll)) return;
                    amberImpactDll = await ChooseNativeLibraryAsync("Choose the Amber Impact DLL");
                    if (string.IsNullOrWhiteSpace(amberImpactDll)) return;
                }
            }
            var result = await RunWithProgressAsync((reporter, token) => Task.Run(() =>
                new MachineRuntimeBuildService(libraryRoot: string.IsNullOrWhiteSpace(SceneLibraryRoot)?GetLibraryRoot():SceneLibraryRoot).BuildFromMachineDocument(project, machinePath, reporter, token), token));
            if (!result.Success)
            {
                SetStatus(result.ErrorMessage ?? "Machine build failed.");
                return;
            }
            SetStatus($"Oasis Player machine built: {result.BuildRoot}");
            if (!launchAfterBuild) return;
            var launch = new OasisPlayerLaunchService().Launch(new OasisPlayerLaunchRequest(executable!, result.BuildRoot!, PlayerPreferences.Fullscreen,
                PlayerPreferences.PreviewWidth, PlayerPreferences.PreviewHeight, fabricRuntimeDll, amberImpactDll));
            SetStatus(launch.Success ? $"Launched Oasis Player at {launch.ExecutablePath}." : launch.ErrorMessage!);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or ArgumentException or InvalidOperationException or UnauthorizedAccessException or JsonException)
        {
            SetStatus(exception.Message);
        }
        catch (OperationCanceledException) { SetStatus("Machine build cancelled."); }
    }

    private async Task ExportMachineAsync()
    {
        try
        {
            var (project, machinePath) = GetBuildInputs();
            if (TopLevel.GetTopLevel(this)?.StorageProvider is not { CanPickFolder: true } storage) throw new InvalidOperationException("This platform does not provide folder selection.");
            var folders = await storage.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "Choose a folder for the portable Player package", AllowMultiple = false });
            var destination = folders.FirstOrDefault()?.TryGetLocalPath();
            if (string.IsNullOrWhiteSpace(destination)) return;
            var result = await RunWithProgressAsync((reporter, token) => Task.Run(() =>
                new MachineRuntimePackageExportService(new MachineRuntimeBuildService(libraryRoot: string.IsNullOrWhiteSpace(SceneLibraryRoot)?GetLibraryRoot():SceneLibraryRoot))
                    .Export(project, machinePath, destination, reporter, token), token));
            SetStatus(result.Success ? $"Portable Player package exported to {result.PackageDirectory}." : result.ErrorMessage!);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or ArgumentException or InvalidOperationException or UnauthorizedAccessException or JsonException)
        {
            SetStatus(exception.Message);
        }
        catch (OperationCanceledException) { SetStatus("Machine export cancelled."); }
    }

    private (EditorProject Project, string MachinePath) GetBuildInputs()
    {
        if (!_machineActive) throw new InvalidOperationException("Open a Machine before building or exporting for Oasis Player.");
        if (ProjectHasUnsavedChanges) throw new InvalidOperationException("Save or undo all open document changes before building or exporting.");
        if (!Machines.IsProjectCurrent) throw new InvalidOperationException("The Machine is no longer bound to the active project. Reopen it before building.");
        Machines.EnsureSavedSourceUnchanged();
        var projectFile = _session.ProjectFilePath ?? throw new InvalidOperationException("Open an Oasis project before building.");
        var machinePath = Machines.MachinePath ?? throw new InvalidOperationException("Open or create a Machine before building.");
        var projectDirectory = Path.GetDirectoryName(Path.GetFullPath(projectFile))!;
        return (new EditorProject
        {
            Name = _session.ProjectName ?? Path.GetFileName(projectDirectory),
            ProjectFilePath = Path.GetFullPath(projectFile),
            ProjectDirectory = projectDirectory,
            AssetsDirectory = Path.Combine(projectDirectory, "Assets"),
            GeneratedDirectory = Path.Combine(projectDirectory, "Generated")
        }, machinePath);
    }

    private async Task<string?> ChoosePlayerExecutableAsync()
    {
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { CanOpen: true } storage) throw new InvalidOperationException("This platform does not provide file selection.");
        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Choose the Oasis Player executable",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("Oasis Player executable") { Patterns = OperatingSystem.IsWindows() ? ["*.exe"] : ["*"] }]
        });
        return files.FirstOrDefault()?.TryGetLocalPath();
    }

    private async Task<string?> ChooseNativeLibraryAsync(string title)
    {
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { CanOpen: true } storage) throw new InvalidOperationException("This platform does not provide file selection.");
        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("Native runtime library") { Patterns = ["*.dll"] }]
        });
        return files.FirstOrDefault()?.TryGetLocalPath();
    }

    private async Task ChooseLibraryRootAsync()
    {
        try
        {
            if (TopLevel.GetTopLevel(this)?.StorageProvider is not { CanPickFolder: true } storage) throw new InvalidOperationException("This platform does not provide folder selection.");
            var folders = await storage.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "Choose the Oasis Asset Library folder", AllowMultiple = false });
            var path = folders.FirstOrDefault()?.TryGetLocalPath();
            if (string.IsNullOrWhiteSpace(path)) return;
            SetSceneLibraryRoot(path); // Validate pending Library edits and the root before persisting preferences.
            var settingsPath = LibrarySettingsPath();
            Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
            File.WriteAllText(settingsPath, Path.GetFullPath(path));
            SetStatus($"Oasis Library folder set to {Path.GetFullPath(path)}.");
        }
        catch(Exception error) when(error is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {SetStatus(error.Message);}
    }

    private static string GetLibraryRoot()
    {
        var settings = LibrarySettingsPath();
        if (File.Exists(settings))
        {
            var configured = File.ReadAllText(settings).Trim();
            if (Directory.Exists(configured)) return Path.GetFullPath(configured);
        }
        var wpfSettings = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OasisEditor", "editor-preferences.json");
        try
        {
            using var json = JsonDocument.Parse(File.ReadAllText(wpfSettings));
            if (TryGetPropertyIgnoreCase(json.RootElement, "assetLibrary", out var library) && TryGetPropertyIgnoreCase(library, "rootPath", out var path)
                && path.GetString() is { } value && Directory.Exists(value)) return Path.GetFullPath(value);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException) { }
        var fallback = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Oasis Library");
        return Directory.Exists(fallback) ? Path.GetFullPath(fallback) : string.Empty;
    }

    private static string LibrarySettingsPath() => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Oasis", "AvaloniaEditor", "library-root.txt");

    private static bool TryGetPropertyIgnoreCase(JsonElement element, string property, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object)
            foreach (var candidate in element.EnumerateObject())
                if (string.Equals(candidate.Name, property, StringComparison.OrdinalIgnoreCase)) { value = candidate.Value; return true; }
        value = default;
        return false;
    }

    private async Task<T> RunWithProgressAsync<T>(Func<IEditorProgressReporter, CancellationToken, Task<T>> work)
    {
        using var cancellation = new CancellationTokenSource();
        var progressWindow = new BuildProgressWindow(cancellation);
        var owner = TopLevel.GetTopLevel(this) as Window
            ?? throw new InvalidOperationException("Build and export require an active editor window.");
        var dialogTask = progressWindow.ShowDialog(owner);
        var reporter = new AvaloniaBuildProgressReporter(progressWindow.Update);
        try
        {
            return await work(reporter, cancellation.Token);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            SetStatus("Build or export cancelled.");
            throw;
        }
        finally
        {
            progressWindow.CloseAfterWork();
            await dialogTask;
        }
    }

    private static void AddMenu(MenuItem parent, string label, Func<Task> action)
    {
        var item = new MenuItem { Header = label };
        item.Click += async (_, _) => await action();
        parent.Items.Add(item);
    }

    private sealed class BuildProgressWindow : Window
    {
        private readonly TextBlock _message = new() { Text = "Preparing…", Margin = new global::Avalonia.Thickness(0, 0, 0, 8) };
        private readonly ProgressBar _progress = new() { Minimum = 0, Maximum = 1, Width = 390 };
        private readonly CancellationTokenSource _cancellation;
        private bool _workCompleted;
        public BuildProgressWindow(CancellationTokenSource cancellation)
        {
            _cancellation = cancellation;
            Title = "Oasis Player build"; Width = 450; Height = 145; CanResize = false;
            Closing += (_, args) => { if (!_workCompleted) args.Cancel = true; };
            var cancel = new Button { Content = "Cancel", HorizontalAlignment = HorizontalAlignment.Right, Margin = new global::Avalonia.Thickness(0, 10, 0, 0), MinWidth = 80 };
            cancel.Click += (_, _) => { cancel.IsEnabled = false; _message.Text = "Cancelling…"; _cancellation.Cancel(); };
            Content = new StackPanel { Margin = new global::Avalonia.Thickness(18), Spacing = 4, Children = { _message, _progress, cancel } };
        }
        public void Update(double value, string message) => Dispatcher.UIThread.Post(() =>
        { _progress.IsIndeterminate = double.IsNaN(value); if (!double.IsNaN(value)) _progress.Value = Math.Clamp(value, 0, 1); _message.Text = message; });
        public void CloseAfterWork() { _workCompleted = true; Close(); }
    }

    private sealed class AvaloniaBuildProgressReporter(Action<double, string> update, double start = 0, double end = 1, string? prefix = null) : IEditorProgressReporter
    {
        public void Report(double normalizedProgress, string message) => update(start + (end - start) * Math.Clamp(normalizedProgress, 0, 1), Prefix(message));
        public void ReportIndeterminate(string message) => update(double.NaN, Prefix(message));
        public void ReportMessage(string message) => update(start, Prefix(message));
        public IEditorProgressReporter CreateChild(double childStart, double childEnd, string? defaultMessagePrefix = null)
            => new AvaloniaBuildProgressReporter(update, start + (end - start) * childStart, start + (end - start) * childEnd, defaultMessagePrefix ?? prefix);
        private string Prefix(string message) => string.IsNullOrWhiteSpace(prefix) ? message : $"{prefix}: {message}";
    }
}
