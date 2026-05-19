using Xunit;

namespace OasisEditor.Tests;

public sealed class Panel2DResizeHandleServiceTests
{
    [Fact]
    public void GetHandles_ReturnsEightHandlesInExpectedOrder()
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

        var handles = Panel2DResizeHandleService.GetHandles(element);

        Assert.Equal(8, handles.Count);
        Assert.Equal(ResizeHandleKind.TopLeft, handles[0].Kind);
        Assert.Equal(ResizeHandleKind.Top, handles[1].Kind);
        Assert.Equal(ResizeHandleKind.TopRight, handles[2].Kind);
        Assert.Equal(ResizeHandleKind.Left, handles[3].Kind);
        Assert.Equal(ResizeHandleKind.Right, handles[4].Kind);
        Assert.Equal(ResizeHandleKind.BottomLeft, handles[5].Kind);
        Assert.Equal(ResizeHandleKind.Bottom, handles[6].Kind);
        Assert.Equal(ResizeHandleKind.BottomRight, handles[7].Kind);
    }
}
