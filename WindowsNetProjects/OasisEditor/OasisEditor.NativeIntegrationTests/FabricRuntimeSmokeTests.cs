using Xunit;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using OasisEditor;

namespace OasisEditor.NativeIntegrationTests;

public sealed class FabricRuntimeSmokeTests
{
    [NativeFact("FABRIC_RUNTIME_DLL")]
    public void Runtime_CanBeCreatedDestroyedAndModuleReloadedRepeatedly()
    {
        var runtimePath = NativePrerequisites.RequireFile("FABRIC_RUNTIME_DLL");
        var stopwatch = Stopwatch.StartNew();
        for (var iteration = 0; iteration < 200; iteration++)
        {
            using var runtime = new FabricRuntimeLibrary(runtimePath);
        }
        stopwatch.Stop();
        RecordTiming("200 runtime load/create/destroy/unload iterations", stopwatch.Elapsed);
    }

    [NativeFact("FABRIC_RUNTIME_DLL")]
    public void Runtime_RejectsUnsupportedAbiAndReturnsNativeErrorText()
    {
        var runtimePath = NativePrerequisites.RequireFile("FABRIC_RUNTIME_DLL");
        using var client = new RuntimeExportClient(runtimePath);

        var result = client.CreateRuntime(0xFFFFFFFF, out var runtime);
        var error = client.GetLastError(runtime);
        if (runtime != 0)
            client.DestroyRuntime(runtime);

        Assert.Equal(FabricResult.UnsupportedVersion, result);
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void Runtime_InvalidPathProducesActionableManagedFailure()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".dll");
        var exception = Assert.Throws<FileNotFoundException>(() => new FabricRuntimeLibrary(path));
        Assert.Equal(path, exception.FileName);
    }

    [NativeTheory("FABRIC_RUNTIME_DLL")]
    [InlineData("not-a-provider", "jpm-system6")]
    [InlineData("amber-api-v2", "not-a-machine")]
    public void Session_InvalidProviderIdentityPreservesResultAndError(string backendKind, string machineIdentifier)
    {
        var runtimePath = NativePrerequisites.RequireFile("FABRIC_RUNTIME_DLL");
        using var runtime = new FabricRuntimeLibrary(runtimePath);
        var exception = Assert.Throws<FabricException>(() => runtime.CreateSession(new FabricLaunchRequest(
            backendKind, machineIdentifier, runtimePath, [], null)));

        Assert.NotEqual(FabricResult.Ok, exception.Result);
        Assert.Contains("FabricCreateSession", exception.Message);
        Assert.Contains(((int)exception.Result).ToString(), exception.Message);
    }

    private static void RecordTiming(string operation, TimeSpan elapsed) =>
        Console.WriteLine($"[Fabric timing] {operation}: {elapsed.TotalMilliseconds:F3} ms");

    private sealed class RuntimeExportClient : IDisposable
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate FabricResult CreateRuntimeDelegate(uint version, out nint runtime);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void DestroyRuntimeDelegate(nint runtime);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private unsafe delegate FabricResult GetErrorDelegate(nint runtime, byte* buffer, uint size, out uint required);

        private readonly nint _module;
        private readonly CreateRuntimeDelegate _create;
        private readonly DestroyRuntimeDelegate _destroy;
        private readonly GetErrorDelegate _getError;

        internal RuntimeExportClient(string path)
        {
            _module = NativeLibrary.Load(path);
            _create = Resolve<CreateRuntimeDelegate>("FabricCreateRuntime");
            _destroy = Resolve<DestroyRuntimeDelegate>("FabricDestroyRuntime");
            _getError = Resolve<GetErrorDelegate>("FabricRuntimeGetLastError");
        }

        internal FabricResult CreateRuntime(uint version, out nint runtime) => _create(version, out runtime);
        internal void DestroyRuntime(nint runtime) => _destroy(runtime);

        internal unsafe string GetLastError(nint runtime)
        {
            _getError(runtime, null, 0, out var required);
            if (required == 0)
                return string.Empty;
            var bytes = new byte[required];
            fixed (byte* pointer = bytes)
            {
                var result = _getError(runtime, pointer, required, out _);
                Assert.Equal(FabricResult.Ok, result);
            }
            var terminator = Array.IndexOf(bytes, (byte)0);
            return Encoding.UTF8.GetString(bytes, 0, terminator < 0 ? bytes.Length : terminator);
        }

        public void Dispose() => NativeLibrary.Free(_module);

        private T Resolve<T>(string name) where T : Delegate =>
            Marshal.GetDelegateForFunctionPointer<T>(NativeLibrary.GetExport(_module, name));
    }
}
