using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace OasisEditor.Tests;

public sealed class System6NativeBackendTests
{
    [Fact]
    public async Task StartAsyncInitialisesLoadsRomsResetsAndStopShutsDown()
    {
        var library = new FakeSystem6NativeLibrary();
        var (dllPath, rom1, rom2) = CreateNativeFiles(2);
        var backend = new System6NativeBackend(dllPath, _ => library);
        var request = CreateLaunchRequest(rom1, rom2);

        await backend.StartAsync(request, CancellationToken.None);
        await backend.StopAsync(CancellationToken.None);

        Assert.True(library.InitialiseCalled);
        Assert.Equal(new[] { rom1, rom2, string.Empty, string.Empty }, library.LoadedRoms);
        Assert.True(library.ResetCalled);
        Assert.Equal(8 * 4 + 1, library.Calls.Count(call => call.StartsWith("Set", StringComparison.Ordinal)));
        Assert.Contains("SetPercent:0", library.Calls);
        Assert.True(library.Calls.IndexOf("Reset") < library.Calls.IndexOf("SetSteps:0:96"));
        Assert.True(library.ShutdownCalled);
        Assert.True(library.DisposeCalled);
        Assert.Equal(EmulationBackendState.Stopped, backend.State);
    }


    [Fact]
    public async Task StartAsyncWithOutputPollingDisabledRunsCoreWithoutPollingOutputs()
    {
        var previousPolling = Environment.GetEnvironmentVariable("OASIS_SYSTEM6_OUTPUT_POLLING");
        var previousPump = Environment.GetEnvironmentVariable("OASIS_SYSTEM6_EMULATION_PUMP_HZ");
        Environment.SetEnvironmentVariable("OASIS_SYSTEM6_OUTPUT_POLLING", "0");
        Environment.SetEnvironmentVariable("OASIS_SYSTEM6_EMULATION_PUMP_HZ", null);
        try
        {
            var library = new FakeSystem6NativeLibrary();
            var (dllPath, rom1, rom2) = CreateNativeFiles(2);
            var backend = new System6NativeBackend(dllPath, _ => library);

            await backend.StartAsync(CreateLaunchRequest(rom1, rom2), CancellationToken.None);
            var observedRun = SpinWait.SpinUntil(() => Volatile.Read(ref library.RunCallCount) > 0, TimeSpan.FromSeconds(1));
            await backend.StopAsync(CancellationToken.None);

            Assert.True(observedRun, "Expected the emulation pump to call Run while output polling was disabled.");
            Assert.Contains("Run:8000", library.Calls);
            Assert.DoesNotContain("LampsUpdate", library.Calls);
            Assert.DoesNotContain(library.Calls, call => call.StartsWith("GetLampsOn:", StringComparison.Ordinal));
            Assert.DoesNotContain(library.Calls, call => call.StartsWith("GetPosOut:", StringComparison.Ordinal));
        }
        finally
        {
            Environment.SetEnvironmentVariable("OASIS_SYSTEM6_OUTPUT_POLLING", previousPolling);
            Environment.SetEnvironmentVariable("OASIS_SYSTEM6_EMULATION_PUMP_HZ", previousPump);
        }
    }

    [Fact]
    public async Task StartAsyncSendsZeroBasedNativeReelOptoIndicesForDisplayedReelsOneAndEight()
    {
        var library = new FakeSystem6NativeLibrary();
        var (dllPath, rom1, rom2) = CreateNativeFiles(2);
        var backend = new System6NativeBackend(dllPath, _ => library);

        await backend.StartAsync(CreateLaunchRequest(rom1, rom2), CancellationToken.None);
        await backend.StopAsync(CancellationToken.None);

        Assert.Contains("SetSteps:0:96", library.Calls);
        Assert.Contains("SetOptoStart:0:5", library.Calls);
        Assert.Contains("SetOptoEnd:0:7", library.Calls);
        Assert.Contains("SetOptoInvert:0:0", library.Calls);
        Assert.Contains("SetSteps:7:96", library.Calls);
        Assert.Contains("SetOptoStart:7:5", library.Calls);
        Assert.Contains("SetOptoEnd:7:7", library.Calls);
        Assert.Contains("SetOptoInvert:7:0", library.Calls);
        Assert.DoesNotContain(library.Calls, call => call.StartsWith("SetSteps:8:", StringComparison.Ordinal));
        Assert.DoesNotContain(library.Calls, call => call.StartsWith("SetOptoStart:8:", StringComparison.Ordinal));
        Assert.DoesNotContain(library.Calls, call => call.StartsWith("SetOptoEnd:8:", StringComparison.Ordinal));
        Assert.DoesNotContain(library.Calls, call => call.StartsWith("SetOptoInvert:8:", StringComparison.Ordinal));
    }

    [Fact]
    public void System6ReelOptoViewModelDisplaysOneBasedReelNumbersFromZeroBasedStoredIndices()
    {
        var firstReel = new System6ReelOptoSettingsViewModel(System6ReelOptoSettings.CreateDefault(0), () => { });
        var eighthReel = new System6ReelOptoSettingsViewModel(System6ReelOptoSettings.CreateDefault(7), () => { });

        Assert.Equal(0, firstReel.ReelIndex);
        Assert.Equal(1, firstReel.ReelNumber);
        Assert.Equal(7, eighthReel.ReelIndex);
        Assert.Equal(8, eighthReel.ReelNumber);
        Assert.Equal(0, firstReel.ToModel().ReelIndex);
        Assert.Equal(7, eighthReel.ToModel().ReelIndex);
    }


    [Fact]
    public async Task StartAsyncAppliesOnlyEnabledNativeCoinRows()
    {
        var library = new FakeSystem6NativeLibrary();
        var (dllPath, rom1, rom2) = CreateNativeFiles(2);
        var backend = new System6NativeBackend(dllPath, _ => library);
        var request = CreateLaunchRequest(rom1, rom2);
        request.System6NativeRoms!.Coins[0].Enabled = true;
        request.System6NativeRoms.Coins[0].Name = "10p";
        request.System6NativeRoms.Coins[0].Num = 2;
        request.System6NativeRoms.Coins[0].Coin = 3;
        request.System6NativeRoms.Coins[0].CoinValue = 10;
        request.System6NativeRoms.Coins[0].CoinEnable = 1;
        request.System6NativeRoms.Coins[0].LockoutValue = 4;
        request.System6NativeRoms.Coins[0].LockoutInvert = 5;
        request.System6NativeRoms.Coins[0].CounterIn = 6;
        request.System6NativeRoms.Coins[0].CounterOut = 7;
        request.System6NativeRoms.Coins[0].PortIndex = 8;
        request.System6NativeRoms.Coins[0].Level = 9;
        request.System6NativeRoms.Coins[0].FullLevel = 10;
        request.System6NativeRoms.Coins[1].Enabled = false;
        request.System6NativeRoms.Coins[1].Num = 9;

        await backend.StartAsync(request, CancellationToken.None);
        await backend.StopAsync(CancellationToken.None);

        Assert.Contains("SetCoinEnable:2:3:1", library.Calls);
        Assert.Contains("SetCoinValue:2:3:10", library.Calls);
        Assert.Contains("SetLockoutVal:2:3:4", library.Calls);
        Assert.Contains("SetLockoutInvert:2:3:5", library.Calls);
        Assert.Contains("SetEnable:2:1", library.Calls);
        Assert.Contains("SetCounterIn:2:6", library.Calls);
        Assert.Contains("SetCounterOut:2:7", library.Calls);
        Assert.Contains("SetPortIndex:2:8", library.Calls);
        Assert.Contains("SetCoin:2:3", library.Calls);
        Assert.Contains("SetLevel:2:9", library.Calls);
        Assert.Contains("SetFullLevel:2:10", library.Calls);
        Assert.DoesNotContain(library.Calls, call => call.StartsWith("SetCoinEnable:9:", StringComparison.Ordinal));
        Assert.DoesNotContain(library.Calls, call => call.StartsWith("SetEnable:9:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SetInputStateAsyncMapsButtonNumberToNativeSwitchCalls()
    {
        var library = new FakeSystem6NativeLibrary();
        var (dllPath, rom1, rom2) = CreateNativeFiles(2);
        var backend = new System6NativeBackend(dllPath, _ => library);

        await backend.StartAsync(CreateLaunchRequest(rom1, rom2), CancellationToken.None);
        var input = new InputDefinitionModel { Id = "btn-1", Kind = InputDefinitionKind.Button, ButtonNumber = "12" };

        await backend.SetInputStateAsync(input, true, CancellationToken.None);
        await backend.SetInputStateAsync(input, false, CancellationToken.None);
        await backend.StopAsync(CancellationToken.None);

        Assert.Equal(new[] { 12 }, library.SwitchesTurnedOn);
        Assert.Equal(new[] { 12 }, library.SwitchesTurnedOff);
    }


    [Fact]
    public async Task SetInputStateAsyncIgnoresCoinInputs()
    {
        var library = new FakeSystem6NativeLibrary();
        var (dllPath, rom1, rom2) = CreateNativeFiles(2);
        var backend = new System6NativeBackend(dllPath, _ => library);
        var input = new InputDefinitionModel { Id = "coin-1", Kind = InputDefinitionKind.Coin, CoinInput = true, ButtonNumber = "1" };

        await backend.StartAsync(CreateLaunchRequest(rom1, rom2), CancellationToken.None);
        await backend.SetInputStateAsync(input, true, CancellationToken.None);
        await backend.StopAsync(CancellationToken.None);

        Assert.Empty(library.SwitchesTurnedOn);
        Assert.Empty(library.SwitchesTurnedOff);
    }

    [Fact]
    public async Task StartAsyncWithoutRomPathsFailsBeforeLoadingRoms()
    {
        var library = new FakeSystem6NativeLibrary();
        var (dllPath, _, _) = CreateNativeFiles(0);
        var backend = new System6NativeBackend(dllPath, _ => library);
        var request = CreateLaunchRequest();

        await Assert.ThrowsAsync<InvalidOperationException>(() => backend.StartAsync(request, CancellationToken.None));

        Assert.True(library.InitialiseCalled);
        Assert.Empty(library.LoadedRoms);
        Assert.False(library.ResetCalled);
        Assert.Equal(EmulationBackendState.Failed, backend.State);
    }

    [Fact]
    public async Task StartAsyncInvalidReelOptosFailsBeforeResetAndRun()
    {
        var library = new FakeSystem6NativeLibrary();
        var (dllPath, rom1, rom2) = CreateNativeFiles(2);
        var backend = new System6NativeBackend(dllPath, _ => library);
        var request = CreateLaunchRequest(rom1, rom2);
        request.System6NativeRoms!.ReelOptos[0].Steps = 0;

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => backend.StartAsync(request, CancellationToken.None));

        Assert.Contains("steps must be between 1 and 255", ex.Message);
        Assert.False(library.ResetCalled);
        Assert.Empty(library.Calls.Where(call => call.StartsWith("Run", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task StartAsyncSkipsDisabledReelOptos()
    {
        var library = new FakeSystem6NativeLibrary();
        var (dllPath, rom1, rom2) = CreateNativeFiles(2);
        var backend = new System6NativeBackend(dllPath, _ => library);
        var request = CreateLaunchRequest(rom1, rom2);
        request.System6NativeRoms!.ReelOptos[1].Enabled = false;
        request.System6NativeRoms.ReelOptos[1].Steps = 0;

        await backend.StartAsync(request, CancellationToken.None);
        await backend.StopAsync(CancellationToken.None);

        Assert.DoesNotContain("SetSteps:1:0", library.Calls);
        Assert.DoesNotContain(library.Calls, call => call.StartsWith("SetOptoStart:1:", StringComparison.Ordinal));
        Assert.Equal(7 * 4 + 1, library.Calls.Count(call => call.StartsWith("Set", StringComparison.Ordinal)));
        Assert.True(library.ResetCalled);
    }

    [Fact]
    public async Task StartAsyncOneRunOnlyAppliesReelOptosAfterResetAndBeforeFirstRun()
    {
        var previousStage = Environment.GetEnvironmentVariable("OASIS_SYSTEM6_STARTUP_STAGE");
        Environment.SetEnvironmentVariable("OASIS_SYSTEM6_STARTUP_STAGE", "OneRunOnly");
        try
        {
            var library = new FakeSystem6NativeLibrary();
            var (dllPath, rom1, rom2) = CreateNativeFiles(2);
            var backend = new System6NativeBackend(dllPath, _ => library);

            await backend.StartAsync(CreateLaunchRequest(rom1, rom2), CancellationToken.None);
            await backend.StopAsync(CancellationToken.None);

            AssertOrdered(
                library.Calls,
                "Initialise",
                "LoadRom",
                "Reset",
                "SetSteps:0:96",
                "SetOptoStart:0:5",
                "SetOptoEnd:0:7",
                "SetOptoInvert:0:0",
                "SetPercent:0",
                "Run:8000");
        }
        finally
        {
            Environment.SetEnvironmentVariable("OASIS_SYSTEM6_STARTUP_STAGE", previousStage);
        }
    }

    [Fact]
    public async Task StartAsyncOneRunOnlyUpdatesLampsBeforePollingAndUsesOneBasedReelOutputEvents()
    {
        var previousStage = Environment.GetEnvironmentVariable("OASIS_SYSTEM6_STARTUP_STAGE");
        Environment.SetEnvironmentVariable("OASIS_SYSTEM6_STARTUP_STAGE", "OneRunOnly");
        try
        {
            var library = new FakeSystem6NativeLibrary();
            library.GetLampsOnValues[0] = true;
            library.PositionOutputs[0] = 10;
            library.PositionOutputs[1] = 20;
            var (dllPath, rom1, rom2) = CreateNativeFiles(2);
            var backend = new System6NativeBackend(dllPath, _ => library);
            var lampEvents = new List<MachineLampChangedEventArgs>();
            var reelEvents = new List<MachineReelChangedEventArgs>();
            backend.LampChanged += (_, e) => lampEvents.Add(e);
            backend.ReelChanged += (_, e) => reelEvents.Add(e);

            await backend.StartAsync(CreateLaunchRequest(rom1, rom2), CancellationToken.None);
            await backend.StopAsync(CancellationToken.None);

            Assert.True(library.Calls.IndexOf("LampsUpdate") < library.Calls.IndexOf("GetLampsOn:0"));
            Assert.Contains(lampEvents, e => e.LampId == 0 && e.Value == 255);
            Assert.Contains(reelEvents, e => e.ReelId == 1 && e.Position == 86);
            Assert.Contains(reelEvents, e => e.ReelId == 2 && e.Position == 76);
            Assert.Contains("GetPosOut:0", library.Calls);
            Assert.Contains("GetPosOut:1", library.Calls);
        }
        finally
        {
            Environment.SetEnvironmentVariable("OASIS_SYSTEM6_STARTUP_STAGE", previousStage);
        }
    }

    [Fact]
    public async Task StartAsyncOneRunOnlyPollsOnlyEnabledReelOptos()
    {
        var previousStage = Environment.GetEnvironmentVariable("OASIS_SYSTEM6_STARTUP_STAGE");
        Environment.SetEnvironmentVariable("OASIS_SYSTEM6_STARTUP_STAGE", "OneRunOnly");
        try
        {
            var library = new FakeSystem6NativeLibrary();
            var (dllPath, rom1, rom2) = CreateNativeFiles(2);
            var backend = new System6NativeBackend(dllPath, _ => library);
            var request = CreateLaunchRequest(rom1, rom2);
            foreach (var reel in request.System6NativeRoms!.ReelOptos)
            {
                reel.Enabled = reel.ReelIndex is 0 or 2 or 4;
            }

            await backend.StartAsync(request, CancellationToken.None);
            await backend.StopAsync(CancellationToken.None);

            Assert.Contains("SetSteps:0:96", library.Calls);
            Assert.Contains("SetSteps:2:96", library.Calls);
            Assert.Contains("SetSteps:4:96", library.Calls);
            Assert.DoesNotContain(library.Calls, call => call.StartsWith("SetSteps:1:", StringComparison.Ordinal));
            Assert.DoesNotContain(library.Calls, call => call.StartsWith("SetOptoStart:3:", StringComparison.Ordinal));
            Assert.DoesNotContain(library.Calls, call => call.StartsWith("SetOptoEnd:5:", StringComparison.Ordinal));
            Assert.DoesNotContain(library.Calls, call => call.StartsWith("SetOptoInvert:7:", StringComparison.Ordinal));

            var getPosOutCalls = library.Calls.Where(call => call.StartsWith("GetPosOut:", StringComparison.Ordinal)).ToArray();
            Assert.Equal(new[] { "GetPosOut:0", "GetPosOut:2", "GetPosOut:4" }, getPosOutCalls);
            Assert.DoesNotContain("GetPosOut:1", library.Calls);
            Assert.DoesNotContain("GetPosOut:3", library.Calls);
            Assert.DoesNotContain("GetPosOut:5", library.Calls);
        }
        finally
        {
            Environment.SetEnvironmentVariable("OASIS_SYSTEM6_STARTUP_STAGE", previousStage);
        }
    }

    [Fact]
    public async Task StartAsyncOneRunOnlyPollsOnlyConfiguredLampIdsIncludingHighLamp()
    {
        var previousStage = Environment.GetEnvironmentVariable("OASIS_SYSTEM6_STARTUP_STAGE");
        Environment.SetEnvironmentVariable("OASIS_SYSTEM6_STARTUP_STAGE", "OneRunOnly");
        try
        {
            var library = new FakeSystem6NativeLibrary();
            library.GetLampsOnValues[5] = true;
            library.GetLampsOnValues[255] = true;
            var (dllPath, rom1, rom2) = CreateNativeFiles(2);
            var backend = new System6NativeBackend(dllPath, _ => library);
            var lampEvents = new List<MachineLampChangedEventArgs>();
            backend.LampChanged += (_, e) => lampEvents.Add(e);

            await backend.StartAsync(CreateLaunchRequest([5, 255], rom1, rom2), CancellationToken.None);
            await backend.StopAsync(CancellationToken.None);

            var getLampCalls = library.Calls.Where(call => call.StartsWith("GetLampsOn:", StringComparison.Ordinal)).ToArray();
            Assert.Equal(new[] { "GetLampsOn:5", "GetLampsOn:255" }, getLampCalls);
            Assert.Contains(lampEvents, e => e.LampId == 5 && e.Value == 255);
            Assert.Contains(lampEvents, e => e.LampId == 255 && e.Value == 255);
            Assert.DoesNotContain("GetLampsOn:0", library.Calls);
            Assert.DoesNotContain("GetLampsOn:31", library.Calls);
        }
        finally
        {
            Environment.SetEnvironmentVariable("OASIS_SYSTEM6_STARTUP_STAGE", previousStage);
        }
    }


    [Theory]
    [InlineData(0, 96, 0)]
    [InlineData(1, 96, 95)]
    [InlineData(95, 96, 1)]
    [InlineData(10, 0, 10)]
    [InlineData(10, -1, 10)]
    public void NormalizeNativeSystem6ReelPosition_ReversesDirectionWhenStepsAreKnown(int rawPosition, int steps, int expected)
    {
        Assert.Equal(expected, System6NativeBackend.NormalizeNativeSystem6ReelPosition(rawPosition, steps));
    }


    [Fact]
    public void FormatAlphaSegments_FormatsHexAndDecimalValues()
    {
        var raw = System6NativeBackend.FormatAlphaSegments(new[] { 0, 17615, -1 });

        Assert.Equal("[0]=0x0000/0 [1]=0x44CF/17615 [2]=0xFFFF/-1", raw);
    }


    [Fact]
    public void System6AlphaSegmentMapper_MapsKnownRawMaskToOasisMask()
    {
        Assert.Equal(0x2003, System6AlphaSegmentMapper.MapNativeMaskToOasisMask(0x8003));
    }

    [Fact]
    public async Task StartAsyncOneRunOnlyPollsAlphaSegmentsAndPublishesNativeAlphaSegmentMasks()
    {
        var previousStage = Environment.GetEnvironmentVariable("OASIS_SYSTEM6_STARTUP_STAGE");
        Environment.SetEnvironmentVariable("OASIS_SYSTEM6_STARTUP_STAGE", "OneRunOnly");
        try
        {
            var library = new FakeSystem6NativeLibrary { AlphaSegmentPollingAvailable = true };
            library.AlphaSegments[0] = 0x0001;
            library.AlphaSegments[1] = 0x8002;
            var (dllPath, rom1, rom2) = CreateNativeFiles(2);
            var backend = new System6NativeBackend(dllPath, _ => library);
            var segmentEvents = new List<MachineSegmentChangedEventArgs>();
            backend.SegmentChanged += (_, e) => segmentEvents.Add(e);

            await backend.StartAsync(CreateLaunchRequest(rom1, rom2), CancellationToken.None);
            await backend.StopAsync(CancellationToken.None);

            Assert.Equal(Enumerable.Range(0, 16).ToArray(), library.AlphaSegmentIndices);
            Assert.Contains(segmentEvents, e => e.CellId == 0 && e.SegmentMask == 0x0001 && e.OutputType == MameSegmentOutputType.NativeAlpha);
            Assert.Contains(segmentEvents, e => e.CellId == 1 && e.SegmentMask == 0x2002 && e.OutputType == MameSegmentOutputType.NativeAlpha);
        }
        finally
        {
            Environment.SetEnvironmentVariable("OASIS_SYSTEM6_STARTUP_STAGE", previousStage);
        }
    }

    [Fact]
    public async Task StartAsyncOneRunOnlyPollsConfiguredSevenSegmentsAndPublishesDigitMasks()
    {
        var previousStage = Environment.GetEnvironmentVariable("OASIS_SYSTEM6_STARTUP_STAGE");
        Environment.SetEnvironmentVariable("OASIS_SYSTEM6_STARTUP_STAGE", "OneRunOnly");
        try
        {
            var library = new FakeSystem6NativeLibrary { SevenSegmentPollingAvailable = true };
            for (ushort index = 32; index <= 37; index++)
            {
                library.SevenSegmentCells[index] = true;
            }

            library.SevenSegmentCells[80 + 1] = true;
            library.SevenSegmentCells[80 + 2] = true;
            library.SevenSegmentCells[80 + 7] = true;
            library.SevenSegmentBrightness[32] = 7;
            var (dllPath, rom1, rom2) = CreateNativeFiles(2);
            var backend = new System6NativeBackend(dllPath, _ => library);
            var segmentEvents = new List<MachineSegmentChangedEventArgs>();
            backend.SegmentChanged += (_, e) => segmentEvents.Add(e);
            var request = CreateLaunchRequest(rom1, rom2) with { ConfiguredSevenSegmentDisplayIds = [2, 5] };

            await backend.StartAsync(request, CancellationToken.None);
            await backend.StopAsync(CancellationToken.None);

            Assert.Contains("UpdateSegs", library.Calls);
            Assert.Contains("GetSegsOn:32", library.Calls);
            Assert.Contains("GetSegsOn:39", library.Calls);
            Assert.Contains("GetSegsOn:80", library.Calls);
            Assert.Contains("GetSegsOn:87", library.Calls);
            Assert.DoesNotContain("GetSegsBright:32", library.Calls);
            Assert.Contains(segmentEvents, e => e.CellId == 2 && e.SegmentMask == 0x3F && e.OutputType == MameSegmentOutputType.Digit);
            Assert.Contains(segmentEvents, e => e.CellId == 5 && e.SegmentMask == 0x86 && e.OutputType == MameSegmentOutputType.Digit);
        }
        finally
        {
            Environment.SetEnvironmentVariable("OASIS_SYSTEM6_STARTUP_STAGE", previousStage);
        }
    }

    [Fact]
    public async Task StartAsyncOneRunOnlyDoesNotPublishAlphaSegmentsWhenExportMissing()
    {
        var previousStage = Environment.GetEnvironmentVariable("OASIS_SYSTEM6_STARTUP_STAGE");
        Environment.SetEnvironmentVariable("OASIS_SYSTEM6_STARTUP_STAGE", "OneRunOnly");
        try
        {
            var library = new FakeSystem6NativeLibrary { AlphaSegmentPollingAvailable = false };
            var (dllPath, rom1, rom2) = CreateNativeFiles(2);
            var backend = new System6NativeBackend(dllPath, _ => library);
            var segmentEvents = new List<MachineSegmentChangedEventArgs>();
            backend.SegmentChanged += (_, e) => segmentEvents.Add(e);

            await backend.StartAsync(CreateLaunchRequest(rom1, rom2), CancellationToken.None);
            await backend.StopAsync(CancellationToken.None);

            Assert.Empty(library.AlphaSegmentIndices);
            Assert.Empty(segmentEvents);
        }
        finally
        {
            Environment.SetEnvironmentVariable("OASIS_SYSTEM6_STARTUP_STAGE", previousStage);
        }
    }


    [Theory]
    [InlineData(0, 0d)]
    [InlineData(1, 1d / 31d)]
    [InlineData(15, 15d / 31d)]
    [InlineData(31, 1d)]
    [InlineData(32, 1d)]
    public void NormalizeSystem6AlphaBrightness_MapsNativeByteToMameBrightnessRange(int rawBrightness, double expected)
    {
        Assert.Equal(expected, System6NativeBackend.NormalizeSystem6AlphaBrightness((byte)rawBrightness), precision: 6);
    }

    [Fact]
    public async Task StartAsyncOneRunOnlyPollsAlphaBrightnessAndPublishesVfdBrightness()
    {
        var previousStage = Environment.GetEnvironmentVariable("OASIS_SYSTEM6_STARTUP_STAGE");
        Environment.SetEnvironmentVariable("OASIS_SYSTEM6_STARTUP_STAGE", "OneRunOnly");
        try
        {
            var library = new FakeSystem6NativeLibrary
            {
                AlphaSegmentPollingAvailable = true,
                AlphaBrightnessPollingAvailable = true,
                AlphaBrightness = 15
            };
            var (dllPath, rom1, rom2) = CreateNativeFiles(2);
            var backend = new System6NativeBackend(dllPath, _ => library);
            var brightnessEvents = new List<MachineVfdBrightnessChangedEventArgs>();
            backend.VfdBrightnessChanged += (_, e) => brightnessEvents.Add(e);

            await backend.StartAsync(CreateLaunchRequest(rom1, rom2), CancellationToken.None);
            await backend.StopAsync(CancellationToken.None);

            var brightnessEvent = Assert.Single(brightnessEvents);
            Assert.Equal(0, brightnessEvent.CellId);
            Assert.Equal(15d / 31d, brightnessEvent.NormalizedBrightness, precision: 6);
            Assert.Contains("GetAlphaBrightness", library.Calls);
        }
        finally
        {
            Environment.SetEnvironmentVariable("OASIS_SYSTEM6_STARTUP_STAGE", previousStage);
        }
    }

    [Fact]
    public async Task StartAsyncAppliesConfiguredPercentSwitchValue()
    {
        var library = new FakeSystem6NativeLibrary();
        var (dllPath, rom1, rom2) = CreateNativeFiles(2);
        var backend = new System6NativeBackend(dllPath, _ => library);
        var request = CreateLaunchRequest(rom1, rom2);
        request.System6NativeRoms!.PercentSwitchValue = 15;

        await backend.StartAsync(request, CancellationToken.None);
        await backend.StopAsync(CancellationToken.None);

        Assert.Contains("SetPercent:15", library.Calls);
    }

    private static void AssertOrdered(IReadOnlyList<string> calls, params string[] expectedCalls)
    {
        var previousIndex = -1;
        foreach (var expectedCall in expectedCalls)
        {
            var index = FindCallIndex(calls, expectedCall);
            Assert.True(index >= 0, $"Expected call '{expectedCall}' was not found. Calls: {string.Join(", ", calls)}");
            Assert.True(index > previousIndex, $"Expected call '{expectedCall}' after index {previousIndex}. Calls: {string.Join(", ", calls)}");
            previousIndex = index;
        }
    }

    private static int FindCallIndex(IReadOnlyList<string> calls, string expectedCall)
    {
        for (var index = 0; index < calls.Count; index++)
        {
            if (string.Equals(calls[index], expectedCall, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    private static EmulationLaunchRequest CreateLaunchRequest(params string[] romPaths)
    {
        return CreateLaunchRequest(null, romPaths);
    }

    private static EmulationLaunchRequest CreateLaunchRequest(IReadOnlyList<int>? configuredLampIds, params string[] romPaths)
    {
        return new EmulationLaunchRequest(
            FruitMachinePlatformType.None,
            "test-machine",
            "C:/roms",
            [],
            string.Empty,
            new System6NativeRomSettings
            {
                ProgramRom1Path = romPaths.Length > 0 ? romPaths[0] : string.Empty,
                ProgramRom2Path = romPaths.Length > 1 ? romPaths[1] : string.Empty
            },
            configuredLampIds,
            null);
    }

    private static (string DllPath, string Rom1, string Rom2) CreateNativeFiles(int romCount)
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var dllPath = Path.Combine(directory, "system6.dll");
        File.WriteAllBytes(dllPath, []);
        var rom1 = Path.Combine(directory, "a.rom");
        var rom2 = Path.Combine(directory, "b.rom");
        if (romCount >= 1) File.WriteAllBytes(rom1, []);
        if (romCount >= 2) File.WriteAllBytes(rom2, []);
        return (dllPath, rom1, rom2);
    }

    private sealed class FakeSystem6NativeLibrary : ISystem6NativeLibrary
    {
        public string LibraryPath => "C:/cores/system6.dll";

        public Architecture ProcessArchitecture => Architecture.X64;

        public bool IsLoaded => !DisposeCalled;

        public bool InitialiseCalled { get; private set; }

        public bool ResetCalled { get; private set; }

        public bool ShutdownCalled { get; private set; }

        public bool DisposeCalled { get; private set; }

        public List<string> LoadedRoms { get; } = new();

        public List<int> SwitchesTurnedOn { get; } = new();

        public List<int> SwitchesTurnedOff { get; } = new();

        public List<string> Calls { get; } = new();

        public int RunCallCount;

        public Dictionary<ushort, bool> GetLampsOnValues { get; } = new();

        public Dictionary<sbyte, short> PositionOutputs { get; } = new();

        public Dictionary<byte, int> AlphaSegments { get; } = new();

        public List<int> AlphaSegmentIndices { get; } = new();

        public bool AlphaSegmentPollingAvailable { get; set; }

        public bool AlphaBrightnessPollingAvailable { get; set; }

        public byte AlphaBrightness { get; set; } = 255;

        public bool SevenSegmentPollingAvailable { get; set; }

        public Dictionary<ushort, bool> SevenSegmentCells { get; } = new();

        public Dictionary<ushort, byte> SevenSegmentBrightness { get; } = new();

        public byte Initialise()
        {
            Calls.Add("Initialise");
            InitialiseCalled = true;
            return 1;
        }

        public int LoadRom(IReadOnlyList<string> programRomPaths, bool flashSwitch)
        {
            Calls.Add("LoadRom");
            LoadedRoms.AddRange(programRomPaths);
            return 1;
        }

        public int LoadSoundRom(IReadOnlyList<string> soundRomPaths)
        {
            return 1;
        }

        public void SetSteps(byte reelNum, byte steps) => Calls.Add($"SetSteps:{reelNum}:{steps}");

        public void SetOptoStart(byte reelNum, byte start) => Calls.Add($"SetOptoStart:{reelNum}:{start}");

        public void SetOptoEnd(byte reelNum, byte end) => Calls.Add($"SetOptoEnd:{reelNum}:{end}");

        public void SetOptoInvert(byte reelNum, byte state) => Calls.Add($"SetOptoInvert:{reelNum}:{state}");

        public bool IsSetPercentAvailable => true;

        public bool IsSevenSegmentPollingAvailable => SevenSegmentPollingAvailable;

        public void UpdateSegs() => Calls.Add("UpdateSegs");

        public int GetSegsOn(ushort index)
        {
            Calls.Add($"GetSegsOn:{index}");
            return SevenSegmentCells.TryGetValue(index, out var isOn) && isOn ? 1 : 0;
        }

        public byte GetSegsBright(ushort index)
        {
            Calls.Add($"GetSegsBright:{index}");
            return SevenSegmentBrightness.TryGetValue(index, out var brightness) ? brightness : (byte)0;
        }

        public void SetPercent(byte percent) => Calls.Add($"SetPercent:{percent}");

        public void SetCoinEnable(byte num, byte coin, byte coinEnable) => Calls.Add($"SetCoinEnable:{num}:{coin}:{coinEnable}");

        public void SetCoinValue(byte num, byte coin, byte coinValue) => Calls.Add($"SetCoinValue:{num}:{coin}:{coinValue}");

        public void SetLockoutVal(byte num, byte coin, byte lockoutValue) => Calls.Add($"SetLockoutVal:{num}:{coin}:{lockoutValue}");

        public void SetLockoutInvert(byte num, byte coin, byte lockoutInvert) => Calls.Add($"SetLockoutInvert:{num}:{coin}:{lockoutInvert}");

        public void SetEnable(byte num, byte enable) => Calls.Add($"SetEnable:{num}:{enable}");

        public void SetCounterIn(byte num, byte counterIn) => Calls.Add($"SetCounterIn:{num}:{counterIn}");

        public void SetCounterOut(byte num, byte counterOut) => Calls.Add($"SetCounterOut:{num}:{counterOut}");

        public void SetPortIndex(byte num, byte portIndex) => Calls.Add($"SetPortIndex:{num}:{portIndex}");

        public void SetCoin(byte num, byte coin) => Calls.Add($"SetCoin:{num}:{coin}");

        public void SetLevel(byte num, byte level) => Calls.Add($"SetLevel:{num}:{level}");

        public void SetFullLevel(byte num, byte fullLevel) => Calls.Add($"SetFullLevel:{num}:{fullLevel}");

        public void Reset()
        {
            Calls.Add("Reset");
            ResetCalled = true;
        }

        public int Run(int cycles)
        {
            Calls.Add($"Run:{cycles}");
            Interlocked.Increment(ref RunCallCount);
            return 1;
        }

        public byte Shutdown()
        {
            ShutdownCalled = true;
            return 1;
        }

        public bool IsLampsUpdateAvailable => true;

        public string? LampsUpdateExportName => "SYSTEM6UpdateLamps";

        public void LampsUpdate() => Calls.Add("LampsUpdate");

        public bool GetLampsOn(ushort lampIndex)
        {
            Calls.Add($"GetLampsOn:{lampIndex}");
            return GetLampsOnValues.TryGetValue(lampIndex, out var isOn) && isOn;
        }

        public float GetLampBrightness(ushort lampIndex) => 0f;

        public short GetPosOut(sbyte positionIndex)
        {
            Calls.Add($"GetPosOut:{positionIndex}");
            return PositionOutputs.TryGetValue(positionIndex, out var position) ? position : (short)0;
        }

        public bool IsAlphaSegmentPollingAvailable => AlphaSegmentPollingAvailable;

        public int GetAlphaSegments(byte index)
        {
            Calls.Add($"GetAlphaSegments:{index}");
            AlphaSegmentIndices.Add(index);
            return AlphaSegments.TryGetValue(index, out var segments) ? segments : 0;
        }

        public bool IsAlphaBrightnessPollingAvailable => AlphaBrightnessPollingAvailable;

        public byte GetAlphaBrightness()
        {
            Calls.Add("GetAlphaBrightness");
            return AlphaBrightness;
        }

        public void TurnSwitchOn(int switchIndex)
        {
            SwitchesTurnedOn.Add(switchIndex);
        }

        public void TurnSwitchOff(int switchIndex)
        {
            SwitchesTurnedOff.Add(switchIndex);
        }

        public void Dispose()
        {
            DisposeCalled = true;
        }
    }
}
