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
        Assert.Equal(8 * 4, library.Calls.Count(call => call.StartsWith("Set", StringComparison.Ordinal)));
        Assert.True(library.Calls.IndexOf("Reset") < library.Calls.IndexOf("SetSteps:0:96"));
        Assert.True(library.ShutdownCalled);
        Assert.True(library.DisposeCalled);
        Assert.Equal(EmulationBackendState.Stopped, backend.State);
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
    public async Task SetInputStateAsyncMapsNumericInputIdToNativeSwitchCalls()
    {
        var library = new FakeSystem6NativeLibrary();
        var (dllPath, rom1, rom2) = CreateNativeFiles(2);
        var backend = new System6NativeBackend(dllPath, _ => library);

        await backend.StartAsync(CreateLaunchRequest(rom1, rom2), CancellationToken.None);
        await backend.SetInputStateAsync(MachineInputReference.FromInputId("12"), true, CancellationToken.None);
        await backend.SetInputStateAsync(MachineInputReference.FromInputId("12"), false, CancellationToken.None);
        await backend.StopAsync(CancellationToken.None);

        Assert.Equal(new[] { 12 }, library.SwitchesTurnedOn);
        Assert.Equal(new[] { 12 }, library.SwitchesTurnedOff);
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
        Assert.Equal(7 * 4, library.Calls.Count(call => call.StartsWith("Set", StringComparison.Ordinal)));
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
                "Run:133333");
        }
        finally
        {
            Environment.SetEnvironmentVariable("OASIS_SYSTEM6_STARTUP_STAGE", previousStage);
        }
    }


    [Fact]
    public void FormatAlphaDebugString_MapsZeroToSpacePrintableAsciiToCharsAndNonPrintableToPlaceholder()
    {
        var text = System6NativeBackend.FormatAlphaDebugString(new byte[] { 0, 65, 31, 126 });

        Assert.Equal(" A?~", text);
    }

    [Fact]
    public void FormatAlphaRawBytes_FormatsUppercaseHexBytes()
    {
        var raw = System6NativeBackend.FormatAlphaRawBytes(new byte[] { 0, 65, 255 });

        Assert.Equal("00 41 FF", raw);
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
            });
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

        public void Reset()
        {
            Calls.Add("Reset");
            ResetCalled = true;
        }

        public int Run(int cycles)
        {
            Calls.Add($"Run:{cycles}");
            return 1;
        }

        public byte Shutdown()
        {
            ShutdownCalled = true;
            return 1;
        }

        public bool GetLampsOn(ushort lampIndex) => false;

        public float GetLampBrightness(ushort lampIndex) => 0f;

        public short GetPosOut(sbyte positionIndex) => 0;

        public bool IsAlphaCharPollingAvailable => false;

        public byte GetAlphaChar(byte index) => 0;

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
