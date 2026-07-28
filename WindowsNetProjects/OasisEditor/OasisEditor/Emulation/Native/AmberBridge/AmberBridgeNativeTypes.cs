using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace OasisEditor;

internal static class AmberApiVersions
{
    internal const uint V1 = 0x00010000u;
    internal const uint V2 = 0x00020000u;

    internal static string Format(uint version) => version is V1 or V2
        ? $"API v{version >> 16} (0x{version:X8})"
        : $"API version 0x{version:X8}";
}

internal enum AmberResult : int
{
    Ok, InvalidArgument, UnsupportedVersion, DllLoadFailed, ExportMissing, InvalidState,
    InstanceLimit, InitialiseFailed, InternalError, NoMoreItems, BufferTooSmall,
    NotSupported, InvalidRange, MalformedConfiguration
}

internal static class AmberNativeConstants
{
    internal const uint StructureVersion1 = 1;
    internal const int MaximumSwitches = 256, MaximumMatrixLamps = 512, MaximumReels = 8,
        MaximumAlphaDisplays = 2, AlphaCharacters = 16, MaximumSevenSegmentDisplays = 40,
        MaximumCoinChannels = 6, MaximumCoinRoutes = 8;
    internal const ulong SwitchInput = 1, OutputSnapshot = 2, Audio = 4, ReelConfiguration = 8,
        CoinConfiguration = 16, PercentageSwitch = 32;
    internal const uint AudioPcmS16 = 1, AudioInterleaved = 1, CoinApplyLockoutPort = 1;
    internal const byte AlphaDecimalPoint = 1, AlphaCommaTail = 2;
}

[StructLayout(LayoutKind.Sequential)] internal unsafe struct AmberCapabilitiesV1Native
{ internal uint StructSize, Version; internal ulong FeatureBits; internal uint MaximumSwitches; internal fixed uint Reserved[3]; }
[StructLayout(LayoutKind.Sequential)] internal struct AmberLampStateV1Native
{ internal uint IsOn, BrightnessQ16_16; }
[StructLayout(LayoutKind.Sequential)] internal unsafe struct AmberAlphaDisplayStateV1Native
{ internal fixed ushort SegmentMasks[16]; internal fixed byte DotComma[16]; internal uint BrightnessQ16_16; }
[StructLayout(LayoutKind.Sequential)] internal struct AmberSevenSegmentStateV1Native
{ internal uint SegmentMask, BrightnessQ16_16; }
[StructLayout(LayoutKind.Sequential)] internal unsafe struct AmberOutputSnapshotV1Native
{
    internal uint StructSize, Version, MatrixLampCount, ReelCount, AlphaDisplayCount, SevenSegmentDisplayCount;
    internal fixed uint Reserved[4];
    internal fixed byte MatrixLamps[512 * 8];
    internal fixed int ReelPositions[8];
    internal fixed byte AlphaDisplays[2 * 52];
    internal fixed byte SevenSegmentDisplays[40 * 8];
}
[StructLayout(LayoutKind.Sequential)] internal unsafe struct AmberAudioFormatV1Native
{ internal uint StructSize, Version, SampleRate, Channels, SampleFormat, Interleaving; internal fixed uint Reserved[2]; }
[StructLayout(LayoutKind.Sequential)] internal struct AmberReelConfigV1Native
{ internal uint ReelIndex, Enabled, Steps, OptoStart, OptoEnd, OptoInvert; }
[StructLayout(LayoutKind.Sequential)] internal unsafe struct AmberReelConfigurationV1Native
{ internal uint StructSize, Version, ReelCount, ApplyMask; internal fixed byte Reels[8 * 24]; }
[StructLayout(LayoutKind.Sequential)] internal struct AmberCoinChannelConfigV1Native
{ internal uint ChannelIndex, Enabled, Value, LockoutInvert, Reserved; }
[StructLayout(LayoutKind.Sequential)] internal struct AmberCoinRouteConfigV1Native
{ internal uint RouteIndex, Enabled, CounterIn, CounterOut, PortIndex, CoinCode, Level, FullLevel; }
[StructLayout(LayoutKind.Sequential)] internal unsafe struct AmberCoinConfigurationV1Native
{
    internal uint StructSize, Version, ChannelApplyMask, RouteApplyMask;
    internal fixed byte Channels[6 * 20]; internal fixed byte Routes[8 * 32];
    internal uint LockoutPortBase, LockoutPortValue, ConfigurationFlags, Reserved;
}

[StructLayout(LayoutKind.Sequential)]
internal struct AmberBridgeInfoNative { internal uint StructSize; internal uint ApiVersion; internal IntPtr Name; internal IntPtr BridgeVersion; }

[StructLayout(LayoutKind.Sequential)]
internal struct AmberCoreInfoNative { internal uint StructSize; internal IntPtr CoreId; internal IntPtr DisplayName; }

[StructLayout(LayoutKind.Sequential)]
internal struct AmberInitialiseParamsNative
{
    internal uint StructSize;
    internal IntPtr Program0, Program1, Program2, Program3;
    internal IntPtr Sound0, Sound1, Sound2, Sound3;
}

[StructLayout(LayoutKind.Sequential)]
internal struct AmberApiV1Native
{
    internal uint StructSize, ApiVersion;
    internal IntPtr GetBridgeInfo, EnumerateCore, Create, Destroy, Initialise, Reset, Run, Shutdown, GetLastError;
}

// API v2 is an ABI-stable extension of the complete v1 prefix. Keeping the
// fields flattened makes every offset directly testable and keeps marshalling
// blittable (80-byte prefix plus eight x64 function pointers = 144 bytes).
[StructLayout(LayoutKind.Sequential)]
internal struct AmberApiV2Native
{
    internal uint StructSize, ApiVersion;
    internal IntPtr GetBridgeInfo, EnumerateCore, Create, Destroy, Initialise, Reset, Run, Shutdown, GetLastError;
    internal IntPtr GetCapabilities, SetSwitchState, GetOutputSnapshot, GetAudioFormat,
        FillAudioFrames, ConfigureReels, ConfigureCoins, SetPercentageSwitch;
}

[UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate AmberResult AmberGetApiDelegate(uint version, uint apiSize, ref AmberApiV2Native api);
[UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate AmberResult GetBridgeInfoDelegate(ref AmberBridgeInfoNative info);
[UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate AmberResult EnumerateCoreDelegate(uint index, ref AmberCoreInfoNative info);
[UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate AmberResult CreateDelegate(IntPtr coreId, out IntPtr handle);
[UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate AmberResult HandleDelegate(IntPtr handle);
[UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate AmberResult InitialiseDelegate(IntPtr handle, ref AmberInitialiseParamsNative parameters);
[UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate AmberResult RunDelegate(IntPtr handle, uint cycles, out int cyclesRun);
[UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate AmberResult GetLastErrorDelegate(IntPtr handle, IntPtr buffer, uint capacity, out uint required);
[UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate AmberResult GetCapabilitiesDelegate(IntPtr handle, ref AmberCapabilitiesV1Native capabilities);
[UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate AmberResult SetSwitchStateDelegate(IntPtr handle, uint switchIndex, uint isOn);
[UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal unsafe delegate AmberResult GetOutputSnapshotDelegate(IntPtr handle, AmberOutputSnapshotV1Native* snapshot);
[UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate AmberResult GetAudioFormatDelegate(IntPtr handle, ref AmberAudioFormatV1Native format);
[UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal unsafe delegate AmberResult FillAudioFramesDelegate(IntPtr handle, short* samples, uint frameCapacity, out uint framesWritten);
[UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal unsafe delegate AmberResult ConfigureReelsDelegate(IntPtr handle, AmberReelConfigurationV1Native* configuration);
[UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal unsafe delegate AmberResult ConfigureCoinsDelegate(IntPtr handle, AmberCoinConfigurationV1Native* configuration);
[UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate AmberResult SetPercentageSwitchDelegate(IntPtr handle, uint rawValue);

internal interface IAmberBridgeModule : IDisposable
{
    AmberGetApiDelegate BindAmberGetApi();
}

internal sealed class AmberBridgeModule : IAmberBridgeModule
{
    private readonly NativeLibraryLoader _loader;
    internal AmberBridgeModule(string path)
    {
        if (!Path.IsPathFullyQualified(path)) throw new ArgumentException("Amber Bridge path must be absolute.", nameof(path));
        Debug.WriteLine("Amber Bridge: Loading AmberBridge.dll.");
        _loader = new NativeLibraryLoader(path);
    }
    public AmberGetApiDelegate BindAmberGetApi() => _loader.BindExport<AmberGetApiDelegate>("AmberGetApi");
    public void Dispose() => _loader.Dispose();
}

internal interface IAmberStringAllocator
{
    IntPtr Allocate(string value);
    void Free(IntPtr pointer);
}

internal sealed class AmberStringAllocator : IAmberStringAllocator
{
    public IntPtr Allocate(string value) => Marshal.StringToCoTaskMemUTF8(value);
    public void Free(IntPtr pointer) => Marshal.FreeCoTaskMem(pointer);
}
