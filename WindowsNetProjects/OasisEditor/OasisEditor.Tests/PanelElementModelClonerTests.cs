namespace OasisEditor.Tests;

public sealed class PanelElementModelClonerTests
{
    [Fact]
    public void Clone_WhenWidthAndHeightProvided_OverridesGeometry()
    {
        var source = new PanelElementModel
        {
            ObjectId = "id-1",
            Name = "Lamp 1",
            Kind = PanelElementKind.Lamp,
            X = 10,
            Y = 20,
            Width = 30,
            Height = 40,
            IsVisible = true
        };

        var clone = PanelElementModelCloner.Clone(source, x: 11, y: 22, width: 33, height: 44);

        Assert.Equal(11, clone.X);
        Assert.Equal(22, clone.Y);
        Assert.Equal(33, clone.Width);
        Assert.Equal(44, clone.Height);
    }

    [Fact]
    public void Clone_WhenWidthAndHeightNotProvided_KeepsSourceGeometry()
    {
        var source = new PanelElementModel
        {
            ObjectId = "id-2",
            Name = "Lamp 2",
            Kind = PanelElementKind.Lamp,
            X = 1,
            Y = 2,
            Width = 3,
            Height = 4,
            IsVisible = true
        };

        var clone = PanelElementModelCloner.Clone(source, x: 5, y: 6);

        Assert.Equal(5, clone.X);
        Assert.Equal(6, clone.Y);
        Assert.Equal(3, clone.Width);
        Assert.Equal(4, clone.Height);
    }
}
