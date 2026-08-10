using System.Runtime.InteropServices;
using System.Text.Json;
using Xunit;

namespace OasisEditor.Tests;

public sealed class EpochSupportTests
{
    [Fact] public void ProjectSchemaIsSix() => Assert.Equal(7, EditorProject.CurrentSchemaVersion);
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
        Assert.Equal(20,Marshal.SizeOf<FabricAmberEpochCoinChannelConfigNative>());
        Assert.Equal(372,Marshal.SizeOf<FabricAmberEpochConfigurationNative>());
        Assert.Equal(0,Offset(nameof(FabricAmberEpochConfigurationNative.Magic)));
        Assert.Equal(4,Offset(nameof(FabricAmberEpochConfigurationNative.Size)));
        Assert.Equal(8,Offset(nameof(FabricAmberEpochConfigurationNative.Version)));
        Assert.Equal(12,Offset(nameof(FabricAmberEpochConfigurationNative.Flags)));
        Assert.Equal(16,Offset(nameof(FabricAmberEpochConfigurationNative.FlashRomMode)));
        Assert.Equal(20,Offset(nameof(FabricAmberEpochConfigurationNative.ReelCount)));
        Assert.Equal(24,Offset(nameof(FabricAmberEpochConfigurationNative.ReelApplyMask)));
        Assert.Equal(28,Offset(nameof(FabricAmberEpochConfigurationNative.ReelExt)));
        Assert.Equal(32,Offset(nameof(FabricAmberEpochConfigurationNative.Reels)));
        Assert.Equal(192,Offset(nameof(FabricAmberEpochConfigurationNative.CommunicationStyle)));
        Assert.Equal(196,Offset(nameof(FabricAmberEpochConfigurationNative.CommunicationInvert)));
        Assert.Equal(200,Offset(nameof(FabricAmberEpochConfigurationNative.PulseCycles)));
        Assert.Equal(204,Offset(nameof(FabricAmberEpochConfigurationNative.EdcEnabled)));
        Assert.Equal(208,Offset(nameof(FabricAmberEpochConfigurationNative.CoinChannelCount)));
        Assert.Equal(212,Offset(nameof(FabricAmberEpochConfigurationNative.CoinApplyMask)));
        Assert.Equal(216,Offset(nameof(FabricAmberEpochConfigurationNative.Coins)));
        Assert.Equal(336,Offset(nameof(FabricAmberEpochConfigurationNative.OptionsApplyMask)));
        Assert.Equal(340,Offset(nameof(FabricAmberEpochConfigurationNative.DipSwitchBits)));
        Assert.Equal(344,Offset(nameof(FabricAmberEpochConfigurationNative.Stake)));
        Assert.Equal(348,Offset(nameof(FabricAmberEpochConfigurationNative.Prize)));
        Assert.Equal(352,Offset(nameof(FabricAmberEpochConfigurationNative.Percentage)));
        Assert.Equal(356,Offset(nameof(FabricAmberEpochConfigurationNative.Reserved)));
    }
    [Fact] public void SerializerWritesAllSectionsAndMasks()
    {
        var settings=new EpochNativeRomSettings{FlashRomMode=true,ConfigureReels=true,ApplyReelExt=true,ReelExt=9,ConfigureCoins=true,ConfigureMachineOptions=true,ApplyDips=true,ApplyStake=true,ApplyPrize=true,ApplyPercentage=true,DipSwitchBits=0xabcd,Stake=5,Prize=100,Percentage=8};
        foreach(var reel in settings.Reels) reel.Apply=true; foreach(var coin in settings.Coins) coin.Apply=true;
        var bytes=FabricAmberEpochConfiguration.FromEpoch(settings).ToNativeBytes();
        Assert.Equal(372,bytes.Length);
        Assert.Equal(0x50454146u,BitConverter.ToUInt32(bytes,0));
        Assert.Equal(372u,BitConverter.ToUInt32(bytes,4));
        Assert.Equal(1u,BitConverter.ToUInt32(bytes,8));
        Assert.Equal(15u,BitConverter.ToUInt32(bytes,12));
        Assert.Equal(1u,BitConverter.ToUInt32(bytes,16));
        Assert.Equal(8u,BitConverter.ToUInt32(bytes,20));
        Assert.Equal(0xffu,BitConverter.ToUInt32(bytes,24));
        Assert.Equal(9u,BitConverter.ToUInt32(bytes,28));
        Assert.Equal(0u,BitConverter.ToUInt32(bytes,192));
        Assert.Equal(0u,BitConverter.ToUInt32(bytes,196));
        Assert.Equal(800_000u,BitConverter.ToUInt32(bytes,200));
        Assert.Equal(0u,BitConverter.ToUInt32(bytes,204));
        Assert.Equal(6u,BitConverter.ToUInt32(bytes,208));
        Assert.Equal(0x3fu,BitConverter.ToUInt32(bytes,212));
        Assert.Equal(15u,BitConverter.ToUInt32(bytes,336));
        Assert.Equal(0xabcdu,BitConverter.ToUInt32(bytes,340));
        Assert.Equal(5u,BitConverter.ToUInt32(bytes,344));
        Assert.Equal(100u,BitConverter.ToUInt32(bytes,348));
        Assert.Equal(8u,BitConverter.ToUInt32(bytes,352));
        Assert.All(Enumerable.Range(0,4),index=>Assert.Equal(0u,BitConverter.ToUInt32(bytes,356+index*4)));
    }
    [Fact] public void ReelExtensionUsesDedicatedFlagAndFlatValue()
    {
        var bytes=FabricAmberEpochConfiguration.FromEpoch(new EpochNativeRomSettings{ApplyReelExt=true,ReelExt=255}).ToNativeBytes();
        Assert.Equal(FabricAmberEpochConfiguration.ConfigureReelExt,BitConverter.ToUInt32(bytes,12));
        Assert.Equal(255u,BitConverter.ToUInt32(bytes,28));
    }
    [Fact] public void AppliedCoinDenominationsFollowFabricRange()
    {
        var settings=new EpochNativeRomSettings();
        settings.Coins[0].Apply=true;
        settings.Coins[0].Value=13;
        Assert.Throws<ArgumentOutOfRangeException>(()=>FabricAmberEpochConfiguration.FromEpoch(settings));
        settings.Coins[0].Value=12;
        settings.Coins[0].LockoutValue=13;
        Assert.Throws<ArgumentOutOfRangeException>(()=>FabricAmberEpochConfiguration.FromEpoch(settings));
    }
    [Fact] public void RomResourcesAreTypedAndValidated()
    {
        var settings=new EpochNativeRomSettings{ProgramRom1Path="p0",ProgramRom2Path="p1",SoundRom1Path="s0"};
        var resources=FabricEmulationBackend.BuildRomResources(settings);
        Assert.Equal([FabricRomRole.Program,FabricRomRole.Program,FabricRomRole.Sound],resources.Select(x=>x.Role));
        Assert.Throws<InvalidOperationException>(()=>FabricEmulationBackend.BuildRomResources(new EpochNativeRomSettings()));
        settings.ProgramRom2Path=""; settings.ProgramRom3Path="p2";
        Assert.Throws<InvalidOperationException>(()=>FabricEmulationBackend.BuildRomResources(settings));
    }
    [Fact] public void EpochUsesAmberAlphaOrdering() => Assert.True(AlphaCellOrder.IsAmberBackedPlatform(FruitMachinePlatformType.Epoch));

    private static int Offset(string fieldName) => Marshal.OffsetOf<FabricAmberEpochConfigurationNative>(fieldName).ToInt32();
}
