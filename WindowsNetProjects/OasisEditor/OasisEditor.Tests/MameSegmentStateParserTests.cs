using OasisEditor;
using Xunit;

namespace OasisEditor.Tests;

public sealed class MameSegmentStateParserTests
{
    [Fact]
    public void TryParse_VfdLine_ParsesCellAndMask()
    {
        var parser = new MameSegmentStateParser();
        var ok = parser.TryParse("vfd12 = 769", out var cellId, out var mask);
        Assert.True(ok);
        Assert.Equal(12, cellId);
        Assert.Equal(769, mask);
    }

    [Fact]
    public void TryParse_DigitLine_ParsesCellAndMask()
    {
        var parser = new MameSegmentStateParser();
        var ok = parser.TryParse("digit3 = 16", out var cellId, out var mask);
        Assert.True(ok);
        Assert.Equal(3, cellId);
        Assert.Equal(16, mask);
    }

    [Fact]
    public void TryParse_DigitLine_NormalizesToActiveHighByte()
    {
        var parser = new MameSegmentStateParser();
        var ok = parser.TryParse("digit4 = 255", out var cellId, out var mask);
        Assert.True(ok);
        Assert.Equal(4, cellId);
        Assert.Equal(255, mask);
    }

    [Fact]
    public void TryParse_DigitiLine_InvertsToActiveHighByte()
    {
        var parser = new MameSegmentStateParser();
        var ok = parser.TryParse("digiti4 = 255", out var cellId, out var mask);
        Assert.True(ok);
        Assert.Equal(4, cellId);
        Assert.Equal(0, mask);
    }

    [Fact]
    public void TryParse_DigitiLine_UsesExactOutputName()
    {
        var parser = new MameSegmentStateParser();
        var ok = parser.TryParse("digitid4 = 0", out _, out _);
        Assert.False(ok);
    }
}
