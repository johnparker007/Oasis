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
}
