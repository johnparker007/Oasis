using System.Runtime.InteropServices;

namespace OasisEditor;

public sealed class EpochNativeLibrary : INativeCoreLibrary
{
    private readonly NativeLibraryLoader _loader;

    public EpochNativeLibrary(string libraryPath)
    {
        _loader = new NativeLibraryLoader(libraryPath);
    }

    public string LibraryPath => _loader.LibraryPath;

    public Architecture ProcessArchitecture => _loader.ProcessArchitecture;

    public bool IsLoaded => _loader.IsLoaded;

    public void Dispose()
    {
        _loader.Dispose();
    }
}
