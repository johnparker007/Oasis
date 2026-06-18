using System.IO;
using System.Runtime.InteropServices;

namespace OasisEditor;

public sealed class NativeLibraryLoader : INativeCoreLibrary
{
    private IntPtr _libraryHandle;
    private bool _disposed;

    public NativeLibraryLoader(string libraryPath)
    {
        if (string.IsNullOrWhiteSpace(libraryPath))
        {
            throw new ArgumentException("Native core library path must not be empty.", nameof(libraryPath));
        }

        LibraryPath = libraryPath;
        ProcessArchitecture = RuntimeInformation.ProcessArchitecture;

        try
        {
            _libraryHandle = NativeLibrary.Load(libraryPath);
        }
        catch (Exception ex) when (ex is DllNotFoundException or BadImageFormatException or FileLoadException or ArgumentException)
        {
            throw NativeCoreExportException.CreateLoadFailure(libraryPath, ProcessArchitecture, ex);
        }
    }

    public string LibraryPath { get; }

    public Architecture ProcessArchitecture { get; }

    public bool IsLoaded => _libraryHandle != IntPtr.Zero;

    public TDelegate BindExport<TDelegate>(string exportName)
        where TDelegate : Delegate
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (string.IsNullOrWhiteSpace(exportName))
        {
            throw new ArgumentException("Native core export name must not be empty.", nameof(exportName));
        }

        IntPtr exportAddress;
        try
        {
            exportAddress = NativeLibrary.GetExport(_libraryHandle, exportName);
        }
        catch (Exception ex) when (ex is EntryPointNotFoundException or ArgumentNullException)
        {
            throw NativeCoreExportException.CreateMissingExport(LibraryPath, ProcessArchitecture, exportName, ex);
        }

        try
        {
            return Marshal.GetDelegateForFunctionPointer<TDelegate>(exportAddress);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidCastException or MarshalDirectiveException)
        {
            throw NativeCoreExportException.CreateInvalidExportBinding(LibraryPath, ProcessArchitecture, exportName, typeof(TDelegate), ex);
        }
    }


    public bool TryBindExport<TDelegate>(string exportName, out TDelegate? export)
        where TDelegate : Delegate
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (string.IsNullOrWhiteSpace(exportName))
        {
            throw new ArgumentException("Native core export name must not be empty.", nameof(exportName));
        }

        export = null;
        if (!NativeLibrary.TryGetExport(_libraryHandle, exportName, out var exportAddress))
        {
            return false;
        }

        try
        {
            export = Marshal.GetDelegateForFunctionPointer<TDelegate>(exportAddress);
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidCastException or MarshalDirectiveException)
        {
            throw NativeCoreExportException.CreateInvalidExportBinding(LibraryPath, ProcessArchitecture, exportName, typeof(TDelegate), ex);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_libraryHandle != IntPtr.Zero)
        {
            NativeLibrary.Free(_libraryHandle);
            _libraryHandle = IntPtr.Zero;
        }

        _disposed = true;
    }
}
