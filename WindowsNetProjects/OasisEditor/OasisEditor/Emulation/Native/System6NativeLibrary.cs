using System.Runtime.InteropServices;

namespace OasisEditor;

public sealed class System6NativeLibrary : ISystem6NativeLibrary
{
    private readonly NativeLibraryLoader _loader;
    private readonly System6InitialiseDelegate _initialise;
    private readonly System6LoadRomDelegate _loadRom;
    private readonly System6LoadSoundRomDelegate _loadSoundRom;
    private readonly System6SetStepsDelegate _setSteps;
    private readonly System6SetOptoStartDelegate _setOptoStart;
    private readonly System6SetOptoEndDelegate _setOptoEnd;
    private readonly System6SetOptoInvertDelegate _setOptoInvert;
    private readonly System6ResetDelegate _reset;
    private readonly System6RunDelegate _run;
    private readonly System6ShutdownDelegate _shutdown;
    private readonly System6GetLampsOnDelegate _getLampsOn;
    private readonly System6LampsUpdateDelegate? _lampsUpdate;
    private readonly System6LampsOnDelegate? _lampsOn;
    private readonly System6GetLampBrightnessDelegate _getLampBrightness;
    private readonly System6GetPosOutDelegate _getPosOut;
    private readonly System6GetAlphaSegmentsDelegate? _getAlphaSegments;
    private readonly System6SetPercentDelegate? _setPercent;
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
        _setSteps = _loader.BindExport<System6SetStepsDelegate>("SetSteps");
        _setOptoStart = _loader.BindExport<System6SetOptoStartDelegate>("SetOptoStart");
        _setOptoEnd = _loader.BindExport<System6SetOptoEndDelegate>("SetOptoEnd");
        _setOptoInvert = _loader.BindExport<System6SetOptoInvertDelegate>("SetOptoInvert");
        _reset = _loader.BindExport<System6ResetDelegate>("SYSTEM6Reset");
        _run = _loader.BindExport<System6RunDelegate>("SYSTEM6Run");
        _shutdown = _loader.BindExport<System6ShutdownDelegate>("SYSTEM6Shutdown");
        _getLampsOn = _loader.BindExport<System6GetLampsOnDelegate>("SYSTEM6GetLampsOn");
        _lampsUpdate = TryBindOptionalExport<System6LampsUpdateDelegate>("LampsUpdate");
        _lampsOn = TryBindOptionalExport<System6LampsOnDelegate>("LampsOn");
        _getLampBrightness = _loader.BindExport<System6GetLampBrightnessDelegate>("SYSTEM6GetLampBrightness");
        _getPosOut = _loader.BindExport<System6GetPosOutDelegate>("SYSTEM6GetPosOut");
        _getAlphaSegments = TryBindOptionalExport<System6GetAlphaSegmentsDelegate>("SYSTEM6GetAlphaSegments");
        _setPercent = TryBindOptionalExport<System6SetPercentDelegate>("SetPercent");
        _turnSwitchOn = _loader.BindExport<System6TurnSwitchOnDelegate>("SYSTEM6TurnSwitchOn");
        _turnSwitchOff = _loader.BindExport<System6TurnSwitchOffDelegate>("SYSTEM6TurnSwitchOff");
    }

    public string LibraryPath => _loader.LibraryPath;

    public Architecture ProcessArchitecture => _loader.ProcessArchitecture;

    public bool IsLoaded => _loader.IsLoaded;

    public byte Initialise() => _initialise();

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

    public void SetSteps(byte reelNum, byte steps) => _setSteps(reelNum, steps);

    public void SetOptoStart(byte reelNum, byte start) => _setOptoStart(reelNum, start);

    public void SetOptoEnd(byte reelNum, byte end) => _setOptoEnd(reelNum, end);

    public void SetOptoInvert(byte reelNum, byte state) => _setOptoInvert(reelNum, state);

    public void Reset() => _reset();

    public int Run(int cycles) => _run(cycles);

    public byte Shutdown() => _shutdown();

    public bool IsLampsUpdateAvailable => _lampsUpdate is not null;

    public bool IsLampsOnAvailable => _lampsOn is not null;

    public void LampsUpdate()
    {
        var lampsUpdate = _lampsUpdate ?? throw new NotSupportedException("System6 native core does not export LampsUpdate.");
        lampsUpdate();
    }

    public bool LampsOn(ushort lampIndex) => LampsOnRaw(lampIndex) != 0;

    public byte LampsOnRaw(ushort lampIndex)
    {
        var lampsOn = _lampsOn ?? throw new NotSupportedException("System6 native core does not export LampsOn.");
        return lampsOn(lampIndex);
    }

    public bool GetLampsOn(ushort lampIndex) => _getLampsOn(lampIndex);

    public float GetLampBrightness(ushort lampIndex) => _getLampBrightness(lampIndex);

    public short GetPosOut(sbyte positionIndex) => _getPosOut(positionIndex);

    public bool IsAlphaSegmentPollingAvailable => _getAlphaSegments is not null;

    public int GetAlphaSegments(byte index)
    {
        var getAlphaSegments = _getAlphaSegments ?? throw new NotSupportedException("System6 native core does not export SYSTEM6GetAlphaSegments.");
        return getAlphaSegments(index);
    }

    public bool IsSetPercentAvailable => _setPercent is not null;

    public void SetPercent(byte percent)
    {
        var setPercent = _setPercent ?? throw new NotSupportedException("System6 native core does not export SetPercent.");
        setPercent(percent);
    }

    public void TurnSwitchOn(int switchIndex) => _turnSwitchOn(switchIndex);

    public void TurnSwitchOff(int switchIndex) => _turnSwitchOff(switchIndex);

    public void Dispose()
    {
        FreeRomPathBuffers();
        _loader.Dispose();
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate byte System6InitialiseDelegate();

    private TDelegate? TryBindOptionalExport<TDelegate>(string exportName)
        where TDelegate : Delegate
    {
        try
        {
            return _loader.BindExport<TDelegate>(exportName);
        }
        catch (NativeCoreExportException ex)
        {
            System.Diagnostics.Debug.WriteLine($"System6 native optional export {exportName} unavailable. {ex.Message}");
            return null;
        }
    }

    private IntPtr[] AllocateRomPathSlots(IReadOnlyList<string> romPaths)
    {
        // Keep ANSI path buffers alive for the lifetime of the native core.
        // Some legacy System6 DLLs retain the char* values passed to LoadROM
        // and read them later during Reset/Run rather than copying immediately.

        var paths = new[] { IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero };
        for (var i = 0; i < paths.Length; i++)
        {
            var path = i < romPaths.Count ? romPaths[i] : string.Empty;
            if (string.IsNullOrWhiteSpace(path))
            {
                paths[i] = IntPtr.Zero;
                continue;
            }

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
    public delegate void System6SetStepsDelegate(byte reelNum, byte steps);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void System6SetOptoStartDelegate(byte reelNum, byte start);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void System6SetOptoEndDelegate(byte reelNum, byte end);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void System6SetOptoInvertDelegate(byte reelNum, byte state);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void System6ResetDelegate();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int System6RunDelegate(int cycles);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate byte System6ShutdownDelegate();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void System6LampsUpdateDelegate();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate byte System6LampsOnDelegate(ushort lampIndex);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public delegate bool System6GetLampsOnDelegate(ushort lampIndex);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate float System6GetLampBrightnessDelegate(ushort lampIndex);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate short System6GetPosOutDelegate(sbyte positionIndex);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int System6GetAlphaSegmentsDelegate(byte index);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void System6SetPercentDelegate(byte percent);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void System6TurnSwitchOnDelegate(int switchIndex);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void System6TurnSwitchOffDelegate(int switchIndex);
}
