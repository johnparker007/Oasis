using OasisEditor;
using Xunit;

namespace OasisEditor.Tests;

public sealed class MameSegmentStateParserTests
{
    [Fact]
    public void TryParse_VfdLine_ParsesCellAndMask()
    {
        var parser = new MameSegmentStateParser();
        var ok = parser.TryParse("vfd12 = 769", out var cellId, out var mask, out var outputType);
        Assert.True(ok);
        Assert.Equal(12, cellId);
        Assert.Equal(769, mask);
        Assert.Equal(MameSegmentOutputType.Vfd, outputType);
    }

    [Fact]
    public void TryParse_DigitLine_ParsesCellAndMask()
    {
        var parser = new MameSegmentStateParser();
        var ok = parser.TryParse("digit3 = 16", out var cellId, out var mask, out var outputType);
        Assert.True(ok);
        Assert.Equal(3, cellId);
        Assert.Equal(16, mask);
        Assert.Equal(MameSegmentOutputType.Digit, outputType);
    }

    [Fact]
    public void TryParse_DigitLine_NormalizesToActiveHighByte()
    {
        var parser = new MameSegmentStateParser();
        var ok = parser.TryParse("digit4 = 255", out var cellId, out var mask, out var outputType);
        Assert.True(ok);
        Assert.Equal(4, cellId);
        Assert.Equal(255, mask);
        Assert.Equal(MameSegmentOutputType.Digit, outputType);
    }

    [Fact]
    public void TryParse_DigitiLine_InvertsToActiveHighByte()
    {
        var parser = new MameSegmentStateParser();
        var ok = parser.TryParse("digiti4 = 255", out var cellId, out var mask, out var outputType);
        Assert.True(ok);
        Assert.Equal(4, cellId);
        Assert.Equal(0, mask);
        Assert.Equal(MameSegmentOutputType.Digiti, outputType);
    }

    [Fact]
    public void TryParse_DigitiLine_UsesExactOutputName()
    {
        var parser = new MameSegmentStateParser();
        var ok = parser.TryParse("digitid4 = 0", out _, out _, out _);
        Assert.False(ok);
    }
}
