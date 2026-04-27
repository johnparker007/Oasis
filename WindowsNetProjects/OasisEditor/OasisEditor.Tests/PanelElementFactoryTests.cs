using Xunit;

namespace OasisEditor.Tests;

public sealed class PanelElementFactoryTests
{
    [Theory]
    [InlineData(PanelElementKind.Background)]
    [InlineData(PanelElementKind.Lamp)]
    [InlineData(PanelElementKind.Reel)]
    [InlineData(PanelElementKind.SevenSegment)]
    [InlineData(PanelElementKind.Alpha)]
    public void CreateVisualFromElement_ForNativeKinds_PreservesElementKindForRoundTrip(PanelElementKind kind)
    {
        var source = new PanelElementFile
        {
            ObjectId = "obj-1",
            Name = "Element",
            Kind = Panel2DDocumentStorage.SerializeElementKind(kind),
            X = 10,
            Y = 20,
            Width = 100,
            Height = 40
        };

        var visual = PanelElementFactory.CreateVisualFromElement(source);

        Assert.NotNull(visual);
        Assert.Equal(kind, PanelElementFactory.GetElementKind(visual!));

        var roundTrip = PanelElementFactory.CreateElementFromVisual(visual!);
        Assert.NotNull(roundTrip);
        Assert.Equal(kind, roundTrip!.ElementKind);
    }
}
