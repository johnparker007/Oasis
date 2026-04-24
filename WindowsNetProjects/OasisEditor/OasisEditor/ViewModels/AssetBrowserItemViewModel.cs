namespace OasisEditor;

public sealed class AssetBrowserItemViewModel
{
    public AssetBrowserItemViewModel(string displayPath, string fullPath)
    {
        DisplayPath = displayPath;
        FullPath = fullPath;
    }

    public string DisplayPath { get; }
    public string FullPath { get; }
}
