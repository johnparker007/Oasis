using System.Windows;
using Xunit;

namespace OasisEditor.Tests;

public sealed class Panel2DResizeHandleHitTestServiceTests
{
    [Fact]
    public void TryHitHandle_ReturnsTopLeftWhenPointInsideTopLeftHandle()
    {
        var element = new PanelElementModel
        {
            ObjectId = "id",
            Name = "name",
            Kind = PanelElementKind.Lamp,
            X = 10,
            Y = 20,
            Width = 30,
            Height = 40,
            IsVisible = true
        };

        var hit = Panel2DResizeHandleHitTestService.TryHitHandle(
            element,
            new Point(10, 20),
            handleSizeDocumentSpace: 8,
            out var kind);

        Assert.True(hit);
        Assert.Equal(ResizeHandleKind.TopLeft, kind);
    }

    [Fact]
    public void TryHitHandle_ReturnsFalseWhenPointOutsideAllHandles()
    {
        var element = new PanelElementModel
        {
            ObjectId = "id",
            Name = "name",
            Kind = PanelElementKind.Lamp,
            X = 10,
            Y = 20,
            Width = 30,
            Height = 40,
            IsVisible = true
        };

        var hit = Panel2DResizeHandleHitTestService.TryHitHandle(
            element,
            new Point(25, 40),
            handleSizeDocumentSpace: 6,
            out var kind);

        Assert.False(hit);
        Assert.Equal(ResizeHandleKind.None, kind);
    }
}
