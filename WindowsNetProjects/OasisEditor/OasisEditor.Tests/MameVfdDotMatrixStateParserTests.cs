using OasisEditor;
using Xunit;

namespace OasisEditor.Tests;

public sealed class MameVfdDotMatrixStateParserTests
{
    [Theory]
    [InlineData("vfddotmatrix0 = 1", 0, 1)]
    [InlineData("vfddotmatrix767 = 0", 767, 0)]
    [InlineData("vfddotmatrix767 = -1", 767, 0)]
    public void TryParse_DotMatrixLine_ParsesRangeAndNormalizesOnState(string line, int expectedIndex, int expectedValue)
    {
        var parser = new MameVfdDotMatrixStateParser();

        var ok = parser.TryParse(line, out var dotIndex, out var dotValue);

        Assert.True(ok);
        Assert.Equal(expectedIndex, dotIndex);
        Assert.Equal(expectedValue, dotValue);
    }

    [Theory]
    [InlineData("vfddotmatrix768 = 1")]
    [InlineData("vfddotmatrix-1 = 1")]
    [InlineData("vfd0 = 1")]
    public void TryParse_InvalidDotMatrixLine_ReturnsFalse(string line)
    {
        var parser = new MameVfdDotMatrixStateParser();

        Assert.False(parser.TryParse(line, out _, out _));
    }
}
