using System.Text.Json;

namespace OasisEditor.Tests;

public sealed class Mpu5ProjectSettingsViewModelTests
{
    [Fact]
    public void ReelEditsSynchronizeEntireProjectModel()
    {
        var model = new Mpu5NativeRomSettings();
        var saves = 0;
        var viewModel = new Mpu5ProjectSettingsViewModel(model, saved => { model = saved; saves++; });

        viewModel.Reels[2].Steps = 144;
        viewModel.Reels[2].OptoStart = 9;
        viewModel.Reels[2].OptoEnd = 12;
        viewModel.Reels[2].OptoInvert = true;
        viewModel.Reels[2].Apply = true;

        Assert.Equal(5, saves);
        Assert.Equal(144, model.Reels[2].Steps);
        Assert.Equal(9, model.Reels[2].OptoStart);
        Assert.Equal(12, model.Reels[2].OptoEnd);
        Assert.True(model.Reels[2].OptoInvert);
        Assert.True(model.Reels[2].Apply);
    }

    [Fact]
    public void CoinAndScalarEditsSynchronizeProjectModel()
    {
        var model = new Mpu5NativeRomSettings();
        var viewModel = new Mpu5ProjectSettingsViewModel(model, saved => model = saved);

        viewModel.ConfigureReels = true;
        viewModel.ConfigureCoins = true;
        viewModel.CommunicationStyle = Mpu5CoinCommunicationStyle.CcTalk;
        viewModel.PulseCycles = 123_456;
        viewModel.ConfigureMachineOptions = true;
        viewModel.ApplyPercentage = true;
        viewModel.Percentage = 7;
        viewModel.ApplyReelJumperProfile0 = true;
        viewModel.ReelJumperProfile0 = Mpu5ReelJumperProfile.LatePic3Reel5;
        viewModel.Coins[1].Apply = true;
        viewModel.Coins[1].Enabled = true;
        viewModel.Coins[1].Value = 20;

        Assert.True(model.ConfigureReels);
        Assert.True(model.ConfigureCoins);
        Assert.Equal(Mpu5CoinCommunicationStyle.CcTalk, model.CommunicationStyle);
        Assert.Equal(123_456u, model.PulseCycles);
        Assert.True(model.ConfigureMachineOptions);
        Assert.True(model.ApplyPercentage);
        Assert.Equal(7u, model.Percentage);
        Assert.True(model.ApplyReelJumperProfile0);
        Assert.Equal(Mpu5ReelJumperProfile.LatePic3Reel5, model.ReelJumperProfile0);
        Assert.True(model.Coins[1].Apply);
        Assert.True(model.Coins[1].Enabled);
        Assert.Equal(20, model.Coins[1].Value);
    }

    [Fact]
    public void EditedSettingsSurviveMetadataSerializationAndFeedRuntimeConfiguration()
    {
        var model = new Mpu5NativeRomSettings();
        var viewModel = new Mpu5ProjectSettingsViewModel(model, saved => model = saved);
        viewModel.ConfigureReels = true;
        viewModel.Reels[0].Apply = true;
        viewModel.Reels[0].Steps = 128;
        viewModel.Reels[0].OptoStart = 3;
        viewModel.Reels[0].OptoEnd = 6;

        var json = JsonSerializer.Serialize(model);
        var reloaded = JsonSerializer.Deserialize<Mpu5NativeRomSettings>(json)!;
        var runtime = FabricAmberMpu5Configuration.FromMpu5(reloaded);

        Assert.Equal(128, reloaded.Reels[0].Steps);
        Assert.Equal(3, reloaded.Reels[0].OptoStart);
        Assert.Equal(6, reloaded.Reels[0].OptoEnd);
        Assert.NotNull(runtime);
    }

    [Fact]
    public void DefaultsStillContainAllReelsAndCoinChannels()
    {
        var model = new Mpu5NativeRomSettings();
        var viewModel = new Mpu5ProjectSettingsViewModel(model, _ => { });

        Assert.Equal(8, viewModel.Reels.Count);
        Assert.Equal(6, viewModel.Coins.Count);
        Assert.Equal(Enumerable.Range(0, 8), viewModel.Reels.Select(reel => reel.ReelIndex));
        Assert.Equal(Enumerable.Range(0, 6), viewModel.Coins.Select(coin => coin.Channel));
    }
}
