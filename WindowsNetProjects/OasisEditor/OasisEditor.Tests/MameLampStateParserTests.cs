using OasisEditor;
using Xunit;

namespace OasisEditor.Tests;

public sealed class MameLampStateParserTests
{
    [Theory]
    [InlineData("lamp0 0", 0, 0)]
    [InlineData("lamp12 1", 12, 1)]
    [InlineData(" lamp99 foo 255 ", 99, 255)]
    public void TryParse_ValidLines_ReturnsLampData(string line, int expectedLampId, int expectedValue)
    {
        var parser = new MameLampStateParser();

        var parsed = parser.TryParse(line, out var lampId, out var lampValue);

        Assert.True(parsed);
        Assert.Equal(expectedLampId, lampId);
        Assert.Equal(expectedValue, lampValue);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("reel0 1")]
    [InlineData("lampx 1")]
    [InlineData("lamp1")]
    [InlineData("lamp1 value")]
    public void TryParse_InvalidLines_ReturnsFalse(string line)
    {
        var parser = new MameLampStateParser();

        var parsed = parser.TryParse(line, out _, out _);

        Assert.False(parsed);
    }
}
