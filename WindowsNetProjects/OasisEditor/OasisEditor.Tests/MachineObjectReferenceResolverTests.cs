using OasisEditor;
using Xunit;

namespace OasisEditor.Tests;

public sealed class MachineObjectReferenceResolverTests
{
    [Theory]
    [InlineData("lamp", 17, MachineObjectKind.Lamp, "17")]
    [InlineData("reel", 2, MachineObjectKind.Reel, "2")]
    [InlineData("alpha", 0, MachineObjectKind.AlphaDisplay, "0")]
    [InlineData("sevenSegment", 12, MachineObjectKind.SevenSegmentDisplay, "12")]
    public void TryGetReference_PanelRuntimeElement_UsesDisplayNumberAsSourceIdentifier(string kindName, int displayNumber, MachineObjectKind expectedKind, string expectedId)
    {
        var element = new PanelElementModel
        {
            ObjectId = "visual-1",
            Kind = ResolvePanelElementKind(kindName),
            DisplayNumber = displayNumber
        };

        var resolved = MachineObjectReferenceResolver.Instance.TryGetReference(element, out var reference);

        Assert.True(resolved);
        Assert.Equal(expectedKind, reference.Kind);
        Assert.Equal(expectedId, reference.Id);
    }

    [Fact]
    public void TryGetReference_AlphaWithoutDisplayNumber_DefaultsToFirstAlphaDisplay()
    {
        var element = new PanelElementModel
        {
            ObjectId = "alpha-visual-1",
            Kind = PanelElementKind.Alpha
        };

        var resolved = MachineObjectReferenceResolver.Instance.TryGetReference(element, out var reference);

        Assert.True(resolved);
        Assert.Equal(MachineObjectKind.AlphaDisplay, reference.Kind);
        Assert.Equal("0", reference.Id);
    }

    private static PanelElementKind ResolvePanelElementKind(string kindName)
    {
        return kindName switch
        {
            "lamp" => PanelElementKind.Lamp,
            "reel" => PanelElementKind.Reel,
            "alpha" => PanelElementKind.Alpha,
            "sevenSegment" => PanelElementKind.SevenSegment,
            _ => PanelElementKind.Unknown
        };
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
