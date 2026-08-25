namespace OasisEditor;

public sealed class AssetBrowserItemViewModel : System.ComponentModel.INotifyPropertyChanged
{
    private bool _isSelected;
    public AssetBrowserItemViewModel(string displayPath, string fullPath, bool isDirectory)
    {
        DisplayPath = displayPath;
        FullPath = fullPath;
        IsDirectory = isDirectory;
    }

    public string DisplayPath { get; }
    public string FullPath { get; }
    public bool IsDirectory { get; }
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
            {
                return;
            }

            _isSelected = value;
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IsSelected)));
        }
    }

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
}
