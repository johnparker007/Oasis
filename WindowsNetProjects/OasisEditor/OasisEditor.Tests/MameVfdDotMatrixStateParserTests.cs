using OasisEditor;
using Xunit;

namespace OasisEditor.Tests;

public sealed class MameVfdDotMatrixStateParserTests
{
    private readonly MameVfdDotMatrixStateParser _parser = new();

    [Theory]
    [InlineData("vfddotmatrix0 = 1", 0, true)]
    [InlineData("vfddotmatrix767 = 1", 767, true)]
    [InlineData("vfddotmatrix767 = 0", 767, false)]
    [InlineData("vfddotmatrix767 = -1", 767, false)]
    public void TryParse_ParsesSupportedDotRange(string line, int expectedIndex, bool expectedOn)
    {
        Assert.True(_parser.TryParse(line, out var dotIndex, out var isOn));
        Assert.Equal(expectedIndex, dotIndex);
        Assert.Equal(expectedOn, isOn);
    }

    [Theory]
    [InlineData("vfddotmatrix768 = 1")]
    [InlineData("vfddotmatrix-1 = 1")]
    [InlineData("vfd0 = 1")]
    public void TryParse_RejectsUnsupportedLines(string line)
    {
        Assert.False(_parser.TryParse(line, out _, out _));
    }
}
