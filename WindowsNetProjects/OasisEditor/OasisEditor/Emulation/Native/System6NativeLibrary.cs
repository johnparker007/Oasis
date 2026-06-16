using System.Runtime.InteropServices;

namespace OasisEditor;

public sealed class System6NativeLibrary : ISystem6NativeLibrary
{
    private readonly NativeLibraryLoader _loader;
    private readonly System6InitialiseDelegate _initialise;
    private readonly System6LoadRomDelegate _loadRom;
    private readonly System6ResetDelegate _reset;
    private readonly System6RunDelegate _run;
    private readonly System6ShutdownDelegate _shutdown;
    private readonly System6GetLampsOnDelegate _getLampsOn;
    private readonly System6GetLampBrightnessDelegate _getLampBrightness;
    private readonly System6GetPosOutDelegate _getPosOut;
    private readonly System6TurnSwitchOnDelegate _turnSwitchOn;
    private readonly System6TurnSwitchOffDelegate _turnSwitchOff;

    public System6NativeLibrary(string libraryPath)
        : this(new NativeLibraryLoader(libraryPath))
    {
    }

    public System6NativeLibrary(NativeLibraryLoader loader)
    {
        _loader = loader ?? throw new ArgumentNullException(nameof(loader));

        _initialise = _loader.BindExport<System6InitialiseDelegate>("SYSTEM6Initialise");
        _loadRom = _loader.BindExport<System6LoadRomDelegate>("SYSTEM6LoadROM");
        _reset = _loader.BindExport<System6ResetDelegate>("SYSTEM6Reset");
        _run = _loader.BindExport<System6RunDelegate>("SYSTEM6Run");
        _shutdown = _loader.BindExport<System6ShutdownDelegate>("SYSTEM6Shutdown");
        _getLampsOn = _loader.BindExport<System6GetLampsOnDelegate>("SYSTEM6GetLampsOn");
        _getLampBrightness = _loader.BindExport<System6GetLampBrightnessDelegate>("SYSTEM6GetLampBrightness");
        _getPosOut = _loader.BindExport<System6GetPosOutDelegate>("SYSTEM6GetPosOut");
        _turnSwitchOn = _loader.BindExport<System6TurnSwitchOnDelegate>("SYSTEM6TurnSwitchOn");
        _turnSwitchOff = _loader.BindExport<System6TurnSwitchOffDelegate>("SYSTEM6TurnSwitchOff");
    }

    public string LibraryPath => _loader.LibraryPath;

    public Architecture ProcessArchitecture => _loader.ProcessArchitecture;

    public bool IsLoaded => _loader.IsLoaded;

    public int Initialise() => _initialise();

    public int LoadRom(string romPath) => _loadRom(romPath);

    public void Reset() => _reset();

    public void Run(int cycles) => _run(cycles);

    public void Shutdown() => _shutdown();

    public int GetLampsOn() => _getLampsOn();

    public int GetLampBrightness(int lampIndex) => _getLampBrightness(lampIndex);

    public int GetPosOut(int positionIndex) => _getPosOut(positionIndex);

    public void TurnSwitchOn(int switchIndex) => _turnSwitchOn(switchIndex);

    public void TurnSwitchOff(int switchIndex) => _turnSwitchOff(switchIndex);

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
    public delegate void System6RunDelegate(int cycles);

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
