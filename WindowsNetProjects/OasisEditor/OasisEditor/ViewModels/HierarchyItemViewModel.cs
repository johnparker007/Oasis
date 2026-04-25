using System.Collections.ObjectModel;

namespace OasisEditor;

public sealed class HierarchyItemViewModel
{
    public HierarchyItemViewModel(
        string displayName,
        bool isGroup = false,
        IReadOnlyList<HierarchyItemViewModel>? children = null,
        PanelSelectionInfo? panelSelection = null)
    {
        DisplayName = displayName;
        IsGroup = isGroup;
        Children = new ObservableCollection<HierarchyItemViewModel>(children ?? []);
        PanelSelection = panelSelection;
    }

    public string DisplayName { get; }
    public bool IsGroup { get; }
    public ObservableCollection<HierarchyItemViewModel> Children { get; }
    public PanelSelectionInfo? PanelSelection { get; }
}
