using System.Windows;
using Xunit;

namespace OasisEditor.Tests;

public sealed class PanelFaceSourceShapeHitTestServiceTests
{
    private static readonly PanelFaceSourceShapeModel Shape = new()
    {
        Id = "source-shape",
        TopLeft = new FacePointModel { X = 0, Y = 0 },
        TopRight = new FacePointModel { X = 100, Y = 10 },
        BottomRight = new FacePointModel { X = 90, Y = 100 },
        BottomLeft = new FacePointModel { X = 10, Y = 90 }
    };

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(100, 10, 1)]
    [InlineData(90, 100, 2)]
    [InlineData(10, 90, 3)]
    public void TryHitCorner_HitsEachCornerHandle(double x, double y, int expectedCornerIndex)
    {
        var hit = PanelFaceSourceShapeHitTestService.TryHitCorner([Shape], new Point(x, y), 8, out var shape, out var cornerIndex);

        Assert.True(hit);
        Assert.Same(Shape, shape);
        Assert.Equal(expectedCornerIndex, cornerIndex);
    }

    [Fact]
    public void TryHitCorner_DoesNotHitQuadrilateralInteriorAwayFromHandles()
    {
        var hit = PanelFaceSourceShapeHitTestService.TryHitCorner([Shape], new Point(50, 50), 8, out _, out _);

        Assert.False(hit);
    }

    [Fact]
    public void InteriorPoint_StillSelectsUnderlyingPanelElement()
    {
        var point = new Point(50, 50);
        var elements = new[]
        {
            new PanelElementModel { ObjectId = "lamp", Kind = PanelElementKind.Lamp, X = 40, Y = 40, Width = 20, Height = 20, IsVisible = true }
        };

        Assert.False(PanelFaceSourceShapeHitTestService.TryHitCorner([Shape], point, 8, out _, out _));
        Assert.Equal("lamp", Panel2DSelectionService.SelectFromPoint(elements, point)?.ObjectId);
    }

    [Fact]
    public void RepeatedInteriorClicks_CycleOnlyThroughOverlappingPanelElements()
    {
        var point = new Point(50, 50);
        var elements = new[]
        {
            new PanelElementModel { ObjectId = "lamp", Kind = PanelElementKind.Lamp, X = 40, Y = 40, Width = 20, Height = 20, IsVisible = true },
            new PanelElementModel { ObjectId = "image", Kind = PanelElementKind.Image, X = 40, Y = 40, Width = 20, Height = 20, IsVisible = true }
        };

        Assert.False(PanelFaceSourceShapeHitTestService.TryHitCorner([Shape], point, 8, out _, out _));
        var first = Panel2DSelectionService.SelectFromPoint(elements, point);
        var second = Panel2DSelectionService.SelectFromPoint(elements, point, first);

        Assert.Equal("image", first?.ObjectId);
        Assert.Equal("lamp", second?.ObjectId);
    }
}
