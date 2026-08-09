using OasisEditor;
using Xunit;

namespace OasisEditor.Tests;

public sealed class InternalReelOffsetResolverTests
{
    [Fact]
    public void ResolveNormalizedOffset_ReturnsExactEpochTwelveStopCorrection()
    {
        Assert.Equal(-0.16d, InternalReelOffsetResolver.ResolveNormalizedOffset(FruitMachinePlatformType.Epoch, 12));
    }

    [Theory]
    [InlineData(FruitMachinePlatformType.MPU5, 16, -0.22d)]
    [InlineData(FruitMachinePlatformType.MPU5, 12, -0.075d)]
    [InlineData(FruitMachinePlatformType.MPU5, 24, 0d)]
    [InlineData(FruitMachinePlatformType.MPU4, 16, -0.05d)]
    [InlineData(FruitMachinePlatformType.Epoch, 12, -0.16d)]
    [InlineData(FruitMachinePlatformType.Epoch, 16, 0d)]
    [InlineData(FruitMachinePlatformType.Impact, 12, 0.025d)]
    [InlineData(FruitMachinePlatformType.Impact, 16, -0.018d)]
    [InlineData(FruitMachinePlatformType.Scorpion4, 12, 0.2d)]
    [InlineData(FruitMachinePlatformType.Scorpion4, 16, 0.671d)]
    [InlineData(FruitMachinePlatformType.None, 16, 0d)]
    [InlineData(FruitMachinePlatformType.MPU4, 12, 0d)]
    public void ResolveNormalizedOffset_ReturnsPlatformSpecificOffsetOrDefault(
        FruitMachinePlatformType platform,
        int stops,
        double expected)
    {
        Assert.Equal(expected, InternalReelOffsetResolver.ResolveNormalizedOffset(platform, stops), 6);
    }
}
