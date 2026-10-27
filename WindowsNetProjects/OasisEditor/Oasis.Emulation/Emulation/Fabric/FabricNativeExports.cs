using System.Runtime.InteropServices;

namespace OasisEditor;

internal sealed class FabricNativeExports
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate FabricResult CreateRuntime(uint version, out nint runtime);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate void DestroyRuntime(nint runtime);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal unsafe delegate FabricResult GetError(nint handle, byte* buffer, uint size, out uint required);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal unsafe delegate FabricResult CreateSession(nint runtime, FabricLaunchRequestNative* request, out nint session);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate void DestroySession(nint session);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate FabricResult SessionOperation(nint session);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate FabricResult Advance(nint session, ulong nanoseconds);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal unsafe delegate FabricResult SubmitInput(nint session, FabricInputNative* input);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal unsafe delegate FabricResult GetCapabilities(nint session, FabricCapabilitiesNative* capabilities);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal unsafe delegate FabricResult GetSnapshot(nint session, FabricMachineSnapshotNative* snapshot);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal unsafe delegate FabricResult GetAudioFormat(nint session, FabricAudioFormatNative* format);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal unsafe delegate FabricResult ReadAudio(nint session, short* samples, uint frames, out uint written);

    internal readonly CreateRuntime CreateRuntimeFn; internal readonly DestroyRuntime DestroyRuntimeFn; internal readonly GetError RuntimeError;
    internal readonly CreateSession CreateSessionFn; internal readonly DestroySession DestroySessionFn;
    internal readonly SessionOperation Initialise,Reset,Shutdown; internal readonly Advance AdvanceFn; internal readonly SubmitInput SubmitInputFn;
    internal readonly GetCapabilities GetCapabilitiesFn; internal readonly GetSnapshot GetSnapshotFn; internal readonly GetAudioFormat GetAudioFormatFn;
    internal readonly ReadAudio ReadAudioFn; internal readonly GetError SessionError;

    internal FabricNativeExports(nint module, string path)
    {
        T Get<T>(string name) where T:Delegate { try { return Marshal.GetDelegateForFunctionPointer<T>(NativeLibrary.GetExport(module,name)); } catch (Exception e) { throw new EntryPointNotFoundException($"Required Fabric export '{name}' is missing from '{path}'.",e); } }
        CreateRuntimeFn=Get<CreateRuntime>("FabricCreateRuntime"); DestroyRuntimeFn=Get<DestroyRuntime>("FabricDestroyRuntime"); RuntimeError=Get<GetError>("FabricRuntimeGetLastError");
        CreateSessionFn=Get<CreateSession>("FabricCreateSession"); DestroySessionFn=Get<DestroySession>("FabricDestroySession"); Initialise=Get<SessionOperation>("FabricSessionInitialise"); Reset=Get<SessionOperation>("FabricSessionReset"); AdvanceFn=Get<Advance>("FabricSessionAdvance"); Shutdown=Get<SessionOperation>("FabricSessionShutdown"); SubmitInputFn=Get<SubmitInput>("FabricSessionSubmitInput"); GetCapabilitiesFn=Get<GetCapabilities>("FabricSessionGetCapabilities"); GetSnapshotFn=Get<GetSnapshot>("FabricSessionGetSnapshot"); GetAudioFormatFn=Get<GetAudioFormat>("FabricSessionGetAudioFormat"); ReadAudioFn=Get<ReadAudio>("FabricSessionReadAudio"); SessionError=Get<GetError>("FabricSessionGetLastError");
    }
}
