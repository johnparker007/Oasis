using System.Runtime.InteropServices;

namespace OasisEditor;

public sealed class System6NativeLibrary : INativeCoreLibrary
{
    private readonly NativeLibraryLoader _loader;

    public System6NativeLibrary(string libraryPath)
        : this(new NativeLibraryLoader(libraryPath))
    {
    }

    public System6NativeLibrary(NativeLibraryLoader loader)
    {
        _loader = loader ?? throw new ArgumentNullException(nameof(loader));

        Initialise = _loader.BindExport<System6InitialiseDelegate>("SYSTEM6Initialise");
        LoadROM = _loader.BindExport<System6LoadRomDelegate>("SYSTEM6LoadROM");
        Reset = _loader.BindExport<System6ResetDelegate>("SYSTEM6Reset");
        Run = _loader.BindExport<System6RunDelegate>("SYSTEM6Run");
        Shutdown = _loader.BindExport<System6ShutdownDelegate>("SYSTEM6Shutdown");
        GetLampsOn = _loader.BindExport<System6GetLampsOnDelegate>("SYSTEM6GetLampsOn");
        GetLampBrightness = _loader.BindExport<System6GetLampBrightnessDelegate>("SYSTEM6GetLampBrightness");
        GetPosOut = _loader.BindExport<System6GetPosOutDelegate>("SYSTEM6GetPosOut");
        TurnSwitchOn = _loader.BindExport<System6TurnSwitchOnDelegate>("SYSTEM6TurnSwitchOn");
        TurnSwitchOff = _loader.BindExport<System6TurnSwitchOffDelegate>("SYSTEM6TurnSwitchOff");
    }

    public string LibraryPath => _loader.LibraryPath;

    public Architecture ProcessArchitecture => _loader.ProcessArchitecture;

    public bool IsLoaded => _loader.IsLoaded;

    public System6InitialiseDelegate Initialise { get; }

    public System6LoadRomDelegate LoadROM { get; }

    public System6ResetDelegate Reset { get; }

    public System6RunDelegate Run { get; }

    public System6ShutdownDelegate Shutdown { get; }

    public System6GetLampsOnDelegate GetLampsOn { get; }

    public System6GetLampBrightnessDelegate GetLampBrightness { get; }

    public System6GetPosOutDelegate GetPosOut { get; }

    public System6TurnSwitchOnDelegate TurnSwitchOn { get; }

    public System6TurnSwitchOffDelegate TurnSwitchOff { get; }

    public void Dispose()
    {
        _loader.Dispose();
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int System6InitialiseDelegate();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int System6LoadRomDelegate([MarshalAs(UnmanagedType.LPStr)] string romPath);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void System6ResetDelegate();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void System6RunDelegate();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void System6ShutdownDelegate();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int System6GetLampsOnDelegate();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int System6GetLampBrightnessDelegate(int lampIndex);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int System6GetPosOutDelegate(int positionIndex);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void System6TurnSwitchOnDelegate(int switchIndex);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void System6TurnSwitchOffDelegate(int switchIndex);
}
