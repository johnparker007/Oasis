using Xunit;
using System.Reflection;
using System.Runtime.InteropServices;
using OasisEditor;

public sealed class FabricAbiLayoutTests
{
    [Fact]
    public void X64NativeLayoutsMatchPublishedAbi()
    {
        if (IntPtr.Size != 8) return;
        Assert.Equal(1208, Marshal.SizeOf<FabricLaunchRequestNative>()); // fixed strings end at 1160; aligned pointers/counts end at 1208.
        Assert.Equal(40, Marshal.SizeOf<FabricRomResourceNative>());
        Assert.Equal(48, Marshal.SizeOf<FabricCapabilitiesNative>());
        Assert.Equal(84, Marshal.SizeOf<FabricInputNative>());
        Assert.Equal(84, Marshal.SizeOf<FabricLampNative>());
        Assert.Equal(80, Marshal.SizeOf<FabricReelNative>());
        Assert.Equal(160, Marshal.SizeOf<FabricCharacterDisplayNative>());
        Assert.Equal(208, Marshal.SizeOf<FabricSegmentDisplayNative>());
        Assert.Equal(80, Marshal.SizeOf<FabricMachineSnapshotNative>());
        Assert.Equal(20, Marshal.SizeOf<FabricAudioFormatNative>());
        Assert.Equal(24, Marshal.SizeOf<AmberReelNative>());
        Assert.Equal(208, Marshal.SizeOf<AmberReelsNative>());
        Assert.Equal(20, Marshal.SizeOf<AmberCoinChannelNative>());
        Assert.Equal(32, Marshal.SizeOf<AmberCoinRouteNative>());
        Assert.Equal(408, Marshal.SizeOf<AmberCoinsNative>());
        Assert.Equal(648, Marshal.SizeOf<FabricAmberConfigurationNative>());
        Assert.Equal(1160, Marshal.OffsetOf<FabricLaunchRequestNative>("RomPaths").ToInt32());
        Assert.Equal(16, Marshal.OffsetOf<FabricRomResourceNative>("Path").ToInt32());
    }

    [Fact]
    public void EveryNativeDelegateIsCdecl()
    {
        foreach (var type in typeof(FabricNativeExports).GetNestedTypes(BindingFlags.NonPublic))
            if (typeof(Delegate).IsAssignableFrom(type))
                Assert.Equal(CallingConvention.Cdecl, type.GetCustomAttribute<UnmanagedFunctionPointerAttribute>()!.CallingConvention);
    }
}
