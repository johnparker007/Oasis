using System.Runtime.InteropServices;

namespace OasisEditor;

public sealed class AmberBridgeLibrary : IAmberBridgeLibrary
{
    internal const string System6CoreId = "jpm-system6";
    private readonly object _sync = new();
    private readonly IAmberBridgeModule _module;
    private readonly IAmberStringAllocator _allocator;
    private readonly NativeApi _api;
    private IntPtr _handle;
    private bool _initialised;
    private bool _shutdownAttempted;
    private bool _destroyAttempted;
    private bool _disposed;

    public AmberBridgeLibrary(string absoluteBridgePath)
        : this(new AmberBridgeModule(absoluteBridgePath), new AmberStringAllocator()) { }

    internal AmberBridgeLibrary(IAmberBridgeModule module, IAmberStringAllocator? allocator = null)
    {
        ArgumentNullException.ThrowIfNull(module);
        _module = module;
        _allocator = allocator ?? new AmberStringAllocator();

        NativeApi? api = null;
        IntPtr handle = IntPtr.Zero;
        try
        {
            api = NegotiateApi(module);
            BridgeDetails = ReadBridgeDetails(api);
            VerifyCore(api);

            using var core = new Utf8Allocation(System6CoreId, _allocator);
            var createResult = api.Create(core.Pointer, out handle);
            ThrowForResult(api, "Create", createResult, handle);
            if (handle == IntPtr.Zero)
            {
                throw new AmberBridgeException("Create", AmberResult.InternalError, "Bridge returned a null handle.");
            }

            _api = api;
            _handle = handle;
        }
        catch
        {
            // Construction has not initialised the instance. Destroy any handle the
            // bridge returned, suppress cleanup failures, and preserve the original error.
            if (handle != IntPtr.Zero && api is not null)
            {
                try { api.Destroy(handle); } catch { }
            }

            try { module.Dispose(); } catch { }
            throw;
        }
    }

    public AmberBridgeDetails BridgeDetails { get; }

    public void Initialise(IReadOnlyList<string> programRomPaths, IReadOnlyList<string>? soundRomPaths = null)
    {
        lock (_sync)
        {
            ThrowIfDisposed();
            if (_initialised)
            {
                throw new InvalidOperationException("Amber Bridge is already initialised.");
            }

            ValidateRomPaths(programRomPaths, nameof(programRomPaths), minimumCount: 1);
            soundRomPaths ??= Array.Empty<string>();
            ValidateRomPaths(soundRomPaths, nameof(soundRomPaths), minimumCount: 0);

            using var paths = new RomPathAllocations(programRomPaths, soundRomPaths, _allocator);
            var parameters = paths.Parameters;
            ThrowForResult(_api, "Initialise", _api.Initialise(_handle, ref parameters), _handle);
            _initialised = true;
        }
    }

    public void Reset()
    {
        lock (_sync)
        {
            RequireRunning();
            ThrowForResult(_api, "Reset", _api.Reset(_handle), _handle);
        }
    }

    public int Run(uint cycles)
    {
        lock (_sync)
        {
            RequireRunning();
            ThrowForResult(_api, "Run", _api.Run(_handle, cycles, out var cyclesRun), _handle);
            return cyclesRun;
        }
    }

    public void Shutdown()
    {
        lock (_sync)
        {
            ThrowIfDisposed();
            if (!_initialised)
            {
                throw new InvalidOperationException("Amber Bridge has not been initialised.");
            }
            if (_shutdownAttempted)
            {
                throw new InvalidOperationException("Amber Bridge shutdown has already been attempted.");
            }

            _shutdownAttempted = true;
            ThrowForResult(_api, "Shutdown", _api.Shutdown(_handle), _handle);
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed) return;

            // Dispose is deliberately non-throwing. Each remaining cleanup stage is
            // attempted once, and later stages run even when native cleanup fails.
            if (_initialised && !_shutdownAttempted)
            {
                _shutdownAttempted = true;
                try { _api.Shutdown(_handle); } catch { }
            }

            if (_handle != IntPtr.Zero && !_destroyAttempted)
            {
                _destroyAttempted = true;
                try { _api.Destroy(_handle); } catch { }
                _handle = IntPtr.Zero;
            }

            try { _module.Dispose(); } catch { }
            _disposed = true;
        }
    }

    private static NativeApi NegotiateApi(IAmberBridgeModule module)
    {
        var table = new AmberApiV1Native { StructSize = SizeOf<AmberApiV1Native>() };
        var result = module.BindAmberGetApi()(1, table.StructSize, ref table);
        if (result != AmberResult.Ok)
        {
            throw new AmberBridgeException("AmberGetApi", result);
        }
        if (table.StructSize < SizeOf<AmberApiV1Native>() || table.ApiVersion != 1)
        {
            throw new AmberBridgeException("AmberGetApi validation", AmberResult.UnsupportedVersion);
        }

        ValidateFunctionPointers(table);
        return new NativeApi(table);
    }

    private static AmberBridgeDetails ReadBridgeDetails(NativeApi api)
    {
        var info = new AmberBridgeInfoNative { StructSize = SizeOf<AmberBridgeInfoNative>() };
        ThrowForResult(api, "GetBridgeInfo", api.GetBridgeInfo(ref info), IntPtr.Zero);
        if (info.StructSize < SizeOf<AmberBridgeInfoNative>() || info.ApiVersion != 1)
        {
            throw new AmberBridgeException("GetBridgeInfo validation", AmberResult.UnsupportedVersion);
        }

        return new AmberBridgeDetails(info.ApiVersion, ReadUtf8(info.Name), ReadUtf8(info.BridgeVersion));
    }

    private static void VerifyCore(NativeApi api)
    {
        for (uint index = 0; ; index++)
        {
            var info = new AmberCoreInfoNative { StructSize = SizeOf<AmberCoreInfoNative>() };
            var result = api.EnumerateCore(index, ref info);
            if (result == AmberResult.NoMoreItems) break;
            ThrowForResult(api, "EnumerateCore", result, IntPtr.Zero);
            if (info.StructSize < SizeOf<AmberCoreInfoNative>())
            {
                throw new AmberBridgeException("EnumerateCore validation", AmberResult.InternalError, "Core information structure is too small.");
            }
            if (info.CoreId == IntPtr.Zero)
            {
                throw new AmberBridgeException("EnumerateCore validation", AmberResult.InternalError, "Core ID is null.");
            }

            var coreId = ReadUtf8(info.CoreId);
            if (coreId.Length == 0)
            {
                throw new AmberBridgeException("EnumerateCore validation", AmberResult.InternalError, "Core ID is empty.");
            }
            if (coreId == System6CoreId) return;
        }

        throw new AmberBridgeException("EnumerateCore", AmberResult.NoMoreItems, $"Required core '{System6CoreId}' was not found.");
    }

    private static void ThrowForResult(NativeApi api, string operation, AmberResult result, IntPtr handle)
    {
        if (result != AmberResult.Ok)
        {
            throw new AmberBridgeException(operation, result, ReadLastError(api, handle));
        }
    }

    private static string? ReadLastError(NativeApi api, IntPtr handle)
    {
        var result = api.GetLastError(handle, IntPtr.Zero, 0, out var required);
        if (result is not (AmberResult.Ok or AmberResult.BufferTooSmall) || required == 0) return null;

        var buffer = Marshal.AllocHGlobal(checked((int)required));
        try
        {
            result = api.GetLastError(handle, buffer, required, out _);
            return result == AmberResult.Ok ? ReadUtf8(buffer) : null;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static void ValidateFunctionPointers(AmberApiV1Native table)
    {
        if (table.GetBridgeInfo == IntPtr.Zero || table.EnumerateCore == IntPtr.Zero ||
            table.Create == IntPtr.Zero || table.Destroy == IntPtr.Zero ||
            table.Initialise == IntPtr.Zero || table.Reset == IntPtr.Zero ||
            table.Run == IntPtr.Zero || table.Shutdown == IntPtr.Zero ||
            table.GetLastError == IntPtr.Zero)
        {
            throw new AmberBridgeException("AmberGetApi validation", AmberResult.ExportMissing,
                "The v1 function table contains a null required function pointer.");
        }
    }

    private void RequireRunning()
    {
        ThrowIfDisposed();
        if (!_initialised || _shutdownAttempted)
        {
            throw new InvalidOperationException("Amber Bridge is not running.");
        }
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
    private static uint SizeOf<T>() => checked((uint)Marshal.SizeOf<T>());
    private static string ReadUtf8(IntPtr pointer) =>
        pointer == IntPtr.Zero ? string.Empty : Marshal.PtrToStringUTF8(pointer) ?? string.Empty;

    private static void ValidateRomPaths(IReadOnlyList<string> paths, string parameterName, int minimumCount)
    {
        ArgumentNullException.ThrowIfNull(paths, parameterName);
        if (paths.Count < minimumCount || paths.Count > 4)
        {
            throw new ArgumentException($"ROM path count must be between {minimumCount} and 4.", parameterName);
        }
        if (paths.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("ROM paths must not be null, empty, or whitespace.", parameterName);
        }
    }

    private sealed class NativeApi
    {
        internal NativeApi(AmberApiV1Native table)
        {
            GetBridgeInfo = Marshal.GetDelegateForFunctionPointer<GetBridgeInfoDelegate>(table.GetBridgeInfo);
            EnumerateCore = Marshal.GetDelegateForFunctionPointer<EnumerateCoreDelegate>(table.EnumerateCore);
            Create = Marshal.GetDelegateForFunctionPointer<CreateDelegate>(table.Create);
            Destroy = Marshal.GetDelegateForFunctionPointer<HandleDelegate>(table.Destroy);
            Initialise = Marshal.GetDelegateForFunctionPointer<InitialiseDelegate>(table.Initialise);
            Reset = Marshal.GetDelegateForFunctionPointer<HandleDelegate>(table.Reset);
            Run = Marshal.GetDelegateForFunctionPointer<RunDelegate>(table.Run);
            Shutdown = Marshal.GetDelegateForFunctionPointer<HandleDelegate>(table.Shutdown);
            GetLastError = Marshal.GetDelegateForFunctionPointer<GetLastErrorDelegate>(table.GetLastError);
        }

        internal GetBridgeInfoDelegate GetBridgeInfo { get; }
        internal EnumerateCoreDelegate EnumerateCore { get; }
        internal CreateDelegate Create { get; }
        internal HandleDelegate Destroy { get; }
        internal InitialiseDelegate Initialise { get; }
        internal HandleDelegate Reset { get; }
        internal RunDelegate Run { get; }
        internal HandleDelegate Shutdown { get; }
        internal GetLastErrorDelegate GetLastError { get; }
    }

    private sealed class Utf8Allocation : IDisposable
    {
        private readonly IAmberStringAllocator _allocator;

        internal Utf8Allocation(string value, IAmberStringAllocator allocator)
        {
            _allocator = allocator;
            Pointer = allocator.Allocate(value);
        }

        internal IntPtr Pointer { get; }
        public void Dispose() => _allocator.Free(Pointer);
    }

    private sealed class RomPathAllocations : IDisposable
    {
        private readonly List<IntPtr> _pointers = [];
        private readonly IAmberStringAllocator _allocator;

        internal RomPathAllocations(
            IReadOnlyList<string> programs,
            IReadOnlyList<string> sounds,
            IAmberStringAllocator allocator)
        {
            _allocator = allocator;
            try
            {
                var programPointers = AllocateSlots(programs);
                var soundPointers = AllocateSlots(sounds);
                Parameters = new AmberInitialiseParamsNative
                {
                    StructSize = SizeOf<AmberInitialiseParamsNative>(),
                    Program0 = programPointers[0], Program1 = programPointers[1],
                    Program2 = programPointers[2], Program3 = programPointers[3],
                    Sound0 = soundPointers[0], Sound1 = soundPointers[1],
                    Sound2 = soundPointers[2], Sound3 = soundPointers[3]
                };
            }
            catch
            {
                FreeAll();
                throw;
            }
        }

        internal AmberInitialiseParamsNative Parameters { get; }

        public void Dispose() => FreeAll();

        private IntPtr[] AllocateSlots(IReadOnlyList<string> paths)
        {
            var slots = new IntPtr[4];
            for (var index = 0; index < paths.Count; index++)
            {
                slots[index] = _allocator.Allocate(paths[index]);
                _pointers.Add(slots[index]);
            }
            return slots;
        }

        private void FreeAll()
        {
            foreach (var pointer in _pointers)
            {
                _allocator.Free(pointer);
            }
            _pointers.Clear();
        }
    }
}
