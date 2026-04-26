using System.Collections.ObjectModel;
using System.ComponentModel;

namespace OasisEditor;

public sealed class AssetDirectoryNodeViewModel : INotifyPropertyChanged
{
    private bool _isExpanded;
    private bool _isSelected;

    public AssetDirectoryNodeViewModel(string displayPath, string fullPath)
    {
        DisplayPath = displayPath;
        FullPath = fullPath;
        Children = new ObservableCollection<AssetDirectoryNodeViewModel>();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string DisplayPath { get; }
    public string FullPath { get; }
    public ObservableCollection<AssetDirectoryNodeViewModel> Children { get; }

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded == value)
            {
                return;
            }

            _isExpanded = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsExpanded)));
        }
    }

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
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
        }
    }
}
