using System.Collections.ObjectModel;
using System.ComponentModel;

namespace OasisEditor;

public sealed class HierarchyItemViewModel : INotifyPropertyChanged
{
    private bool _isExpanded;
    private bool _isSelected;

    public event PropertyChangedEventHandler? PropertyChanged;

    public HierarchyItemViewModel(
        string displayName,
        string nodeKey,
        bool isGroup = false,
        IReadOnlyList<HierarchyItemViewModel>? children = null,
        PanelSelectionInfo? panelSelection = null)
    {
        DisplayName = displayName;
        NodeKey = nodeKey;
        IsGroup = isGroup;
        Children = new ObservableCollection<HierarchyItemViewModel>(children ?? []);
        PanelSelection = panelSelection;
    }

    public string DisplayName { get; }
    public string NodeKey { get; }
    public bool IsGroup { get; }
    public ObservableCollection<HierarchyItemViewModel> Children { get; }
    public PanelSelectionInfo? PanelSelection { get; }

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
