using System.Windows;
using Xunit;

namespace OasisEditor.Tests;

public sealed class Panel2DResizeComputationServiceTests
{
    [Fact]
    public void ComputeResizedElement_RightHandle_UpdatesWidthOnly()
    {
        var source = new PanelElementModel { ObjectId = "id", Name = "n", Kind = PanelElementKind.Lamp, X = 10, Y = 20, Width = 30, Height = 40, IsVisible = true };

        var resized = Panel2DResizeComputationService.ComputeResizedElement(
            source,
            ResizeHandleKind.Right,
            new Point(0, 0),
            new Point(5, 0));

        Assert.Equal(10, resized.X);
        Assert.Equal(20, resized.Y);
        Assert.Equal(35, resized.Width);
        Assert.Equal(40, resized.Height);
    }

    [Fact]
    public void ComputeResizedElement_TopLeftHandle_UpdatesPositionAndSize()
    {
        var source = new PanelElementModel { ObjectId = "id", Name = "n", Kind = PanelElementKind.Lamp, X = 10, Y = 20, Width = 30, Height = 40, IsVisible = true };

        var resized = Panel2DResizeComputationService.ComputeResizedElement(
            source,
            ResizeHandleKind.TopLeft,
            new Point(0, 0),
            new Point(5, 10));

        Assert.Equal(15, resized.X);
        Assert.Equal(30, resized.Y);
        Assert.Equal(25, resized.Width);
        Assert.Equal(30, resized.Height);
    }
}
