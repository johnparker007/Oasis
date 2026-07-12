using Xunit;

namespace OasisEditor.Tests;

public sealed class DocumentSelectionStateTests
{
    private static readonly EditorSelectionItem A = new(EditorSelectionDomain.PanelElement, "a");
    private static readonly EditorSelectionItem B = new(EditorSelectionDomain.PanelElement, "b");

    [Fact]
    public void ReplaceAddRemoveToggleClear_MaintainOrderedUniqueSelection()
    {
        var state = new DocumentSelectionState();
        state.Replace(A);
        state.Add(B);
        state.Add(A);

        Assert.Equal(new[] { A, B }, state.Items);
        Assert.Equal(A, state.HierarchyAnchorItem);
        Assert.Equal(A, state.PrimaryItem); // Add existing does not duplicate or steal primary.

        state.Toggle(B);
        Assert.Equal(new[] { A }, state.Items);

        state.Remove(A);
        Assert.Empty(state.Items);
        Assert.Null(state.PrimaryItem);
        Assert.Null(state.HierarchyAnchorItem);

        state.Replace(B);
        state.Clear();
        Assert.Empty(state.Items);
    }

    [Fact]
    public void PrimaryAndAnchor_CanBeUpdatedSeparately()
    {
        var state = new DocumentSelectionState();
        state.Replace(A);
        state.Add(B);

        state.SetPrimary(B);
        state.SetHierarchyAnchor(A);

        Assert.Equal(B, state.PrimaryItem);
        Assert.Equal(A, state.HierarchyAnchorItem);
    }

    [Fact]
    public void Reconcile_PrunesStaleItemsAndPreservesSurvivors()
    {
        var state = new DocumentSelectionState();
        state.Replace(A);
        state.Add(B);
        state.SetPrimary(B);
        state.SetHierarchyAnchor(A);

        state.Reconcile(item => item == A);

        Assert.Equal(new[] { A }, state.Items);
        Assert.Equal(A, state.PrimaryItem);
        Assert.Equal(A, state.HierarchyAnchorItem);
    }

    [Fact]
    public void DocumentTabs_RetainDocumentLocalSelectionState()
    {
        var first = new DocumentTabViewModel(EditorDocument.CreatePanel2DStub("A"));
        var second = new DocumentTabViewModel(EditorDocument.CreatePanel2DStub("B"));

        first.SelectionState.Replace(A);
        second.SelectionState.Replace(B);

        Assert.Equal(A, first.SelectionState.PrimaryItem);
        Assert.Equal(B, second.SelectionState.PrimaryItem);
    }
}
