using System.Collections.ObjectModel;

namespace OasisEditor;

public enum EditorSelectionDomain
{
    PanelElement,
    FaceElement,
    PanelFaceSourceShape,
    FaceMaskLayer
}

public readonly record struct EditorSelectionItem(EditorSelectionDomain Domain, string ObjectId)
{
    public bool IsValid => !string.IsNullOrWhiteSpace(ObjectId);
}

public sealed class DocumentSelectionChangedEventArgs : EventArgs
{
    public DocumentSelectionChangedEventArgs(IReadOnlyList<EditorSelectionItem> items, EditorSelectionItem? primaryItem, EditorSelectionItem? hierarchyAnchorItem)
    {
        Items = items;
        PrimaryItem = primaryItem;
        HierarchyAnchorItem = hierarchyAnchorItem;
    }

    public IReadOnlyList<EditorSelectionItem> Items { get; }
    public EditorSelectionItem? PrimaryItem { get; }
    public EditorSelectionItem? HierarchyAnchorItem { get; }
}

public sealed class DocumentSelectionState
{
    private readonly List<EditorSelectionItem> _items = [];
    private EditorSelectionItem? _primaryItem;
    private EditorSelectionItem? _hierarchyAnchorItem;

    public event EventHandler<DocumentSelectionChangedEventArgs>? SelectionChanged;

    public IReadOnlyList<EditorSelectionItem> Items => new ReadOnlyCollection<EditorSelectionItem>(_items);
    public EditorSelectionItem? PrimaryItem => _primaryItem;
    public EditorSelectionItem? HierarchyAnchorItem => _hierarchyAnchorItem;

    public void Replace(EditorSelectionItem? item)
    {
        if (item is not { IsValid: true } valid)
        {
            Clear();
            return;
        }

        if (_items.Count == 1 && _items[0] == valid && _primaryItem == valid && _hierarchyAnchorItem == valid)
        {
            return;
        }

        _items.Clear();
        _items.Add(valid);
        _primaryItem = valid;
        _hierarchyAnchorItem = valid;
        RaiseChanged();
    }

    public void Replace(IEnumerable<EditorSelectionItem> items, EditorSelectionItem? primaryItem = null, bool updateHierarchyAnchor = true)
    {
        ArgumentNullException.ThrowIfNull(items);
        var distinct = items.Where(item => item.IsValid).Distinct().ToList();
        var primary = primaryItem is { IsValid: true } validPrimary && distinct.Contains(validPrimary)
            ? validPrimary
            : distinct.Count > 0 ? distinct[^1] : (EditorSelectionItem?)null;
        var anchor = updateHierarchyAnchor ? primary : _hierarchyAnchorItem;
        if (anchor is { } existingAnchor && !distinct.Contains(existingAnchor))
        {
            anchor = primary;
        }

        if (_items.SequenceEqual(distinct) && _primaryItem == primary && _hierarchyAnchorItem == anchor)
        {
            return;
        }

        _items.Clear();
        _items.AddRange(distinct);
        _primaryItem = primary;
        _hierarchyAnchorItem = anchor;
        RaiseChanged();
    }

    public void Add(EditorSelectionItem item)
    {
        AddRange([item], item);
    }

    public void AddRange(IEnumerable<EditorSelectionItem> items, EditorSelectionItem? primaryItem = null, bool updateHierarchyAnchor = false)
    {
        ArgumentNullException.ThrowIfNull(items);
        var changed = false;
        EditorSelectionItem? lastAdded = null;
        foreach (var item in items.Where(item => item.IsValid).Distinct())
        {
            if (!_items.Contains(item))
            {
                _items.Add(item);
                changed = true;
            }
            lastAdded = item;
        }

        var requestedPrimary = primaryItem is { IsValid: true } validPrimary && _items.Contains(validPrimary)
            ? validPrimary
            : lastAdded is { } added && _items.Contains(added) ? added : (EditorSelectionItem?)null;
        if (requestedPrimary is { } primary && _primaryItem != primary)
        {
            _primaryItem = primary;
            changed = true;
        }

        if ((updateHierarchyAnchor || _hierarchyAnchorItem is null) && _primaryItem is { } currentPrimary)
        {
            if (_hierarchyAnchorItem != currentPrimary)
            {
                _hierarchyAnchorItem = currentPrimary;
                changed = true;
            }
        }

        if (changed) RaiseChanged();
    }

    public void Remove(EditorSelectionItem item)
    {
        if (!_items.Remove(item)) return;
        if (_primaryItem == item) _primaryItem = _items.Count > 0 ? _items[^1] : null;
        if (_hierarchyAnchorItem == item) _hierarchyAnchorItem = _primaryItem;
        RaiseChanged();
    }

    public void Toggle(EditorSelectionItem item)
    {
        if (_items.Contains(item)) Remove(item); else Add(item);
    }

    public void Clear()
    {
        if (_items.Count == 0 && _primaryItem is null && _hierarchyAnchorItem is null) return;
        _items.Clear();
        _primaryItem = null;
        _hierarchyAnchorItem = null;
        RaiseChanged();
    }

    public void SetPrimary(EditorSelectionItem? item)
    {
        if (item is not { IsValid: true } valid || !_items.Contains(valid)) return;
        if (_primaryItem == valid) return;
        _primaryItem = valid;
        RaiseChanged();
    }

    public void SetHierarchyAnchor(EditorSelectionItem? item)
    {
        if (item is not { IsValid: true } valid || !_items.Contains(valid)) return;
        if (_hierarchyAnchorItem == valid) return;
        _hierarchyAnchorItem = valid;
        RaiseChanged();
    }

    public void Reconcile(Func<EditorSelectionItem, bool> exists)
    {
        ArgumentNullException.ThrowIfNull(exists);
        var oldItems = _items.ToArray();
        _items.RemoveAll(item => !exists(item));
        var changed = oldItems.Length != _items.Count;
        if (_primaryItem is { } primary && !_items.Contains(primary))
        {
            _primaryItem = _items.Count > 0 ? _items[^1] : null;
            changed = true;
        }
        if (_hierarchyAnchorItem is { } anchor && !_items.Contains(anchor))
        {
            _hierarchyAnchorItem = _primaryItem;
            changed = true;
        }
        if (_items.Count == 0 && (_primaryItem is not null || _hierarchyAnchorItem is not null))
        {
            _primaryItem = null;
            _hierarchyAnchorItem = null;
            changed = true;
        }
        if (changed) RaiseChanged();
    }

    private void RaiseChanged() => SelectionChanged?.Invoke(this, new DocumentSelectionChangedEventArgs(Items, _primaryItem, _hierarchyAnchorItem));
}
