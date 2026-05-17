using OasisEditor;
using Xunit;

namespace OasisEditor.Tests;

public sealed class MameVfdDutyParserTests
{
    [Fact]
    public void TryParseNormalized_Mpu4_UsesRangeZeroToThirtyOne()
    {
        var parser = new MameVfdDutyParser();

        var parsed = parser.TryParseNormalized("vfdduty0 = 31", FruitMachinePlatformType.MPU4, out var cellId, out var brightness);

        Assert.True(parsed);
        Assert.Equal(0, cellId);
        Assert.Equal(1d, brightness);
    }

    [Fact]
    public void TryParseNormalized_Scorpion4_UsesRangeZeroToSeven()
    {
        var parser = new MameVfdDutyParser();

        var parsed = parser.TryParseNormalized("vfdduty2 = 7", FruitMachinePlatformType.Scorpion4, out var cellId, out var brightness);

        Assert.True(parsed);
        Assert.Equal(2, cellId);
        Assert.Equal(1d, brightness);
    }

    [Fact]
    public void TryParseNormalized_UnknownPlatform_FallsBackToMpu4Range()
    {
        var parser = new MameVfdDutyParser();

        var parsed = parser.TryParseNormalized("vfdduty5 = 31", FruitMachinePlatformType.MPU3, out _, out var brightness);

        Assert.True(parsed);
        Assert.Equal(1d, brightness);
    }
}
