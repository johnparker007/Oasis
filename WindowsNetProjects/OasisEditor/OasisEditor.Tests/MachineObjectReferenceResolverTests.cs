using OasisEditor;
using Xunit;

namespace OasisEditor.Tests;

public sealed class MachineObjectReferenceResolverTests
{
    [Theory]
    [InlineData(PanelElementKind.Lamp, 17, MachineObjectKind.Lamp, "17")]
    [InlineData(PanelElementKind.Reel, 2, MachineObjectKind.Reel, "2")]
    [InlineData(PanelElementKind.Alpha, 0, MachineObjectKind.AlphaDisplay, "0")]
    [InlineData(PanelElementKind.SevenSegment, 12, MachineObjectKind.SevenSegmentDisplay, "12")]
    public void TryGetReference_PanelRuntimeElement_UsesDisplayNumberAsSourceIdentifier(PanelElementKind kind, int displayNumber, MachineObjectKind expectedKind, string expectedId)
    {
        var element = new PanelElementModel
        {
            ObjectId = "visual-1",
            Kind = kind,
            DisplayNumber = displayNumber
        };

        var resolved = MachineObjectReferenceResolver.Instance.TryGetReference(element, out var reference);

        Assert.True(resolved);
        Assert.Equal(expectedKind, reference.Kind);
        Assert.Equal(expectedId, reference.Id);
    }

    [Fact]
    public void TryGetReference_PanelElementWithoutRuntimeIdentity_ReturnsFalse()
    {
        var element = new PanelElementModel
        {
            ObjectId = "image-1",
            Kind = PanelElementKind.Image
        };

        var resolved = MachineObjectReferenceResolver.Instance.TryGetReference(element, out var reference);

        Assert.False(resolved);
        Assert.True(reference.IsEmpty);
    }

    [Fact]
    public void TryGetReference_InputDefinition_UsesInputId()
    {
        var input = new InputDefinitionModel
        {
            Id = "start-button"
        };

        var resolved = MachineObjectReferenceResolver.Instance.TryGetReference(input, out var reference);

        Assert.True(resolved);
        Assert.Equal(MachineObjectKind.Input, reference.Kind);
        Assert.Equal("start-button", reference.Id);
    }
}
