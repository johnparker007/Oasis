using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.ComponentModel;

namespace OasisPlayer.Fabric
{
    public sealed unsafe class FabricRuntimeLibrary : IFabricRuntimeLibrary
    {
        private readonly object _gate = new object();
        private readonly string _path;
        private nint _module, _runtime;
        private FabricNativeExports _exports;
        private int _sessions;
        private bool _disposeRequested;

        private const uint LoadLibrarySearchDllLoadDir = 0x00000100;
        private const uint LoadLibrarySearchDefaultDirs = 0x00001000;

        [DllImport("kernel32.dll", EntryPoint = "LoadLibraryExW", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern nint LoadLibraryEx(string fileName, nint file, uint flags);
        [DllImport("kernel32.dll", EntryPoint = "GetProcAddress", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
        internal static extern nint GetProcAddress(nint module, string name);
        [DllImport("kernel32.dll", EntryPoint = "FreeLibrary", ExactSpelling = true, SetLastError = true)]
        private static extern bool FreeLibrary(nint module);

        public FabricRuntimeLibrary(string exactPath)
        {
            if (string.IsNullOrWhiteSpace(exactPath) || !Path.IsPathFullyQualified(exactPath))
                throw new ArgumentException("Fabric runtime DLL path must be absolute.", nameof(exactPath));
            if (!File.Exists(exactPath))
                throw new FileNotFoundException("Fabric runtime DLL was not found.", exactPath);
            _path = Path.GetFullPath(exactPath);
            try
            {
                _module = LoadLibraryEx(_path, IntPtr.Zero, LoadLibrarySearchDllLoadDir | LoadLibrarySearchDefaultDirs);
                if (_module == IntPtr.Zero)
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not load Fabric runtime DLL '" + _path + "'.");
                _exports = new FabricNativeExports(_module, _path);
                var result = _exports.CreateRuntimeFn(FabricAbi.Version, out _runtime);
                if (result != FabricResult.Ok)
                    throw Error(result, "FabricCreateRuntime", _runtime, false);
            }
            catch { Cleanup(); throw; }
        }

        public IFabricMachineSession CreateSession(FabricLaunchRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            Validate(request);
            lock (_gate)
            {
                if (_disposeRequested) throw new ObjectDisposedException(nameof(FabricRuntimeLibrary));
            }
            var allocations = new List<nint>();
            try
            {
                var resources = request.RomResources.Select(r => new FabricRomResourceNative
                {
                    Size = (uint)sizeof(FabricRomResourceNative), Version = FabricAbi.Version,
                    Role = (uint)r.Role, Slot = r.Slot, Path = AllocUtf8(r.Path, allocations)
                }).ToArray();
                var resourceBytes = resources.Length * sizeof(FabricRomResourceNative);
                var resourcePtr = resourceBytes == 0 ? IntPtr.Zero : Marshal.AllocHGlobal(resourceBytes);
                if (resourcePtr != IntPtr.Zero)
                {
                    allocations.Add(resourcePtr);
                    fixed (FabricRomResourceNative* p = resources)
                        Buffer.MemoryCopy(p, (void*)resourcePtr, resourceBytes, resourceBytes);
                }
                var config = request.Configuration == null ? Array.Empty<byte>() : request.Configuration.ToNativeBytes();
                if (config == null) throw new ArgumentException("Fabric configuration returned a null byte array.", nameof(request));
                var configPtr = config.Length == 0 ? IntPtr.Zero : Marshal.AllocHGlobal(config.Length);
                if (configPtr != IntPtr.Zero) { allocations.Add(configPtr); Marshal.Copy(config, 0, configPtr, config.Length); }
                var native = new FabricLaunchRequestNative
                {
                    Size = (uint)sizeof(FabricLaunchRequestNative), Version = FabricAbi.Version,
                    Resources = resourcePtr, ResourceCount = (uint)resources.Length,
                    Configuration = configPtr, ConfigurationSize = (uint)config.Length
                };
                byte* backendKind = native.BackendKind;
                byte* machineIdentifier = native.MachineIdentifier;
                byte* backendPath = native.BackendPath;
                WriteFixed(request.BackendKind, backendKind, FabricAbi.IdentifierCapacity);
                WriteFixed(request.MachineIdentifier, machineIdentifier, FabricAbi.IdentifierCapacity);
                WriteFixed(request.BackendPath, backendPath, FabricAbi.PathCapacity);
                var result = _exports.CreateSessionFn(_runtime, &native, out var session);
                if (result != FabricResult.Ok) throw Error(result, "FabricCreateSession", _runtime, false);
                try
                {
                    var managedSession = FabricMachineSession.Create(session, _exports, SessionDisposed);
                    lock (_gate) _sessions++;
                    managedSession.ActivateOwnership();
                    session = IntPtr.Zero;
                    return managedSession;
                }
                finally { if (session != IntPtr.Zero) _exports.DestroySessionFn(session); }
            }
            finally
            {
                for (var index = allocations.Count - 1; index >= 0; index--) Marshal.FreeHGlobal(allocations[index]);
            }
        }

        private static nint AllocUtf8(string value, List<nint> owned)
        {
            var bytes = Encoding.UTF8.GetBytes(value + '\0');
            var pointer = Marshal.AllocHGlobal(bytes.Length);
            Marshal.Copy(bytes, 0, pointer, bytes.Length);
            owned.Add(pointer);
            return pointer;
        }

        internal static void WriteFixed(string value, byte* destination, int capacity)
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            if (bytes.Length >= capacity)
                throw new ArgumentException("UTF-8 value requires " + (bytes.Length + 1) + " bytes but capacity is " + capacity + ".");
            var target = new Span<byte>(destination, capacity);
            target.Clear();
            bytes.CopyTo(target);
        }

        internal static void Validate(FabricLaunchRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.BackendKind) || string.IsNullOrWhiteSpace(request.MachineIdentifier))
                throw new ArgumentException("Backend kind and machine identifier are required.");
            if (request.RomResources == null) throw new ArgumentException("ROM resources are required.", nameof(request));
            ValidateFile(request.BackendPath, "backend");
            foreach (var resource in request.RomResources)
            {
                if (resource == null) throw new ArgumentException("ROM resources cannot contain null entries.", nameof(request));
                ValidateFile(resource.Path, "ROM");
            }
            foreach (var group in request.RomResources.GroupBy(resource => resource.Role))
            {
                var slots = group.Select(resource => resource.Slot).OrderBy(slot => slot).ToArray();
                if ((group.Key == FabricRomRole.Program || group.Key == FabricRomRole.Sound) && slots.Length > 4)
                    throw new ArgumentException("Amber supports at most four ROMs per role.");
                for (var index = 0; index < slots.Length; index++)
                    if (slots[index] != (uint)index)
                        throw new ArgumentException(group.Key + " ROM slots must be unique and contiguous from zero.");
            }
        }

        private static void ValidateFile(string path, string kind)
        {
            if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
                throw new ArgumentException("Fabric " + kind + " path must be absolute: '" + path + "'.");
            if (!File.Exists(path)) throw new FileNotFoundException("Fabric " + kind + " file was not found.", path);
        }

        internal static FabricException Error(FabricResult result, string operation, nint handle, bool session, FabricNativeExports exports = null)
        {
            string detail = null;
            if (exports != null && handle != IntPtr.Zero)
            {
                var getError = session ? exports.SessionError : exports.RuntimeError;
                getError(handle, null, 0, out var required);
                if (required > 0 && required <= 65536)
                {
                    var bytes = new byte[(int)required];
                    fixed (byte* pointer = bytes)
                    {
                        if (getError(handle, pointer, required, out _) == FabricResult.Ok)
                        {
                            var terminator = Array.IndexOf(bytes, (byte)0);
                            detail = Encoding.UTF8.GetString(bytes, 0, terminator >= 0 ? terminator : bytes.Length);
                        }
                    }
                }
            }
            return new FabricException(result, operation, detail);
        }

        private void SessionDisposed()
        {
            lock (_gate) { _sessions--; if (_disposeRequested && _sessions == 0) Cleanup(); }
        }
        public void Dispose()
        {
            lock (_gate) { if (_disposeRequested) return; _disposeRequested = true; if (_sessions == 0) Cleanup(); }
        }
        private void Cleanup()
        {
            try { if (_runtime != IntPtr.Zero && _exports != null) _exports.DestroyRuntimeFn(_runtime); }
            catch { }
            finally
            {
                _runtime = IntPtr.Zero;
                _exports = null;
                if (_module != IntPtr.Zero) { FreeLibrary(_module); _module = IntPtr.Zero; }
            }
        }
    }
}
