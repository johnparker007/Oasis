using Xunit;
using System.Diagnostics;
using OasisEditor;

namespace OasisEditor.NativeIntegrationTests;

public sealed class FabricProviderIntegrationTests
{
    [NativeFact("FABRIC_RUNTIME_DLL", "AMBER_FAKE_API_V2_DLL")]
    public void FakeAmber_ExercisesCompleteBoundaryAndStressPaths()
    {
        var runtimePath = NativePrerequisites.RequireFile("FABRIC_RUNTIME_DLL");
        var amberPath = NativePrerequisites.RequireFile("AMBER_FAKE_API_V2_DLL");
        using var roms = TemporaryRomSet.Create();

        var stopwatch = Stopwatch.StartNew();
        using var runtime = new FabricRuntimeLibrary(runtimePath);
        using var session = runtime.CreateSession(CreateRequest(amberPath, roms.Resources));
        session.Initialise();
        session.Reset();
        stopwatch.Stop();
        RecordTiming("fake-provider startup", stopwatch.Elapsed);

        Assert.True(session.Capabilities.Has(FabricCapability.DigitalInput));
        Assert.True(session.Capabilities.Has(FabricCapability.Audio));

        for (var iteration = 0; iteration < 1_000; iteration++)
        {
            session.SubmitInput(new FabricInput("oasis.switch.0", 0, FabricInputKind.Digital, true));
            session.SubmitInput(new FabricInput("oasis.switch.0", 0, FabricInputKind.Digital, false));
            session.Advance((ulong)(1_000_000 + iteration % 17));
        }

        ulong? previousSequence = null;
        stopwatch.Restart();
        for (var iteration = 0; iteration < 1_000; iteration++)
        {
            var snapshot = session.GetSnapshot();
            if (previousSequence.HasValue)
                Assert.True(snapshot.Sequence > previousSequence.Value,
                    $"Snapshot sequence did not increase: {previousSequence} -> {snapshot.Sequence}.");
            previousSequence = snapshot.Sequence;
        }
        stopwatch.Stop();
        RecordTiming("1,000 snapshots", stopwatch.Elapsed);

        var format = session.GetAudioFormat();
        Assert.Equal((uint)48_000, format.SampleRate);
        Assert.Equal((ushort)2, format.ChannelCount);
        var samples = new short[512 * format.ChannelCount];
        stopwatch.Restart();
        var sawZero = false;
        var sawPartial = false;
        for (var iteration = 0; iteration < 1_000; iteration++)
        {
            Array.Fill(samples, short.MinValue);
            var written = session.ReadAudio(samples, 512);
            Assert.InRange(written, 0, 512);
            sawZero |= written == 0;
            sawPartial |= written is > 0 and < 512;
            var firstUnwritten = checked(written * format.ChannelCount);
            if (firstUnwritten < samples.Length)
                Assert.Equal(short.MinValue, samples[firstUnwritten]);
        }
        stopwatch.Stop();
        RecordTiming("1,000 audio reads", stopwatch.Elapsed);
        Console.WriteLine($"[Fabric audio] observed zero={sawZero}, partial={sawPartial}");

        session.Shutdown();
    }

    [NativeFact("FABRIC_RUNTIME_DLL", "AMBER_FAKE_API_V2_DLL")]
    public void FakeAmber_RepeatedSessionLifecycleKeepsRuntimeAliveUntilSessionsEnd()
    {
        var runtimePath = NativePrerequisites.RequireFile("FABRIC_RUNTIME_DLL");
        var amberPath = NativePrerequisites.RequireFile("AMBER_FAKE_API_V2_DLL");
        using var roms = TemporaryRomSet.Create();

        var stopwatch = Stopwatch.StartNew();
        for (var iteration = 0; iteration < 200; iteration++)
        {
            using var runtime = new FabricRuntimeLibrary(runtimePath);
            using var session = runtime.CreateSession(CreateRequest(amberPath, roms.Resources));
            session.Initialise();
            session.Shutdown();
        }
        stopwatch.Stop();
        RecordTiming("200 full runtime/session lifecycles", stopwatch.Elapsed);
    }

    [NativeFact("FABRIC_RUNTIME_DLL", "AMBER_FAKE_API_V2_DLL")]
    public void RuntimeDisposalIsDeferredWhileFakeProviderSessionIsAlive()
    {
        var runtimePath = NativePrerequisites.RequireFile("FABRIC_RUNTIME_DLL");
        var amberPath = NativePrerequisites.RequireFile("AMBER_FAKE_API_V2_DLL");
        using var roms = TemporaryRomSet.Create();

        var runtime = new FabricRuntimeLibrary(runtimePath);
        var session = runtime.CreateSession(CreateRequest(amberPath, roms.Resources));
        runtime.Dispose();

        session.Initialise();
        session.Advance(1_000_000);
        session.Shutdown();
        session.Dispose();

        // A fresh load proves the prior session released the deferred runtime/module ownership.
        using var reloaded = new FabricRuntimeLibrary(runtimePath);
    }

    [NativeFact("FABRIC_RUNTIME_DLL", "AMBER_API_V2_DLL", "AMBER_TEST_ROM_DIRECTORY")]
    public void RealAmber_OptionalLifecycleSmokeTest()
    {
        var runtimePath = NativePrerequisites.RequireFile("FABRIC_RUNTIME_DLL");
        var amberPath = NativePrerequisites.RequireFile("AMBER_API_V2_DLL");
        var romDirectory = NativePrerequisites.RequireDirectory("AMBER_TEST_ROM_DIRECTORY");
        var resources = DiscoverRealRomResources(romDirectory);

        using var runtime = new FabricRuntimeLibrary(runtimePath);
        using var session = runtime.CreateSession(CreateRequest(amberPath, resources));
        session.Initialise();
        session.Reset();
        for (var iteration = 0; iteration < 100; iteration++)
            session.Advance(1_000_000);
        _ = session.GetSnapshot();
        if (session.Capabilities.Has(FabricCapability.Audio))
        {
            var format = session.GetAudioFormat();
            var samples = new short[checked(256 * format.ChannelCount)];
            _ = session.ReadAudio(samples, 256);
        }
        session.Shutdown();
    }

    private static FabricLaunchRequest CreateRequest(string amberPath, IReadOnlyList<FabricRomResource> resources)
    {
        var settings = new System6NativeRomSettings
        {
            PercentSwitchValue = 0,
            CoinCommunicationStyle = AmberCoinCommunicationStyle.Parallel,
            CoinCommunicationInvert = false,
            CoinPulseCycles = 800_000,
            CoinEdcEnabled = false,
            Coins = [new System6CoinSettings { Num = 0, Enabled = true, CoinEnable = 1, CoinValue = 0 }]
        };
        return new FabricLaunchRequest(
            "amber", "jpm-system6", amberPath, resources,
            FabricAmberConfiguration.FromSystem6(settings));
    }

    private static IReadOnlyList<FabricRomResource> DiscoverRealRomResources(string root)
    {
        var resources = new List<FabricRomResource>();
        AddRole("program", FabricRomRole.Program);
        AddRole("sound", FabricRomRole.Sound);
        if (resources.Count == 0)
            throw new InvalidOperationException(
                "AMBER_TEST_ROM_DIRECTORY must contain program and/or sound subdirectories with up to four ROM files.");
        return resources;

        void AddRole(string directoryName, FabricRomRole role)
        {
            var directory = Path.Combine(root, directoryName);
            if (!Directory.Exists(directory))
                return;
            var paths = Directory.GetFiles(directory).Order(StringComparer.Ordinal).ToArray();
            if (paths.Length > 4)
                throw new InvalidOperationException($"{directory} contains {paths.Length} files; the Fabric Amber provider supports at most four {role} ROMs.");
            for (var slot = 0; slot < paths.Length; slot++)
                resources.Add(new FabricRomResource(role, (uint)slot, paths[slot]));
        }
    }

    private static void RecordTiming(string operation, TimeSpan elapsed) =>
        Console.WriteLine($"[Fabric timing] {operation}: {elapsed.TotalMilliseconds:F3} ms");

    private sealed class TemporaryRomSet : IDisposable
    {
        private readonly string _directory;
        private TemporaryRomSet(string directory, IReadOnlyList<FabricRomResource> resources)
        {
            _directory = directory;
            Resources = resources;
        }

        internal IReadOnlyList<FabricRomResource> Resources { get; }

        internal static TemporaryRomSet Create()
        {
            var directory = Path.Combine(Path.GetTempPath(), "oasis-fabric-native-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            var program0 = Path.Combine(directory, "program-0.rom");
            var program1 = Path.Combine(directory, "program-1.rom");
            var sound0 = Path.Combine(directory, "sound-0.rom");
            File.WriteAllBytes(program0, [0x00, 0x01, 0x02, 0x03]);
            File.WriteAllBytes(program1, [0x10, 0x11, 0x12, 0x13]);
            File.WriteAllBytes(sound0, [0x20, 0x21, 0x22, 0x23]);
            return new TemporaryRomSet(directory,
            [
                new(FabricRomRole.Program, 0, program0),
                new(FabricRomRole.Program, 1, program1),
                new(FabricRomRole.Sound, 0, sound0)
            ]);
        }

        public void Dispose()
        {
            try { Directory.Delete(_directory, true); } catch { }
        }
    }
}
