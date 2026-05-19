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
