namespace OasisEditor.Avalonia;

public sealed partial class WorkspaceView
{
    private void RefreshHierarchyRows()
    {
        var elements = _machineActive ? [] : VisibleHierarchyRows();
        // Preserve unchanged rows at either end. Expand/collapse is a single
        // range insertion/removal, without resetting selection or all containers.
        var start = 0;
        while (start < _rows.Count && start < elements.Count && _rows[start].Id == elements[start].Id) start++;
        var tail = 0;
        while (tail < _rows.Count - start && tail < elements.Count - start &&
               _rows[_rows.Count - tail - 1].Id == elements[elements.Count - tail - 1].Id) tail++;
        var remove = _rows.Count - start - tail;
        if (remove > 0) _rows.RemoveRange(start, remove);
        var insert = elements.Count - start - tail;
        if (insert > 0) _rows.InsertRange(start, elements.Skip(start).Take(insert));
        for (var i = 0; i < elements.Count; i++)
            if (_rows[i] != elements[i]) _rows[i] = elements[i];

        // Keep the ListBox's range anchor and keyboard focus when it originated selection.
        var selected = SelectedIds.ToHashSet();
        var selectedRows = _rows.Where(e => selected.Contains(e.Id)).ToArray();
        if (!_hierarchy.SelectedItems!.OfType<ElementSnapshot>().ToHashSet().SetEquals(selectedRows))
        {
            _hierarchy.SelectedItems.Clear();
            foreach (var row in selectedRows) _hierarchy.SelectedItems.Add(row);
        }
    }
}
