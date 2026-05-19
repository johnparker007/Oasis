using System.Windows;
using Xunit;

namespace OasisEditor.Tests;

public sealed class Panel2DMoveComputationServiceTests
{
    [Fact]
    public void ComputeMovedElement_AppliesDocumentSpaceDelta()
    {
        var source = new PanelElementModel
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

        var moved = Panel2DMoveComputationService.ComputeMovedElement(
            source,
            new Point(100, 200),
            new Point(115, 230));

        Assert.Equal(25, moved.X);
        Assert.Equal(50, moved.Y);
        Assert.Equal(30, moved.Width);
        Assert.Equal(40, moved.Height);
    }
}
