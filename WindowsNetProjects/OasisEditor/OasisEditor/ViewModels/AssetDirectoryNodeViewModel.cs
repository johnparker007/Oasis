using System.Collections.ObjectModel;

namespace OasisEditor;

public sealed class AssetDirectoryNodeViewModel
{
    public AssetDirectoryNodeViewModel(string displayPath, string fullPath)
    {
        DisplayPath = displayPath;
        FullPath = fullPath;
        Children = new ObservableCollection<AssetDirectoryNodeViewModel>();
    }

    public string DisplayPath { get; }
    public string FullPath { get; }
    public ObservableCollection<AssetDirectoryNodeViewModel> Children { get; }
}
