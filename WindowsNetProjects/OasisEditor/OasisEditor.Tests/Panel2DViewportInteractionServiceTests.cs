using System.Windows;
using Xunit;

namespace OasisEditor.Tests;

public sealed class Panel2DViewportInteractionServiceTests
{
    [Fact]
    public void ShouldStartDragSelection_WhenDistanceMeetsThreshold_ReturnsTrue()
    {
        var result = Panel2DViewportInteractionService.ShouldStartDragSelection(
            new Point(0, 0),
            new Point(3, 4),
            threshold: 5);

        Assert.True(result);
    }

    [Fact]
    public void HasDocumentDelta_WhenPointsEqual_ReturnsFalse()
    {
        var result = Panel2DViewportInteractionService.HasDocumentDelta(
            new Point(10, 20),
            new Point(10, 20));

        Assert.False(result);
    }
}
