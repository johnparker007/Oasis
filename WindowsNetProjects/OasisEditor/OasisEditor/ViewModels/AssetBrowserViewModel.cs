using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Input;

namespace OasisEditor;

public sealed class AssetBrowserViewModel
{
    private readonly Func<EditorProject?> _loadedProjectAccessor;
    private readonly Action _selectionChanged;
    private readonly Action _notifyInspectorChanged;
    private readonly Action<string, OutputLogStatus> _addOutputEntry;
    private readonly Action<AssetBrowserItemViewModel?> _openAsset;
    private AssetBrowserItemViewModel? _selectedAsset;
    private AssetDirectoryNodeViewModel? _selectedDirectory;
    public event Action? StateChanged;

    public AssetBrowserViewModel(
        Func<EditorProject?> loadedProjectAccessor,
        Action selectionChanged,
        Action notifyInspectorChanged,
        Action<string, OutputLogStatus> addOutputEntry,
        Action<AssetBrowserItemViewModel?> openAsset)
    {
        _loadedProjectAccessor = loadedProjectAccessor;
        _selectionChanged = selectionChanged;
        _notifyInspectorChanged = notifyInspectorChanged;
        _addOutputEntry = addOutputEntry;
        _openAsset = openAsset;

        AssetBrowserItems = new ObservableCollection<AssetBrowserItemViewModel>();
        AssetDirectoryTree = new ObservableCollection<AssetDirectoryNodeViewModel>();
        RefreshAssetBrowserCommand = new RelayCommand(RefreshAssetBrowser, CanRefreshAssetBrowser);
        OpenAssetCommand = new PaneItemCommand<AssetBrowserItemViewModel>(
            () => SelectedAsset,
            asset => OpenAsset(asset),
            CanOpenAsset);
        ShowInExplorerCommand = new PaneItemCommand<AssetBrowserItemViewModel>(
            () => SelectedAsset,
            asset => ShowInExplorer(asset));
        RenameAssetCommand = new PaneItemCommand<AssetBrowserItemViewModel>(
            () => SelectedAsset,
            asset => RenameAsset(asset));
        DeleteAssetCommand = new PaneItemCommand<AssetBrowserItemViewModel>(
            () => SelectedAsset,
            asset => DeleteAsset(asset));
    }

    public ObservableCollection<AssetBrowserItemViewModel> AssetBrowserItems { get; }
    public ObservableCollection<AssetDirectoryNodeViewModel> AssetDirectoryTree { get; }
    public ICommand RefreshAssetBrowserCommand { get; }
    public ICommand OpenAssetCommand { get; }
    public ICommand ShowInExplorerCommand { get; }
    public ICommand RenameAssetCommand { get; }
    public ICommand DeleteAssetCommand { get; }

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
            _notifyInspectorChanged();
            NotifyRefreshCommand();
            NotifyOpenAssetCommand();
            NotifyAssetContextCommands();
            StateChanged?.Invoke();
        }
    }

    private bool CanRefreshAssetBrowser()
    {
        return _loadedProjectAccessor() is not null;
    }

    public void RefreshAssetBrowser()
    {
        var selectedDirectoryPath = SelectedDirectory?.FullPath;
        var selectedAssetPath = SelectedAsset?.FullPath;

        var loadedProject = _loadedProjectAccessor();
        if (loadedProject is null)
        {
            AssetDirectoryTree.Clear();
            AssetBrowserItems.Clear();
            SelectedDirectory = null;
            SelectedAsset = null;
            _notifyInspectorChanged();
            _addOutputEntry("Asset browser cleared (no project loaded).", OutputLogStatus.Info);
            return;
        }

        var assetDirectory = loadedProject.AssetsDirectory;
        if (!Directory.Exists(assetDirectory))
        {
            Directory.CreateDirectory(assetDirectory);
        }

        var rootNode = BuildDirectoryTree(assetDirectory, assetDirectory);
        AssetDirectoryTree.Clear();
        AssetDirectoryTree.Add(rootNode);
        SelectedDirectory = FindDirectoryByPath(rootNode, selectedDirectoryPath) ?? rootNode;
        RestoreSelectedAsset(selectedAssetPath);
        _notifyInspectorChanged();
        _addOutputEntry($"Asset browser refreshed ({AssetBrowserItems.Count} items).", OutputLogStatus.Info);
        StateChanged?.Invoke();
    }

    public void NotifyRefreshCommand()
    {
        if (RefreshAssetBrowserCommand is RelayCommand refreshRelayCommand)
        {
            refreshRelayCommand.RaiseCanExecuteChanged();
        }
    }

    private AssetDirectoryNodeViewModel BuildDirectoryTree(string assetsRoot, string directoryPath)
    {
        var displayPath = GetDirectoryDisplayPath(assetsRoot, directoryPath);
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
        var selectedAssetPath = SelectedAsset?.FullPath;
        AssetBrowserItems.Clear();

        var loadedProject = _loadedProjectAccessor();
        if (loadedProject is null || SelectedDirectory is null)
        {
            SelectedAsset = null;
            return;
        }

        var assetsRoot = loadedProject.AssetsDirectory;
        if (!IsPathInsideRoot(assetsRoot, SelectedDirectory.FullPath))
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

        RestoreSelectedAsset(selectedAssetPath);
        StateChanged?.Invoke();
    }

    private void OpenAsset(AssetBrowserItemViewModel asset)
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

    private static bool CanOpenAsset(AssetBrowserItemViewModel asset)
    {
        return asset.IsDirectory
            ? Directory.Exists(asset.FullPath)
            : File.Exists(asset.FullPath);
    }

    private void NotifyOpenAssetCommand()
    {
        if (OpenAssetCommand is PaneItemCommand<AssetBrowserItemViewModel> openAssetCommand)
        {
            openAssetCommand.RaiseCanExecuteChanged();
        }
    }

    private void NotifyAssetContextCommands()
    {
        if (ShowInExplorerCommand is PaneItemCommand<AssetBrowserItemViewModel> showInExplorerCommand)
        {
            showInExplorerCommand.RaiseCanExecuteChanged();
        }

        if (RenameAssetCommand is PaneItemCommand<AssetBrowserItemViewModel> renameAssetCommand)
        {
            renameAssetCommand.RaiseCanExecuteChanged();
        }

        if (DeleteAssetCommand is PaneItemCommand<AssetBrowserItemViewModel> deleteAssetCommand)
        {
            deleteAssetCommand.RaiseCanExecuteChanged();
        }
    }

    private void ShowInExplorer(AssetBrowserItemViewModel asset)
    {
        if (asset.IsDirectory)
        {
            if (!Directory.Exists(asset.FullPath))
            {
                _addOutputEntry($"Cannot show folder in Explorer; path does not exist: {asset.FullPath}", OutputLogStatus.Warning);
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{asset.FullPath}\"")
                {
                    UseShellExecute = true
                });
                _addOutputEntry($"Opened folder in Explorer: {asset.DisplayPath}", OutputLogStatus.Info);
            }
            catch (Exception ex)
            {
                _addOutputEntry($"Failed to open folder in Explorer: {ex.Message}", OutputLogStatus.Warning);
            }

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

    private void RenameAsset(AssetBrowserItemViewModel asset)
    {
        _addOutputEntry($"Rename is not implemented yet ({asset.DisplayPath}).", OutputLogStatus.Info);
    }

    private void DeleteAsset(AssetBrowserItemViewModel asset)
    {
        _addOutputEntry($"Delete is not implemented yet ({asset.DisplayPath}).", OutputLogStatus.Info);
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

    private void RestoreSelectedAsset(string? selectedAssetPath)
    {
        if (!string.IsNullOrWhiteSpace(selectedAssetPath))
        {
            var existing = AssetBrowserItems.FirstOrDefault(item =>
                string.Equals(item.FullPath, selectedAssetPath, StringComparison.OrdinalIgnoreCase));
            if (existing is not null)
            {
                SelectedAsset = existing;
                return;
            }
        }

        SelectedAsset = AssetBrowserItems.FirstOrDefault();
    }

    private void SelectDirectoryByPath(string path)
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
