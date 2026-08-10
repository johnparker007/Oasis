using Xunit;

namespace OasisEditor.Tests;

public sealed class EmulationBackendFactoryTests
{
    [Fact]
    public void None_ReturnsNull()
    {
        var factory = CreateFactory(null, null);
        Assert.Null(factory.CreateBackend(FruitMachinePlatformType.None));
    }

    [Fact]
    public void Impact_WithValidConfiguration_ReturnsFabricBackend()
    {
        using var files = NativeFiles.Create();
        var factory = CreateFactory(files.Runtime, files.Amber);
        Assert.IsType<FabricEmulationBackend>(factory.CreateBackend(FruitMachinePlatformType.Impact));
    }

    [Fact]
    public void Mpu5_WithValidPlatformSpecificProvider_ReturnsFabricBackend()
    {
        using var files = NativeFiles.Create();
        var factory = new EmulationBackendFactory(() => files.Runtime, () => null, () => 50,
            mpu5AmberPathProvider: () => files.Amber);
        Assert.IsType<FabricEmulationBackend>(factory.CreateBackend(FruitMachinePlatformType.MPU5));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Impact_MissingConfiguredPath_ThrowsActionableError(bool runtime)
    {
        using var files = NativeFiles.Create();
        var factory = CreateFactory(runtime ? null : files.Runtime, runtime ? files.Amber : null);
        var error = Assert.Throws<InvalidOperationException>(() => factory.CreateBackend(FruitMachinePlatformType.Impact));
        Assert.Contains(runtime ? "Fabric runtime" : "Production Amber", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Preferences", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Impact_MissingFile_ThrowsActionableError(bool runtime)
    {
        using var files = NativeFiles.Create();
        var missing = Path.Combine(files.Directory, "missing.dll");
        var factory = CreateFactory(runtime ? missing : files.Runtime, runtime ? files.Amber : missing);
        var error = Assert.Throws<FileNotFoundException>(() => factory.CreateBackend(FruitMachinePlatformType.Impact));
        Assert.Contains(runtime ? "FabricRuntime.dll" : "production Amber", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Epoch_WithValidPlatformSpecificProvider_ReturnsFabricBackend()
    {
        using var files = NativeFiles.Create();
        var factory = new EmulationBackendFactory(() => files.Runtime, () => null, () => 50,
            epochAmberPathProvider: () => files.Amber);
        Assert.IsType<FabricEmulationBackend>(factory.CreateBackend(FruitMachinePlatformType.Epoch));
    }

    [Fact]
    public void Mpu3_UsesOnlyDedicatedProviderAndReportsMissingPath()
    {
        using var files = NativeFiles.Create();
        var valid = new EmulationBackendFactory(() => files.Runtime, () => null, () => 50,
            mpu3AmberPathProvider: () => files.Amber);
        Assert.IsType<FabricEmulationBackend>(valid.CreateBackend(FruitMachinePlatformType.MPU3));
        var missing = new EmulationBackendFactory(() => files.Runtime, () => files.Amber, () => 50,
            mpu3AmberPathProvider: () => null);
        var error = Assert.Throws<InvalidOperationException>(() => missing.CreateBackend(FruitMachinePlatformType.MPU3));
        Assert.Contains("MPU3", error.Message);
    }

    [Fact]
    public void EveryOtherUnsupportedEnumValue_ThrowsNotSupportedException()
    {
        foreach (var platform in Enum.GetValues<FruitMachinePlatformType>().Where(value => value is not FruitMachinePlatformType.None and not FruitMachinePlatformType.Impact and not FruitMachinePlatformType.MPU3 and not FruitMachinePlatformType.MPU5 and not FruitMachinePlatformType.Epoch and not FruitMachinePlatformType.MaygayM1))
            Assert.Throws<NotSupportedException>(() => CreateFactory(null, null).CreateBackend(platform));
    }

    [Fact]
    public void ConfiguredAudioBufferLength_ReachesFabricAudioSinkFactory()
    {
        using var files = NativeFiles.Create();
        var received = 0;
        var factory = CreateFactory(files.Runtime, files.Amber, () => 73, value => { received = value; return new NullSink(); });
        Assert.IsType<FabricEmulationBackend>(factory.CreateBackend(FruitMachinePlatformType.Impact));
        Assert.Equal(73, received);
    }

    private static EmulationBackendFactory CreateFactory(string? runtime, string? amber, Func<int>? buffer = null, Func<int, IEmulationAudioSink>? sink = null) =>
        new(() => runtime, () => amber, buffer, _ => throw new InvalidOperationException("Created only on start."), sink ?? (_ => new NullSink()), new StopwatchFabricClock(), null);

    private sealed class NullSink : IEmulationAudioSink
    {
        public void Start(EmulationAudioFormat format) { }
        public void PushPcm(ReadOnlySpan<byte> pcmBytes) { }
        public void Stop() { }
        public void Clear() { }
        public EmulationAudioPlaybackStatistics GetStatistics() => default;
        public int WritableFrames => int.MaxValue;
        public int CapacityFrames => int.MaxValue;
        public void Dispose() { }
    }

    private sealed class NativeFiles : IDisposable
    {
        private NativeFiles(string directory, string runtime, string amber) { Directory = directory; Runtime = runtime; Amber = amber; }
        public string Directory { get; }
        public string Runtime { get; }
        public string Amber { get; }
        public static NativeFiles Create()
        {
            var directory = Path.Combine(Path.GetTempPath(), "oasis-factory-" + Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(directory);
            var runtime = Path.Combine(directory, "FabricRuntime.dll");
            var amber = Path.Combine(directory, "ProductionAmber.dll");
            File.WriteAllBytes(runtime, []); File.WriteAllBytes(amber, []);
            return new(directory, runtime, amber);
        }
        public void Dispose() { if (System.IO.Directory.Exists(Directory)) System.IO.Directory.Delete(Directory, true); }
    }
}
