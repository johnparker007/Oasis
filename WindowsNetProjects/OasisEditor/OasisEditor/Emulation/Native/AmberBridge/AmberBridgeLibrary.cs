using System.Runtime.InteropServices;

namespace OasisEditor;

public sealed class AmberBridgeLibrary : IAmberBridgeLibrary
{
    internal const string System6CoreId = "jpm-system6";
    private readonly object _sync = new();
    private readonly IAmberBridgeModule _module;
    private readonly IAmberStringAllocator _allocator;
    private readonly GetBridgeInfoDelegate _getBridgeInfo;
    private readonly EnumerateCoreDelegate _enumerateCore;
    private readonly CreateDelegate _create;
    private readonly HandleDelegate _destroy, _reset, _shutdown;
    private readonly InitialiseDelegate _initialise;
    private readonly RunDelegate _run;
    private readonly GetLastErrorDelegate _getLastError;
    private IntPtr _handle;
    private bool _initialised, _shutdownCalled, _disposed;

    public AmberBridgeLibrary(string absoluteBridgePath) : this(new AmberBridgeModule(absoluteBridgePath), new AmberStringAllocator()) { }

    internal AmberBridgeLibrary(IAmberBridgeModule module, IAmberStringAllocator? allocator = null)
    {
        _module = module ?? throw new ArgumentNullException(nameof(module));
        _allocator = allocator ?? new AmberStringAllocator();
        try
        {
            var api = new AmberApiV1Native { StructSize = SizeOf<AmberApiV1Native>() };
            CheckWithoutHandle("AmberGetApi", _module.BindAmberGetApi()(1, api.StructSize, ref api));
            if (api.StructSize < SizeOf<AmberApiV1Native>() || api.ApiVersion != 1)
                throw new AmberBridgeException("AmberGetApi validation", AmberResult.UnsupportedVersion);
            ValidatePointers(api);
            _getBridgeInfo = Marshal.GetDelegateForFunctionPointer<GetBridgeInfoDelegate>(api.GetBridgeInfo);
            _enumerateCore = Marshal.GetDelegateForFunctionPointer<EnumerateCoreDelegate>(api.EnumerateCore);
            _create = Marshal.GetDelegateForFunctionPointer<CreateDelegate>(api.Create);
            _destroy = Marshal.GetDelegateForFunctionPointer<HandleDelegate>(api.Destroy);
            _initialise = Marshal.GetDelegateForFunctionPointer<InitialiseDelegate>(api.Initialise);
            _reset = Marshal.GetDelegateForFunctionPointer<HandleDelegate>(api.Reset);
            _run = Marshal.GetDelegateForFunctionPointer<RunDelegate>(api.Run);
            _shutdown = Marshal.GetDelegateForFunctionPointer<HandleDelegate>(api.Shutdown);
            _getLastError = Marshal.GetDelegateForFunctionPointer<GetLastErrorDelegate>(api.GetLastError);

            var info = new AmberBridgeInfoNative { StructSize = SizeOf<AmberBridgeInfoNative>() };
            Check("GetBridgeInfo", _getBridgeInfo(ref info));
            if (info.StructSize < SizeOf<AmberBridgeInfoNative>() || info.ApiVersion != 1)
                throw new AmberBridgeException("GetBridgeInfo validation", AmberResult.UnsupportedVersion);
            BridgeDetails = new(info.ApiVersion, Utf8(info.Name), Utf8(info.BridgeVersion));
            VerifyCore();
            using var core = new Utf8Allocation(System6CoreId, _allocator);
            Check("Create", _create(core.Pointer, out _handle));
            if (_handle == IntPtr.Zero) throw new AmberBridgeException("Create", AmberResult.InternalError, "Bridge returned a null handle.");
        }
        catch
        {
            if (_handle != IntPtr.Zero && _destroy is not null) _destroy(_handle);
            _module.Dispose();
            _disposed = true;
            throw;
        }
    }

    public AmberBridgeDetails BridgeDetails { get; } = null!;

    public void Initialise(IReadOnlyList<string> programRomPaths, IReadOnlyList<string>? soundRomPaths = null)
    {
        lock (_sync)
        {
            ThrowIfDisposed();
            if (_initialised) throw new InvalidOperationException("Amber Bridge is already initialised.");
            ValidateRoms(programRomPaths, nameof(programRomPaths));
            soundRomPaths ??= Array.Empty<string>(); ValidateRoms(soundRomPaths, nameof(soundRomPaths));
            using var paths = new RomPathAllocations(programRomPaths, soundRomPaths, _allocator);
            var p = paths.Parameters;
            Check("Initialise", _initialise(_handle, ref p));
            _initialised = true;
        }
    }

    public void Reset() { lock (_sync) { RequireInitialised(); Check("Reset", _reset(_handle)); } }
    public int Run(uint cycles) { lock (_sync) { RequireInitialised(); Check("Run", _run(_handle, cycles, out var ran)); return ran; } }
    public void Shutdown() { lock (_sync) { ThrowIfDisposed(); ShutdownCore(); } }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed) return;
            try { if (_initialised && !_shutdownCalled) { _shutdown(_handle); _shutdownCalled = true; } }
            finally
            {
                try { if (_handle != IntPtr.Zero) { _destroy(_handle); _handle = IntPtr.Zero; } }
                finally { _module.Dispose(); _disposed = true; }
            }
        }
    }

    private void VerifyCore()
    {
        for (uint i = 0; ; i++)
        {
            var info = new AmberCoreInfoNative { StructSize = SizeOf<AmberCoreInfoNative>() };
            var result = _enumerateCore(i, ref info);
            if (result == AmberResult.NoMoreItems) break;
            Check("EnumerateCore", result);
            if (info.StructSize < SizeOf<AmberCoreInfoNative>()) throw new AmberBridgeException("EnumerateCore validation", AmberResult.InternalError);
            if (Utf8(info.CoreId) == System6CoreId) return;
        }
        throw new AmberBridgeException("EnumerateCore", AmberResult.NoMoreItems, $"Required core '{System6CoreId}' was not found.");
    }

    private void ShutdownCore()
    {
        if (!_initialised) throw new InvalidOperationException("Amber Bridge has not been initialised.");
        if (_shutdownCalled) return;
        Check("Shutdown", _shutdown(_handle)); _shutdownCalled = true;
    }
    private void RequireInitialised() { ThrowIfDisposed(); if (!_initialised || _shutdownCalled) throw new InvalidOperationException("Amber Bridge is not running."); }
    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
    private void Check(string operation, AmberResult result) { if (result != AmberResult.Ok) throw new AmberBridgeException(operation, result, LastError()); }
    private static void CheckWithoutHandle(string operation, AmberResult result) { if (result != AmberResult.Ok) throw new AmberBridgeException(operation, result); }

    private string? LastError()
    {
        var result = _getLastError(_handle, IntPtr.Zero, 0, out var required);
        if (result is not (AmberResult.Ok or AmberResult.BufferTooSmall) || required == 0) return null;
        var buffer = Marshal.AllocHGlobal(checked((int)required));
        try { result = _getLastError(_handle, buffer, required, out required); return result == AmberResult.Ok ? Utf8(buffer) : null; }
        finally { Marshal.FreeHGlobal(buffer); }
    }

    private static void ValidatePointers(AmberApiV1Native a)
    {
        if (a.GetBridgeInfo == IntPtr.Zero || a.EnumerateCore == IntPtr.Zero || a.Create == IntPtr.Zero || a.Destroy == IntPtr.Zero ||
            a.Initialise == IntPtr.Zero || a.Reset == IntPtr.Zero || a.Run == IntPtr.Zero || a.Shutdown == IntPtr.Zero || a.GetLastError == IntPtr.Zero)
            throw new AmberBridgeException("AmberGetApi validation", AmberResult.ExportMissing, "The v1 function table contains a null required function pointer.");
    }
    private static uint SizeOf<T>() => checked((uint)Marshal.SizeOf<T>());
    private static string Utf8(IntPtr p) => p == IntPtr.Zero ? string.Empty : Marshal.PtrToStringUTF8(p) ?? string.Empty;
    private static void ValidateRoms(IReadOnlyList<string> paths, string name)
    {
        ArgumentNullException.ThrowIfNull(paths);
        if (paths.Count > 4) throw new ArgumentException("Amber Bridge accepts at most four ROM paths.", name);
        if (paths.Any(string.IsNullOrWhiteSpace)) throw new ArgumentException("ROM paths must not be empty; omit absent trailing slots.", name);
    }

    private sealed class Utf8Allocation : IDisposable
    {
        private readonly IAmberStringAllocator _allocator;
        internal Utf8Allocation(string value, IAmberStringAllocator allocator) { _allocator = allocator; Pointer = allocator.Allocate(value); }
        internal IntPtr Pointer { get; }
        public void Dispose() => _allocator.Free(Pointer);
    }
    private sealed class RomPathAllocations : IDisposable
    {
        private readonly List<IntPtr> _pointers = [];
        private readonly IAmberStringAllocator _allocator;
        internal RomPathAllocations(IReadOnlyList<string> programs, IReadOnlyList<string> sounds, IAmberStringAllocator allocator)
        {
            _allocator = allocator;
            var p = programs.Select(Allocate).Concat(Enumerable.Repeat(IntPtr.Zero, 4 - programs.Count)).ToArray();
            var s = sounds.Select(Allocate).Concat(Enumerable.Repeat(IntPtr.Zero, 4 - sounds.Count)).ToArray();
            Parameters = new AmberInitialiseParamsNative { StructSize = SizeOf<AmberInitialiseParamsNative>(), Program0=p[0], Program1=p[1], Program2=p[2], Program3=p[3], Sound0=s[0], Sound1=s[1], Sound2=s[2], Sound3=s[3] };
        }
        internal AmberInitialiseParamsNative Parameters { get; }
        private IntPtr Allocate(string value) { var p = _allocator.Allocate(value); _pointers.Add(p); return p; }
        public void Dispose() { foreach (var p in _pointers) _allocator.Free(p); _pointers.Clear(); }
    }
}
