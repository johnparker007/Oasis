using OasisEditor;
using Xunit;

namespace OasisEditor.Tests;

public sealed class MameReelRuntimeAdapterTests
{
    [Theory]
    [InlineData(FruitMachinePlatformType.Impact, 12, -0.025d)]
    [InlineData(FruitMachinePlatformType.Impact, 16, -0.08d)]
    [InlineData(FruitMachinePlatformType.Impact, 24, 0d)]
    public void ResolvePlatformBandOffsetNormalized_Impact_UsesConfiguredStopOffsets(
        FruitMachinePlatformType platform,
        int stops,
        double expected)
    {
        var actual = MameReelRuntimeAdapter.ResolvePlatformBandOffsetNormalized(platform, stops);

        Assert.Equal(expected, actual, 6);
    }
}
