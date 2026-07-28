using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace OasisEditor;

internal static class AmberApiVersions
{
    internal const uint V1 = 0x00010000u;

    internal static string Format(uint version) => version == V1
        ? $"API v1 (0x{version:X8})"
        : $"API version 0x{version:X8}";
}

internal enum AmberResult : int
{
    Ok, InvalidArgument, UnsupportedVersion, DllLoadFailed, ExportMissing, InvalidState,
    InstanceLimit, InitialiseFailed, InternalError, NoMoreItems, BufferTooSmall
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

[UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate AmberResult AmberGetApiDelegate(uint version, uint apiSize, ref AmberApiV1Native api);
[UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate AmberResult GetBridgeInfoDelegate(ref AmberBridgeInfoNative info);
[UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate AmberResult EnumerateCoreDelegate(uint index, ref AmberCoreInfoNative info);
[UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate AmberResult CreateDelegate(IntPtr coreId, out IntPtr handle);
[UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate AmberResult HandleDelegate(IntPtr handle);
[UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate AmberResult InitialiseDelegate(IntPtr handle, ref AmberInitialiseParamsNative parameters);
[UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate AmberResult RunDelegate(IntPtr handle, uint cycles, out int cyclesRun);
[UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate AmberResult GetLastErrorDelegate(IntPtr handle, IntPtr buffer, uint capacity, out uint required);

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
