using OasisEditor;
using Xunit;

namespace OasisEditor.Tests;

public sealed class HierarchyMultiSelectionTests
{
    private static readonly EditorSelectionItem A = new(EditorSelectionDomain.PanelElement, "a");
    private static readonly EditorSelectionItem B = new(EditorSelectionDomain.PanelElement, "b");
    private static readonly EditorSelectionItem C = new(EditorSelectionDomain.PanelElement, "c");
    private static readonly EditorSelectionItem D = new(EditorSelectionDomain.PanelElement, "d");

    [Fact]
    public void FlattenVisible_IncludesExpandedAndExcludesCollapsedDescendants_InDisplayOrder()
    {
        var roots = CreateTree();
        roots[0].IsExpanded = true;
        roots[2].IsExpanded = false;

        var visible = HierarchyVisibleRowService.FlattenVisible(roots);

        Assert.Equal(["Group 1", "A", "B", "Structural", "Group 2"], visible.Select(row => row.DisplayName).ToArray());
    }

    [Fact]
    public void RangeSelection_CrossesStructuralRows_ButReturnsOnlySelectableComponents()
    {
        var roots = CreateTree();
        roots[0].IsExpanded = true;
        roots[2].IsExpanded = true;
        var visible = HierarchyVisibleRowService.FlattenVisible(roots);
        var clicked = visible.Single(row => row.DisplayName == "D");

        var range = HierarchyVisibleRowService.GetSelectableRange(visible, B, clicked);

        Assert.Equal([B, C, D], range);
    }

    [Fact]
    public void MouseSelection_Click_ReplacesPrimaryAndAnchor()
    {
        var rows = CreateFlatRows();
        var state = new DocumentSelectionState();

        HierarchyMouseSelectionService.ApplySelection(state, rows, rows[1], HierarchySelectionModifier.None);

        Assert.Equal([B], state.Items);
        Assert.Equal(B, state.PrimaryItem);
        Assert.Equal(B, state.HierarchyAnchorItem);
    }

    [Fact]
    public void MouseSelection_CtrlClick_AddsAndUpdatesAnchor()
    {
        var rows = CreateFlatRows();
        var state = new DocumentSelectionState();
        state.Replace(A);

        HierarchyMouseSelectionService.ApplySelection(state, rows, rows[1], HierarchySelectionModifier.Control);

        Assert.Equal([A, B], state.Items);
        Assert.Equal(B, state.PrimaryItem);
        Assert.Equal(B, state.HierarchyAnchorItem);
    }

    [Fact]
    public void MouseSelection_CtrlClickSelected_RemovesOnlyThatItemAndChoosesSurvivingPrimary()
    {
        var rows = CreateFlatRows();
        var state = new DocumentSelectionState();
        state.Replace([A, B, C], B);

        HierarchyMouseSelectionService.ApplySelection(state, rows, rows[1], HierarchySelectionModifier.Control);

        Assert.Equal([A, C], state.Items);
        Assert.Equal(C, state.PrimaryItem);
    }

    [Fact]
    public void MouseSelection_ShiftClick_ReplacesWithVisibleRangeAndKeepsAnchor()
    {
        var rows = CreateFlatRows();
        var state = new DocumentSelectionState();
        state.Replace(B);

        HierarchyMouseSelectionService.ApplySelection(state, rows, rows[3], HierarchySelectionModifier.Shift);

        Assert.Equal([B, C, D], state.Items);
        Assert.Equal(D, state.PrimaryItem);
        Assert.Equal(B, state.HierarchyAnchorItem);
    }

    [Fact]
    public void MouseSelection_CtrlShiftClick_AddsVisibleRangeAndKeepsAnchor()
    {
        var rows = CreateFlatRows();
        var state = new DocumentSelectionState();
        state.Replace(A);
        state.AddRange([D], D, updateHierarchyAnchor: false);

        HierarchyMouseSelectionService.ApplySelection(state, rows, rows[2], HierarchySelectionModifier.ControlShift);

        Assert.Equal([A, D, B, C], state.Items);
        Assert.Equal(C, state.PrimaryItem);
        Assert.Equal(A, state.HierarchyAnchorItem);
    }

    [Fact]
    public void HierarchyViewModel_SyncSelection_UpdatesAllRowsAndPrimaryWithoutRebuild()
    {
        var document = CreatePanelDocument();
        var hierarchy = new HierarchyViewModel(() => document, [new Panel2DHierarchyProvider()]);
        hierarchy.Refresh();

        document.SelectionState.Replace([A, C], C);
        hierarchy.SyncSelection(document.SelectionState);

        var rows = FlattenAll(hierarchy.Items).Where(row => row.SelectionItem is not null).ToArray();
        Assert.True(rows.Single(row => row.SelectionItem == A).IsSelected);
        Assert.True(rows.Single(row => row.SelectionItem == C).IsSelected);
        Assert.True(rows.Single(row => row.SelectionItem == C).IsPrimarySelected);
    }

    private static IReadOnlyList<HierarchyItemViewModel> CreateFlatRows()
    {
        return [Row("A", A), Row("B", B), Row("C", C), Row("D", D)];
    }

    private static List<HierarchyItemViewModel> CreateTree()
    {
        return
        [
            new HierarchyItemViewModel("Group 1", "group:1", isGroup: true, children: [Row("A", A), Row("B", B)]),
            new HierarchyItemViewModel("Structural", "structural"),
            new HierarchyItemViewModel("Group 2", "group:2", isGroup: true, children: [Row("C", C), Row("D", D)])
        ];
    }

    private static HierarchyItemViewModel Row(string name, EditorSelectionItem item)
    {
        return new HierarchyItemViewModel(name, $"row:{item.ObjectId}", panelSelection: new PanelSelectionInfo(item.ObjectId, "lamp", 0, 0, 1, 1));
    }

    private static DocumentTabViewModel CreatePanelDocument()
    {
        var json = Panel2DDocumentStorage.Serialize("Panel", null,
        [
            new PanelElementFile { ObjectId = "a", Kind = "lamp", Width = 1, Height = 1 },
            new PanelElementFile { ObjectId = "c", Kind = "lamp", Width = 1, Height = 1 }
        ], []);
        return new DocumentTabViewModel(EditorDocument.CreatePanel2DStub("Panel"), panelLayoutJson: json);
    }

    private static IEnumerable<HierarchyItemViewModel> FlattenAll(IEnumerable<HierarchyItemViewModel> rows)
    {
        foreach (var row in rows)
        {
            yield return row;
            foreach (var child in FlattenAll(row.Children))
            {
                yield return child;
            }
        }
    }
}
