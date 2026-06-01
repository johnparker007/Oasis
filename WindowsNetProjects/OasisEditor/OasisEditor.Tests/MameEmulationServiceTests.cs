using System.Diagnostics;
using Xunit;

namespace OasisEditor.Tests;

public sealed class MameEmulationServiceTests
{
    [Fact]
    public async Task StartAndLoadStateAsync_AddsLegacyStateArgument()
    {
        var runner = new RecordingMameProcessRunner();
        var service = new MameEmulationService(
            new MameProcessStartInfoBuilder(),
            runner,
            () => new MameProcessLaunchRequest(
                @"C:\MAME\mame.exe",
                "m4test",
                @"C:\MAME\roms",
                @"C:\Plugins\oasis",
                string.Empty));

        await service.StartAndLoadStateAsync(CancellationToken.None);

        Assert.Contains("-state oasis_save_state", runner.StartInfo?.Arguments);
        Assert.Equal(MameEmulationState.Running, service.State);
    }

    [Fact]
    public async Task StartDebuggerAsync_EnablesDebuggerLaunchArguments()
    {
        var runner = new RecordingMameProcessRunner();
        var service = new MameEmulationService(
            new MameProcessStartInfoBuilder(),
            runner,
            () => new MameProcessLaunchRequest(
                @"C:\MAME\mame.exe",
                "m4test",
                @"C:\MAME\roms",
                @"C:\Plugins\oasis",
                string.Empty));

        await service.StartDebuggerAsync(CancellationToken.None);

        Assert.Contains("-debug", runner.StartInfo?.Arguments);
        Assert.Contains("-plugin oasis", runner.StartInfo?.Arguments);
        Assert.Contains("-output console", runner.StartInfo?.Arguments);
    }

    [Theory]
    [InlineData(nameof(IMameEmulationService.LoadStateAsync), "state_load oasis_save_state")]
    [InlineData(nameof(IMameEmulationService.SaveStateAsync), "state_save oasis_save_state")]
    [InlineData(nameof(IMameEmulationService.PauseAsync), "pause")]
    [InlineData(nameof(IMameEmulationService.ResumeAsync), "resume")]
    [InlineData(nameof(IMameEmulationService.SoftResetAsync), "soft_reset")]
    [InlineData(nameof(IMameEmulationService.HardResetAsync), "hard_reset")]
    public async Task RuntimeCommands_WriteLegacyLuaPluginCommand(string methodName, string expectedCommand)
    {
        var runner = new RecordingMameProcessRunner();
        var service = await CreateStartedServiceAsync(runner);

        await InvokeRuntimeCommandAsync(service, methodName);

        Assert.Equal(expectedCommand, Assert.Single(runner.Commands));
    }

    [Theory]
    [InlineData(true, "throttled true")]
    [InlineData(false, "throttled false")]
    public async Task SetThrottleAsync_WritesLegacyLuaPluginCommand(bool isThrottled, string expectedCommand)
    {
        var runner = new RecordingMameProcessRunner();
        var service = await CreateStartedServiceAsync(runner);

        await service.SetThrottleAsync(isThrottled, CancellationToken.None);

        Assert.Equal(expectedCommand, Assert.Single(runner.Commands));
    }

    [Fact]
    public async Task SaveStateAndExitAsync_WritesSaveAndExitBeforeStopping()
    {
        var runner = new RecordingMameProcessRunner();
        var service = await CreateStartedServiceAsync(runner);

        await service.SaveStateAndExitAsync(CancellationToken.None);

        Assert.Equal("state_save_and_exit oasis_save_state", Assert.Single(runner.Commands));
        Assert.Equal(1, runner.StopCount);
        Assert.Equal(MameEmulationState.Stopped, service.State);
    }

    private static async Task<MameEmulationService> CreateStartedServiceAsync(RecordingMameProcessRunner runner)
    {
        var service = new MameEmulationService(
            new MameProcessStartInfoBuilder(),
            runner,
            () => new MameProcessLaunchRequest(
                @"C:\MAME\mame.exe",
                "m4test",
                @"C:\MAME\roms",
                @"C:\Plugins\oasis",
                string.Empty));

        await service.StartAsync(CancellationToken.None);
        runner.Commands.Clear();
        return service;
    }

    private static Task InvokeRuntimeCommandAsync(IMameEmulationService service, string methodName)
    {
        return methodName switch
        {
            nameof(IMameEmulationService.LoadStateAsync) => service.LoadStateAsync(CancellationToken.None),
            nameof(IMameEmulationService.SaveStateAsync) => service.SaveStateAsync(CancellationToken.None),
            nameof(IMameEmulationService.PauseAsync) => service.PauseAsync(CancellationToken.None),
            nameof(IMameEmulationService.ResumeAsync) => service.ResumeAsync(CancellationToken.None),
            nameof(IMameEmulationService.SoftResetAsync) => service.SoftResetAsync(CancellationToken.None),
            nameof(IMameEmulationService.HardResetAsync) => service.HardResetAsync(CancellationToken.None),
            _ => throw new ArgumentOutOfRangeException(nameof(methodName), methodName, null)
        };
    }

    private sealed class RecordingMameProcessRunner : IMameProcessRunner
    {
        public ProcessStartInfo? StartInfo { get; private set; }
        public List<string> Commands { get; } = [];
        public int StopCount { get; private set; }

        public Task StartAsync(ProcessStartInfo startInfo, CancellationToken cancellationToken)
        {
            StartInfo = startInfo;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            StopCount++;
            return Task.CompletedTask;
        }

        public Task WriteStandardInputAsync(string command, CancellationToken cancellationToken)
        {
            Commands.Add(command);
            return Task.CompletedTask;
        }
    }
}
