using System.Windows;
using Xunit;

namespace OasisEditor.Tests;

public sealed class Panel2DSelectionBoundsServiceTests
{
    [Fact]
    public void CreateNormalizedDocumentRect_NormalizesWhenDraggedUpLeft()
    {
        var rect = Panel2DSelectionBoundsService.CreateNormalizedDocumentRect(
            new Point(20, 30),
            new Point(5, 10));

        Assert.Equal(5, rect.Left);
        Assert.Equal(10, rect.Top);
        Assert.Equal(15, rect.Width);
        Assert.Equal(20, rect.Height);
    }
}
