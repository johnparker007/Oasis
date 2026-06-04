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
