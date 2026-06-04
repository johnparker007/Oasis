using OasisEditor;
using Xunit;

namespace OasisEditor.Tests;

public sealed class MameReelStateParserTests
{
    [Theory]
    [InlineData("sreel3 = 0", 3, 0)]
    [InlineData("sreel3 = 64170", 3, 94)]
    [InlineData("sreel3 = 65535", 3, 0)]
    [InlineData("sreel3 = -1", 3, 0)]
    public void TryParse_WhenSreelLine_ConvertsToLegacyReelPosition(string line, int expectedReelId, int expectedValue)
    {
        var parser = new MameReelStateParser();

        var parsed = parser.TryParse(line, out var reelId, out var reelValue);

        Assert.True(parsed);
        Assert.Equal(expectedReelId, reelId);
        Assert.Equal(expectedValue, reelValue);
    }

    [Theory]
    [InlineData("reel3 = 94", 3, 94)]
    [InlineData("reel3 94", 3, 94)]
    public void TryParse_WhenReelLine_UsesPositionDirectly(string line, int expectedReelId, int expectedValue)
    {
        var parser = new MameReelStateParser();

        var parsed = parser.TryParse(line, out var reelId, out var reelValue);

        Assert.True(parsed);
        Assert.Equal(expectedReelId, reelId);
        Assert.Equal(expectedValue, reelValue);
    }

    [Theory]
    [InlineData("reel3 = 96")]
    [InlineData("reel3 = -1")]
    [InlineData("unknown output")]
    public void TryParse_WhenLineUnsupported_ReturnsFalse(string line)
    {
        var parser = new MameReelStateParser();

        var parsed = parser.TryParse(line, out var reelId, out var reelValue);

        Assert.False(parsed);
        Assert.Equal(0, reelId);
        Assert.Equal(0, reelValue);
    }
}
