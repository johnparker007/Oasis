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
        var session = new FakeSession { AdvanceFailure = new InvalidOperationException("advance failed") };
        var runtime = new FakeRuntime(session);
        var backend = new FabricEmulationBackend("runtime", "amber", _ => runtime, new FakeAudioSink(), new FakeClock());
        var request = new EmulationLaunchRequest(FruitMachinePlatformType.Impact, "machine", "", [], "", new System6NativeRomSettings());

        await backend.StartAsync(request, CancellationToken.None);
        await session.FirstAdvance.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await session.Disposed.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.IsType<InvalidOperationException>(backend.LastFailure);
        Assert.Equal(EmulationBackendState.Failed, backend.State);
        Assert.Equal(1, session.ShutdownCount);
        Assert.Equal(1, session.DisposeCount);
        Assert.Equal(1, runtime.DisposeCount);
        await backend.StopAsync(CancellationToken.None);
    }

    private sealed class FakeClock : IFabricClock
    {
        private long _timestamp;
        public long Frequency => 1_000;
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
        public TaskCompletionSource Disposed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Exception? AdvanceFailure { get; init; }
        public int MaximumConcurrentCalls { get; private set; }
        public int ResetCount { get; private set; }
        public int ShutdownCount { get; private set; }
        public int DisposeCount { get; private set; }
        public FabricCapabilities Capabilities => new((ulong)(FabricCapability.DigitalInput | FabricCapability.Audio));
        public void Initialise() => Invoke(() => { });
        public void Reset() => Invoke(() => ResetCount++);
        public void Advance(ulong elapsedNanoseconds) => Invoke(() => { FirstAdvance.TrySetResult(); if (AdvanceFailure is not null) throw AdvanceFailure; });
        public void SubmitInput(FabricInput input) => Invoke(() => { });
        public FabricMachineSnapshot GetSnapshot() => Invoke(() => new FabricMachineSnapshot(1, [], [], [], []));
        public FabricAudioFormat GetAudioFormat() => Invoke(() => new FabricAudioFormat(48000, 2, 16, true, true, true));
        public int ReadAudio(Span<short> samples, int frameCapacity) => Invoke(() => 0);
        public void Shutdown() => Invoke(() => ShutdownCount++);
        public void Dispose() { DisposeCount++; Disposed.TrySetResult(); }
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
        public void Start(EmulationAudioFormat format) => StartCount++;
        public void PushPcm(ReadOnlySpan<byte> pcmBytes) { }
        public void Stop() => StopCount++;
        public void Clear() { }
        public void Dispose() { }
    }
}
