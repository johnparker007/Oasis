using System.Runtime.InteropServices;

namespace OasisEditor;

public interface INativeCoreLibrary : IDisposable
{
    string LibraryPath { get; }

    Architecture ProcessArchitecture { get; }

    bool IsLoaded { get; }
}
