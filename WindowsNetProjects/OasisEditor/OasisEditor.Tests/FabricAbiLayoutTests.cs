using Xunit;
using System.Reflection;
using System.Runtime.InteropServices;
using OasisEditor;

public sealed class FabricAbiLayoutTests
{
    [Fact]
    public void X64NativeLayoutsMatchPublishedAbi()
    {
        Assert.Equal(0x00030000u, FabricAbi.Version);
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
        Assert.Equal(80, Marshal.SizeOf<FabricMachineSnapshotNative>());
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
        Assert.Equal(16, Marshal.OffsetOf<FabricRomResourceNative>("Path").ToInt32());
        Assert.Equal(1UL << 6, (ulong)FabricCapability.CoinInput);
        Assert.Equal(9, (int)FabricResult.InputRejected);
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
        var bytes = FabricAmberConfiguration.FromSystem6(new System6NativeRomSettings
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
        var bytes = FabricAmberConfiguration.FromSystem6(new System6NativeRomSettings
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
