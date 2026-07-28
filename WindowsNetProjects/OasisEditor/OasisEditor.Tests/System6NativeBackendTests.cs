using Xunit;

namespace OasisEditor.Tests;

public sealed class System6NativeBackendTests
{
    [Fact]
    public async Task StartUsesBridgePathAndPassesOrderedRomsThenResets()
    {
        using var files = NativeFiles.Create(programCount: 2, soundCount: 2);
        var fake = new FakeAmberBridge();
        string? requestedPath = null;
        var backend = new System6NativeBackend(files.Bridge, path => { requestedPath = path; fake.Calls.Add("Create"); return fake; });

        await backend.StartAsync(Request(files), CancellationToken.None);
        await backend.StopAsync(CancellationToken.None);

        Assert.Equal(files.Bridge, requestedPath);
        Assert.EndsWith("AmberBridge.dll", requestedPath, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("AmberOasis.JPMSystem6.dll", requestedPath, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(files.Programs, fake.ProgramRoms);
        Assert.Equal(files.Sounds, fake.SoundRoms);
        Assert.Equal(new[] { "Create", "Initialise", "Reset" }, fake.Calls.Take(3));
        Assert.Equal(1, fake.ShutdownCount);
        Assert.True(fake.Disposed);
    }

    [Fact]
    public async Task MissingSoundRomsArePassedAsEmptyCollection()
    {
        using var files = NativeFiles.Create(2, 0);
        var fake = new FakeAmberBridge();
        var backend = new System6NativeBackend(files.Bridge, _ => fake);
        await backend.StartAsync(Request(files), CancellationToken.None);
        await backend.StopAsync(CancellationToken.None);
        Assert.Empty(fake.SoundRoms);
    }

    [Fact]
    public async Task MissingRequiredProgramRomFailsBeforeBridgeCreation()
    {
        using var files = NativeFiles.Create(1, 0);
        var created = false;
        var backend = new System6NativeBackend(files.Bridge, _ => { created = true; return new FakeAmberBridge(); });
        await Assert.ThrowsAsync<InvalidOperationException>(() => backend.StartAsync(Request(files), CancellationToken.None));
        Assert.False(created);
        Assert.Equal(EmulationBackendState.Failed, backend.State);
    }

    [Fact]
    public async Task InitialiseFailureDisposesBridgeAndPreventsRun()
    {
        using var files = NativeFiles.Create(2, 0);
        var fake = new FakeAmberBridge { InitialiseException = new TestException() };
        var backend = new System6NativeBackend(files.Bridge, _ => fake);
        await Assert.ThrowsAsync<TestException>(() => backend.StartAsync(Request(files), CancellationToken.None));
        Assert.True(fake.Disposed);
        Assert.Empty(fake.RunRequests);
        Assert.Throws<InvalidOperationException>(() => backend.RunCycles(1));
    }

    [Fact]
    public async Task RunMapsUnsignedRequestAndPreservesSignedResult()
    {
        using var files = NativeFiles.Create(2, 0);
        var fake = new FakeAmberBridge { RunResult = -17 };
        var backend = new System6NativeBackend(files.Bridge, _ => fake);
        await backend.StartAsync(Request(files), CancellationToken.None);
        Assert.Equal(-17, backend.RunCycles(123));
        Assert.Equal(-17, backend.LastCyclesRun);
        Assert.Contains(123u, fake.RunRequests);
        Assert.Throws<ArgumentOutOfRangeException>(() => backend.RunCycles(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => backend.RunCycles((long)uint.MaxValue + 1));
        await backend.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ResetShutdownAndDisposalAreMappedExactlyOnce()
    {
        using var files = NativeFiles.Create(2, 0);
        var fake = new FakeAmberBridge();
        var backend = new System6NativeBackend(files.Bridge, _ => fake);
        await backend.StartAsync(Request(files), CancellationToken.None);
        await backend.ResetAsync(EmulationResetKind.Hard, CancellationToken.None);
        await backend.StopAsync(CancellationToken.None);
        await backend.StopAsync(CancellationToken.None);
        await backend.DisposeAsync();
        await backend.DisposeAsync();
        Assert.Equal(2, fake.ResetCount); // startup plus explicit reset
        Assert.Equal(1, fake.ShutdownCount);
        Assert.Equal(1, fake.DisposeCount);
        Assert.Throws<ObjectDisposedException>(() => backend.RunCycles(1));
    }

    [Fact]
    public async Task SwitchInputIsExplicitlyUnsupported()
    {
        using var files = NativeFiles.Create(2, 0);
        var backend = new System6NativeBackend(files.Bridge, _ => new FakeAmberBridge());
        await backend.StartAsync(Request(files), CancellationToken.None);
        var ex = await Assert.ThrowsAsync<NotSupportedException>(() => backend.SetInputStateAsync(new InputDefinitionModel { Id = "button" }, true, CancellationToken.None));
        Assert.Contains("Amber Bridge v0.1.1", ex.Message);
        await backend.StopAsync(CancellationToken.None);
    }

    private static EmulationLaunchRequest Request(NativeFiles files)
    {
        var settings = new System6NativeRomSettings
        {
            ProgramRom1Path = files.Programs.ElementAtOrDefault(0) ?? "",
            ProgramRom2Path = files.Programs.ElementAtOrDefault(1) ?? "",
            ProgramRom3Path = files.Programs.ElementAtOrDefault(2) ?? "",
            ProgramRom4Path = files.Programs.ElementAtOrDefault(3) ?? "",
            SoundRom1Path = files.Sounds.ElementAtOrDefault(0) ?? "",
            SoundRom2Path = files.Sounds.ElementAtOrDefault(1) ?? "",
            SoundRom3Path = files.Sounds.ElementAtOrDefault(2) ?? "",
            SoundRom4Path = files.Sounds.ElementAtOrDefault(3) ?? ""
        };
        return new(FruitMachinePlatformType.Impact, "test", files.Directory, [], "", settings);
    }

    private sealed class FakeAmberBridge : IAmberBridgeLibrary
    {
        public AmberBridgeDetails BridgeDetails { get; } = new(AmberApiVersions.V1, "Test Amber Bridge", "0.1.1");
        public List<string> Calls { get; } = [];
        public IReadOnlyList<string> ProgramRoms { get; private set; } = [];
        public IReadOnlyList<string> SoundRoms { get; private set; } = [];
        public List<uint> RunRequests { get; } = [];
        public Exception? InitialiseException { get; init; }
        public int RunResult { get; init; } = 1;
        public int ResetCount { get; private set; }
        public int ShutdownCount { get; private set; }
        public int DisposeCount { get; private set; }
        public bool Disposed => DisposeCount != 0;
        public void Initialise(IReadOnlyList<string> programRomPaths, IReadOnlyList<string>? soundRomPaths = null)
        {
            Calls.Add("Initialise");
            if (InitialiseException is not null) throw InitialiseException;
            ProgramRoms = programRomPaths.ToArray();
            SoundRoms = soundRomPaths?.ToArray() ?? [];
        }
        public void Reset() { Calls.Add("Reset"); ResetCount++; }
        public int Run(uint cycles) { lock (RunRequests) RunRequests.Add(cycles); return RunResult; }
        public void Shutdown() { Calls.Add("Shutdown"); ShutdownCount++; }
        public void Dispose() { Calls.Add("Dispose"); DisposeCount++; }
    }

    private sealed class TestException : Exception;

    private sealed class NativeFiles : IDisposable
    {
        private NativeFiles(string directory, string bridge, string[] programs, string[] sounds)
            => (Directory, Bridge, Programs, Sounds) = (directory, bridge, programs, sounds);
        public string Directory { get; }
        public string Bridge { get; }
        public string[] Programs { get; }
        public string[] Sounds { get; }
        public static NativeFiles Create(int programCount, int soundCount)
        {
            var directory = Path.Combine(Path.GetTempPath(), "oasis-amber-tests", Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(directory);
            var bridge = Touch(directory, "AmberBridge.dll");
            var programs = Enumerable.Range(1, programCount).Select(i => Touch(directory, $"program{i}.rom")).ToArray();
            var sounds = Enumerable.Range(1, soundCount).Select(i => Touch(directory, $"sound{i}.rom")).ToArray();
            return new(directory, bridge, programs, sounds);
        }
        private static string Touch(string directory, string name) { var path = Path.Combine(directory, name); File.WriteAllBytes(path, []); return path; }
        public void Dispose() => System.IO.Directory.Delete(Directory, true);
    }
}
