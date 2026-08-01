using Xunit;
using OasisEditor;
using System.Reflection;
using System.Text.Json;

namespace OasisEditor.Tests;

public sealed class FabricManagedBehaviorTests
{
    [Fact]
    public unsafe void NativeCharacterDisplayConversionPreservesBrightnessAndRejectsNonFiniteValues()
    {
        var native = new FabricCharacterDisplayNative { Brightness = 0.625f };

        var managed = FabricMachineSession.ConvertCharacterDisplay(ref native);

        Assert.Equal(0.625f, managed.Brightness);
        native.Brightness = float.NaN;
        Assert.Throws<InvalidDataException>(() => FabricMachineSession.ConvertCharacterDisplay(ref native));
    }

    public static TheoryData<int, int> System6AlphaBitMapping => new()
    {
        { 0, 0 }, { 1, 1 }, { 2, 2 }, { 3, 3 },
        { 4, 4 }, { 5, 5 }, { 6, 6 }, { 7, 7 },
        { 8, 10 }, { 9, 14 }, { 10, 9 }, { 11, 15 },
        { 12, 11 }, { 13, 12 }, { 14, 8 }, { 15, 13 },
    };

    public static TheoryData<int, int> System6SevenSegmentBitMapping => new()
    {
        { 0, 7 },
        { 1, 6 },
        { 2, 5 },
        { 3, 4 },
        { 4, 3 },
        { 5, 2 },
        { 6, 1 },
        { 7, 0 },
    };

    [Theory]
    [MemberData(nameof(System6SevenSegmentBitMapping))]
    public void System6SevenSegmentMapper_MapsEachNativeNamedSegmentToTheCanonicalOasisSegment(
        int nativeBit, int oasisBit)
    {
        Assert.Equal(1 << oasisBit,
            System6SevenSegmentMapper.MapNativeMaskToOasisMask(1 << nativeBit));
    }

    [Fact]
    public void System6SevenSegmentMapper_MapsZeroCombinedSegmentsAndIgnoresUnknownBits()
    {
        Assert.Equal(0, System6SevenSegmentMapper.MapNativeMaskToOasisMask(0));

        var nativeMask = (1 << 0) | (1 << 4) | (1 << 7) | (1 << 12);
        var expectedOasisMask = (1 << 7) | (1 << 3) | (1 << 0);
        Assert.Equal(expectedOasisMask, System6SevenSegmentMapper.MapNativeMaskToOasisMask(nativeMask));
    }

    [Theory]
    [MemberData(nameof(System6AlphaBitMapping))]
    public void System6AlphaMapper_MapsEveryNativeBitToItsOasisBit(int nativeBit, int oasisBit)
    {
        Assert.Equal(1 << oasisBit,
            System6AlphaSegmentMapper.MapNativeMaskToOasisMask(1 << nativeBit));
    }

    [Fact]
    public void System6AlphaMapper_MapsZeroAndMultipleBits()
    {
        Assert.Equal(0, System6AlphaSegmentMapper.MapNativeMaskToOasisMask(0));

        var nativeMask = (1 << 2) | (1 << 8) | (1 << 10) | (1 << 14);
        var expectedOasisMask = (1 << 2) | (1 << 10) | (1 << 9) | (1 << 8);
        Assert.Equal(expectedOasisMask, System6AlphaSegmentMapper.MapNativeMaskToOasisMask(nativeMask));
    }

    [Fact]
    public void Backend_PublishesMappedAlphaWithPunctuationAndMappedSevenSegmentMask()
    {
        var backend = CreateBackend(new FakeSession(), new FakeAudioSink());
        var changes = new List<MachineSegmentChangedEventArgs>();
        backend.SegmentChanged += (_, change) => changes.Add(change);
        var nativeAlphaMask = (1u << 8) | (1u << 14);
        const ulong sevenSegmentMask = 0x5a;
        var snapshot = new FabricMachineSnapshot(1, [], [],
            [new FabricCharacterDisplay("alpha", [nativeAlphaMask], [0b11], 0.5f)],
            [new FabricSegmentDisplay("seven", [sevenSegmentMask])]);

        backend.PublishSnapshot(snapshot);

        Assert.Collection(changes,
            alphaChange =>
            {
                Assert.Equal((1 << 10) | (1 << 8) | (1 << 16) | (1 << 17), alphaChange.SegmentMask);
                Assert.Equal(SegmentOutputType.NativeAlpha, alphaChange.OutputType);
            },
            sevenSegmentChange =>
            {
                Assert.Equal(0x5a, unchecked((int)sevenSegmentMask));
                Assert.Equal(0x5a, sevenSegmentChange.SegmentMask);
                Assert.Equal(SegmentOutputType.Digit, sevenSegmentChange.OutputType);
            });
    }

    [Fact]
    public void Backend_PublishesDisplayBrightnessIndependentlyFromSegmentChanges()
    {
        var backend = CreateBackend(new FakeSession(), new FakeAudioSink());
        var brightnessChanges = new List<MachineVfdBrightnessChangedEventArgs>();
        var segmentChanges = new List<MachineSegmentChangedEventArgs>();
        backend.VfdBrightnessChanged += (_, change) => brightnessChanges.Add(change);
        backend.SegmentChanged += (_, change) => segmentChanges.Add(change);

        backend.PublishSnapshot(new(1, [], [],
        [
            new("alpha.0", [1u], [0], 0.25f),
            new("alpha.1", [2u], [0], 0.75f)
        ], []));
        backend.PublishSnapshot(new(2, [], [],
        [
            new("alpha.0", [1u], [0], 0.5f),
            new("alpha.1", [2u], [0], 0.75f)
        ], []));
        backend.PublishSnapshot(new(3, [], [],
        [
            new("alpha.0", [4u], [0], 0.5f),
            new("alpha.1", [2u], [0], 0.75f)
        ], []));

        Assert.Collection(brightnessChanges,
            change => { Assert.Equal(0, change.CellId); Assert.Equal(0.25, change.NormalizedBrightness); },
            change => { Assert.Equal(FabricAbi.CharacterCapacity, change.CellId); Assert.Equal(0.75, change.NormalizedBrightness); },
            change => { Assert.Equal(0, change.CellId); Assert.Equal(0.5, change.NormalizedBrightness); });
        Assert.Equal(3, segmentChanges.Count);
    }

    [Fact]
    public async Task Backend_ResetClearsDisplayBrightnessCache()
    {
        var backend = CreateBackend(new FakeSession(), new FakeAudioSink());
        var brightnessChanges = new List<MachineVfdBrightnessChangedEventArgs>();
        backend.VfdBrightnessChanged += (_, change) => brightnessChanges.Add(change);
        var snapshot = new FabricMachineSnapshot(1, [], [],
            [new("alpha", [], [], 0.4f)], []);

        await backend.StartAsync(CreateRequest(), CancellationToken.None);
        backend.PublishSnapshot(snapshot);
        await backend.ResetAsync(EmulationResetKind.Soft, CancellationToken.None);
        backend.PublishSnapshot(snapshot);
        await backend.StopAsync(CancellationToken.None);

        Assert.Equal(2, brightnessChanges.Count);
    }

    [Fact]
    public void Backend_DoesNotRepublishAnUnchangedMappedSevenSegmentMask()
    {
        var backend = CreateBackend(new FakeSession(), new FakeAudioSink());
        var changes = new List<MachineSegmentChangedEventArgs>();
        backend.SegmentChanged += (_, change) => changes.Add(change);
        var snapshot = new FabricMachineSnapshot(1, [], [], [],
            [new FabricSegmentDisplay("seven", [0x41UL])]);

        backend.PublishSnapshot(snapshot);
        backend.PublishSnapshot(new FabricMachineSnapshot(2, [], [], [],
            [new FabricSegmentDisplay("seven", [0x141UL])]));

        var change = Assert.Single(changes);
        Assert.Equal(0x82, change.SegmentMask);
    }

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
                new() { Num = 1, Enabled = true, CoinEnable = 1, CoinValue = 20, LockoutValue = 2, LockoutInvert = 1, CounterIn = 2, CounterOut = 3, PortIndex = 4, Coin = 5, Level = 6, FullLevel = 7 },
                new() { Num = 2, Enabled = false }
            ],
            PercentSwitchValue = 12
        };

        var configuration = FabricAmberConfiguration.FromSystem6(settings);

        Assert.Equal(1u << 2, configuration.ReelApplyMask);
        Assert.Equal(new FabricAmberReel(2, false, 96, 5, 7, true), Assert.Single(configuration.Reels));
        Assert.Equal(1u << 1, configuration.CoinChannelApplyMask);
        Assert.Equal(1u << 1, configuration.CoinRouteApplyMask);
        Assert.Equal(new FabricAmberCoinChannel(1, true, 20, 2, true), Assert.Single(configuration.CoinChannels));
        Assert.Equal(new FabricAmberCoinRoute(1, true, 2, 3, 4, 5, 6, 7), Assert.Single(configuration.CoinRoutes));
        Assert.Equal(12u, configuration.PercentageSwitch);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 0)]
    [InlineData(2, 1)]
    public void AmberConfiguration_MapsLockoutValuesWithoutInference(int lockoutValue, int lockoutInvert)
    {
        var settings = new System6NativeRomSettings
        {
            Coins =
            [
                new()
                {
                    Num = 2, Enabled = true, CoinEnable = 1, CoinValue = 20,
                    LockoutValue = lockoutValue, LockoutInvert = lockoutInvert
                }
            ]
        };

        var channel = Assert.Single(FabricAmberConfiguration.FromSystem6(settings).CoinChannels);

        Assert.Equal(new FabricAmberCoinChannel(2, true, 20, checked((uint)lockoutValue), lockoutInvert != 0), channel);
    }

    [Fact]
    public void AmberConfiguration_NativeBytesUseCurrentChannelLayoutAndPreserveRouteOffset()
    {
        var configuration = FabricAmberConfiguration.FromSystem6(new System6NativeRomSettings
        {
            Coins =
            [
                new()
                {
                    Num = 2, Enabled = true, CoinEnable = 1, CoinValue = 20,
                    LockoutValue = 1, LockoutInvert = 1, CounterIn = 7
                }
            ]
        });

        var bytes = configuration.ToNativeBytes();
        const int firstChannelOffset = 240;
        const int firstRouteOffset = 360;

        Assert.Equal(2u, BitConverter.ToUInt32(bytes, firstChannelOffset));
        Assert.Equal(1u, BitConverter.ToUInt32(bytes, firstChannelOffset + 4));
        Assert.Equal(20u, BitConverter.ToUInt32(bytes, firstChannelOffset + 8));
        Assert.Equal(1u, BitConverter.ToUInt32(bytes, firstChannelOffset + 12));
        Assert.Equal(1u, BitConverter.ToUInt32(bytes, firstChannelOffset + 16));
        Assert.Equal(2u, BitConverter.ToUInt32(bytes, firstRouteOffset));
        Assert.Equal(1u, BitConverter.ToUInt32(bytes, firstRouteOffset + 4));
        Assert.Equal(7u, BitConverter.ToUInt32(bytes, firstRouteOffset + 8));
    }

    [Fact]
    public async Task Backend_LaunchPreservesLockoutValueAndLogsSourceAndFabricValues()
    {
        var session = new FakeSession();
        var runtime = new FakeRuntime(session);
        var diagnostics = new List<string>();
        var backend = new FabricEmulationBackend("runtime", "amber", _ => runtime,
            new FakeAudioSink(), new FakeClock(), diagnosticLogger: diagnostics.Add);
        var request = new EmulationLaunchRequest(new System6NativeRomSettings
        {
            Coins = [new() { Num = 2, Enabled = true, CoinEnable = 1, CoinValue = 20, LockoutValue = 1 }]
        });

        await backend.StartAsync(request, CancellationToken.None);
        await session.FirstAdvance.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await backend.StopAsync(CancellationToken.None);

        var configuration = Assert.IsType<FabricAmberConfiguration>(runtime.Request!.Configuration);
        Assert.Equal(1u, Assert.Single(configuration.CoinChannels).LockoutValue);
        Assert.Contains("[Coin Config Source] channelIndex=2 enabled=true value=20 lockoutValue=1 lockoutInvert=false", diagnostics);
        Assert.Contains("[Coin Config Fabric] channelIndex=2 enabled=true value=20 lockoutValue=1 lockoutInvert=false", diagnostics);
    }

    [Fact]
    public void CoinSettingsViewModel_EditPreservesLockoutValueInBackingModel()
    {
        System6CoinSettings? saved = null;
        System6CoinSettingsViewModel? viewModel = null;
        viewModel = new System6CoinSettingsViewModel(new System6CoinSettings { LockoutValue = 0 },
            () => saved = viewModel!.ToModel());

        viewModel.LockoutValue = 2;

        Assert.Equal(2, saved!.LockoutValue);
    }

    [Fact]
    public void ProjectSettingsSerialization_RoundTripsLockoutValue()
    {
        var settings = new System6NativeRomSettings
        {
            Coins = [new() { Num = 0, Enabled = true, CoinEnable = 1, LockoutValue = 2 }]
        };
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WritePropertyName("project_settings");
            writer.WriteStartObject();
            typeof(MainWindowViewModel).GetMethod("WriteSystem6NativeRomSettings",
                BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [writer, settings]);
            writer.WriteEndObject();
            writer.WriteEndObject();
        }
        using var document = JsonDocument.Parse(stream.ToArray());

        var reloaded = Assert.IsType<System6NativeRomSettings>(
            typeof(MainWindowViewModel).GetMethod("ResolveSystem6NativeRomSettings",
                BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [document.RootElement]));

        Assert.Equal(2, reloaded.Coins[0].LockoutValue);
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
        var request = new EmulationLaunchRequest(new System6NativeRomSettings { ProgramRom1Path = "p0", SoundRom1Path = "s0" });

        await backend.StartAsync(request, CancellationToken.None);
        await session.FirstAdvance.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await backend.ResetAsync(EmulationResetKind.Soft, CancellationToken.None);
        await backend.StopAsync(CancellationToken.None);

        Assert.Equal("C:/fabric/FabricRuntime.dll", runtimePath);
        Assert.Equal("amber", runtime.Request!.BackendKind);
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
        var request = new EmulationLaunchRequest(new System6NativeRomSettings());

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
        new(new System6NativeRomSettings());

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
