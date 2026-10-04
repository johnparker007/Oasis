using OasisEditor;
using Xunit;

namespace OasisEditor.Tests;

public sealed class MachineObjectReferenceTests
{
    [Theory]
    [InlineData("lamp:17", MachineObjectKind.Lamp, "17")]
    [InlineData("reel:2", MachineObjectKind.Reel, "2")]
    [InlineData("alpha:0", MachineObjectKind.AlphaDisplay, "0")]
    [InlineData("sevenSegment:12", MachineObjectKind.SevenSegmentDisplay, "12")]
    [InlineData("input:Start", MachineObjectKind.Input, "Start")]
    [InlineData("object:ball08", MachineObjectKind.Object, "ball08")]
    public void TryParse_ValidReference_ReturnsMachineObjectReference(string value, MachineObjectKind expectedKind, string expectedId)
    {
        var parsed = MachineObjectReference.TryParse(value, out var reference);

        Assert.True(parsed);
        Assert.Equal(expectedKind, reference.Kind);
        Assert.Equal(expectedId, reference.Id);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("lamp")]
    [InlineData("unknown:1")]
    public void TryParse_InvalidReference_ReturnsFalse(string? value)
    {
        var parsed = MachineObjectReference.TryParse(value, out var reference);

        Assert.False(parsed);
        Assert.True(reference.IsEmpty);
    }

    [Fact]
    public void ToString_UsesStableStringBackedIdentity()
    {
        var reference = MachineObjectReference.SevenSegmentDisplay(12);

        Assert.Equal("sevenSegment:12", reference.ToString());
    }

    [Fact]
    public void Object_UsesCanonicalObjectPrefix() => Assert.Equal("object:ball08", MachineObjectReference.Object("ball08").ToString());

    [Theory]
    [InlineData("")]
    [InlineData("bad id")]
    [InlineData("bad:id")]
    [InlineData("bad/id")]
    [InlineData("mole.3")]
    public void Object_InvalidInstanceIdDoesNotCreateReference(string id)
    {
        var reference = MachineObjectReference.Object(id);

        Assert.True(reference.IsEmpty);
        Assert.NotEqual(MachineObjectKind.Object, reference.Kind);
    }

    [Theory]
    [InlineData("object:")]
    [InlineData("object:bad id")]
    [InlineData("object:bad:id")]
    [InlineData("object:bad/id")]
    public void TryParse_InvalidObjectInstanceId_ReturnsFalse(string value)
    {
        Assert.False(MachineObjectReference.TryParse(value, out var reference));
        Assert.True(reference.IsEmpty);
    }

    [Theory]
    [InlineData("cueBall")]
    [InlineData("ball01")]
    [InlineData("mole-3")]
    public void Object_ValidInstanceIdsRoundTrip(string id)
    {
        var formatted = MachineObjectReference.Object(id).ToString();

        Assert.Equal($"object:{id}", formatted);
        Assert.True(MachineObjectReference.TryParse(formatted, out var parsed));
        Assert.Equal(new MachineObjectReference(MachineObjectKind.Object, id), parsed);
    }
}
