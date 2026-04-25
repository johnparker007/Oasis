using System.Collections.ObjectModel;

namespace OasisEditor;

public sealed class HierarchyItemViewModel
{
    public HierarchyItemViewModel(string displayName, bool isGroup = false, IReadOnlyList<HierarchyItemViewModel>? children = null)
    {
        DisplayName = displayName;
        IsGroup = isGroup;
        Children = new ObservableCollection<HierarchyItemViewModel>(children ?? []);
    }

    public string DisplayName { get; }
    public bool IsGroup { get; }
    public ObservableCollection<HierarchyItemViewModel> Children { get; }
}
