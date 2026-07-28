using System.Runtime.InteropServices;
using OasisEditor;
using Xunit;

namespace OasisEditor.Tests;

public sealed class AmberBridgeLibraryTests
{
    [Theory]
    [InlineData(0u, 0d)]
    [InlineData(65536u, 1d)]
    [InlineData(131072u, 2d)]
    [InlineData(uint.MaxValue, 65535.99998474121d)]
    public void Q16_16DecodingPreservesUnsignedRange(uint raw, double expected) =>
        Assert.Equal(expected, AmberQ16_16.Decode(raw), 10);

    [Fact]
    public void NegotiatesV2ExactlyOnceReadsInformationEnumeratesCoreAndCreates()
    {
        using var native = new FakeBridge();
        using var bridge = new AmberBridgeLibrary(native);

        Assert.Equal(AmberApiVersions.V2, native.RequestedVersion);
        Assert.Equal((uint)Marshal.SizeOf<AmberApiV2Native>(), native.RequestedSize);
        Assert.Equal(1, native.GetApiCallCount);
        Assert.Equal(new AmberBridgeDetails(AmberApiVersions.V1, "Amber", "0.1.1"), bridge.BridgeDetails);
        Assert.Contains("Create:jpm-system6", native.Calls);
    }

    [Fact]
    public void NativeLayoutsMatchTheArchitectureSensitiveV1Abi()
    {
        Assert.Equal(typeof(int), Enum.GetUnderlyingType(typeof(AmberResult)));
        Assert.Equal(8 + (2 * IntPtr.Size), Marshal.SizeOf<AmberBridgeInfoNative>());
        Assert.Equal(4 + (2 * IntPtr.Size) + (IntPtr.Size == 8 ? 4 : 0), Marshal.SizeOf<AmberCoreInfoNative>());
        Assert.Equal(80, Marshal.SizeOf<AmberApiV1Native>());
        Assert.Equal(144, Marshal.SizeOf<AmberApiV2Native>());
        string[] prefix = ["StructSize", "ApiVersion", "GetBridgeInfo", "EnumerateCore", "Create", "Destroy", "Initialise", "Reset", "Run", "Shutdown", "GetLastError"];
        foreach (var field in prefix)
            Assert.Equal(Marshal.OffsetOf<AmberApiV1Native>(field), Marshal.OffsetOf<AmberApiV2Native>(field));
        Assert.Equal(80, Marshal.OffsetOf<AmberApiV2Native>(nameof(AmberApiV2Native.GetCapabilities)).ToInt32());
        Assert.Equal(88, Marshal.OffsetOf<AmberApiV2Native>(nameof(AmberApiV2Native.SetSwitchState)).ToInt32());
        Assert.Equal(96, Marshal.OffsetOf<AmberApiV2Native>(nameof(AmberApiV2Native.GetOutputSnapshot)).ToInt32());
        Assert.Equal(104, Marshal.OffsetOf<AmberApiV2Native>(nameof(AmberApiV2Native.GetAudioFormat)).ToInt32());
        Assert.Equal(112, Marshal.OffsetOf<AmberApiV2Native>(nameof(AmberApiV2Native.FillAudioFrames)).ToInt32());
        Assert.Equal(120, Marshal.OffsetOf<AmberApiV2Native>(nameof(AmberApiV2Native.ConfigureReels)).ToInt32());
        Assert.Equal(128, Marshal.OffsetOf<AmberApiV2Native>(nameof(AmberApiV2Native.ConfigureCoins)).ToInt32());
        Assert.Equal(136, Marshal.OffsetOf<AmberApiV2Native>(nameof(AmberApiV2Native.SetPercentageSwitch)).ToInt32());
        Assert.Equal(0x00020000u, AmberApiVersions.V2);
        Assert.Equal(11, (int)AmberResult.NotSupported);
        Assert.Equal(12, (int)AmberResult.InvalidRange);
        Assert.Equal(13, (int)AmberResult.MalformedConfiguration);
        Assert.Equal(32, Marshal.SizeOf<AmberCapabilitiesV1Native>());
        Assert.Equal(8, Marshal.SizeOf<AmberLampStateV1Native>());
        Assert.Equal(52, Marshal.SizeOf<AmberAlphaDisplayStateV1Native>());
        Assert.Equal(8, Marshal.SizeOf<AmberSevenSegmentStateV1Native>());
        Assert.Equal(4592, Marshal.SizeOf<AmberOutputSnapshotV1Native>());
        Assert.Equal(40, Marshal.OffsetOf<AmberOutputSnapshotV1Native>(nameof(AmberOutputSnapshotV1Native.MatrixLamps)).ToInt32());
        Assert.Equal(4136, Marshal.OffsetOf<AmberOutputSnapshotV1Native>(nameof(AmberOutputSnapshotV1Native.ReelPositions)).ToInt32());
        Assert.Equal(4168, Marshal.OffsetOf<AmberOutputSnapshotV1Native>(nameof(AmberOutputSnapshotV1Native.AlphaDisplays)).ToInt32());
        Assert.Equal(4272, Marshal.OffsetOf<AmberOutputSnapshotV1Native>(nameof(AmberOutputSnapshotV1Native.SevenSegmentDisplays)).ToInt32());
        Assert.Equal(32, Marshal.SizeOf<AmberAudioFormatV1Native>());
        Assert.Equal(24, Marshal.SizeOf<AmberReelConfigV1Native>());
        Assert.Equal(208, Marshal.SizeOf<AmberReelConfigurationV1Native>());
        Assert.Equal(20, Marshal.SizeOf<AmberCoinChannelConfigV1Native>());
        Assert.Equal(32, Marshal.SizeOf<AmberCoinRouteConfigV1Native>());
        Assert.Equal(408, Marshal.SizeOf<AmberCoinConfigurationV1Native>());
        Assert.Equal(136, Marshal.OffsetOf<AmberCoinConfigurationV1Native>(nameof(AmberCoinConfigurationV1Native.Routes)).ToInt32());
        Assert.Equal(392, Marshal.OffsetOf<AmberCoinConfigurationV1Native>(nameof(AmberCoinConfigurationV1Native.LockoutPortBase)).ToInt32());
        Assert.Equal(4 + (8 * IntPtr.Size) + (IntPtr.Size == 8 ? 4 : 0), Marshal.SizeOf<AmberInitialiseParamsNative>());
    }

    [Theory]
    [InlineData((int)AmberResult.UnsupportedVersion, 0x00020000u)]
    [InlineData((int)AmberResult.Ok, 1u)]
    public void NegotiationFailuresUnloadTheModule(int resultCode, uint returnedVersion)
    {
        using var native = new FakeBridge
        {
            GetApiResult = (AmberResult)resultCode,
            ReturnedVersion = returnedVersion
        };

        var error = Assert.Throws<AmberBridgeException>(() => new AmberBridgeLibrary(native));

        Assert.Contains("AmberGetApi", error.Operation);
        Assert.Equal(new[] { "Unload" }, native.CleanupCalls);
    }

    [Fact]
    public void InvalidFunctionTableUnloadsWithoutUsingUnassignedDelegates()
    {
        using var native = new FakeBridge { OmitRun = true };

        var error = Assert.Throws<AmberBridgeException>(() => new AmberBridgeLibrary(native));

        Assert.Equal((int)AmberResult.ExportMissing, error.ResultCode);
        Assert.Equal(new[] { "Unload" }, native.CleanupCalls);
    }

    [Fact]
    public void GetBridgeInfoFailureUnloadsModule()
    {
        using var native = new FakeBridge { GetBridgeInfoResult = AmberResult.InternalError };

        var error = Assert.Throws<AmberBridgeException>(() => new AmberBridgeLibrary(native));

        Assert.Equal("GetBridgeInfo", error.Operation);
        Assert.Equal("native detail", error.BridgeError);
        Assert.Equal(new[] { "Unload" }, native.CleanupCalls);
    }

    [Fact]
    public void UndersizedBridgeMetadataIsRejected()
    {
        using var native = new FakeBridge
        {
            BridgeInfoSize = 0
        };

        var error = Assert.Throws<AmberBridgeException>(() => new AmberBridgeLibrary(native));

        Assert.Equal("GetBridgeInfo validation", error.Operation);
        Assert.Equal(new[] { "Unload" }, native.CleanupCalls);
    }

    [Fact]
    public void BridgeMetadataVersionIsIndependentOfNegotiatedTableVersion()
    {
        using var native = new FakeBridge { BridgeInfoApiVersion = AmberApiVersions.V1 };

        using var bridge = new AmberBridgeLibrary(native);

        Assert.Equal(AmberApiVersions.V1, bridge.BridgeDetails.ApiVersion);
        Assert.Equal(AmberApiVersions.V2, native.RequestedVersion);
    }

    [Fact]
    public void CoreEnumerationFailureUnloadsModule()
    {
        using var native = new FakeBridge { EnumerateResult = AmberResult.InternalError };

        var error = Assert.Throws<AmberBridgeException>(() => new AmberBridgeLibrary(native));

        Assert.Equal("EnumerateCore", error.Operation);
        Assert.Equal(new[] { "Unload" }, native.CleanupCalls);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void NullOrEmptyCoreIdIsRejected(string? coreId)
    {
        using var native = new FakeBridge { CoreId = coreId };

        var error = Assert.Throws<AmberBridgeException>(() => new AmberBridgeLibrary(native));

        Assert.Equal("EnumerateCore validation", error.Operation);
        Assert.Equal(new[] { "Unload" }, native.CleanupCalls);
    }

    [Fact]
    public void UndersizedCoreInformationIsRejectedBeforeReadingCoreId()
    {
        using var native = new FakeBridge { CoreInfoSize = 0 };

        var error = Assert.Throws<AmberBridgeException>(() => new AmberBridgeLibrary(native));

        Assert.Equal("EnumerateCore validation", error.Operation);
        Assert.Equal(new[] { "Unload" }, native.CleanupCalls);
    }

    [Fact]
    public void MissingRequiredCoreReachesNoMoreItemsAndUnloads()
    {
        using var native = new FakeBridge { CoreId = "another-core" };

        var error = Assert.Throws<AmberBridgeException>(() => new AmberBridgeLibrary(native));

        Assert.Equal((int)AmberResult.NoMoreItems, error.ResultCode);
        Assert.Equal(new[] { "Unload" }, native.CleanupCalls);
    }

    [Fact]
    public void CreateFailureWithHandleDestroysBeforeUnloadAndPreservesOriginalFailure()
    {
        using var native = new FakeBridge
        {
            CreateResult = AmberResult.InstanceLimit,
            ReturnHandleOnCreateFailure = true,
            DestroyResult = AmberResult.InternalError,
            ErrorText = "one instance only"
        };

        var error = Assert.Throws<AmberBridgeException>(() => new AmberBridgeLibrary(native));

        Assert.Equal("Create", error.Operation);
        Assert.Equal("one instance only", error.BridgeError);
        Assert.Equal(new[] { "Destroy", "Unload" }, native.CleanupCalls);
        Assert.DoesNotContain("Shutdown", native.Calls);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    public void InitialiseMarshalsProgramRomsAndNullTrailingSlots(int count)
    {
        using var native = new FakeBridge();
        var allocator = new RecordingAllocator();
        using var bridge = new AmberBridgeLibrary(native, allocator);
        var programs = Enumerable.Range(1, count).Select(index => $"p{index}.rom").ToArray();

        bridge.Initialise(programs);

        Assert.Equal(programs, native.ProgramPaths.Take(count));
        Assert.All(native.ProgramPaths.Skip(count), Assert.Null);
        Assert.All(native.SoundPaths, Assert.Null);
        Assert.Equal(allocator.AllocationCount, allocator.FreeCount);
        Assert.Empty(allocator.OutstandingPointers);
        Assert.False(allocator.DoubleFreeDetected);
    }

    [Fact]
    public void ZeroProgramRomsAreRejectedBeforeAllocationOrNativeCall()
    {
        using var native = new FakeBridge();
        var allocator = new RecordingAllocator();
        using var bridge = new AmberBridgeLibrary(native, allocator);
        var allocationsAfterConstruction = allocator.AllocationCount;

        Assert.Throws<ArgumentException>(() => bridge.Initialise([]));

        Assert.Equal(allocationsAfterConstruction, allocator.AllocationCount);
        Assert.DoesNotContain("Initialise", native.Calls);
    }

    [Fact]
    public void NullProgramCollectionIsRejectedBeforeNativeCall()
    {
        using var native = new FakeBridge();
        using var bridge = new AmberBridgeLibrary(native);

        Assert.Throws<ArgumentNullException>(() => bridge.Initialise(null!));

        Assert.DoesNotContain("Initialise", native.Calls);
    }

    [Fact]
    public void TooManyProgramOrSoundRomsAreRejectedBeforeNativeCall()
    {
        using var native = new FakeBridge();
        using var bridge = new AmberBridgeLibrary(native);
        var five = Enumerable.Range(0, 5).Select(index => $"{index}.rom").ToArray();

        Assert.Throws<ArgumentException>(() => bridge.Initialise(five));
        Assert.Throws<ArgumentException>(() => bridge.Initialise(["program.rom"], five));

        Assert.DoesNotContain("Initialise", native.Calls);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void InvalidRomEntryIsRejectedBeforeNativeCall(string? invalidPath)
    {
        using var native = new FakeBridge();
        using var bridge = new AmberBridgeLibrary(native);

        Assert.Throws<ArgumentException>(() => bridge.Initialise(new[] { invalidPath! }));

        Assert.DoesNotContain("Initialise", native.Calls);
    }

    [Fact]
    public void LaterProgramAllocationFailureFreesEveryEarlierAllocationOnce()
    {
        AssertAllocationFailureIsLeakFree(["p1", "p2", "p3"], [], throwOnPathAllocation: 3, expectedTotalFrees: 3);
    }

    [Fact]
    public void SoundAllocationFailureFreesProgramAndEarlierSoundAllocationsOnce()
    {
        AssertAllocationFailureIsLeakFree(["p1", "p2"], ["s1", "s2"], throwOnPathAllocation: 4, expectedTotalFrees: 4);
    }

    [Fact]
    public void NativeInitialiseFailureFreesPathsAndDisposeSkipsShutdown()
    {
        using var native = new FakeBridge { InitialiseResult = AmberResult.InitialiseFailed };
        var allocator = new RecordingAllocator();
        var bridge = new AmberBridgeLibrary(native, allocator);

        Assert.Throws<AmberBridgeException>(() => bridge.Initialise(["program.rom"], ["sound.rom"]));
        Assert.Equal(allocator.AllocationCount, allocator.FreeCount);
        Assert.Throws<InvalidOperationException>(() => bridge.Run(1));

        bridge.Dispose();

        Assert.Equal(new[] { "Destroy", "Unload" }, native.CleanupCalls);
    }

    [Fact]
    public void RepeatedRunReturnsReportedCyclesAndResetMaps()
    {
        using var native = new FakeBridge { CyclesRun = -17 };
        using var bridge = new AmberBridgeLibrary(native);
        bridge.Initialise(["program.rom"]);

        Assert.Equal(-17, bridge.Run(100));
        Assert.Equal(-17, bridge.Run(200));
        bridge.Reset();

        Assert.Equal(2, native.Calls.Count(call => call.StartsWith("Run:", StringComparison.Ordinal)));
        Assert.Contains("Reset", native.Calls);
    }

    [Fact]
    public void V2SemanticOperationsTranslateCapabilitiesAudioAndFrames()
    {
        using var native = new FakeBridge();
        using var bridge = new AmberBridgeLibrary(native);
        var capabilities = bridge.GetCapabilities();
        Assert.Equal(0x3ful, capabilities.RawFeatureBits);
        Assert.True(capabilities.SupportsSwitchInput);
        bridge.Initialise(["program.rom"]);
        Assert.Equal(new AmberAudioFormat(48000, 2, 1, 1), bridge.GetAudioFormat());
        Span<short> samples = stackalloc short[8];
        Assert.Equal(4u, bridge.FillAudioFrames(samples, 4));
        Assert.Equal(new short[] { 0, 1, 2, 3, 4, 5, 6, 7 }, samples.ToArray());
        bridge.SetSwitchState(255, true);
        Assert.Throws<ArgumentOutOfRangeException>(() => bridge.SetSwitchState(256, true));
        Assert.Throws<ArgumentOutOfRangeException>(() => bridge.SetPercentageSwitch(16));
    }

    [Fact]
    public void DisposeAfterInitialiseAutomaticallyOrdersShutdownDestroyUnload()
    {
        var native = new FakeBridge();
        var bridge = new AmberBridgeLibrary(native);
        bridge.Initialise(["program.rom"]);

        bridge.Dispose();
        bridge.Dispose();

        Assert.Equal(new[] { "Shutdown", "Destroy", "Unload" }, native.CleanupCalls);
        native.Dispose();
    }

    [Fact]
    public void ExplicitShutdownIsNotRepeatedByDispose()
    {
        var native = new FakeBridge();
        var bridge = new AmberBridgeLibrary(native);
        bridge.Initialise(["program.rom"]);

        bridge.Shutdown();
        bridge.Dispose();

        Assert.Equal(new[] { "Shutdown", "Destroy", "Unload" }, native.CleanupCalls);
        native.Dispose();
    }

    [Fact]
    public void ShutdownFailureStillDestroysAndUnloadsWithoutRetryingShutdown()
    {
        var native = new FakeBridge { ShutdownResult = AmberResult.InternalError };
        var bridge = new AmberBridgeLibrary(native);
        bridge.Initialise(["program.rom"]);

        var error = Assert.Throws<AmberBridgeException>(() => bridge.Shutdown());
        bridge.Dispose();
        bridge.Dispose();

        Assert.Equal("Shutdown", error.Operation);
        Assert.Equal(new[] { "Shutdown", "Destroy", "Unload" }, native.CleanupCalls);
        native.Dispose();
    }

    [Fact]
    public void AutomaticShutdownFailureStillDestroysAndUnloads()
    {
        var native = new FakeBridge { ShutdownResult = AmberResult.InternalError };
        var bridge = new AmberBridgeLibrary(native);
        bridge.Initialise(["program.rom"]);

        var exception = Record.Exception(bridge.Dispose);

        Assert.Null(exception);
        Assert.Equal(new[] { "Shutdown", "Destroy", "Unload" }, native.CleanupCalls);
        native.Dispose();
    }

    [Fact]
    public void DestroyFailureStillUnloadsAndIsNotRetried()
    {
        var native = new FakeBridge { DestroyResult = AmberResult.InternalError };
        var bridge = new AmberBridgeLibrary(native);

        var exception = Record.Exception(bridge.Dispose);
        bridge.Dispose();

        Assert.Null(exception);
        Assert.Equal(new[] { "Destroy", "Unload" }, native.CleanupCalls);
        native.Dispose();
    }

    [Fact]
    public void DisposeDuringExceptionUnwindingDoesNotReplaceActiveException()
    {
        var native = new FakeBridge
        {
            ShutdownResult = AmberResult.InternalError,
            DestroyResult = AmberResult.InternalError
        };

        var error = Assert.Throws<InvalidOperationException>(() => ThrowWithUsingBridge(native));

        Assert.Equal("original", error.Message);
        Assert.Equal(new[] { "Shutdown", "Destroy", "Unload" }, native.CleanupCalls);
        native.Dispose();
    }

    [Fact]
    public void RunAndResetAreRejectedOutsideRunningState()
    {
        var native = new FakeBridge();
        var bridge = new AmberBridgeLibrary(native);

        Assert.Throws<InvalidOperationException>(() => bridge.Run(1));
        Assert.Throws<InvalidOperationException>(bridge.Reset);

        bridge.Initialise(["program.rom"]);
        bridge.Shutdown();

        Assert.Throws<InvalidOperationException>(() => bridge.Run(1));
        Assert.Throws<InvalidOperationException>(bridge.Reset);

        bridge.Dispose();

        Assert.Throws<ObjectDisposedException>(() => bridge.Run(1));
        Assert.Throws<ObjectDisposedException>(bridge.Reset);
        native.Dispose();
    }

    [Fact]
    public void SecondInitialiseIsRejectedWithoutSecondNativeCall()
    {
        using var native = new FakeBridge();
        using var bridge = new AmberBridgeLibrary(native);
        bridge.Initialise(["program.rom"]);

        Assert.Throws<InvalidOperationException>(() => bridge.Initialise(["other.rom"]));

        Assert.Equal(1, native.Calls.Count(call => call == "Initialise"));
    }

    [Fact]
    public async Task ConcurrentInstanceOperationsAreSerialized()
    {
        using var native = new FakeBridge { DelayRun = true };
        using var bridge = new AmberBridgeLibrary(native);
        bridge.Initialise(["program.rom"]);

        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() => bridge.Run(1))));

        Assert.Equal(1, native.MaximumConcurrentRuns);
    }

    private static void AssertAllocationFailureIsLeakFree(
        IReadOnlyList<string> programs,
        IReadOnlyList<string> sounds,
        int throwOnPathAllocation,
        int expectedTotalFrees)
    {
        using var native = new FakeBridge();
        var allocator = new RecordingAllocator();
        using var bridge = new AmberBridgeLibrary(native, allocator);
        allocator.ThrowOnAllocation = allocator.AllocationCalls + throwOnPathAllocation;

        Assert.Throws<AllocationTestException>(() => bridge.Initialise(programs, sounds));

        Assert.Equal(expectedTotalFrees, allocator.FreeCount);
        Assert.Equal(allocator.AllocationCount, allocator.FreeCount);
        Assert.Empty(allocator.OutstandingPointers);
        Assert.False(allocator.DoubleFreeDetected);
        Assert.DoesNotContain("Initialise", native.Calls);
    }

    private static void ThrowWithUsingBridge(FakeBridge native)
    {
        using var bridge = new AmberBridgeLibrary(native);
        bridge.Initialise(["program.rom"]);
        throw new InvalidOperationException("original");
    }
}

internal sealed class FakeBridge : IAmberBridgeModule
{
    private readonly List<Delegate> _delegates = [];
    private readonly List<IntPtr> _strings = [];
    private int _activeRuns;
    private int _maximumConcurrentRuns;

    internal FakeBridge() => GetApi = Root<AmberGetApiDelegate>(GetApiImpl);

    internal AmberGetApiDelegate GetApi { get; }
    internal AmberResult GetApiResult { get; set; } = AmberResult.Ok;
    internal uint ReturnedVersion { get; set; } = AmberApiVersions.V2;
    internal bool OmitRun { get; set; }
    internal AmberResult GetBridgeInfoResult { get; set; } = AmberResult.Ok;
    internal uint BridgeInfoSize { get; set; } = (uint)Marshal.SizeOf<AmberBridgeInfoNative>();
    internal uint BridgeInfoApiVersion { get; set; } = AmberApiVersions.V1;
    internal AmberResult EnumerateResult { get; set; } = AmberResult.Ok;
    internal uint CoreInfoSize { get; set; } = (uint)Marshal.SizeOf<AmberCoreInfoNative>();
    internal string? CoreId { get; set; } = AmberBridgeLibrary.System6CoreId;
    internal AmberResult CreateResult { get; set; } = AmberResult.Ok;
    internal bool ReturnHandleOnCreateFailure { get; set; }
    internal AmberResult InitialiseResult { get; set; } = AmberResult.Ok;
    internal AmberResult ShutdownResult { get; set; } = AmberResult.Ok;
    internal AmberResult DestroyResult { get; set; } = AmberResult.Ok;
    internal string ErrorText { get; set; } = "native detail";
    internal int CyclesRun { get; set; } = 123;
    internal bool DelayRun { get; set; }
    internal uint RequestedVersion { get; private set; }
    internal uint RequestedSize { get; private set; }
    internal int GetApiCallCount { get; private set; }
    internal bool Disposed { get; private set; }
    internal List<string> Calls { get; } = [];
    internal string?[] ProgramPaths { get; private set; } = new string?[4];
    internal string?[] SoundPaths { get; private set; } = new string?[4];
    internal int MaximumConcurrentRuns => _maximumConcurrentRuns;
    internal string[] CleanupCalls => Calls.Where(call => call is "Shutdown" or "Destroy" or "Unload").ToArray();

    public AmberGetApiDelegate BindAmberGetApi() => GetApi;

    private AmberResult GetApiImpl(uint version, uint size, ref AmberApiV2Native api)
    {
        GetApiCallCount++;
        RequestedVersion = version;
        RequestedSize = size;
        if (GetApiResult != AmberResult.Ok) return GetApiResult;
        if (version != AmberApiVersions.V2 || size != 144) return AmberResult.UnsupportedVersion;

        api.StructSize = (uint)Marshal.SizeOf<AmberApiV2Native>();
        api.ApiVersion = ReturnedVersion;
        api.GetBridgeInfo = Pointer<GetBridgeInfoDelegate>(GetBridgeInfo);
        api.EnumerateCore = Pointer<EnumerateCoreDelegate>(EnumerateCore);
        api.Create = Pointer<CreateDelegate>(Create);
        api.Destroy = Pointer<HandleDelegate>(Destroy);
        api.Initialise = Pointer<InitialiseDelegate>(Initialise);
        api.Reset = Pointer<HandleDelegate>(Reset);
        api.Run = OmitRun ? IntPtr.Zero : Pointer<RunDelegate>(Run);
        api.Shutdown = Pointer<HandleDelegate>(Shutdown);
        api.GetLastError = Pointer<GetLastErrorDelegate>(GetLastError);
        api.GetCapabilities = Pointer<GetCapabilitiesDelegate>(GetCapabilities);
        api.SetSwitchState = Pointer<SetSwitchStateDelegate>(SetSwitchState);
        api.GetOutputSnapshot = Pointer<GetOutputSnapshotDelegate>(GetOutputSnapshot);
        api.GetAudioFormat = Pointer<GetAudioFormatDelegate>(GetAudioFormat);
        api.FillAudioFrames = Pointer<FillAudioFramesDelegate>(FillAudioFrames);
        api.ConfigureReels = Pointer<ConfigureReelsDelegate>(ConfigureReels);
        api.ConfigureCoins = Pointer<ConfigureCoinsDelegate>(ConfigureCoins);
        api.SetPercentageSwitch = Pointer<SetPercentageSwitchDelegate>(SetPercentageSwitch);
        return AmberResult.Ok;
    }

    private AmberResult GetBridgeInfo(ref AmberBridgeInfoNative info)
    {
        if (GetBridgeInfoResult != AmberResult.Ok) return GetBridgeInfoResult;
        info.StructSize = BridgeInfoSize;
        info.ApiVersion = BridgeInfoApiVersion;
        info.Name = AllocateString("Amber");
        info.BridgeVersion = AllocateString("0.1.1");
        return AmberResult.Ok;
    }

    private AmberResult EnumerateCore(uint index, ref AmberCoreInfoNative info)
    {
        if (EnumerateResult != AmberResult.Ok) return EnumerateResult;
        if (index > 0) return AmberResult.NoMoreItems;

        info.StructSize = CoreInfoSize;
        info.CoreId = CoreId is null ? IntPtr.Zero : AllocateString(CoreId);
        info.DisplayName = AllocateString("System 6");
        return AmberResult.Ok;
    }

    private AmberResult Create(IntPtr coreId, out IntPtr handle)
    {
        Calls.Add("Create:" + Marshal.PtrToStringUTF8(coreId));
        handle = CreateResult == AmberResult.Ok || ReturnHandleOnCreateFailure ? (IntPtr)42 : IntPtr.Zero;
        return CreateResult;
    }

    private AmberResult Destroy(IntPtr handle)
    {
        Calls.Add("Destroy");
        return DestroyResult;
    }

    private AmberResult Initialise(IntPtr handle, ref AmberInitialiseParamsNative parameters)
    {
        ProgramPaths = ReadSlots(parameters.Program0, parameters.Program1, parameters.Program2, parameters.Program3);
        SoundPaths = ReadSlots(parameters.Sound0, parameters.Sound1, parameters.Sound2, parameters.Sound3);
        Calls.Add("Initialise");
        return InitialiseResult;
    }

    private AmberResult Reset(IntPtr handle)
    {
        Calls.Add("Reset");
        return AmberResult.Ok;
    }

    private AmberResult GetCapabilities(IntPtr handle, ref AmberCapabilitiesV1Native capabilities) { capabilities.FeatureBits = 0x3f; capabilities.MaximumSwitches = 256; return AmberResult.Ok; }
    private AmberResult SetSwitchState(IntPtr handle, uint index, uint isOn) => AmberResult.Ok;
    private unsafe AmberResult GetOutputSnapshot(IntPtr handle, AmberOutputSnapshotV1Native* snapshot) { snapshot->StructSize = (uint)sizeof(AmberOutputSnapshotV1Native); snapshot->Version = 1; return AmberResult.Ok; }
    private AmberResult GetAudioFormat(IntPtr handle, ref AmberAudioFormatV1Native format) { format.SampleRate = 48000; format.Channels = 2; format.SampleFormat = 1; format.Interleaving = 1; return AmberResult.Ok; }
    private unsafe AmberResult FillAudioFrames(IntPtr handle, short* samples, uint capacity, out uint written) { written = capacity; if (samples != null) for (var i = 0; i < checked((int)capacity * 2); i++) samples[i] = (short)i; return AmberResult.Ok; }
    private unsafe AmberResult ConfigureReels(IntPtr handle, AmberReelConfigurationV1Native* configuration) => AmberResult.Ok;
    private unsafe AmberResult ConfigureCoins(IntPtr handle, AmberCoinConfigurationV1Native* configuration) => AmberResult.Ok;
    private AmberResult SetPercentageSwitch(IntPtr handle, uint rawValue) => AmberResult.Ok;

    private AmberResult Run(IntPtr handle, uint cycles, out int cyclesRun)
    {
        var concurrentRuns = Interlocked.Increment(ref _activeRuns);
        _maximumConcurrentRuns = Math.Max(_maximumConcurrentRuns, concurrentRuns);
        if (DelayRun) Thread.Sleep(15);
        Calls.Add("Run:" + cycles);
        cyclesRun = CyclesRun;
        Interlocked.Decrement(ref _activeRuns);
        return AmberResult.Ok;
    }

    private AmberResult Shutdown(IntPtr handle)
    {
        Calls.Add("Shutdown");
        return ShutdownResult;
    }

    private AmberResult GetLastError(IntPtr handle, IntPtr buffer, uint capacity, out uint required)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(ErrorText + '\0');
        required = (uint)bytes.Length;
        if (buffer == IntPtr.Zero || capacity < required) return AmberResult.BufferTooSmall;
        Marshal.Copy(bytes, 0, buffer, bytes.Length);
        return AmberResult.Ok;
    }

    private T Root<T>(T value) where T : Delegate
    {
        _delegates.Add(value);
        return value;
    }

    private IntPtr Pointer<T>(T value) where T : Delegate => Marshal.GetFunctionPointerForDelegate(Root(value));

    private IntPtr AllocateString(string value)
    {
        var pointer = Marshal.StringToCoTaskMemUTF8(value);
        _strings.Add(pointer);
        return pointer;
    }

    private static string?[] ReadSlots(params IntPtr[] pointers) =>
        pointers.Select(pointer => pointer == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(pointer)).ToArray();

    public void Dispose()
    {
        if (Disposed) return;
        Calls.Add("Unload");
        foreach (var pointer in _strings) Marshal.FreeCoTaskMem(pointer);
        _strings.Clear();
        Disposed = true;
    }
}

internal sealed class RecordingAllocator : IAmberStringAllocator
{
    private int _callCount;

    internal int? ThrowOnAllocation { get; set; }
    internal HashSet<IntPtr> OutstandingPointers { get; } = [];
    internal int AllocationCalls => _callCount;
    internal int AllocationCount { get; private set; }
    internal int FreeCount { get; private set; }
    internal bool DoubleFreeDetected { get; private set; }

    public IntPtr Allocate(string value)
    {
        _callCount++;
        if (_callCount == ThrowOnAllocation) throw new AllocationTestException();
        var pointer = Marshal.StringToCoTaskMemUTF8(value);
        AllocationCount++;
        OutstandingPointers.Add(pointer);
        return pointer;
    }

    public void Free(IntPtr pointer)
    {
        if (!OutstandingPointers.Remove(pointer))
        {
            DoubleFreeDetected = true;
            return;
        }
        FreeCount++;
        Marshal.FreeCoTaskMem(pointer);
    }
}

internal sealed class AllocationTestException : Exception
{
}
