using Xunit;
using OasisEditor;

namespace OasisEditor.Tests;

public sealed class FabricManagedBehaviorTests
{
    [Fact]
    public void ElapsedTime_PreservesFractionalRemainderWithoutDrift()
    {
        var converter = new FabricElapsedTime(3);
        converter.Reset(0);

        Assert.Equal(333_333_333UL, converter.AdvanceTo(1));
        Assert.Equal(333_333_333UL, converter.AdvanceTo(2));
        Assert.Equal(333_333_334UL, converter.AdvanceTo(3));
    }

    [Fact]
    public void RomResources_PreserveRolesSlotsAndTrailingBlanks()
    {
        var settings = new System6NativeRomSettings
        {
            ProgramRom1Path = "p0", ProgramRom2Path = "p1",
            SoundRom1Path = "s0", SoundRom2Path = string.Empty
        };

        var resources = FabricEmulationBackend.BuildRomResources(settings);

        Assert.Collection(resources,
            item => Assert.Equal(new FabricRomResource(FabricRomRole.Program, 0, "p0"), item),
            item => Assert.Equal(new FabricRomResource(FabricRomRole.Program, 1, "p1"), item),
            item => Assert.Equal(new FabricRomResource(FabricRomRole.Sound, 0, "s0"), item));
    }

    [Fact]
    public void RomResources_RejectMiddleGapWithoutDroppingLaterPath()
    {
        var settings = new System6NativeRomSettings
        {
            ProgramRom1Path = "p0", ProgramRom2Path = string.Empty, ProgramRom3Path = "p2"
        };

        var error = Assert.Throws<InvalidOperationException>(() => FabricEmulationBackend.BuildRomResources(settings));
        Assert.Contains("Program ROM slot 2", error.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    [InlineData(15)]
    public void AmberConfiguration_PreservesRawPercentageSwitch(int value)
    {
        var configuration = FabricAmberConfiguration.FromSystem6(new System6NativeRomSettings { PercentSwitchValue = value });
        Assert.Equal((uint)value, configuration.PercentageSwitch);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(16)]
    public void AmberConfiguration_RejectsInvalidPercentageSwitch(int value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            FabricAmberConfiguration.FromSystem6(new System6NativeRomSettings { PercentSwitchValue = value }));
    }

    [Fact]
    public void AmberConfiguration_MapsDirectBackendReelAndEnabledCoinPolicy()
    {
        var settings = new System6NativeRomSettings
        {
            ReelOptos = [new() { ReelIndex = 2, Enabled = false, Steps = 96, OptoStart = 5, OptoEnd = 7, OptoInvert = true }],
            Coins =
            [
                new() { Num = 1, Enabled = true, CoinEnable = 1, CoinValue = 20, LockoutInvert = 1, CounterIn = 2, CounterOut = 3, PortIndex = 4, Coin = 5, Level = 6, FullLevel = 7 },
                new() { Num = 2, Enabled = false }
            ],
            PercentSwitchValue = 12
        };

        var configuration = FabricAmberConfiguration.FromSystem6(settings);

        Assert.Equal(1u << 2, configuration.ReelApplyMask);
        Assert.Equal(new FabricAmberReel(2, false, 96, 5, 7, true), Assert.Single(configuration.Reels));
        Assert.Equal(1u << 1, configuration.CoinChannelApplyMask);
        Assert.Equal(1u << 1, configuration.CoinRouteApplyMask);
        Assert.Equal(new FabricAmberCoinChannel(1, true, 20, true), Assert.Single(configuration.CoinChannels));
        Assert.Equal(new FabricAmberCoinRoute(1, true, 2, 3, 4, 5, 6, 7), Assert.Single(configuration.CoinRoutes));
        Assert.Equal(12u, configuration.PercentageSwitch);
    }

    [Fact]
    public async Task Backend_UsesExactLaunchValuesAndSerializesResetWithPump()
    {
        var clock = new FakeClock();
        var session = new FakeSession();
        var runtime = new FakeRuntime(session);
        string? runtimePath = null;
        var audio = new FakeAudioSink();
        var backend = new FabricEmulationBackend("C:/fabric/FabricRuntime.dll", "D:/amber/amber.dll",
            path => { runtimePath = path; return runtime; }, audio, clock);
        var request = new EmulationLaunchRequest(FruitMachinePlatformType.Impact, "machine", "", [], "",
            new System6NativeRomSettings { ProgramRom1Path = "p0", SoundRom1Path = "s0" });

        await backend.StartAsync(request, CancellationToken.None);
        await session.FirstAdvance.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await backend.ResetAsync(EmulationResetKind.Soft, CancellationToken.None);
        await backend.StopAsync(CancellationToken.None);

        Assert.Equal("C:/fabric/FabricRuntime.dll", runtimePath);
        Assert.Equal("amber-api-v2", runtime.Request!.BackendKind);
        Assert.Equal("jpm-system6", runtime.Request.MachineIdentifier);
        Assert.Equal("D:/amber/amber.dll", runtime.Request.BackendPath);
        Assert.Equal(1, session.MaximumConcurrentCalls);
        Assert.Equal(1, session.ResetCount);
        Assert.Equal(1, session.ShutdownCount);
        Assert.Equal(1, session.DisposeCount);
        Assert.Equal(1, runtime.DisposeCount);
        Assert.Equal(1, audio.StartCount);
        Assert.Equal(1, audio.StopCount);
    }

    [Fact]
    public async Task Backend_PumpFailureIsRecordedAndCleanedUp()
    {
        var failure = new FabricException(FabricResult.BackendError, "FabricSessionAdvance",
            "production Amber adapter: Run returned an invalid result",
            new InvalidOperationException("inner adapter failure"));
        var session = new FakeSession { AdvanceFailure = failure };
        var runtime = new FakeRuntime(session);
        var errors = new List<string>();
        var backend = new FabricEmulationBackend("runtime", "amber", _ => runtime, new FakeAudioSink(), new FakeClock(), errors.Add);
        var request = new EmulationLaunchRequest(FruitMachinePlatformType.Impact, "machine", "", [], "", new System6NativeRomSettings());

        await backend.StartAsync(request, CancellationToken.None);
        await session.FirstAdvance.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await session.Disposed.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await WaitForStateAsync(backend, EmulationBackendState.Failed);

        Assert.Same(failure, backend.LastFailure);
        var error = Assert.Single(errors);
        Assert.Contains("Fabric emulation pump failed", error);
        Assert.Contains("FabricSessionAdvance", error);
        Assert.Contains("7 (BackendError)", error);
        Assert.Contains("production Amber adapter", error);
        Assert.Contains("inner adapter failure", error);
        Assert.Contains(nameof(FabricException), error);
        Assert.Equal(EmulationBackendState.Failed, backend.State);
        Assert.Equal(1, session.ShutdownCount);
        Assert.Equal(1, session.DisposeCount);
        Assert.Equal(1, runtime.DisposeCount);
        await backend.StopAsync(CancellationToken.None);
    }

    [Theory]
    [InlineData("snapshot")]
    [InlineData("audio")]
    public async Task Backend_PumpFailureLogIdentifiesSnapshotAndAudioOperations(string failingOperation)
    {
        var operation = failingOperation == "snapshot" ? "FabricSessionGetSnapshot" : "FabricSessionReadAudio";
        var failure = new FabricException(FabricResult.InternalError, operation, "native last error");
        var session = new FakeSession
        {
            SnapshotFailure = failingOperation == "snapshot" ? failure : null,
            AudioFailure = failingOperation == "audio" ? failure : null
        };
        var errors = new List<string>();
        var backend = new FabricEmulationBackend("runtime", "amber", _ => new FakeRuntime(session),
            new FakeAudioSink(), new FakeClock(), errors.Add);

        await backend.StartAsync(CreateRequest(), CancellationToken.None);
        await session.Disposed.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await WaitForStateAsync(backend, EmulationBackendState.Failed);

        Assert.Same(failure, backend.LastFailure);
        Assert.Contains(operation, Assert.Single(errors));
        Assert.Equal(EmulationBackendState.Failed, backend.State);
    }

    [Fact]
    public async Task Backend_CleanupFailureIsLoggedWithoutReplacingPumpFailure()
    {
        var pumpFailure = new InvalidOperationException("original pump failure");
        var session = new FakeSession
        {
            AdvanceFailure = pumpFailure,
            ShutdownFailure = new InvalidOperationException("cleanup shutdown failure"),
            DisposeFailure = new InvalidOperationException("cleanup dispose failure")
        };
        var runtime = new FakeRuntime(session);
        var errors = new List<string>();
        var backend = new FabricEmulationBackend("runtime", "amber", _ => runtime,
            new FakeAudioSink(), new FakeClock(), errors.Add);

        await backend.StartAsync(CreateRequest(), CancellationToken.None);
        await session.Disposed.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await WaitForStateAsync(backend, EmulationBackendState.Failed);

        Assert.Same(pumpFailure, backend.LastFailure);
        Assert.Equal(2, errors.Count);
        Assert.Contains("original pump failure", errors[0]);
        Assert.Contains("cleanup after pump failure", errors[1]);
        Assert.Contains("cleanup shutdown failure", errors[1]);
        Assert.Contains("cleanup dispose failure", errors[1]);
        Assert.Equal(1, runtime.DisposeCount);
        Assert.Equal(EmulationBackendState.Failed, backend.State);
    }

    [Fact]
    public async Task Backend_NormalStopCancellationDoesNotReportPumpFailure()
    {
        var session = new FakeSession();
        var errors = new List<string>();
        var backend = new FabricEmulationBackend("runtime", "amber", _ => new FakeRuntime(session),
            new FakeAudioSink(), new FakeClock(), errors.Add);

        await backend.StartAsync(CreateRequest(), CancellationToken.None);
        await session.FirstAdvance.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await backend.StopAsync(CancellationToken.None);

        Assert.Null(backend.LastFailure);
        Assert.Empty(errors);
        Assert.Equal(EmulationBackendState.Stopped, backend.State);
    }

    [Theory]
    [InlineData(44100, 2)]
    [InlineData(48000, 2)]
    [InlineData(32000, 1)]
    public async Task Backend_AcceptsPositivePcm16FormatsAndPassesFormatToSink(uint sampleRate, ushort channels)
    {
        var session = new FakeSession { AudioFormat = new(sampleRate, channels, 16, true, true, true) };
        var audio = new FakeAudioSink();
        var backend = CreateBackend(session, audio);

        await backend.StartAsync(CreateRequest(), CancellationToken.None);
        await session.FirstAudioRead.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await backend.StopAsync(CancellationToken.None);

        Assert.Equal(new EmulationAudioFormat(checked((int)sampleRate), channels, 16), audio.StartedFormat);
        Assert.Equal((checked((int)sampleRate) + 999) / 1000, session.LastFrameCapacity);
        Assert.Equal(0, session.LastSampleCapacity % channels);
    }

    [Theory]
    [InlineData(48000, 48)]
    [InlineData(44100, 45)]
    [InlineData(1, 1)]
    public void AudioCapacity_HoldsMaximumSingleTickEntitlement(int sampleRate, int expectedFrames)
    {
        Assert.Equal(expectedFrames, FabricEmulationBackend.CalculateAudioFramesPerTick(sampleRate));
    }

    [Fact]
    public async Task Backend_UsesFixedOneMillisecondAdvancesAndSubmitsFramesExactlyOnce()
    {
        var session = new FakeSession { FramesToWrite = 48 };
        var audio = new FakeAudioSink();
        var backend = CreateBackend(session, audio);

        await backend.StartAsync(CreateRequest(), CancellationToken.None);
        await session.FirstAudioRead.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await backend.StopAsync(CancellationToken.None);

        Assert.All(session.Advances, value => Assert.Equal(1_000_000UL, value));
        Assert.Equal(48, session.LastFrameCapacity);
        Assert.Equal(48 * 2 * sizeof(short), audio.LastPcmBytes);
    }

    [Theory]
    [InlineData(48000, 2, 16, false, true, true)]
    [InlineData(48000, 2, 16, true, false, true)]
    [InlineData(48000, 2, 16, true, true, false)]
    [InlineData(48000, 2, 8, true, true, true)]
    [InlineData(0, 2, 16, true, true, true)]
    [InlineData(48000, 0, 16, true, true, true)]
    public async Task Backend_RejectsUnsupportedAudioFormats(
        uint sampleRate, ushort channels, ushort bitsPerSample,
        bool interleaved, bool signedSamples, bool littleEndian)
    {
        var session = new FakeSession
        {
            AudioFormat = new(sampleRate, channels, bitsPerSample, interleaved, signedSamples, littleEndian)
        };
        var audio = new FakeAudioSink();
        var backend = CreateBackend(session, audio);

        await Assert.ThrowsAsync<NotSupportedException>(() =>
            backend.StartAsync(CreateRequest(), CancellationToken.None));

        Assert.Null(audio.StartedFormat);
    }

    private static FabricEmulationBackend CreateBackend(FakeSession session, FakeAudioSink audio) =>
        new("runtime", "amber", _ => new FakeRuntime(session), audio, new FakeClock());

    private static EmulationLaunchRequest CreateRequest() =>
        new(FruitMachinePlatformType.Impact, "machine", "", [], "", new System6NativeRomSettings());

    private static async Task WaitForStateAsync(FabricEmulationBackend backend, EmulationBackendState state)
    {
        var timeout = DateTime.UtcNow + TimeSpan.FromSeconds(2);
        while (backend.State != state && DateTime.UtcNow < timeout)
            await Task.Delay(10);
        Assert.Equal(state, backend.State);
    }

    private sealed class FakeClock : IFabricClock
    {
        private long _timestamp;
        public long Frequency => 10_000;
        public long GetTimestamp() => Interlocked.Increment(ref _timestamp);
    }

    private sealed class FakeRuntime(IFabricMachineSession session) : IFabricRuntimeLibrary
    {
        public FabricLaunchRequest? Request { get; private set; }
        public int DisposeCount { get; private set; }
        public IFabricMachineSession CreateSession(FabricLaunchRequest request) { Request = request; return session; }
        public void Dispose() => DisposeCount++;
    }

    private sealed class FakeSession : IFabricMachineSession
    {
        private int _activeCalls;
        public TaskCompletionSource FirstAdvance { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource FirstAudioRead { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Disposed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Exception? AdvanceFailure { get; init; }
        public Exception? SnapshotFailure { get; init; }
        public Exception? AudioFailure { get; init; }
        public Exception? ShutdownFailure { get; init; }
        public Exception? DisposeFailure { get; init; }
        public FabricAudioFormat AudioFormat { get; init; } = new(48000, 2, 16, true, true, true);
        public int LastFrameCapacity { get; private set; }
        public int LastSampleCapacity { get; private set; }
        public int MaximumConcurrentCalls { get; private set; }
        public int ResetCount { get; private set; }
        public int ShutdownCount { get; private set; }
        public int DisposeCount { get; private set; }
        public int FramesToWrite { get; init; }
        public List<ulong> Advances { get; } = [];
        public FabricCapabilities Capabilities => new((ulong)(FabricCapability.DigitalInput | FabricCapability.Audio));
        public void Initialise() => Invoke(() => { });
        public void Reset() => Invoke(() => ResetCount++);
        public void Advance(ulong elapsedNanoseconds) => Invoke(() => { Advances.Add(elapsedNanoseconds); FirstAdvance.TrySetResult(); if (AdvanceFailure is not null) throw AdvanceFailure; });
        public void SubmitInput(FabricInput input) => Invoke(() => { });
        public FabricMachineSnapshot GetSnapshot() => Invoke(() => SnapshotFailure is null
            ? new FabricMachineSnapshot(1, [], [], [], [])
            : throw SnapshotFailure);
        public FabricAudioFormat GetAudioFormat() => Invoke(() => AudioFormat);
        public int ReadAudio(Span<short> samples, int frameCapacity)
        {
            var sampleCapacity = samples.Length;
            return Invoke(() =>
            {
                LastFrameCapacity = frameCapacity;
                LastSampleCapacity = sampleCapacity;
                FirstAudioRead.TrySetResult();
                if (AudioFailure is not null) throw AudioFailure;
                return Math.Min(FramesToWrite, frameCapacity);
            });
        }
        public void Shutdown() => Invoke(() => { ShutdownCount++; if (ShutdownFailure is not null) throw ShutdownFailure; });
        public void Dispose()
        {
            DisposeCount++;
            Disposed.TrySetResult();
            if (DisposeFailure is not null) throw DisposeFailure;
        }
        private void Invoke(Action action) => Invoke(() => { action(); return true; });
        private T Invoke<T>(Func<T> action)
        {
            var active = Interlocked.Increment(ref _activeCalls);
            MaximumConcurrentCalls = Math.Max(MaximumConcurrentCalls, active);
            try { return action(); }
            finally { Interlocked.Decrement(ref _activeCalls); }
        }
    }

    private sealed class FakeAudioSink : IEmulationAudioSink
    {
        public int StartCount { get; private set; }
        public int StopCount { get; private set; }
        public EmulationAudioFormat? StartedFormat { get; private set; }
        public int LastPcmBytes { get; private set; }
        public void Start(EmulationAudioFormat format) { StartedFormat = format; StartCount++; }
        public void PushPcm(ReadOnlySpan<byte> pcmBytes) => LastPcmBytes = pcmBytes.Length;
        public void Stop() => StopCount++;
        public void Clear() { }
        public void Dispose() { }
    }
}
