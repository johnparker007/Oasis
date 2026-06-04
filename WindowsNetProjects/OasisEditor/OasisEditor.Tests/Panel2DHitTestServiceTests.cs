using System.Windows;
using Xunit;

namespace OasisEditor.Tests;

public sealed class Panel2DHitTestServiceTests
{
    [Fact]
    public void HitTopmostAtPoint_ReturnsTopmostVisibleUnlockedElement()
    {
        var bottom = new PanelElementModel { ObjectId = "bottom", Kind = PanelElementKind.Lamp, X = 0, Y = 0, Width = 20, Height = 20, IsVisible = true };
        var top = new PanelElementModel { ObjectId = "top", Kind = PanelElementKind.Lamp, X = 0, Y = 0, Width = 20, Height = 20, IsVisible = true };
        var elements = new[] { bottom, top };

        var hit = Panel2DHitTestService.HitTopmostAtPoint(elements, new Point(10, 10));

        Assert.NotNull(hit);
        Assert.Equal("top", hit!.ObjectId);
    }

    [Fact]
    public void HitTopmostAtPoint_SkipsLockedAndHiddenElements()
    {
        var visible = new PanelElementModel { ObjectId = "visible", Kind = PanelElementKind.Lamp, X = 0, Y = 0, Width = 20, Height = 20, IsVisible = true };
        var locked = new PanelElementModel { ObjectId = "locked", Kind = PanelElementKind.Lamp, X = 0, Y = 0, Width = 20, Height = 20, IsVisible = true, IsLocked = true };
        var hidden = new PanelElementModel { ObjectId = "hidden", Kind = PanelElementKind.Lamp, X = 0, Y = 0, Width = 20, Height = 20, IsVisible = false };
        var elements = new[] { visible, locked, hidden };

        var hit = Panel2DHitTestService.HitTopmostAtPoint(elements, new Point(10, 10));

        Assert.NotNull(hit);
        Assert.Equal("visible", hit!.ObjectId);
    }

    [Fact]
    public void HitAllAtPoint_ReturnsHitsFromFrontToBack()
    {
        var bottom = new PanelElementModel { ObjectId = "bottom", Kind = PanelElementKind.Background, X = 0, Y = 0, Width = 30, Height = 30, IsVisible = true };
        var middle = new PanelElementModel { ObjectId = "middle", Kind = PanelElementKind.Lamp, X = 0, Y = 0, Width = 20, Height = 20, IsVisible = true };
        var top = new PanelElementModel { ObjectId = "top", Kind = PanelElementKind.Lamp, X = 0, Y = 0, Width = 10, Height = 10, IsVisible = true };
        var elements = new[] { bottom, middle, top };

        var hits = Panel2DHitTestService.HitAllAtPoint(elements, new Point(5, 5));

        Assert.Equal(new[] { "top", "middle", "bottom" }, hits.Select(hit => hit.ObjectId));
    }

    [Fact]
    public void HitTopmostIntersectingRect_ReturnsTopmostIntersectingElement()
    {
        var bottom = new PanelElementModel { ObjectId = "bottom", Kind = PanelElementKind.Lamp, X = 10, Y = 10, Width = 10, Height = 10, IsVisible = true };
        var top = new PanelElementModel { ObjectId = "top", Kind = PanelElementKind.Lamp, X = 12, Y = 12, Width = 10, Height = 10, IsVisible = true };
        var elements = new[] { bottom, top };

        var hit = Panel2DHitTestService.HitTopmostIntersectingRect(elements, 11, 11, 13, 13);

        Assert.NotNull(hit);
        Assert.Equal("top", hit!.ObjectId);
    }
}
