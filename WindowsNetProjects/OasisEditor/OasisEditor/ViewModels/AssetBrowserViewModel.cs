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
    private readonly Action<string> _addOutputEntry;
    private AssetBrowserItemViewModel? _selectedAsset;

    public AssetBrowserViewModel(
        Func<EditorProject?> loadedProjectAccessor,
        Action selectionChanged,
        Action notifyInspectorChanged,
        Action<string> addOutputEntry)
    {
        _loadedProjectAccessor = loadedProjectAccessor;
        _selectionChanged = selectionChanged;
        _notifyInspectorChanged = notifyInspectorChanged;
        _addOutputEntry = addOutputEntry;

        AssetBrowserItems = new ObservableCollection<AssetBrowserItemViewModel>();
        RefreshAssetBrowserCommand = new RelayCommand(RefreshAssetBrowser, CanRefreshAssetBrowser);
    }

    public ObservableCollection<AssetBrowserItemViewModel> AssetBrowserItems { get; }
    public ICommand RefreshAssetBrowserCommand { get; }

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
            AssetBrowserItems.Clear();
            SelectedAsset = null;
            _notifyInspectorChanged();
            _addOutputEntry("Asset browser cleared (no project loaded).");
            return;
        }

        var assetDirectory = loadedProject.AssetsDirectory;
        if (!Directory.Exists(assetDirectory))
        {
            Directory.CreateDirectory(assetDirectory);
        }

        var files = Directory
            .GetFiles(assetDirectory, "*", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        AssetBrowserItems.Clear();
        foreach (var file in files)
        {
            var relativePath = Path.GetRelativePath(assetDirectory, file);
            AssetBrowserItems.Add(new AssetBrowserItemViewModel(relativePath, file));
        }

        SelectedAsset = AssetBrowserItems.FirstOrDefault();
        _notifyInspectorChanged();
        _addOutputEntry($"Asset browser refreshed ({AssetBrowserItems.Count} files).");
    }

    public void NotifyRefreshCommand()
    {
        if (RefreshAssetBrowserCommand is RelayCommand refreshRelayCommand)
        {
            refreshRelayCommand.RaiseCanExecuteChanged();
        }
    }
}
