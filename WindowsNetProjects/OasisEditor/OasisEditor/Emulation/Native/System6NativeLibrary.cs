using System.Runtime.InteropServices;

namespace OasisEditor;

public sealed class System6NativeLibrary : ISystem6NativeLibrary
{
    private readonly NativeLibraryLoader _loader;
    private readonly System6InitialiseDelegate _initialise;
    private readonly System6LoadRomDelegate _loadRom;
    private readonly System6LoadSoundRomDelegate _loadSoundRom;
    private readonly System6SetByteByteDelegate _setSteps;
    private readonly System6SetByteByteDelegate _setOptoStart;
    private readonly System6SetByteByteDelegate _setOptoEnd;
    private readonly System6SetByteByteDelegate _setOptoInvert;
    private readonly System6ResetDelegate _reset;
    private readonly System6RunDelegate _run;
    private readonly System6ShutdownDelegate _shutdown;
    private readonly System6GetOutputSnapshotSizeDelegate _getOutputSnapshotSize;
    private readonly System6GetOutputSnapshotDelegate _getOutputSnapshot;
    private readonly System6SetByteDelegate? _setPercent;
    private readonly System6SetByteByteDelegate _setCoinEnable;
    private readonly System6SetByteByteDelegate _setCoinValue;
    private readonly System6SetByteByteDelegate _setLockoutVal;
    private readonly System6SetByteByteDelegate _setLockoutInvert;
    private readonly System6SetByteByteDelegate _setEnable;
    private readonly System6SetByteUIntDelegate _setCounterIn;
    private readonly System6SetByteUIntDelegate _setCounterOut;
    private readonly System6SetByteByteDelegate _setPortIndex;
    private readonly System6SetByteByteDelegate _setCoin;
    private readonly System6SetByteByteDelegate _setLevel;
    private readonly System6SetByteByteDelegate _setFullLevel;
    private readonly System6SetByteDelegate _turnSwitchOn;
    private readonly System6SetByteDelegate _turnSwitchOff;
    private readonly List<IntPtr> _romPathBuffers = [];

    public System6NativeLibrary(string libraryPath) : this(new NativeLibraryLoader(libraryPath)) { }

    public System6NativeLibrary(NativeLibraryLoader loader)
    {
        _loader = loader ?? throw new ArgumentNullException(nameof(loader));
        _initialise = _loader.BindExport<System6InitialiseDelegate>("Initialise");
        _loadRom = _loader.BindExport<System6LoadRomDelegate>("LoadROM");
        _loadSoundRom = _loader.BindExport<System6LoadSoundRomDelegate>("LoadSoundROM");
        _setSteps = _loader.BindExport<System6SetByteByteDelegate>("SetSteps");
        _setOptoStart = _loader.BindExport<System6SetByteByteDelegate>("SetOptoStart");
        _setOptoEnd = _loader.BindExport<System6SetByteByteDelegate>("SetOptoEnd");
        _setOptoInvert = _loader.BindExport<System6SetByteByteDelegate>("SetOptoInvert");
        _reset = _loader.BindExport<System6ResetDelegate>("Reset");
        _run = _loader.BindExport<System6RunDelegate>("Run");
        _shutdown = _loader.BindExport<System6ShutdownDelegate>("Shutdown");
        _getOutputSnapshotSize = _loader.BindExport<System6GetOutputSnapshotSizeDelegate>("GetOutputSnapshotSize");
        _getOutputSnapshot = _loader.BindExport<System6GetOutputSnapshotDelegate>("GetOutputSnapshot");
        _setPercent = TryBindOptionalExport<System6SetByteDelegate>("SetPercent");
        _setCoinEnable = _loader.BindExport<System6SetByteByteDelegate>("SetCoinEnable");
        _setCoinValue = _loader.BindExport<System6SetByteByteDelegate>("SetCoinValue");
        _setLockoutVal = _loader.BindExport<System6SetByteByteDelegate>("SetLockoutVal");
        _setLockoutInvert = _loader.BindExport<System6SetByteByteDelegate>("SetLockoutInvert");
        _setEnable = _loader.BindExport<System6SetByteByteDelegate>("SetEnable");
        _setCounterIn = _loader.BindExport<System6SetByteUIntDelegate>("SetCounterIn");
        _setCounterOut = _loader.BindExport<System6SetByteUIntDelegate>("SetCounterOut");
        _setPortIndex = _loader.BindExport<System6SetByteByteDelegate>("SetPortIndex");
        _setCoin = _loader.BindExport<System6SetByteByteDelegate>("SetCoin");
        _setLevel = _loader.BindExport<System6SetByteByteDelegate>("SetLevel");
        _setFullLevel = _loader.BindExport<System6SetByteByteDelegate>("SetFullLevel");
        _turnSwitchOn = _loader.BindExport<System6SetByteDelegate>("TurnSwitchOn");
        _turnSwitchOff = _loader.BindExport<System6SetByteDelegate>("TurnSwitchOff");
    }

    public string LibraryPath => _loader.LibraryPath;
    public Architecture ProcessArchitecture => _loader.ProcessArchitecture;
    public bool IsLoaded => _loader.IsLoaded;
    public byte Initialise() => _initialise();

    public int LoadRom(IReadOnlyList<string> programRomPaths, bool flashSwitch)
    {
        var paths = AllocateRomPathSlots(programRomPaths);
        return checked((int)_loadRom(paths[0], paths[1], paths[2], paths[3]));
    }

    public int LoadSoundRom(IReadOnlyList<string> soundRomPaths)
    {
        var paths = AllocateRomPathSlots(soundRomPaths);
        return checked((int)_loadSoundRom(paths[0], paths[1], paths[2], paths[3]));
    }

    public void SetSteps(byte reelNum, byte steps) => _setSteps(reelNum, steps);
    public void SetOptoStart(byte reelNum, byte start) => _setOptoStart(reelNum, start);
    public void SetOptoEnd(byte reelNum, byte end) => _setOptoEnd(reelNum, end);
    public void SetOptoInvert(byte reelNum, byte state) => _setOptoInvert(reelNum, state);
    public void Reset() => _reset();
    public int Run(int cycles) => _run(checked((uint)cycles));
    public byte Shutdown() => _shutdown();
    public uint GetOutputSnapshotSize() => _getOutputSnapshotSize();

    public System6NativeOutputSnapshot GetOutputSnapshot()
    {
        var snapshot = new System6NativeOutputSnapshot();
        var size = checked((uint)Marshal.SizeOf<System6NativeOutputSnapshot>());
        var written = _getOutputSnapshot(ref snapshot, size);
        if (written == 0 || snapshot.Version != System6NativeOutputSnapshot.VersionValue)
        {
            throw new InvalidOperationException($"OasisEmulator GetOutputSnapshot failed or returned unsupported snapshot version {snapshot.Version}.");
        }

        return snapshot;
    }

    public bool IsSetPercentAvailable => _setPercent is not null;
    public void SetPercent(byte percent) => (_setPercent ?? throw new NotSupportedException("OasisEmulator does not export SetPercent."))(percent);
    public void SetCoinEnable(byte coin, byte coinEnable) => _setCoinEnable(coin, coinEnable);
    public void SetCoinValue(byte coin, byte coinValue) => _setCoinValue(coin, coinValue);
    public void SetLockoutVal(byte coin, byte lockoutValue) => _setLockoutVal(coin, lockoutValue);
    public void SetLockoutInvert(byte coin, byte lockoutInvert) => _setLockoutInvert(coin, lockoutInvert);
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

    private TDelegate? TryBindOptionalExport<TDelegate>(string exportName) where TDelegate : Delegate
        => _loader.TryBindExport<TDelegate>(exportName, out var export) ? export : null;

    private IntPtr[] AllocateRomPathSlots(IReadOnlyList<string> romPaths)
    {
        var paths = new[] { IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero };
        for (var i = 0; i < paths.Length; i++)
        {
            var path = i < romPaths.Count ? romPaths[i] : string.Empty;
            if (string.IsNullOrWhiteSpace(path)) continue;
            paths[i] = Marshal.StringToHGlobalAnsi(path);
            _romPathBuffers.Add(paths[i]);
        }
        return paths;
    }

    private void FreeRomPathBuffers()
    {
        foreach (var buffer in _romPathBuffers)
        {
            if (buffer != IntPtr.Zero) Marshal.FreeHGlobal(buffer);
        }
        _romPathBuffers.Clear();
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate byte System6InitialiseDelegate();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate uint System6LoadRomDelegate(IntPtr romPath1, IntPtr romPath2, IntPtr romPath3, IntPtr romPath4);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate uint System6LoadSoundRomDelegate(IntPtr romPath1, IntPtr romPath2, IntPtr romPath3, IntPtr romPath4);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void System6ResetDelegate();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate int System6RunDelegate(uint cycles);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate byte System6ShutdownDelegate();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate uint System6GetOutputSnapshotSizeDelegate();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate uint System6GetOutputSnapshotDelegate(ref System6NativeOutputSnapshot outBuffer, uint outBufferSize);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void System6SetByteDelegate(byte value);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void System6SetByteByteDelegate(byte first, byte second);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void System6SetByteUIntDelegate(byte first, uint second);
}
