using System.Collections.ObjectModel;
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
            asset => !asset.IsDirectory);
    }

    public ObservableCollection<AssetBrowserItemViewModel> AssetBrowserItems { get; }
    public ObservableCollection<AssetDirectoryNodeViewModel> AssetDirectoryTree { get; }
    public ICommand RefreshAssetBrowserCommand { get; }
    public ICommand OpenAssetCommand { get; }

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
            RefreshDirectoryContents();
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
        }
    }

    private bool CanRefreshAssetBrowser()
    {
        return _loadedProjectAccessor() is not null;
    }

    public void RefreshAssetBrowser()
    {
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
        SelectedDirectory = rootNode;
        _notifyInspectorChanged();
        _addOutputEntry($"Asset browser refreshed ({AssetBrowserItems.Count} items).", OutputLogStatus.Info);
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

        SelectedAsset = AssetBrowserItems.FirstOrDefault();
    }

    private void OpenAsset(AssetBrowserItemViewModel asset)
    {
        if (asset.IsDirectory)
        {
            return;
        }

        SelectedAsset = asset;
        _openAsset(asset);
    }

    private void NotifyOpenAssetCommand()
    {
        if (OpenAssetCommand is PaneItemCommand<AssetBrowserItemViewModel> openAssetCommand)
        {
            openAssetCommand.RaiseCanExecuteChanged();
        }
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
}
