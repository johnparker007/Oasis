using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace OasisEditor;

public sealed class AssetBrowserViewModel : IDisposable
{
    private readonly Func<EditorProject?> _loadedProjectAccessor;
    private readonly Func<string> _libraryRootAccessor;
    private readonly Action _selectionChanged;
    private readonly Action _notifyInspectorChanged;
    private readonly Action<string, OutputLogStatus> _addOutputEntry;
    private readonly Action<AssetBrowserItemViewModel?> _openAsset;
    private readonly Func<string, string?> _requestAssetRename;
    private readonly Func<IReadOnlyList<AssetBrowserItemViewModel>, bool> _confirmAssetDelete;
    private readonly List<AssetBrowserItemViewModel> _selectedAssets = new();
    private AssetBrowserItemViewModel? _selectedAsset;
    private AssetDirectoryNodeViewModel? _selectedDirectory;
    private FileSystemWatcher? _assetsWatcher;
    private FileSystemWatcher? _libraryWatcher;
    private string? _watchedAssetsDirectory;
    private readonly DispatcherTimer _refreshDebounceTimer;
    public event Action? StateChanged;
    public event Action? AssetCatalogChanged;

    public AssetBrowserViewModel(
        Func<EditorProject?> loadedProjectAccessor,
        Action selectionChanged,
        Action notifyInspectorChanged,
        Action<string, OutputLogStatus> addOutputEntry,
        Action<AssetBrowserItemViewModel?> openAsset,
        Func<string, string?> requestAssetRename,
        Func<IReadOnlyList<AssetBrowserItemViewModel>, bool> confirmAssetDelete,
        Func<string>? libraryRootAccessor = null)
    {
        _loadedProjectAccessor = loadedProjectAccessor;
        _selectionChanged = selectionChanged;
        _notifyInspectorChanged = notifyInspectorChanged;
        _addOutputEntry = addOutputEntry;
        _openAsset = openAsset;
        _requestAssetRename = requestAssetRename;
        _confirmAssetDelete = confirmAssetDelete;
        _libraryRootAccessor = libraryRootAccessor ?? (() => string.Empty);
        _refreshDebounceTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(200)
        };
        _refreshDebounceTimer.Tick += OnRefreshDebounceTimerTick;

        AssetBrowserItems = new ObservableCollection<AssetBrowserItemViewModel>();
        AssetDirectoryTree = new ObservableCollection<AssetDirectoryNodeViewModel>();
        RefreshAssetBrowserCommand = new RelayCommand(RefreshAssetBrowserPreservingState, CanRefreshAssetBrowser);
        OpenAssetCommand = new PaneItemCommand<object>(
            GetSelectedAssetContext,
            OpenAsset,
            CanOpenAssetContext);
        ShowInExplorerCommand = new PaneItemCommand<object>(
            GetSelectedAssetContext,
            ShowInExplorer,
            CanOpenAssetContext);
        RenameAssetCommand = new PaneItemCommand<object>(
            GetSelectedAssetContext,
            RenameAsset,
            CanRenameProjectAssetContext);
        DeleteAssetCommand = new PaneItemCommand<object>(
            GetSelectedAssetContext,
            DeleteAsset,
            CanDeleteProjectAssetContext);
        CopyToLibraryCommand = new PaneItemCommand<object>(GetSelectedAssetContext, CopyToLibrary, CanCopyToLibrary);
    }

    public ObservableCollection<AssetBrowserItemViewModel> AssetBrowserItems { get; }
    public ObservableCollection<AssetDirectoryNodeViewModel> AssetDirectoryTree { get; }
    public ICommand RefreshAssetBrowserCommand { get; }
    public ICommand OpenAssetCommand { get; }
    public ICommand ShowInExplorerCommand { get; }
    public ICommand RenameAssetCommand { get; }
    public ICommand DeleteAssetCommand { get; }
    public ICommand CopyToLibraryCommand { get; }
    public IReadOnlyList<AssetBrowserItemViewModel> SelectedAssets => _selectedAssets;

    public AssetDirectoryNodeViewModel? SelectedDirectory
    {
        get => _selectedDirectory;
        set
        {
            if (ReferenceEquals(_selectedDirectory, value))
            {
                return;
            }

            _selectedDirectory = value;
            UpdateDirectorySelectionState(value);
            RefreshDirectoryContents();
            NotifyOpenAssetCommand();
            NotifyAssetContextCommands();
            StateChanged?.Invoke();
        }
    }

    public AssetBrowserItemViewModel? SelectedAsset
    {
        get => _selectedAsset;
        set
        {
            if (ReferenceEquals(_selectedAsset, value))
            {
                return;
            }

            _selectedAsset = value;
            _selectionChanged();
            if (value is not null && !value.IsDirectory)
            {
                _notifyInspectorChanged();
            }
            NotifyRefreshCommand();
            NotifyOpenAssetCommand();
            NotifyAssetContextCommands();
            StateChanged?.Invoke();
        }
    }

    public void SetSelectedAssets(IEnumerable<AssetBrowserItemViewModel> assets)
    {
        var selected = assets.Where(AssetBrowserItems.Contains).ToArray();
        _selectedAssets.Clear();
        _selectedAssets.AddRange(selected);

        var primarySelection = _selectedAssets.Count == 0
            ? null
            : _selectedAssets.Contains(SelectedAsset!) ? SelectedAsset : _selectedAssets[^1];
        SelectedAsset = primarySelection;
        NotifyOpenAssetCommand();
        NotifyAssetContextCommands();
    }

    private bool CanRefreshAssetBrowser()
    {
        return _loadedProjectAccessor() is not null;
    }

    public void RefreshAssetBrowser()
    {
        RefreshAssetBrowserPreservingState();
    }

    public void RefreshAssetBrowserPreservingState()
    {
        var selectedDirectoryPath = SelectedDirectory?.FullPath;
        var selectedAssetPaths = CaptureSelectedAssetPaths();
        var expandedDirectoryPaths = CaptureExpandedDirectoryPaths();

        var loadedProject = _loadedProjectAccessor();
        if (loadedProject is null)
        {
            AssetDirectoryTree.Clear();
            AssetBrowserItems.Clear();
            SelectedDirectory = null;
            SelectedAsset = null;
            _notifyInspectorChanged();
            StopWatchingAssetsDirectory();
            StopWatchingLibraryDirectory();
            _addOutputEntry("Asset browser cleared (no project loaded).", OutputLogStatus.Info);
            AssetCatalogChanged?.Invoke();
            return;
        }

        var assetDirectory = loadedProject.AssetsDirectory;
        if (!Directory.Exists(assetDirectory))
        {
            Directory.CreateDirectory(assetDirectory);
        }

        StartWatchingAssetsDirectory(assetDirectory);
        var libraryRoot = _libraryRootAccessor();
        if (!string.IsNullOrWhiteSpace(libraryRoot))
        {
            Directory.CreateDirectory(libraryRoot);
            StartWatchingLibraryDirectory(libraryRoot);
        }
        else StopWatchingLibraryDirectory();

        var rootNode = BuildDirectoryTree(assetDirectory, assetDirectory);
        RestoreExpandedDirectoryPaths(rootNode, expandedDirectoryPaths);
        AssetDirectoryTree.Clear();
        AssetDirectoryTree.Add(rootNode);
        AssetDirectoryNodeViewModel? libraryNode = null;
        if (!string.IsNullOrWhiteSpace(libraryRoot))
        {
            libraryNode = BuildDirectoryTree(libraryRoot, libraryRoot, "Library");
            RestoreExpandedDirectoryPaths(libraryNode, expandedDirectoryPaths);
            AssetDirectoryTree.Add(libraryNode);
        }
        SelectedDirectory = AssetDirectoryTree.Select(root => FindDirectoryByPath(root, selectedDirectoryPath)).FirstOrDefault(node => node is not null)
            ?? AssetDirectoryTree.Select(root => FindNearestExistingDirectory(root, selectedDirectoryPath)).FirstOrDefault(node => node is not null)
            ?? rootNode;
        RestoreSelectedAssets(selectedAssetPaths);
        _notifyInspectorChanged();
        _addOutputEntry($"Asset browser refreshed ({AssetBrowserItems.Count} items).", OutputLogStatus.Info);
        StateChanged?.Invoke();
        AssetCatalogChanged?.Invoke();
    }


    public void ScheduleRefreshFromDisk()
    {
        var dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
        if (dispatcher.CheckAccess())
        {
            RestartRefreshDebounceTimer();
            return;
        }

        _ = dispatcher.BeginInvoke((Action)RestartRefreshDebounceTimer);
    }

    public void Dispose()
    {
        StopWatchingAssetsDirectory();
        StopWatchingLibraryDirectory();
        _refreshDebounceTimer.Stop();
        _refreshDebounceTimer.Tick -= OnRefreshDebounceTimerTick;
    }

    private void StartWatchingAssetsDirectory(string assetsDirectory)
    {
        var fullAssetsDirectory = Path.GetFullPath(assetsDirectory);
        if (_assetsWatcher is not null
            && string.Equals(_watchedAssetsDirectory, fullAssetsDirectory, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        StopWatchingAssetsDirectory();

        _assetsWatcher = new FileSystemWatcher(fullAssetsDirectory)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.DirectoryName
                           | NotifyFilters.FileName
                           | NotifyFilters.LastWrite
                           | NotifyFilters.CreationTime
        };
        _assetsWatcher.Created += OnAssetsWatcherChanged;
        _assetsWatcher.Deleted += OnAssetsWatcherChanged;
        _assetsWatcher.Changed += OnAssetsWatcherChanged;
        _assetsWatcher.Renamed += OnAssetsWatcherRenamed;
        _assetsWatcher.EnableRaisingEvents = true;
        _watchedAssetsDirectory = fullAssetsDirectory;
    }

    private void StopWatchingAssetsDirectory()
    {
        if (_assetsWatcher is null)
        {
            _watchedAssetsDirectory = null;
            return;
        }

        _assetsWatcher.EnableRaisingEvents = false;
        _assetsWatcher.Created -= OnAssetsWatcherChanged;
        _assetsWatcher.Deleted -= OnAssetsWatcherChanged;
        _assetsWatcher.Changed -= OnAssetsWatcherChanged;
        _assetsWatcher.Renamed -= OnAssetsWatcherRenamed;
        _assetsWatcher.Dispose();
        _assetsWatcher = null;
        _watchedAssetsDirectory = null;
    }

    private void StartWatchingLibraryDirectory(string libraryRoot)
    {
        var fullRoot = Path.GetFullPath(libraryRoot);
        if (_libraryWatcher is not null && string.Equals(_libraryWatcher.Path, fullRoot, StringComparison.OrdinalIgnoreCase)) return;
        StopWatchingLibraryDirectory();
        _libraryWatcher = new FileSystemWatcher(fullRoot)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.DirectoryName | NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime,
            EnableRaisingEvents = true
        };
        _libraryWatcher.Created += OnAssetsWatcherChanged;
        _libraryWatcher.Deleted += OnAssetsWatcherChanged;
        _libraryWatcher.Changed += OnAssetsWatcherChanged;
        _libraryWatcher.Renamed += OnAssetsWatcherRenamed;
    }

    private void StopWatchingLibraryDirectory()
    {
        if (_libraryWatcher is null) return;
        _libraryWatcher.EnableRaisingEvents = false;
        _libraryWatcher.Created -= OnAssetsWatcherChanged;
        _libraryWatcher.Deleted -= OnAssetsWatcherChanged;
        _libraryWatcher.Changed -= OnAssetsWatcherChanged;
        _libraryWatcher.Renamed -= OnAssetsWatcherRenamed;
        _libraryWatcher.Dispose();
        _libraryWatcher = null;
    }

    private void OnAssetsWatcherChanged(object sender, FileSystemEventArgs e) => ScheduleRefreshFromDisk();

    private void OnAssetsWatcherRenamed(object sender, RenamedEventArgs e) => ScheduleRefreshFromDisk();

    private void RestartRefreshDebounceTimer()
    {
        _refreshDebounceTimer.Stop();
        _refreshDebounceTimer.Start();
    }

    private void OnRefreshDebounceTimerTick(object? sender, EventArgs e)
    {
        _refreshDebounceTimer.Stop();
        RefreshAssetBrowserPreservingState();
    }

    public void NotifyRefreshCommand()
    {
        if (RefreshAssetBrowserCommand is RelayCommand refreshRelayCommand)
        {
            refreshRelayCommand.RaiseCanExecuteChanged();
        }
    }

    private AssetDirectoryNodeViewModel BuildDirectoryTree(string assetsRoot, string directoryPath, string? rootLabel = null)
    {
        var displayPath = string.Equals(assetsRoot, directoryPath, StringComparison.OrdinalIgnoreCase) && rootLabel is not null ? rootLabel : GetDirectoryDisplayPath(assetsRoot, directoryPath);
        var node = new AssetDirectoryNodeViewModel(displayPath, directoryPath);

        var childDirectories = Directory.GetDirectories(directoryPath, "*", SearchOption.TopDirectoryOnly)
            .Where(path => IsPathInsideRoot(assetsRoot, path))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase);

        foreach (var childDirectory in childDirectories)
        {
            node.Children.Add(BuildDirectoryTree(assetsRoot, childDirectory));
        }

        return node;
    }

    private void RefreshDirectoryContents()
    {
        var selectedAssetPaths = CaptureSelectedAssetPaths();
        AssetBrowserItems.Clear();

        var loadedProject = _loadedProjectAccessor();
        if (loadedProject is null || SelectedDirectory is null)
        {
            SelectedAsset = null;
            return;
        }

        var assetsRoot = RootForPath(loadedProject, SelectedDirectory.FullPath);
        if (assetsRoot is null)
        {
            SelectedAsset = null;
            _addOutputEntry("Selected directory is outside the Assets root and was ignored.", OutputLogStatus.Warning);
            return;
        }

        var childDirectories = Directory.GetDirectories(SelectedDirectory.FullPath, "*", SearchOption.TopDirectoryOnly)
            .Where(path => IsPathInsideRoot(assetsRoot, path))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase);
        foreach (var directory in childDirectories)
        {
            AssetBrowserItems.Add(new AssetBrowserItemViewModel(
                displayPath: Path.GetFileName(directory),
                fullPath: directory,
                isDirectory: true));
        }

        var files = Directory.GetFiles(SelectedDirectory.FullPath, "*", SearchOption.TopDirectoryOnly)
            .Where(path => IsPathInsideRoot(assetsRoot, path))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase);
        foreach (var file in files)
        {
            AssetBrowserItems.Add(new AssetBrowserItemViewModel(
                displayPath: Path.GetFileName(file),
                fullPath: file,
                isDirectory: false));
        }

        RestoreSelectedAssets(selectedAssetPaths);
        StateChanged?.Invoke();
    }

    private object? GetSelectedAssetContext()
    {
        return _selectedAssets.Count > 0
            ? new AssetCommandSelection(_selectedAssets.ToArray())
            : SelectedAsset ?? (object?)SelectedDirectory;
    }

    private static IReadOnlyList<AssetBrowserItemViewModel> ToAssetContextItems(object? context)
    {
        if (context is AssetCommandSelection selection)
        {
            return selection.Items;
        }

        var item = ToAssetContextItem(context);
        return item is null ? Array.Empty<AssetBrowserItemViewModel>() : new[] { item };
    }

    private static AssetBrowserItemViewModel? ToAssetContextItem(object? context)
    {
        return context switch
        {
            AssetBrowserItemViewModel assetItem => assetItem,
            AssetDirectoryNodeViewModel directoryNode => new AssetBrowserItemViewModel(
                directoryNode.DisplayPath,
                directoryNode.FullPath,
                isDirectory: true),
            _ => null
        };
    }

    private void OpenAsset(object context)
    {
        var assets = ToAssetContextItems(context);
        foreach (var asset in assets)
        {
            OpenSingleAsset(asset);
        }
    }

    private void OpenSingleAsset(AssetBrowserItemViewModel asset)
    {
        if (asset.IsDirectory)
        {
            if (!Directory.Exists(asset.FullPath))
            {
                _addOutputEntry($"Cannot open folder; path does not exist: {asset.FullPath}", OutputLogStatus.Warning);
                return;
            }

            SelectDirectoryByPath(asset.FullPath);
            return;
        }

        if (!File.Exists(asset.FullPath))
        {
            _addOutputEntry($"Cannot open asset; path does not exist: {asset.FullPath}", OutputLogStatus.Warning);
            return;
        }

        SelectedAsset = asset;
        _openAsset(asset);
    }

    private static bool CanOpenAssetContext(object context)
    {
        var assets = ToAssetContextItems(context);
        return assets.Count > 0 && assets.All(asset => asset.IsDirectory
            ? Directory.Exists(asset.FullPath)
            : File.Exists(asset.FullPath));
    }

    private void NotifyOpenAssetCommand()
    {
        if (OpenAssetCommand is PaneItemCommand<object> openAssetCommand)
        {
            openAssetCommand.RaiseCanExecuteChanged();
        }
    }

    private void NotifyAssetContextCommands()
    {
        if (ShowInExplorerCommand is PaneItemCommand<object> showInExplorerCommand)
        {
            showInExplorerCommand.RaiseCanExecuteChanged();
        }

        if (RenameAssetCommand is PaneItemCommand<object> renameAssetCommand)
        {
            renameAssetCommand.RaiseCanExecuteChanged();
        }

        if (DeleteAssetCommand is PaneItemCommand<object> deleteAssetCommand)
        {
            deleteAssetCommand.RaiseCanExecuteChanged();
        }
        if (CopyToLibraryCommand is PaneItemCommand<object> copyCommand) copyCommand.RaiseCanExecuteChanged();
    }

    private string? RootForPath(EditorProject project, string path)
    {
        if (IsPathInsideRoot(project.AssetsDirectory, path)) return project.AssetsDirectory;
        var library = _libraryRootAccessor();
        return !string.IsNullOrWhiteSpace(library) && IsPathInsideRoot(library, path) ? library : null;
    }

    private bool CanCopyToLibrary(object context)
    {
        var project = _loadedProjectAccessor();
        var items = ToAssetContextItems(context);
        var item = items.Count == 1 ? items[0] : null;
        return project is not null && item is not null && IsPathInsideRoot(project.AssetsDirectory, item.FullPath) && TryGetReusablePackage(item.FullPath, out _, out _);
    }

    private void CopyToLibrary(object context)
    {
        var item = ToAssetContextItems(context).Single();
        if (!TryGetReusablePackage(item.FullPath, out var package, out var typeFolder)) return;
        var libraryRoot = _libraryRootAccessor();
        if (string.IsNullOrWhiteSpace(libraryRoot)) { _addOutputEntry("Configure the Oasis Library root in Preferences before copying an asset.", OutputLogStatus.Warning); return; }
        var packageName = Path.GetFileName(package);
        var destination = Path.Combine(libraryRoot, typeFolder, packageName);
        if (Directory.Exists(destination))
        {
            var requested = _requestAssetRename(packageName);
            if (string.IsNullOrWhiteSpace(requested)) return;
            packageName = new ProjectAssetPathService().SanitizePathSegment(requested);
            destination = Path.Combine(libraryRoot, typeFolder, packageName);
            if (Directory.Exists(destination)) { _addOutputEntry($"Library package already exists: {destination}", OutputLogStatus.Warning); return; }
        }
        CopyDirectory(package, destination);
        _addOutputEntry($"Copied '{Path.GetFileName(package)}' to Oasis Library {typeFolder} as '{packageName}'.", OutputLogStatus.Info);
        RefreshAssetBrowserPreservingState();
    }

    private static bool TryGetReusablePackage(string path, out string package, out string typeFolder)
    {
        package = Directory.Exists(path) ? path : Path.GetDirectoryName(path) ?? string.Empty;
        if (File.Exists(Path.Combine(package, ProjectAssetPathService.Cabinet3DManifestFileName))) { typeFolder = "Cabinets"; return true; }
        if (File.Exists(Path.Combine(package, ProjectAssetPathService.ReelManifestFileName))) { typeFolder = "Reels"; return true; }
        typeFolder = string.Empty; return false;
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories)) Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, directory)));
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories)) File.Copy(file, Path.Combine(destination, Path.GetRelativePath(source, file)), false);
    }

    private void ShowInExplorer(object context)
    {
        var assets = ToAssetContextItems(context);
        if (assets.Count == 0)
        {
            return;
        }

        if (assets.Count > 1)
        {
            ShowDirectoryInExplorer(Path.GetDirectoryName(assets[0].FullPath)!, "selected assets");
            return;
        }

        var asset = assets[0];

        if (asset.IsDirectory)
        {
            if (!Directory.Exists(asset.FullPath))
            {
                _addOutputEntry($"Cannot show folder in Explorer; path does not exist: {asset.FullPath}", OutputLogStatus.Warning);
                return;
            }

            ShowDirectoryInExplorer(asset.FullPath, asset.DisplayPath);

            return;
        }

        if (!File.Exists(asset.FullPath))
        {
            _addOutputEntry($"Cannot show file in Explorer; path does not exist: {asset.FullPath}", OutputLogStatus.Warning);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{asset.FullPath}\"")
            {
                UseShellExecute = true
            });
            _addOutputEntry($"Selected file in Explorer: {asset.DisplayPath}", OutputLogStatus.Info);
        }
        catch (Exception ex)
        {
            _addOutputEntry($"Failed to show file in Explorer: {ex.Message}", OutputLogStatus.Warning);
        }
    }

    private void ShowDirectoryInExplorer(string directoryPath, string description)
    {
        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{directoryPath}\"") { UseShellExecute = true });
            _addOutputEntry($"Opened folder in Explorer: {description}", OutputLogStatus.Info);
        }
        catch (Exception ex)
        {
            _addOutputEntry($"Failed to open folder in Explorer: {ex.Message}", OutputLogStatus.Warning);
        }
    }

    private void RenameAsset(object context)
    {
        var assets = ToAssetContextItems(context);
        if (assets.Count != 1)
        {
            return;
        }

        var asset = assets[0];

        var requestedName = _requestAssetRename(asset.DisplayPath);
        if (string.IsNullOrWhiteSpace(requestedName))
        {
            return;
        }

        var trimmedName = requestedName.Trim();
        if (trimmedName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || trimmedName.Contains(Path.DirectorySeparatorChar)
            || trimmedName.Contains(Path.AltDirectorySeparatorChar))
        {
            _addOutputEntry($"Rename failed: invalid asset name '{trimmedName}'.", OutputLogStatus.Warning);
            return;
        }

        var parentDirectory = Path.GetDirectoryName(asset.FullPath);
        if (string.IsNullOrWhiteSpace(parentDirectory))
        {
            _addOutputEntry("Rename failed: could not resolve parent directory.", OutputLogStatus.Warning);
            return;
        }

        var targetPath = Path.Combine(parentDirectory, trimmedName);
        var loadedProject = _loadedProjectAccessor();
        if (loadedProject is null || !IsPathInsideRoot(loadedProject.AssetsDirectory, targetPath))
        {
            _addOutputEntry("Rename blocked: target path escapes the Assets root.", OutputLogStatus.Warning);
            return;
        }

        if (string.Equals(asset.FullPath, targetPath, StringComparison.OrdinalIgnoreCase))
        {
            _addOutputEntry($"Rename skipped: '{asset.DisplayPath}' is unchanged.", OutputLogStatus.Info);
            return;
        }

        if (File.Exists(targetPath) || Directory.Exists(targetPath))
        {
            _addOutputEntry($"Rename failed: '{trimmedName}' already exists.", OutputLogStatus.Warning);
            return;
        }

        try
        {
            if (asset.IsDirectory)
            {
                if (!Directory.Exists(asset.FullPath))
                {
                    _addOutputEntry($"Rename failed: folder not found '{asset.FullPath}'.", OutputLogStatus.Warning);
                    return;
                }

                Directory.Move(asset.FullPath, targetPath);
            }
            else
            {
                if (!File.Exists(asset.FullPath))
                {
                    _addOutputEntry($"Rename failed: file not found '{asset.FullPath}'.", OutputLogStatus.Warning);
                    return;
                }

                File.Move(asset.FullPath, targetPath);
            }

            RefreshAssetBrowserPreservingState();
            SelectDirectoryByPath(parentDirectory);
            SelectedAsset = AssetBrowserItems.FirstOrDefault(item =>
                string.Equals(item.FullPath, targetPath, StringComparison.OrdinalIgnoreCase));
            _addOutputEntry($"Renamed '{asset.DisplayPath}' to '{trimmedName}'.", OutputLogStatus.Info);
        }
        catch (Exception ex)
        {
            _addOutputEntry($"Rename failed: {ex.Message}", OutputLogStatus.Warning);
        }
    }

    private bool CanRenameProjectAssetContext(object context)
    {
        var assets = ToAssetContextItems(context);
        var project = _loadedProjectAccessor();
        return project is not null && assets.Count == 1 && CanOpenAssetContext(assets[0]) && IsPathInsideRoot(project.AssetsDirectory, assets[0].FullPath);
    }

    private bool CanDeleteProjectAssetContext(object context)
    {
        var assets = ToAssetContextItems(context);
        var project = _loadedProjectAccessor();
        return project is not null && assets.Count > 0 && CanOpenAssetContext(context)
            && assets.All(asset => IsPathInsideRoot(project.AssetsDirectory, asset.FullPath));
    }

    private void DeleteAsset(object context)
    {
        var assets = ToAssetContextItems(context);
        if (assets.Count == 0 || !_confirmAssetDelete(assets))
        {
            return;
        }

        var loadedProject = _loadedProjectAccessor();
        if (loadedProject is null || assets.Any(asset => !IsPathInsideRoot(loadedProject.AssetsDirectory, asset.FullPath)))
        {
            _addOutputEntry("Delete blocked: target path is outside the Assets root.", OutputLogStatus.Warning);
            return;
        }

        var parentDirectory = Path.GetDirectoryName(assets[0].FullPath) ?? SelectedDirectory?.FullPath ?? loadedProject.AssetsDirectory;
        var selectedDirectoryPath = SelectedDirectory?.FullPath;

        try
        {
            foreach (var asset in assets)
            {
                if (asset.IsDirectory)
                {
                    if (!Directory.Exists(asset.FullPath))
                    {
                        _addOutputEntry($"Delete skipped: folder not found '{asset.FullPath}'.", OutputLogStatus.Warning);
                        continue;
                    }

                    Directory.Delete(asset.FullPath, recursive: true);
                    _addOutputEntry($"Deleted folder and contents: {asset.DisplayPath}", OutputLogStatus.Info);
                }
                else
                {
                    if (!File.Exists(asset.FullPath))
                    {
                        _addOutputEntry($"Delete skipped: file not found '{asset.FullPath}'.", OutputLogStatus.Warning);
                        continue;
                    }

                    File.Delete(asset.FullPath);
                    _addOutputEntry($"Deleted file: {asset.DisplayPath}", OutputLogStatus.Info);
                }
            }

            _selectedAssets.Clear();
            SelectedAsset = null;
            var directoryToSelect = ResolvePostDeleteDirectoryPath(
                loadedProject.AssetsDirectory,
                selectedDirectoryPath,
                parentDirectory);

            RefreshAssetBrowserPreservingState();
            SelectDirectoryByPath(directoryToSelect);
        }
        catch (Exception ex)
        {
            _addOutputEntry($"Delete failed: {ex.Message}", OutputLogStatus.Warning);
        }
    }

    private static string ResolvePostDeleteDirectoryPath(
        string assetsRoot,
        string? selectedDirectoryPath,
        string parentDirectory)
    {
        if (!string.IsNullOrWhiteSpace(selectedDirectoryPath)
            && Directory.Exists(selectedDirectoryPath))
        {
            return selectedDirectoryPath;
        }

        if (Directory.Exists(parentDirectory))
        {
            return parentDirectory;
        }

        return assetsRoot;
    }

    private static string GetDirectoryDisplayPath(string assetsRoot, string directoryPath)
    {
        if (string.Equals(assetsRoot, directoryPath, StringComparison.OrdinalIgnoreCase))
        {
            return "Assets";
        }

        return Path.GetFileName(directoryPath);
    }

    private static bool IsPathInsideRoot(string rootDirectory, string path)
    {
        var relativePath = Path.GetRelativePath(rootDirectory, path);
        return !relativePath.StartsWith("..", StringComparison.Ordinal)
               && !Path.IsPathRooted(relativePath);
    }

    private string[] CaptureSelectedAssetPaths()
    {
        return (_selectedAssets.Count > 0 ? _selectedAssets : SelectedAsset is null ? [] : [SelectedAsset])
            .Select(asset => asset.FullPath)
            .ToArray();
    }

    private void RestoreSelectedAssets(IEnumerable<string> selectedAssetPaths)
    {
        var paths = selectedAssetPaths.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var restoredAssets = AssetBrowserItems.Where(item => paths.Contains(item.FullPath)).ToArray();
        SetSelectedAssets(restoredAssets);
        foreach (var asset in restoredAssets)
        {
            asset.IsSelected = true;
        }
    }

    private sealed record AssetCommandSelection(IReadOnlyList<AssetBrowserItemViewModel> Items);


    private HashSet<string> CaptureExpandedDirectoryPaths()
    {
        var expandedDirectoryPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in AssetDirectoryTree)
        {
            CaptureExpandedDirectoryPathsRecursive(root, expandedDirectoryPaths);
        }

        return expandedDirectoryPaths;
    }

    private static void CaptureExpandedDirectoryPathsRecursive(
        AssetDirectoryNodeViewModel node,
        ISet<string> expandedDirectoryPaths)
    {
        if (node.IsExpanded)
        {
            expandedDirectoryPaths.Add(node.FullPath);
        }

        foreach (var child in node.Children)
        {
            CaptureExpandedDirectoryPathsRecursive(child, expandedDirectoryPaths);
        }
    }

    private static void RestoreExpandedDirectoryPaths(
        AssetDirectoryNodeViewModel node,
        ISet<string> expandedDirectoryPaths)
    {
        node.IsExpanded = expandedDirectoryPaths.Contains(node.FullPath);
        foreach (var child in node.Children)
        {
            RestoreExpandedDirectoryPaths(child, expandedDirectoryPaths);
        }
    }

    private static AssetDirectoryNodeViewModel? FindNearestExistingDirectory(
        AssetDirectoryNodeViewModel root,
        string? requestedPath)
    {
        if (string.IsNullOrWhiteSpace(requestedPath))
        {
            return null;
        }

        var currentPath = requestedPath;
        while (!string.IsNullOrWhiteSpace(currentPath))
        {
            var match = FindDirectoryByPath(root, currentPath);
            if (match is not null)
            {
                return match;
            }

            currentPath = Path.GetDirectoryName(currentPath);
        }

        return null;
    }

    public void SelectDirectoryByPath(string path)
    {
        foreach (var root in AssetDirectoryTree)
        {
            var match = FindDirectoryByPath(root, path);
            if (match is not null)
            {
                SelectedDirectory = match;
                return;
            }
        }
    }

    private void UpdateDirectorySelectionState(AssetDirectoryNodeViewModel? selectedDirectory)
    {
        foreach (var root in AssetDirectoryTree)
        {
            UpdateDirectorySelectionStateRecursive(root, selectedDirectory);
        }
    }

    private static void UpdateDirectorySelectionStateRecursive(
        AssetDirectoryNodeViewModel node,
        AssetDirectoryNodeViewModel? selectedDirectory)
    {
        var isSelected = selectedDirectory is not null
                         && string.Equals(node.FullPath, selectedDirectory.FullPath, StringComparison.OrdinalIgnoreCase);
        node.IsSelected = isSelected;

        if (isSelected)
        {
            node.IsExpanded = true;
        }

        foreach (var child in node.Children)
        {
            UpdateDirectorySelectionStateRecursive(child, selectedDirectory);
            if (child.IsSelected)
            {
                node.IsExpanded = true;
            }
        }
    }

    private static AssetDirectoryNodeViewModel? FindDirectoryByPath(AssetDirectoryNodeViewModel root, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        if (string.Equals(root.FullPath, path, StringComparison.OrdinalIgnoreCase))
        {
            return root;
        }

        foreach (var child in root.Children)
        {
            var match = FindDirectoryByPath(child, path);
            if (match is not null)
            {
                return match;
            }
        }

        return null;
    }
}
