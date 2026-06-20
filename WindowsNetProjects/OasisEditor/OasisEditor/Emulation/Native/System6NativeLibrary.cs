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
    private readonly string? _lampsUpdateExportName;
    private readonly System6GetLampBrightnessDelegate _getLampBrightness;
    private readonly System6GetPosOutDelegate _getPosOut;
    private readonly System6GetAlphaSegmentsDelegate? _getAlphaSegments;
    private readonly System6GetAlphaBrightnessDelegate? _getAlphaBrightness;
    private readonly System6SetPercentDelegate? _setPercent;
    private readonly System6SetCoinEnableDelegate _setCoinEnable;
    private readonly System6SetCoinValueDelegate _setCoinValue;
    private readonly System6SetLockoutValDelegate _setLockoutVal;
    private readonly System6SetLockoutInvertDelegate _setLockoutInvert;
    private readonly System6SetEnableDelegate _setEnable;
    private readonly System6SetCounterInDelegate _setCounterIn;
    private readonly System6SetCounterOutDelegate _setCounterOut;
    private readonly System6SetPortIndexDelegate _setPortIndex;
    private readonly System6SetCoinDelegate _setCoin;
    private readonly System6SetLevelDelegate _setLevel;
    private readonly System6SetFullLevelDelegate _setFullLevel;
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
        (_lampsUpdate, _lampsUpdateExportName) = TryBindOptionalExportCandidate<System6LampsUpdateDelegate>("SYSTEM6UpdateLamps");
        _getLampBrightness = _loader.BindExport<System6GetLampBrightnessDelegate>("SYSTEM6GetLampBrightness");
        _getPosOut = _loader.BindExport<System6GetPosOutDelegate>("SYSTEM6GetPosOut");
        _getAlphaSegments = TryBindOptionalExport<System6GetAlphaSegmentsDelegate>("SYSTEM6GetAlphaSegments");
        _getAlphaBrightness = TryBindOptionalExport<System6GetAlphaBrightnessDelegate>("SYSTEM6GetAlphaBright");
        _setPercent = TryBindOptionalExport<System6SetPercentDelegate>("SetPercent");
        _setCoinEnable = _loader.BindExport<System6SetCoinEnableDelegate>("SYSTEM6SetCoinEnable");
        _setCoinValue = _loader.BindExport<System6SetCoinValueDelegate>("SYSTEM6SetCoinValue");
        _setLockoutVal = _loader.BindExport<System6SetLockoutValDelegate>("SYSTEM6SetLockoutVal");
        _setLockoutInvert = _loader.BindExport<System6SetLockoutInvertDelegate>("SYSTEM6SetLockoutInvert");
        _setEnable = _loader.BindExport<System6SetEnableDelegate>("SYSTEM6SetEnable");
        _setCounterIn = _loader.BindExport<System6SetCounterInDelegate>("SYSTEM6SetCounterIn");
        _setCounterOut = _loader.BindExport<System6SetCounterOutDelegate>("SYSTEM6SetCounterOut");
        _setPortIndex = _loader.BindExport<System6SetPortIndexDelegate>("SYSTEM6SetPortIndex");
        _setCoin = _loader.BindExport<System6SetCoinDelegate>("SYSTEM6SetCoin");
        _setLevel = _loader.BindExport<System6SetLevelDelegate>("SYSTEM6SetLevel");
        _setFullLevel = _loader.BindExport<System6SetFullLevelDelegate>("SYSTEM6SetFullLevel");
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

    public string? LampsUpdateExportName => _lampsUpdateExportName;

    public void LampsUpdate()
    {
        var lampsUpdate = _lampsUpdate ?? throw new NotSupportedException("System6 native core does not export LampsUpdate.");
        lampsUpdate();
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

    public bool IsAlphaBrightnessPollingAvailable => _getAlphaBrightness is not null;

    public byte GetAlphaBrightness()
    {
        var getAlphaBrightness = _getAlphaBrightness ?? throw new NotSupportedException("System6 native core does not export SYSTEM6GetAlphaBright.");
        return getAlphaBrightness();
    }

    public bool IsSetPercentAvailable => _setPercent is not null;

    public void SetPercent(byte percent)
    {
        var setPercent = _setPercent ?? throw new NotSupportedException("System6 native core does not export SetPercent.");
        setPercent(percent);
    }

    public void SetCoinEnable(byte num, byte coin, byte coinEnable) => _setCoinEnable(num, coin, coinEnable);

    public void SetCoinValue(byte num, byte coin, byte coinValue) => _setCoinValue(num, coin, coinValue);

    public void SetLockoutVal(byte num, byte coin, byte lockoutValue) => _setLockoutVal(num, coin, lockoutValue);

    public void SetLockoutInvert(byte num, byte coin, byte lockoutInvert) => _setLockoutInvert(num, coin, lockoutInvert);

    public void SetEnable(byte num, byte enable) => _setEnable(num, enable);

    public void SetCounterIn(byte num, byte counterIn) => _setCounterIn(num, counterIn);

    public void SetCounterOut(byte num, byte counterOut) => _setCounterOut(num, counterOut);

    public void SetPortIndex(byte num, byte portIndex) => _setPortIndex(num, portIndex);

    public void SetCoin(byte num, byte coin) => _setCoin(num, coin);

    public void SetLevel(byte num, byte level) => _setLevel(num, level);

    public void SetFullLevel(byte num, byte fullLevel) => _setFullLevel(num, fullLevel);

    public void TurnSwitchOn(int switchIndex) => _turnSwitchOn(checked((byte)switchIndex));

    public void TurnSwitchOff(int switchIndex) => _turnSwitchOff(checked((byte)switchIndex));

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
        var (export, _) = TryBindOptionalExportCandidate<TDelegate>(exportName);
        return export;
    }

    private (TDelegate? Export, string? ExportName) TryBindOptionalExportCandidate<TDelegate>(params string[] exportNames)
        where TDelegate : Delegate
    {
        foreach (var exportName in exportNames)
        {
            if (_loader.TryBindExport<TDelegate>(exportName, out var export))
            {
                return (export, exportName);
            }
        }

        System.Diagnostics.Debug.WriteLine($"System6 native optional exports {string.Join("/", exportNames)} unavailable in '{_loader.LibraryPath}'.");
        return (null, null);
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
    [return: MarshalAs(UnmanagedType.I1)]
    public delegate bool System6GetLampsOnDelegate(ushort lampIndex);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate float System6GetLampBrightnessDelegate(ushort lampIndex);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate short System6GetPosOutDelegate(sbyte positionIndex);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int System6GetAlphaSegmentsDelegate(byte index);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate byte System6GetAlphaBrightnessDelegate();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void System6SetPercentDelegate(byte percent);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void System6SetCoinEnableDelegate(byte num, byte coin, byte coinEnable);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void System6SetCoinValueDelegate(byte num, byte coin, byte coinValue);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void System6SetLockoutValDelegate(byte num, byte coin, byte lockoutValue);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void System6SetLockoutInvertDelegate(byte num, byte coin, byte lockoutInvert);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void System6SetEnableDelegate(byte num, byte enable);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void System6SetCounterInDelegate(byte num, byte counterIn);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void System6SetCounterOutDelegate(byte num, byte counterOut);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void System6SetPortIndexDelegate(byte num, byte portIndex);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void System6SetCoinDelegate(byte num, byte coin);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void System6SetLevelDelegate(byte num, byte level);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void System6SetFullLevelDelegate(byte num, byte fullLevel);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void System6TurnSwitchOnDelegate(byte switchIndex);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void System6TurnSwitchOffDelegate(byte switchIndex);
}
