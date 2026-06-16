Task 03 - Native Core DLL ABI Layer
Goal

Create a clean C# native DLL ABI layer for fruit-machine emulator cores.

This layer replaces the legacy TechsClass.cpp style with strongly typed C# wrappers.

Scope

Implement the native DLL loader and export binding layer.

Do not integrate with Play View yet.

Do not implement the full run loop yet.

Background

The legacy reference class dynamically resolves many functions from native core DLLs.

Example core families:

System6
Epoch
MPU4, possibly later

The old code uses LoadLibrary, GetProcAddress, FARPROC, and repeated local typedefs.

The new implementation should use:

NativeLibrary.Load
NativeLibrary.GetExport
Marshal.GetDelegateForFunctionPointer<TDelegate>
Strongly typed delegates
Safe disposal
Clear errors for missing exports
Suggested Types
INativeCoreLibrary
public interface INativeCoreLibrary : IDisposable
{
    string LibraryPath { get; }
    bool IsLoaded { get; }
}
NativeLibraryLoader

Responsibilities:

Load native library from path
Resolve function pointer
Convert pointer to delegate
Throw meaningful exception if loading or binding fails
Free native library handle on dispose
NativeCoreExportException

Use for:

Missing export
Invalid export
Unsupported function signature
System6NativeLibrary

Contains strongly typed delegates for System6 exports.

Initial required exports:

SYSTEM6Initialise
SYSTEM6LoadROM
SYSTEM6Reset
SYSTEM6Run
SYSTEM6Shutdown
SYSTEM6GetLampsOn
SYSTEM6GetLampBrightness
SYSTEM6GetPosOut
SYSTEM6TurnSwitchOn
SYSTEM6TurnSwitchOff

Add more exports only when needed for Task 04.

EpochNativeLibrary

Stub or partial implementation is acceptable.

Do not overbuild Epoch before System6 is proven.

Calling Convention

The legacy code uses __cdecl.

Delegates should use:

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
String Marshalling

Legacy functions use char*.

Initial wrappers should use ANSI strings:

[MarshalAs(UnmanagedType.LPStr)] string

Be careful with returned char* functions. Avoid exposing unmanaged pointers directly unless the lifetime is known.

Bitness

Native DLL bitness must match the Oasis Editor process bitness.

Document this in comments and diagnostics.

If DLL loading fails, include:

DLL path
process architecture
original exception/error
Success Criteria
Native library binding code compiles.
Loader can be unit tested without real DLLs where possible.
No MAME behaviour changes.
No Play View integration yet.
Deliverable Summary

When complete, report:

Files added
Delegate signatures added
Known unknowns
Tests run
Recommended next task
