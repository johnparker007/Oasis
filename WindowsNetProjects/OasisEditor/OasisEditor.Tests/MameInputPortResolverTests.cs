using Xunit;

namespace OasisEditor.Tests;

public sealed class MameInputPortResolverTests
{
    private readonly MameInputPortResolver _resolver = new();

    [Fact]
    public void TryResolve_CoinInput_UsesLegacyCoinTarget()
    {
        var input = new InputDefinitionModel { Id = "1", Kind = InputDefinitionKind.Coin, CoinInput = true };

        var ok = _resolver.TryResolve(FruitMachinePlatformType.MPU4, input, out var target);

        Assert.True(ok);
        Assert.Equal("COINS", target.Tag);
        Assert.Equal("1", target.Mask);
    }

    [Fact]
    public void TryResolve_Mpu4Button_ResolvesTagAndMask()
    {
        var input = new InputDefinitionModel { Id = "1", Kind = InputDefinitionKind.Button, ButtonNumber = "10" };

        var ok = _resolver.TryResolve(FruitMachinePlatformType.MPU4, input, out var target);

        Assert.True(ok);
        Assert.Equal("ORANGE2", target.Tag);
        Assert.Equal("4", target.Mask);
    }

    [Theory]
    [InlineData("0", "IN0", "1")]
    [InlineData("17", "COINS", "2")]
    [InlineData("24", "STAKE", "1")]
    [InlineData("48", "CAB1", "1")]
    [InlineData("57", "CAB2", "2")]
    [InlineData("64", "DSW1", "1")]
    [InlineData("73", "DSW2", "2")]
    public void TryResolve_EpochButton_ResolvesTagAndMask(string buttonNumber, string expectedTag, string expectedMask)
    {
        var input = new InputDefinitionModel { Id = "1", Kind = InputDefinitionKind.Button, ButtonNumber = buttonNumber };

        var ok = _resolver.TryResolve(FruitMachinePlatformType.Epoch, input, out var target);

        Assert.True(ok);
        Assert.Equal(expectedTag, target.Tag);
        Assert.Equal(expectedMask, target.Mask);
    }

    [Fact]
    public void TryResolve_UnsupportedPlatform_ReturnsFalse()
    {
        var input = new InputDefinitionModel { Id = "1", Kind = InputDefinitionKind.Button, ButtonNumber = "1" };

        var ok = _resolver.TryResolve(FruitMachinePlatformType.MPU3, input, out _);

        Assert.False(ok);
    }
}
