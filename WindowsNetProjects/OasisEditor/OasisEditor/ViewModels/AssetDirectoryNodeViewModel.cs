using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Collections.Specialized;

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
        Children.CollectionChanged += OnChildrenChanged;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string DisplayPath { get; }
    public string FullPath { get; }
    public ObservableCollection<AssetDirectoryNodeViewModel> Children { get; }
    public bool HasChildDirectories => Children.Count > 0;

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

    private void OnChildrenChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasChildDirectories)));
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
