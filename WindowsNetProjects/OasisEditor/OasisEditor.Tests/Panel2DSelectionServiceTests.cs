using System.Windows;
using Xunit;

namespace OasisEditor.Tests;

public sealed class Panel2DSelectionServiceTests
{
    [Fact]
    public void SelectFromPoint_ReturnsSelectionInfoForTopmostHit()
    {
        var elements = new[]
        {
            new PanelElementModel { ObjectId = "a", Name = "a", Kind = PanelElementKind.Lamp, X = 0, Y = 0, Width = 10, Height = 10, IsVisible = true },
            new PanelElementModel { ObjectId = "b", Name = "b", Kind = PanelElementKind.Lamp, X = 0, Y = 0, Width = 10, Height = 10, IsVisible = true }
        };

        var selection = Panel2DSelectionService.SelectFromPoint(elements, new Point(5, 5));

        Assert.NotNull(selection);
        Assert.Equal("b", selection!.Value.ObjectId);
    }

    [Fact]
    public void SelectFromPoint_WithCurrentSelectionCyclesThroughOverlappingHitsFrontToBack()
    {
        var elements = new[]
        {
            new PanelElementModel { ObjectId = "background", Name = "background", Kind = PanelElementKind.Background, X = 0, Y = 0, Width = 100, Height = 100, IsVisible = true },
            new PanelElementModel { ObjectId = "lamp1", Name = "lamp1", Kind = PanelElementKind.Lamp, X = 0, Y = 0, Width = 10, Height = 10, IsVisible = true },
            new PanelElementModel { ObjectId = "lamp2", Name = "lamp2", Kind = PanelElementKind.Lamp, X = 0, Y = 0, Width = 10, Height = 10, IsVisible = true }
        };
        var current = new PanelSelectionInfo("lamp2", "Lamp", 0, 0, 10, 10);

        var selection = Panel2DSelectionService.SelectFromPoint(elements, new Point(5, 5), current);

        Assert.NotNull(selection);
        Assert.Equal("lamp1", selection!.Value.ObjectId);
    }

    [Fact]
    public void SelectFromPoint_WithRearCurrentSelectionWrapsToFrontmostHit()
    {
        var elements = new[]
        {
            new PanelElementModel { ObjectId = "background", Name = "background", Kind = PanelElementKind.Background, X = 0, Y = 0, Width = 100, Height = 100, IsVisible = true },
            new PanelElementModel { ObjectId = "lamp", Name = "lamp", Kind = PanelElementKind.Lamp, X = 0, Y = 0, Width = 10, Height = 10, IsVisible = true }
        };
        var current = new PanelSelectionInfo("background", "Background", 0, 0, 100, 100);

        var selection = Panel2DSelectionService.SelectFromPoint(elements, new Point(5, 5), current);

        Assert.NotNull(selection);
        Assert.Equal("lamp", selection!.Value.ObjectId);
    }

    [Fact]
    public void SelectFromRect_ReturnsNullWhenNoIntersection()
    {
        var elements = new[]
        {
            new PanelElementModel { ObjectId = "a", Name = "a", Kind = PanelElementKind.Lamp, X = 20, Y = 20, Width = 10, Height = 10, IsVisible = true }
        };

        var selection = Panel2DSelectionService.SelectFromRect(elements, 0, 0, 5, 5);

        Assert.Null(selection);
    }
}

public sealed class Panel2DSelectionInteractionServiceTests
{
    private static readonly EditorSelectionItem A = new(EditorSelectionDomain.PanelElement, "a");
    private static readonly EditorSelectionItem B = new(EditorSelectionDomain.PanelElement, "b");

    [Fact]
    public void DocumentSelection_ClickReplacePreserveCtrlAndEmptySpaceBehaviors()
    {
        var state = new DocumentSelectionState();
        state.Replace(A);
        state.Add(B);

        // Click an already-selected item: preserve group, make clicked item primary.
        state.SetPrimary(A);
        Assert.Equal(new[] { A, B }, state.Items);
        Assert.Equal(A, state.PrimaryItem);

        // Click an unselected item: replace.
        var c = new EditorSelectionItem(EditorSelectionDomain.PanelElement, "c");
        state.Replace(c);
        Assert.Equal(new[] { c }, state.Items);

        // Ctrl-click add/remove.
        state.Add(A);
        Assert.Equal(new[] { c, A }, state.Items);
        Assert.Equal(A, state.PrimaryItem);
        state.Toggle(A);
        Assert.Equal(new[] { c }, state.Items);

        // Empty-space clear and Ctrl-empty preserve.
        state.Clear();
        Assert.Empty(state.Items);
        state.Replace(c);
        Assert.Equal(new[] { c }, state.Items);
    }

    [Fact]
    public void SelectItemsFromRect_UsesFullEnclosureAndReturnsAllEligiblePanelElements()
    {
        var elements = new[]
        {
            new PanelElementModel { ObjectId = "a", Kind = PanelElementKind.Lamp, X = 0, Y = 0, Width = 10, Height = 10, IsVisible = true },
            new PanelElementModel { ObjectId = "partial", Kind = PanelElementKind.Lamp, X = 8, Y = 8, Width = 10, Height = 10, IsVisible = true },
            new PanelElementModel { ObjectId = "hidden", Kind = PanelElementKind.Lamp, X = 0, Y = 0, Width = 10, Height = 10, IsVisible = false },
            new PanelElementModel { ObjectId = "b", Kind = PanelElementKind.Reel, X = 20, Y = 20, Width = 5, Height = 5, IsVisible = true }
        };

        var selected = Panel2DSelectionInteractionService.SelectItemsFromRect(elements, new Rect(0, 0, 25, 25));

        Assert.Equal(new[] { "a", "b" }, selected.Select(item => item.ObjectId));
    }

    [Fact]
    public void RectangleReplaceAndCtrlAdd_UseDocumentSelectionState()
    {
        var state = new DocumentSelectionState();
        state.Replace(A);
        state.Replace(new[] { B });
        Assert.Equal(new[] { B }, state.Items);

        state.AddRange(new[] { A });
        Assert.Equal(new[] { B, A }, state.Items);
    }

    [Fact]
    public void LockedElementCannotStartMoveAndSingleUnlockedSelectionCanShowResizeHandles()
    {
        var state = new DocumentSelectionState();
        var unlocked = new PanelElementModel { ObjectId = "unlocked", IsTransformLocked = false };
        var locked = new PanelElementModel { ObjectId = "locked", IsTransformLocked = true };
        state.Replace(Panel2DSelectionInteractionService.ToSelectionItem(unlocked));
        state.Add(Panel2DSelectionInteractionService.ToSelectionItem(locked));

        Assert.True(Panel2DSelectionInteractionService.CanStartGroupMoveFrom(unlocked, state));
        Assert.False(Panel2DSelectionInteractionService.CanStartGroupMoveFrom(locked, state));
        Assert.False(Panel2DSelectionInteractionService.CanShowResizeHandles(state.Items, unlocked));

        state.Replace(Panel2DSelectionInteractionService.ToSelectionItem(unlocked));
        Assert.True(Panel2DSelectionInteractionService.CanShowResizeHandles(state.Items, unlocked));
        Assert.False(Panel2DSelectionInteractionService.CanShowResizeHandles(state.Items, locked));
    }
}
