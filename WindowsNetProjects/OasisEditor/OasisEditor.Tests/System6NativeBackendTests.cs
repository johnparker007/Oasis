using System;
using System.Collections.Generic;
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
        var backend = new System6NativeBackend("C:/cores/system6.dll", _ => library);
        var request = CreateLaunchRequest("C:/roms/a.rom", "C:/roms/b.rom");

        await backend.StartAsync(request, CancellationToken.None);
        await backend.StopAsync(CancellationToken.None);

        Assert.True(library.InitialiseCalled);
        Assert.Equal(new[] { "C:/roms/a.rom", "C:/roms/b.rom" }, library.LoadedRoms);
        Assert.True(library.ResetCalled);
        Assert.True(library.ShutdownCalled);
        Assert.True(library.DisposeCalled);
        Assert.Equal(EmulationBackendState.Stopped, backend.State);
    }

    [Fact]
    public async Task SetInputStateAsyncMapsNumericInputIdToNativeSwitchCalls()
    {
        var library = new FakeSystem6NativeLibrary();
        var backend = new System6NativeBackend("C:/cores/system6.dll", _ => library);

        await backend.StartAsync(CreateLaunchRequest("C:/roms/a.rom"), CancellationToken.None);
        await backend.SetInputStateAsync(MachineInputReference.FromInputId("12"), true, CancellationToken.None);
        await backend.SetInputStateAsync(MachineInputReference.FromInputId("12"), false, CancellationToken.None);
        await backend.StopAsync(CancellationToken.None);

        Assert.Equal(new[] { 12 }, library.SwitchesTurnedOn);
        Assert.Equal(new[] { 12 }, library.SwitchesTurnedOff);
    }

    [Fact]
    public async Task StartAsyncWithoutRomPathsFailsBeforeLoadingNativeLibrary()
    {
        var library = new FakeSystem6NativeLibrary();
        var backend = new System6NativeBackend("C:/cores/system6.dll", _ => library);
        var request = CreateLaunchRequest();

        await Assert.ThrowsAsync<InvalidOperationException>(() => backend.StartAsync(request, CancellationToken.None));

        Assert.False(library.InitialiseCalled);
        Assert.Equal(EmulationBackendState.Failed, backend.State);
    }

    private static EmulationLaunchRequest CreateLaunchRequest(params string[] romPaths)
    {
        return new EmulationLaunchRequest(
            FruitMachinePlatformType.None,
            "test-machine",
            "C:/roms",
            romPaths,
            string.Empty);
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

        public int Initialise()
        {
            InitialiseCalled = true;
            return 0;
        }

        public int LoadRom(string romPath)
        {
            LoadedRoms.Add(romPath);
            return 0;
        }

        public void Reset()
        {
            ResetCalled = true;
        }

        public void Run(int cycles)
        {
        }

        public void Shutdown()
        {
            ShutdownCalled = true;
        }

        public int GetLampsOn() => 0;

        public int GetLampBrightness(int lampIndex) => 0;

        public int GetPosOut(int positionIndex) => 0;

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
