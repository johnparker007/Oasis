namespace OasisEditor;

public enum HierarchySelectionModifier
{
    None,
    Control,
    Shift,
    ControlShift
}

public static class HierarchySelectionIdentityService
{
    public static EditorSelectionItem? ToSelectionItem(PanelSelectionInfo? selection)
    {
        if (selection is not PanelSelectionInfo value || string.IsNullOrWhiteSpace(value.ObjectId))
        {
            return null;
        }

        if (string.Equals(value.Kind, PanelFaceSourceShapeCommands.SelectionKind, StringComparison.Ordinal))
        {
            return new EditorSelectionItem(EditorSelectionDomain.PanelFaceSourceShape, value.ObjectId);
        }

        if (FaceSelectionService.IsFaceSelectionKind(value.Kind))
        {
            return new EditorSelectionItem(EditorSelectionDomain.FaceElement, value.ObjectId);
        }

        if (FaceMaskLayerSelectionService.IsMaskLayerSelection(value))
        {
            return new EditorSelectionItem(EditorSelectionDomain.FaceMaskLayer, value.ObjectId);
        }

        return new EditorSelectionItem(EditorSelectionDomain.PanelElement, value.ObjectId);
    }

    public static bool IsRangeSelectable(EditorSelectionItem item)
    {
        return item.Domain is EditorSelectionDomain.PanelElement or EditorSelectionDomain.FaceElement;
    }
}

public static class HierarchyVisibleRowService
{
    public static IReadOnlyList<HierarchyItemViewModel> FlattenVisible(IEnumerable<HierarchyItemViewModel> roots)
    {
        ArgumentNullException.ThrowIfNull(roots);
        var rows = new List<HierarchyItemViewModel>();
        foreach (var root in roots)
        {
            AddVisible(root, rows);
        }

        return rows;
    }

    public static IReadOnlyList<EditorSelectionItem> GetSelectableRange(
        IReadOnlyList<HierarchyItemViewModel> visibleRows,
        EditorSelectionItem anchor,
        HierarchyItemViewModel clickedRow)
    {
        ArgumentNullException.ThrowIfNull(visibleRows);
        ArgumentNullException.ThrowIfNull(clickedRow);

        var anchorIndex = -1;
        var clickedIndex = -1;
        for (var i = 0; i < visibleRows.Count; i++)
        {
            var row = visibleRows[i];
            if (row.SelectionItem == anchor)
            {
                anchorIndex = i;
            }

            if (ReferenceEquals(row, clickedRow))
            {
                clickedIndex = i;
            }
        }

        if (anchorIndex < 0 || clickedIndex < 0)
        {
            return [];
        }

        var start = Math.Min(anchorIndex, clickedIndex);
        var end = Math.Max(anchorIndex, clickedIndex);
        return visibleRows
            .Skip(start)
            .Take(end - start + 1)
            .Select(row => row.SelectionItem)
            .Where(item => item is { } value && HierarchySelectionIdentityService.IsRangeSelectable(value))
            .Select(item => item!.Value)
            .Distinct()
            .ToArray();
    }

    private static void AddVisible(HierarchyItemViewModel row, List<HierarchyItemViewModel> rows)
    {
        rows.Add(row);
        if (!row.IsExpanded)
        {
            return;
        }

        foreach (var child in row.Children)
        {
            AddVisible(child, rows);
        }
    }
}

public static class HierarchyMouseSelectionService
{
    public static void ApplySelection(
        DocumentSelectionState selectionState,
        IReadOnlyList<HierarchyItemViewModel> visibleRows,
        HierarchyItemViewModel row,
        HierarchySelectionModifier modifier)
    {
        ArgumentNullException.ThrowIfNull(selectionState);
        ArgumentNullException.ThrowIfNull(visibleRows);
        ArgumentNullException.ThrowIfNull(row);

        var item = row.SelectionItem;
        if (item is null)
        {
            return;
        }

        var isShift = modifier is HierarchySelectionModifier.Shift or HierarchySelectionModifier.ControlShift;
        var isControl = modifier is HierarchySelectionModifier.Control or HierarchySelectionModifier.ControlShift;

        if (isShift && selectionState.HierarchyAnchorItem is { } anchor)
        {
            var range = HierarchyVisibleRowService.GetSelectableRange(visibleRows, anchor, row);
            if (range.Count == 0)
            {
                return;
            }

            if (isControl)
            {
                selectionState.AddRange(range, range[^1], updateHierarchyAnchor: false);
            }
            else
            {
                selectionState.Replace(range, range[^1], updateHierarchyAnchor: false);
            }

            return;
        }

        if (isControl)
        {
            if (selectionState.Items.Contains(item.Value))
            {
                selectionState.Remove(item.Value);
            }
            else
            {
                selectionState.AddRange([item.Value], item.Value, updateHierarchyAnchor: true);
            }

            return;
        }

        selectionState.Replace(item.Value);
    }
}
