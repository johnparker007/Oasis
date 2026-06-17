using System.Runtime.InteropServices;

namespace OasisEditor;

public sealed class System6NativeLibrary : ISystem6NativeLibrary
{
    private readonly NativeLibraryLoader _loader;
    private readonly System6InitialiseDelegate _initialise;
    private readonly System6LoadRomDelegate _loadRom;
    private readonly System6LoadSoundRomDelegate _loadSoundRom;
    private readonly System6ResetDelegate _reset;
    private readonly System6RunDelegate _run;
    private readonly System6ShutdownDelegate _shutdown;
    private readonly System6GetLampsOnDelegate _getLampsOn;
    private readonly System6GetLampBrightnessDelegate _getLampBrightness;
    private readonly System6GetPosOutDelegate _getPosOut;
    private readonly System6TurnSwitchOnDelegate _turnSwitchOn;
    private readonly System6TurnSwitchOffDelegate _turnSwitchOff;
    private readonly List<IntPtr> _romPathBuffers = [];

    public System6NativeLibrary(string libraryPath)
        : this(new NativeLibraryLoader(libraryPath))
    {
    }

    public System6NativeLibrary(NativeLibraryLoader loader)
    {
        _loader = loader ?? throw new ArgumentNullException(nameof(loader));

        _initialise = _loader.BindExport<System6InitialiseDelegate>("SYSTEM6Initialise");
        _loadRom = _loader.BindExport<System6LoadRomDelegate>("SYSTEM6LoadROM");
        _loadSoundRom = _loader.BindExport<System6LoadSoundRomDelegate>("SYSTEM6LoadSoundROM");
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

    public int LoadRom(IReadOnlyList<string> programRomPaths, bool flashSwitch)
    {
        var paths = AllocateRomPathSlots(programRomPaths);
        return _loadRom(paths[0], paths[1], paths[2], paths[3], flashSwitch ? (byte)1 : (byte)0);
    }

    public int LoadSoundRom(IReadOnlyList<string> soundRomPaths)
    {
        var paths = AllocateRomPathSlots(soundRomPaths);
        return _loadSoundRom(paths[0], paths[1], paths[2], paths[3]);
    }

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
        FreeRomPathBuffers();
        _loader.Dispose();
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int System6InitialiseDelegate();

    private IntPtr[] AllocateRomPathSlots(IReadOnlyList<string> romPaths)
    {
        // Keep ANSI path buffers alive for the lifetime of the native core.
        // Some legacy System6 DLLs retain the char* values passed to LoadROM
        // and read them later during Reset/Run rather than copying immediately.

        var paths = new[] { IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero };
        for (var i = 0; i < paths.Length; i++)
        {
            var path = i < romPaths.Count ? romPaths[i] ?? string.Empty : string.Empty;
            paths[i] = Marshal.StringToHGlobalAnsi(path);
            _romPathBuffers.Add(paths[i]);
        }

        return paths;
    }

    private void FreeRomPathBuffers()
    {
        foreach (var buffer in _romPathBuffers)
        {
            if (buffer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        _romPathBuffers.Clear();
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int System6LoadRomDelegate(
        IntPtr romPath1,
        IntPtr romPath2,
        IntPtr romPath3,
        IntPtr romPath4,
        byte flashSwitch);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int System6LoadSoundRomDelegate(
        IntPtr romPath1,
        IntPtr romPath2,
        IntPtr romPath3,
        IntPtr romPath4);

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
