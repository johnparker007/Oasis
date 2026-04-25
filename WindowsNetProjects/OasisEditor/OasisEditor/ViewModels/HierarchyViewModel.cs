using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace OasisEditor;

public sealed class HierarchyViewModel : INotifyPropertyChanged
{
    private readonly Func<DocumentTabViewModel?> _getSelectedDocument;
    private readonly IReadOnlyList<IDocumentHierarchyProvider> _providers;
    private string _emptyStateMessage = "No active document.";

    public event PropertyChangedEventHandler? PropertyChanged;

    public HierarchyViewModel(
        Func<DocumentTabViewModel?> getSelectedDocument,
        IReadOnlyList<IDocumentHierarchyProvider> providers)
    {
        _getSelectedDocument = getSelectedDocument;
        _providers = providers;
    }

    public ObservableCollection<HierarchyItemViewModel> Items { get; } = [];

    public bool HasItems => Items.Count > 0;

    public string EmptyStateMessage
    {
        get => _emptyStateMessage;
        private set
        {
            if (string.Equals(_emptyStateMessage, value, StringComparison.Ordinal))
            {
                return;
            }

            _emptyStateMessage = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(EmptyStateMessage)));
        }
    }

    public void Refresh()
    {
        var expandedNodeKeys = CollectExpandedNodeKeys(Items);
        var selectedDocument = _getSelectedDocument();
        var provider = _providers.FirstOrDefault(p => p.CanBuild(selectedDocument));

        Items.Clear();

        if (selectedDocument is null)
        {
            EmptyStateMessage = "No active document.";
            NotifyCollectionStateChanged();
            return;
        }

        if (provider is null)
        {
            EmptyStateMessage = $"Hierarchy is not available for {selectedDocument.TypeLabel}.";
            NotifyCollectionStateChanged();
            return;
        }

        foreach (var item in provider.Build(selectedDocument))
        {
            RestoreExpandedState(item, expandedNodeKeys);
            Items.Add(item);
        }

        ApplySelection(selectedDocument?.HierarchySelectedPanelSelection);
        EmptyStateMessage = Items.Count > 0 ? string.Empty : "This document has no hierarchy items yet.";
        NotifyCollectionStateChanged();
    }

    private static HashSet<string> CollectExpandedNodeKeys(IEnumerable<HierarchyItemViewModel> items)
    {
        var expandedNodeKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in Flatten(items))
        {
            if (item.IsExpanded)
            {
                expandedNodeKeys.Add(item.NodeKey);
            }
        }

        return expandedNodeKeys;
    }

    private static void RestoreExpandedState(HierarchyItemViewModel item, ISet<string> expandedNodeKeys)
    {
        item.IsExpanded = expandedNodeKeys.Contains(item.NodeKey);
        foreach (var child in item.Children)
        {
            RestoreExpandedState(child, expandedNodeKeys);
        }
    }

    private void ApplySelection(PanelSelectionInfo? selection)
    {
        foreach (var item in Flatten(Items))
        {
            item.IsSelected = false;
        }

        if (selection is not PanelSelectionInfo targetSelection)
        {
            return;
        }

        var selectedItem = Flatten(Items)
            .FirstOrDefault(item => item.PanelSelection is PanelSelectionInfo itemSelection
                && IsSelectionMatch(itemSelection, targetSelection));

        if (selectedItem is null)
        {
            return;
        }

        selectedItem.IsSelected = true;
        ExpandParents(Items, selectedItem.NodeKey);
    }

    private static bool ExpandParents(IEnumerable<HierarchyItemViewModel> roots, string targetNodeKey)
    {
        foreach (var root in roots)
        {
            if (string.Equals(root.NodeKey, targetNodeKey, StringComparison.Ordinal))
            {
                return true;
            }

            if (ExpandParents(root.Children, targetNodeKey))
            {
                root.IsExpanded = true;
                return true;
            }
        }

        return false;
    }

    private static bool IsSelectionMatch(PanelSelectionInfo left, PanelSelectionInfo right)
    {
        return PanelSelectionContract.IsSameSelection(left, right);
    }

    private static IEnumerable<HierarchyItemViewModel> Flatten(IEnumerable<HierarchyItemViewModel> items)
    {
        foreach (var item in items)
        {
            yield return item;
            foreach (var child in Flatten(item.Children))
            {
                yield return child;
            }
        }
    }

    private void NotifyCollectionStateChanged()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasItems)));
    }
}
