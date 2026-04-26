namespace OasisEditor;

public sealed class AssetBrowserItemViewModel
{
    public AssetBrowserItemViewModel(string displayPath, string fullPath, bool isDirectory)
    {
        DisplayPath = displayPath;
        FullPath = fullPath;
        IsDirectory = isDirectory;
    }

    public string DisplayPath { get; }
    public string FullPath { get; }
    public bool IsDirectory { get; }
}
