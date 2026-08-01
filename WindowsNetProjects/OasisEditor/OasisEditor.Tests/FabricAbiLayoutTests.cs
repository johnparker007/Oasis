using Xunit;
using System.Reflection;
using System.Runtime.InteropServices;
using OasisEditor;

public sealed class FabricAbiLayoutTests
{
    [Fact]
    public void X64NativeLayoutsMatchPublishedAbi()
    {
        Assert.Equal(0x00020000u, FabricAbi.Version);
        if (IntPtr.Size != 8) return;
        Assert.Equal(1208, Marshal.SizeOf<FabricLaunchRequestNative>()); // fixed strings end at 1160; aligned pointers/counts end at 1208.
        Assert.Equal(40, Marshal.SizeOf<FabricRomResourceNative>());
        Assert.Equal(48, Marshal.SizeOf<FabricCapabilitiesNative>());
        Assert.Equal(84, Marshal.SizeOf<FabricInputNative>());
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
        Assert.Equal(408, Marshal.SizeOf<AmberCoinsNative>());
        Assert.Equal(648, Marshal.SizeOf<FabricAmberConfigurationNative>());
        Assert.Equal(0, Marshal.OffsetOf<FabricAmberConfigurationNative>("Magic").ToInt32());
        Assert.Equal(4, Marshal.OffsetOf<FabricAmberConfigurationNative>("Size").ToInt32());
        Assert.Equal(8, Marshal.OffsetOf<FabricAmberConfigurationNative>("Version").ToInt32());
        Assert.Equal(12, Marshal.OffsetOf<FabricAmberConfigurationNative>("Flags").ToInt32());
        Assert.Equal(16, Marshal.OffsetOf<FabricAmberConfigurationNative>("Reels").ToInt32());
        Assert.Equal(224, Marshal.OffsetOf<FabricAmberConfigurationNative>("Coins").ToInt32());
        Assert.Equal(632, Marshal.OffsetOf<FabricAmberConfigurationNative>("Percentage").ToInt32());
        Assert.Equal(8, Marshal.OffsetOf<AmberCoinsNative>("ChannelMask").ToInt32());
        Assert.Equal(12, Marshal.OffsetOf<AmberCoinsNative>("RouteMask").ToInt32());
        Assert.Equal(16, Marshal.OffsetOf<AmberCoinsNative>("Channels").ToInt32());
        Assert.Equal(136, Marshal.OffsetOf<AmberCoinsNative>("Routes").ToInt32());
        Assert.Equal(1160, Marshal.OffsetOf<FabricLaunchRequestNative>("RomPaths").ToInt32());
        Assert.Equal(16, Marshal.OffsetOf<FabricRomResourceNative>("Path").ToInt32());
    }

    [Fact]
    public void AmberConfigurationBytesContainPublishedHeaderAndCoinMasks()
    {
        var configuration = FabricAmberConfiguration.FromSystem6(new System6NativeRomSettings
        {
            Coins = [new() { Num = 2, Enabled = true, CoinEnable = 1, CoinValue = 20 }]
        });

        var bytes = configuration.ToNativeBytes();

        Assert.Equal(648, bytes.Length);
        Assert.Equal(FabricAmberConfiguration.NativeMagic, BitConverter.ToUInt32(bytes, 0));
        Assert.Equal(648u, BitConverter.ToUInt32(bytes, 4));
        Assert.Equal(FabricAmberConfiguration.NativeVersion, BitConverter.ToUInt32(bytes, 8));
        Assert.Equal(7u, BitConverter.ToUInt32(bytes, 12));
        Assert.Equal(408u, BitConverter.ToUInt32(bytes, 224));
        Assert.Equal(1u, BitConverter.ToUInt32(bytes, 228));
        Assert.Equal(1u << 2, BitConverter.ToUInt32(bytes, 232));
        Assert.Equal(1u << 2, BitConverter.ToUInt32(bytes, 236));
        Assert.Equal(2u, BitConverter.ToUInt32(bytes, 240));
        Assert.Equal(1u, BitConverter.ToUInt32(bytes, 244));
        Assert.Equal(20u, BitConverter.ToUInt32(bytes, 248));
        Assert.Equal(2u, BitConverter.ToUInt32(bytes, 360));
    }

    [Fact]
    public void EveryNativeDelegateIsCdecl()
    {
        foreach (var type in typeof(FabricNativeExports).GetNestedTypes(BindingFlags.NonPublic))
            if (typeof(Delegate).IsAssignableFrom(type))
                Assert.Equal(CallingConvention.Cdecl, type.GetCustomAttribute<UnmanagedFunctionPointerAttribute>()!.CallingConvention);
    }
}
