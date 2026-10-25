using System;
using System.Runtime.InteropServices;

namespace OasisPlayer.Fabric
{
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

        internal readonly CreateRuntime CreateRuntimeFn;
        internal readonly DestroyRuntime DestroyRuntimeFn;
        internal readonly GetError RuntimeError;
        internal readonly CreateSession CreateSessionFn;
        internal readonly DestroySession DestroySessionFn;
        internal readonly SessionOperation Initialise, Reset, Shutdown;
        internal readonly Advance AdvanceFn;
        internal readonly SubmitInput SubmitInputFn;
        internal readonly GetCapabilities GetCapabilitiesFn;
        internal readonly GetSnapshot GetSnapshotFn;
        internal readonly GetAudioFormat GetAudioFormatFn;
        internal readonly ReadAudio ReadAudioFn;
        internal readonly GetError SessionError;

        internal FabricNativeExports(nint module, string path)
        {
            CreateRuntimeFn = Get<CreateRuntime>(module, path, "FabricCreateRuntime");
            DestroyRuntimeFn = Get<DestroyRuntime>(module, path, "FabricDestroyRuntime");
            RuntimeError = Get<GetError>(module, path, "FabricRuntimeGetLastError");
            CreateSessionFn = Get<CreateSession>(module, path, "FabricCreateSession");
            DestroySessionFn = Get<DestroySession>(module, path, "FabricDestroySession");
            Initialise = Get<SessionOperation>(module, path, "FabricSessionInitialise");
            Reset = Get<SessionOperation>(module, path, "FabricSessionReset");
            AdvanceFn = Get<Advance>(module, path, "FabricSessionAdvance");
            Shutdown = Get<SessionOperation>(module, path, "FabricSessionShutdown");
            SubmitInputFn = Get<SubmitInput>(module, path, "FabricSessionSubmitInput");
            GetCapabilitiesFn = Get<GetCapabilities>(module, path, "FabricSessionGetCapabilities");
            GetSnapshotFn = Get<GetSnapshot>(module, path, "FabricSessionGetSnapshot");
            GetAudioFormatFn = Get<GetAudioFormat>(module, path, "FabricSessionGetAudioFormat");
            ReadAudioFn = Get<ReadAudio>(module, path, "FabricSessionReadAudio");
            SessionError = Get<GetError>(module, path, "FabricSessionGetLastError");
        }

        private static T Get<T>(nint module, string path, string name) where T : Delegate
        {
            var address = FabricRuntimeLibrary.GetProcAddress(module, name);
            if (address == IntPtr.Zero)
                throw new EntryPointNotFoundException("Required Fabric export '" + name + "' is missing from '" + path + "'.");
            return Marshal.GetDelegateForFunctionPointer<T>(address);
        }
    }
}
