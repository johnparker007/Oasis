using Xunit;

namespace OasisEditor.Avalonia.Tests;

public sealed class NativePreviewSessionTests
{
    private static MachineDocument Machine() => MachineDocument.Create("Native test") with
    {
        Runtime = EmulationRuntimeDefinition.Create(FruitMachinePlatformType.Impact),
        InputDefinitions = [new() { Id = "door", Name = "Door", ButtonNumber = "7" },
            new() { Id = "coin", Name = "Coin", CoinInput = true, CoinChannel = 0, CoinValue = 5 }]
    };
    [Fact]
    public async Task HeldInputsAreReleasedOnPauseAndRepeatDoesNotRepeatCoins()
    {
        var backend = new Backend();
        await using var preview = new NativePreviewSession((_, _, _) => backend);
        var machine = Machine();
        await preview.StartAsync(machine, new());
        machine.InputDefinitions[0].ButtonNumber = "99";
        Assert.Equal("7", preview.Machine!.InputDefinitions[0].ButtonNumber);
        await preview.SetInputAsync("door", true);
        await preview.SetInputAsync("coin", true);
        await preview.SetInputAsync("coin", true);
        await preview.ControlAsync("pause");
        Assert.Equal(new[] { "door:True", "coin:insert", "door:False", "coin:release" }.Order(), backend.Calls.Order());
        Assert.Equal(EmulationBackendState.Paused, preview.State);
        await Assert.ThrowsAsync<InvalidOperationException>(() => preview.SetInputAsync("door", true));
        await preview.ControlAsync("resume");
        await preview.SetInputAsync("door", true);
        await preview.ControlAsync("reset");
        Assert.Equal("door:False", backend.Calls.Last());
        Assert.Equal(1, backend.Resets);
    }
    [Fact]
    public async Task StaleSourceBlocksInputsButStillAllowsStopAndDisposal()
    {
        var backend = new Backend(); var current = true;
        await using var preview = new NativePreviewSession((_, _, _) => backend);
        await preview.StartAsync(Machine(), new(), sourceCurrent: () => current);
        await preview.SetInputAsync("door", true); current = false;
        await Assert.ThrowsAsync<InvalidOperationException>(() => preview.SetInputAsync("coin", true));
        await Assert.ThrowsAsync<InvalidOperationException>(() => preview.ControlAsync("resume"));
        await preview.ControlAsync("stop");
        Assert.Equal("door:False", backend.Calls.Last());
        Assert.True(backend.Disposed); Assert.Null(preview.Backend);
    }
    [Fact]
    public async Task RejectedCoinNeverBecomesHeldAndFailedStartupDisposesBackend()
    {
        var backend = new Backend { CoinResult = CoinInputResult.Rejected };
        await using var preview = new NativePreviewSession((_, _, _) => backend);
        await preview.StartAsync(Machine(), new());
        Assert.Equal(CoinInputResult.Rejected, await preview.SetInputAsync("coin", true));
        await preview.ReleaseInputsAsync();
        Assert.DoesNotContain("coin:release", backend.Calls);
        await preview.ControlAsync("stop"); backend.FailStart = true;
        await Assert.ThrowsAsync<IOException>(() => preview.StartAsync(Machine(), new()));
        Assert.Null(preview.Backend); Assert.Equal("Native provider failed", preview.LastError);
        Assert.True(backend.Disposed);
    }
    [Fact]
    public async Task PointerReleaseDoesNotReleaseAnInputStillHeldByKeyboard()
    {
        var backend = new Backend(); await using var preview = new NativePreviewSession((_, _, _) => backend);
        await preview.StartAsync(Machine(), new());
        await preview.SetInputAsync("door", true, source: "keyboard");
        await preview.SetInputAsync("door", true, source: "pointer");
        await preview.SetInputAsync("door", false, source: "pointer");
        Assert.Equal(new[] { "door:True" }, backend.Calls);
        await preview.SetInputAsync("door", false, source: "keyboard");
        Assert.Equal(new[] { "door:True", "door:False" }, backend.Calls);
    }
    [Fact]
    public async Task InvalidNativeLibraryDoesNotRetainTheProcessLease()
    {
        await using var first = new NativePreviewSession();
        await using var second = new NativePreviewSession();
        await Assert.ThrowsAsync<FileNotFoundException>(() => first.StartAsync(Machine(), new()));
        await Assert.ThrowsAsync<FileNotFoundException>(() => second.StartAsync(Machine(), new()));
        Assert.Null(first.Backend); Assert.Null(second.Backend);
    }
    [Fact]
    public void DiagnosticAudioCountsRealPcmWithoutPlayback()
    {
        using var sink = new DiagnosticAudioSink(); sink.Start(new(48000, 2, 16));
        sink.PushPcm([0, 0, 1, 0, 2, 0, 0, 0]);
        Assert.Equal(2, sink.Frames); Assert.Equal(2, sink.NonZeroSamples);
        Assert.False(sink.GetStatistics().PlaybackStarted);
        sink.Clear(); Assert.Equal(0, sink.Frames);
    }
    internal sealed class Backend : IEmulationBackend
    {
        public readonly List<string> Calls = [];
        public bool Disposed, FailStart; public int Resets;
        public CoinInputResult CoinResult = CoinInputResult.Accepted;
        public EmulationBackendKind BackendKind => EmulationBackendKind.Fabric;
        public EmulationBackendState State { get; private set; }
        public EmulationBackendCapabilities Capabilities => new(true, true, true, false, false, true);
        public bool IsThrottleEnabled { get; private set; } = true;
#pragma warning disable CS0067
        public event EventHandler<EmulationBackendState>? StateChanged;
        public event EventHandler<MachineLampChangedEventArgs>? LampChanged;
        public event EventHandler<MachineReelChangedEventArgs>? ReelChanged;
        public event EventHandler<MachineSegmentChangedEventArgs>? SegmentChanged;
        public event EventHandler<MachineVfdBrightnessChangedEventArgs>? VfdBrightnessChanged;
        public event EventHandler<MachineDotMatrixChangedEventArgs>? DotMatrixChanged;
#pragma warning restore CS0067
        public Task StartAsync(EmulationLaunchRequest request, CancellationToken token) { if (FailStart) throw new IOException("Native provider failed"); State = EmulationBackendState.Running; return Task.CompletedTask; }
        public Task StopAsync(CancellationToken token) { State = EmulationBackendState.Stopped; return Task.CompletedTask; }
        public Task PauseAsync(CancellationToken token) { State = EmulationBackendState.Paused; return Task.CompletedTask; }
        public Task ResumeAsync(CancellationToken token) { State = EmulationBackendState.Running; return Task.CompletedTask; }
        public Task ResetAsync(CancellationToken token) { Resets++; return Task.CompletedTask; }
        public Task SetThrottleEnabledAsync(bool enabled, CancellationToken token) { IsThrottleEnabled = enabled; return Task.CompletedTask; }
        public Task SetInputStateAsync(InputDefinitionModel input, bool pressed, CancellationToken token) { Calls.Add(input.Id + ":" + pressed); return Task.CompletedTask; }
        public Task<CoinInputResult> InsertCoinAsync(InputDefinitionModel input, CancellationToken token) { Calls.Add(input.Id + ":insert"); return Task.FromResult(CoinResult); }
        public Task ReleaseCoinAsync(InputDefinitionModel input, CancellationToken token) { Calls.Add(input.Id + ":release"); return Task.CompletedTask; }
        public ValueTask DisposeAsync() { Disposed = true; return ValueTask.CompletedTask; }
    }
}
