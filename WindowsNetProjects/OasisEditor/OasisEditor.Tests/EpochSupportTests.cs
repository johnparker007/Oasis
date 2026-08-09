using System.Runtime.InteropServices;
using System.Text.Json;
using Xunit;

namespace OasisEditor.Tests;

public sealed class EpochSupportTests
{
    [Fact] public void ProjectSchemaIsFour() => Assert.Equal(4, EditorProject.CurrentSchemaVersion);
    [Fact] public void DefaultsAreIndependentAndComplete()
    {
        var first=new EpochNativeRomSettings(); var second=new EpochNativeRomSettings();
        Assert.Equal(8,first.Reels.Count); Assert.Equal(6,first.Coins.Count); Assert.All(first.Reels,x=>Assert.Equal(96,x.Steps));
        first.Reels[0].Steps=1; first.Coins[0].Value=3;
        Assert.Equal(96,second.Reels[0].Steps); Assert.Equal(0,second.Coins[0].Value);
    }
    [Fact] public void SettingsRoundTrip()
    {
        var value=new EpochNativeRomSettings{ProgramRom1Path="flash.bin",FlashRomMode=true,ConfigureReels=true,ApplyReelExt=true,ReelExt=7,ConfigureCoins=true,ConfigureMachineOptions=true,ApplyDips=true,DipSwitchBits=0x1234};
        value.Reels[7].Apply=true; value.Coins[5].Apply=true; value.Coins[5].LockoutValue=12;
        var restored=JsonSerializer.Deserialize<EpochNativeRomSettings>(JsonSerializer.Serialize(value))!;
        Assert.True(restored.FlashRomMode); Assert.True(restored.Reels[7].Apply); Assert.Equal(12,restored.Coins[5].LockoutValue); Assert.Equal((uint)0x1234,restored.DipSwitchBits);
    }
    [Fact] public void NativeLayoutMatchesFabricV3Header()
    {
        Assert.Equal(20,Marshal.SizeOf<FabricAmberEpochReelConfigNative>());
        Assert.Equal(184,Marshal.SizeOf<FabricAmberEpochReelConfigurationNative>());
        Assert.Equal(20,Marshal.SizeOf<FabricAmberEpochCoinChannelConfigNative>());
        Assert.Equal(152,Marshal.SizeOf<FabricAmberEpochCoinConfigurationNative>());
        Assert.Equal(44,Marshal.SizeOf<FabricAmberEpochOptionsNative>());
        Assert.Equal(400,Marshal.SizeOf<FabricAmberEpochConfigurationNative>());
        Assert.Equal(20,Marshal.OffsetOf<FabricAmberEpochConfigurationNative>(nameof(FabricAmberEpochConfigurationNative.Reels)).ToInt32());
        Assert.Equal(204,Marshal.OffsetOf<FabricAmberEpochConfigurationNative>(nameof(FabricAmberEpochConfigurationNative.Coins)).ToInt32());
        Assert.Equal(356,Marshal.OffsetOf<FabricAmberEpochConfigurationNative>(nameof(FabricAmberEpochConfigurationNative.Options)).ToInt32());
    }
    [Fact] public void SerializerWritesAllSectionsAndMasks()
    {
        var settings=new EpochNativeRomSettings{FlashRomMode=true,ConfigureReels=true,ApplyReelExt=true,ReelExt=9,ConfigureCoins=true,ConfigureMachineOptions=true,ApplyDips=true,ApplyStake=true,ApplyPrize=true,ApplyPercentage=true,DipSwitchBits=0xabcd,Stake=5,Prize=100,Percentage=8};
        foreach(var reel in settings.Reels) reel.Apply=true; foreach(var coin in settings.Coins) coin.Apply=true;
        var bytes=FabricAmberEpochConfiguration.FromEpoch(settings).ToNativeBytes();
        Assert.Equal(400,bytes.Length); Assert.Equal(1u,BitConverter.ToUInt32(bytes,16)); Assert.Equal(0xffu,BitConverter.ToUInt32(bytes,32)); Assert.Equal(0x3fu,BitConverter.ToUInt32(bytes,216)); Assert.Equal(15u,BitConverter.ToUInt32(bytes,364));
    }
    [Fact] public void RomResourcesAreTypedAndValidated()
    {
        var settings=new EpochNativeRomSettings{ProgramRom1Path="p0",ProgramRom2Path="p1",SoundRom1Path="s0"};
        var resources=FabricEmulationBackend.BuildRomResources(settings);
        Assert.Equal([FabricRomRole.Program,FabricRomRole.Program,FabricRomRole.Sound],resources.Select(x=>x.Role));
        Assert.Throws<InvalidOperationException>(()=>FabricEmulationBackend.BuildRomResources(new()));
        settings.ProgramRom2Path=""; settings.ProgramRom3Path="p2";
        Assert.Throws<InvalidOperationException>(()=>FabricEmulationBackend.BuildRomResources(settings));
    }
    [Fact] public void EpochUsesAmberAlphaOrdering() => Assert.True(AlphaCellOrder.IsAmberBackedPlatform(FruitMachinePlatformType.Epoch));
}
