using Xunit;
using System.Reflection;
using System.Runtime.InteropServices;
using OasisEditor;

public sealed class FabricAbiLayoutTests
{
    [Fact]
    public void Mpu5NativeLayoutsMatchFabricHeader()
    {
        Assert.Equal(20, Marshal.SizeOf<FabricAmberMpu5ReelConfigNative>());
        Assert.Equal(176, Marshal.SizeOf<FabricAmberMpu5ReelConfigurationNative>());
        Assert.Equal(20, Marshal.SizeOf<FabricAmberMpu5CoinChannelConfigNative>());
        Assert.Equal(152, Marshal.SizeOf<FabricAmberMpu5CoinConfigurationNative>());
        Assert.Equal(60, Marshal.SizeOf<FabricAmberMpu5OptionsNative>());
        Assert.Equal(404, Marshal.SizeOf<FabricAmberMpu5ConfigurationNative>());
        Assert.Equal(16, Marshal.OffsetOf<FabricAmberMpu5ConfigurationNative>(nameof(FabricAmberMpu5ConfigurationNative.Reels)).ToInt32());
        Assert.Equal(192, Marshal.OffsetOf<FabricAmberMpu5ConfigurationNative>(nameof(FabricAmberMpu5ConfigurationNative.Coins)).ToInt32());
        Assert.Equal(344, Marshal.OffsetOf<FabricAmberMpu5ConfigurationNative>(nameof(FabricAmberMpu5ConfigurationNative.Options)).ToInt32());
    }

    [Fact]
    public void Mpu5SerializerWritesExactHeaderMasksAndAppliedZeroOptions()
    {
        var settings=new Mpu5NativeRomSettings { ConfigureReels=true,ConfigureCoins=true,ConfigureMachineOptions=true,ApplyPercentage=true,Percentage=0,ApplyHopperType=true,HopperType=Mpu5HopperType.Compact };
        settings.Reels[7].Apply=true; settings.Coins[5].Apply=true; settings.Coins[5].Value=255;
        var bytes=FabricAmberMpu5Configuration.FromMpu5(settings)!.ToNativeBytes();
        Assert.Equal(404,bytes.Length); Assert.Equal(0x354D4146u,BitConverter.ToUInt32(bytes,0)); Assert.Equal(1u,BitConverter.ToUInt32(bytes,8)); Assert.Equal(7u,BitConverter.ToUInt32(bytes,12));
        Assert.Equal(1u<<7,BitConverter.ToUInt32(bytes,28)); Assert.Equal(1u<<5,BitConverter.ToUInt32(bytes,204));
        Assert.Equal(FabricAmberMpu5Configuration.OptionPercentage|FabricAmberMpu5Configuration.OptionHopperType,BitConverter.ToUInt32(bytes,352));
        Assert.All(bytes[396..404], value=>Assert.Equal(0,value));
    }

    [Fact]
    public void Mpu5SerializerReturnsNullOnlyWithoutSelectedSections()
    {
        Assert.Null(FabricAmberMpu5Configuration.FromMpu5(new()));
        Assert.NotNull(FabricAmberMpu5Configuration.FromMpu5(new(){ConfigureMachineOptions=true}));
    }

    [Theory]
    [InlineData(0, 0, 0, 0)]
    [InlineData(3, 3, 2, 255)]
    public void Mpu5SerializerAcceptsConfirmedEnumAndCoinRanges(int communication, int hopper, int jumper, int coinValue)
    {
        var s=new Mpu5NativeRomSettings { ConfigureCoins=true,CommunicationStyle=(Mpu5CoinCommunicationStyle)communication,HopperType=(Mpu5HopperType)hopper,ReelJumperProfile0=(Mpu5ReelJumperProfile)jumper };
        s.Coins[0].Value=coinValue;
        Assert.Equal(404,FabricAmberMpu5Configuration.FromMpu5(s)!.ToNativeBytes().Length);
    }

    [Fact]
    public void Mpu5SerializerRejectsInvalidNativeRangesBeforeStartup()
    {
        var s=new Mpu5NativeRomSettings { ConfigureReels=true }; s.Reels[0].Steps=0;
        Assert.Throws<ArgumentOutOfRangeException>(()=>FabricAmberMpu5Configuration.FromMpu5(s));
        s=new(){ConfigureCoins=true}; s.Coins[0].Value=256;
        Assert.Throws<ArgumentOutOfRangeException>(()=>FabricAmberMpu5Configuration.FromMpu5(s));
    }

    [Fact]
    public void X64NativeLayoutsMatchPublishedAbi()
    {
        Assert.Equal(0x00040000u, FabricAbi.Version);
        if (IntPtr.Size != 8) return;
        Assert.Equal(1208, Marshal.SizeOf<FabricLaunchRequestNative>()); // fixed strings end at 1160; aligned pointers/counts end at 1208.
        Assert.Equal(40, Marshal.SizeOf<FabricRomResourceNative>());
        Assert.Equal(48, Marshal.SizeOf<FabricCapabilitiesNative>());
        Assert.Equal(88, Marshal.SizeOf<FabricInputNative>());
        Assert.Equal(72, Marshal.OffsetOf<FabricInputNative>(nameof(FabricInputNative.NumericalIndex)).ToInt32());
        Assert.Equal(76, Marshal.OffsetOf<FabricInputNative>(nameof(FabricInputNative.Kind)).ToInt32());
        Assert.Equal(80, Marshal.OffsetOf<FabricInputNative>(nameof(FabricInputNative.Active)).ToInt32());
        Assert.Equal(81, Marshal.OffsetOf<FabricInputNative>(nameof(FabricInputNative.CoinChannel)).ToInt32());
        Assert.Equal(82, Marshal.OffsetOf<FabricInputNative>(nameof(FabricInputNative.CoinValue)).ToInt32());
        Assert.Equal(84, Marshal.SizeOf<FabricLampNative>());
        Assert.Equal(80, Marshal.SizeOf<FabricReelNative>());
        Assert.Equal(164, Marshal.SizeOf<FabricCharacterDisplayNative>());
        Assert.Equal(160, Marshal.OffsetOf<FabricCharacterDisplayNative>("Brightness").ToInt32());
        Assert.Equal(208, Marshal.SizeOf<FabricSegmentDisplayNative>());
        Assert.Equal(2140, Marshal.SizeOf<FabricDotMatrixDisplayNative>());
        Assert.Equal(72, Marshal.OffsetOf<FabricDotMatrixDisplayNative>(nameof(FabricDotMatrixDisplayNative.Width)).ToInt32());
        Assert.Equal(88, Marshal.OffsetOf<FabricDotMatrixDisplayNative>(nameof(FabricDotMatrixDisplayNative.Dots)).ToInt32());
        Assert.Equal(2136, Marshal.OffsetOf<FabricDotMatrixDisplayNative>(nameof(FabricDotMatrixDisplayNative.Brightness)).ToInt32());
        Assert.Equal(96, Marshal.SizeOf<FabricMachineSnapshotNative>());
        Assert.Equal(80, Marshal.OffsetOf<FabricMachineSnapshotNative>(nameof(FabricMachineSnapshotNative.DotMatrices)).ToInt32());
        Assert.Equal(20, Marshal.SizeOf<FabricAudioFormatNative>());
        Assert.Equal(24, Marshal.SizeOf<AmberReelNative>());
        Assert.Equal(208, Marshal.SizeOf<AmberReelsNative>());
        Assert.Equal(20, Marshal.SizeOf<AmberCoinChannelNative>());
        Assert.Equal(32, Marshal.SizeOf<AmberCoinRouteNative>());
        Assert.Equal(0, Marshal.OffsetOf<AmberCoinsNative>(nameof(AmberCoinsNative.Size)).ToInt32());
        Assert.Equal(4, Marshal.OffsetOf<AmberCoinsNative>(nameof(AmberCoinsNative.Version)).ToInt32());
        Assert.Equal(8, Marshal.OffsetOf<AmberCoinsNative>(nameof(AmberCoinsNative.ChannelMask)).ToInt32());
        Assert.Equal(12, Marshal.OffsetOf<AmberCoinsNative>(nameof(AmberCoinsNative.RouteMask)).ToInt32());
        Assert.Equal(16, Marshal.OffsetOf<AmberCoinsNative>(nameof(AmberCoinsNative.CommunicationStyle)).ToInt32());
        Assert.Equal(20, Marshal.OffsetOf<AmberCoinsNative>(nameof(AmberCoinsNative.CommunicationInvert)).ToInt32());
        Assert.Equal(24, Marshal.OffsetOf<AmberCoinsNative>(nameof(AmberCoinsNative.PulseCycles)).ToInt32());
        Assert.Equal(28, Marshal.OffsetOf<AmberCoinsNative>(nameof(AmberCoinsNative.EdcEnabled)).ToInt32());
        Assert.Equal(32, Marshal.OffsetOf<AmberCoinsNative>("Channels").ToInt32());
        Assert.Equal(152, Marshal.OffsetOf<AmberCoinsNative>("Routes").ToInt32());
        Assert.Equal(408, Marshal.SizeOf<AmberCoinsNative>());
        Assert.Equal(648, Marshal.SizeOf<FabricAmberConfigurationNative>());
        Assert.Equal(224, Marshal.OffsetOf<FabricAmberConfigurationNative>(nameof(FabricAmberConfigurationNative.Coins)).ToInt32());
        Assert.Equal(632, Marshal.OffsetOf<FabricAmberConfigurationNative>(nameof(FabricAmberConfigurationNative.Percentage)).ToInt32());
        Assert.Equal(1160, Marshal.OffsetOf<FabricLaunchRequestNative>("RomPaths").ToInt32());
        Assert.Equal(0, Marshal.OffsetOf<FabricRomResourceNative>(nameof(FabricRomResourceNative.Size)).ToInt32());
        Assert.Equal(4, Marshal.OffsetOf<FabricRomResourceNative>(nameof(FabricRomResourceNative.Version)).ToInt32());
        Assert.Equal(8, Marshal.OffsetOf<FabricRomResourceNative>(nameof(FabricRomResourceNative.Role)).ToInt32());
        Assert.Equal(12, Marshal.OffsetOf<FabricRomResourceNative>(nameof(FabricRomResourceNative.Slot)).ToInt32());
        Assert.Equal(16, Marshal.OffsetOf<FabricRomResourceNative>("Path").ToInt32());
        Assert.Equal(24, Marshal.OffsetOf<FabricRomResourceNative>(nameof(FabricRomResourceNative.Reserved)).ToInt32());
        Assert.Equal(1UL << 6, (ulong)FabricCapability.CoinInput);
        Assert.Equal(1UL << 7, (ulong)FabricCapability.DotMatrixDisplays);
        Assert.Equal(9, (int)FabricResult.InputRejected);
    }

    [Fact]
    public unsafe void RomResourceTrailingReservedValuesDefaultToZero()
    {
        var resource = new FabricRomResourceNative();
        Assert.Equal(0ul, resource.Reserved[0]);
        Assert.Equal(0ul, resource.Reserved[1]);
        Assert.Null(typeof(FabricRomResourceNative).GetField("LoadAddress", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic));
    }

    [Fact]
    public void EveryNativeDelegateIsCdecl()
    {
        foreach (var type in typeof(FabricNativeExports).GetNestedTypes(BindingFlags.NonPublic))
            if (typeof(Delegate).IsAssignableFrom(type))
                Assert.Equal(CallingConvention.Cdecl, type.GetCustomAttribute<UnmanagedFunctionPointerAttribute>()!.CallingConvention);
    }

    [Fact]
    public void AmberV2SerializesGlobalCoinMechanismWithoutTruncatingPulseCycles()
    {
        var bytes = FabricAmberSystem6Configuration.FromSystem6(new System6NativeRomSettings
        {
            CoinCommunicationStyle = AmberCoinCommunicationStyle.Parallel,
            CoinCommunicationInvert = false,
            CoinPulseCycles = 800_000,
            CoinEdcEnabled = false,
            Coins = [new System6CoinSettings { Num = 0, Enabled = true, CoinEnable = 1, CoinValue = 0 }]
        }).ToNativeBytes();

        Assert.Equal(648, bytes.Length);
        Assert.Equal(2u, BitConverter.ToUInt32(bytes, 8));
        Assert.Equal(2u, BitConverter.ToUInt32(bytes, 228));
        Assert.Equal(0u, BitConverter.ToUInt32(bytes, 240));
        Assert.Equal(0u, BitConverter.ToUInt32(bytes, 244));
        Assert.Equal(800_000u, BitConverter.ToUInt32(bytes, 248));
        Assert.Equal(0u, BitConverter.ToUInt32(bytes, 252));

        const int channel0 = 256;
        Assert.Equal(0u, BitConverter.ToUInt32(bytes, channel0));
        Assert.Equal(1u, BitConverter.ToUInt32(bytes, channel0 + 4));
        Assert.Equal(0u, BitConverter.ToUInt32(bytes, channel0 + 8));
        Assert.Equal(0u, BitConverter.ToUInt32(bytes, channel0 + 12));
        Assert.Equal(0u, BitConverter.ToUInt32(bytes, channel0 + 16));
    }

    [Fact]
    public void AmberV2SerializesNonContiguousChannelsAndRoutesIntoIndexedSlots()
    {
        var bytes = FabricAmberSystem6Configuration.FromSystem6(new System6NativeRomSettings
        {
            Coins = [new System6CoinSettings { Num = 2, Enabled = true, CoinEnable = 1, CoinValue = 3 }]
        }).ToNativeBytes();

        Assert.Equal(1u << 2, BitConverter.ToUInt32(bytes, 232));
        Assert.Equal(1u << 2, BitConverter.ToUInt32(bytes, 236));
        Assert.All(bytes[256..296], value => Assert.Equal(0, value));
        Assert.Equal(2u, BitConverter.ToUInt32(bytes, 296));
        Assert.All(bytes[376..440], value => Assert.Equal(0, value));
        Assert.Equal(2u, BitConverter.ToUInt32(bytes, 440));
    }
}
