using System;
using System.Collections.Generic;
using System.IO;
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
        Assert.True(library.ShutdownCalled);
        Assert.True(library.DisposeCalled);
        Assert.Equal(EmulationBackendState.Stopped, backend.State);
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

        public byte Initialise()
        {
            InitialiseCalled = true;
            return 1;
        }

        public int LoadRom(IReadOnlyList<string> programRomPaths, bool flashSwitch)
        {
            LoadedRoms.AddRange(programRomPaths);
            return 1;
        }

        public int LoadSoundRom(IReadOnlyList<string> soundRomPaths)
        {
            return 1;
        }

        public void Reset()
        {
            ResetCalled = true;
        }

        public int Run(int cycles)
        {
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
