using Xunit;

namespace OasisEditor.Tests;

public sealed class MameInputCommandServiceTests
{
    [Fact]
    public async Task TrySendInputStateAsync_WithResolvableInput_WritesFormattedCommand()
    {
        var resolver = new MameInputPortResolver();
        var processRunner = new FakeMameProcessRunner();
        var service = new MameInputCommandService(resolver);
        var input = new InputDefinitionModel { Id = "input-1", Kind = InputDefinitionKind.Button, ButtonNumber = "2" };

        var wrote = await service.TrySendInputStateAsync(processRunner, FruitMachinePlatformType.MPU4, input, isPressed: true, CancellationToken.None);

        Assert.True(wrote);
        Assert.Single(processRunner.Commands);
        Assert.Equal("set_input_value ORANGE1 4 1", processRunner.Commands[0]);
    }

    [Fact]
    public async Task TrySendInputStateAsync_WithUnresolvableInput_DoesNotWriteCommand()
    {
        var resolver = new MameInputPortResolver();
        var processRunner = new FakeMameProcessRunner();
        var service = new MameInputCommandService(resolver);
        var input = new InputDefinitionModel { Id = "input-1", Kind = InputDefinitionKind.Button, ButtonNumber = "ABC" };

        var wrote = await service.TrySendInputStateAsync(processRunner, FruitMachinePlatformType.MPU4, input, isPressed: true, CancellationToken.None);

        Assert.False(wrote);
        Assert.Empty(processRunner.Commands);
    }

    [Fact]
    public async Task TrySendInputStateAsync_WithReleaseState_WritesZeroState()
    {
        var resolver = new MameInputPortResolver();
        var processRunner = new FakeMameProcessRunner();
        var service = new MameInputCommandService(resolver);
        var input = new InputDefinitionModel { Id = "input-1", Kind = InputDefinitionKind.Coin, CoinInput = true };

        var wrote = await service.TrySendInputStateAsync(processRunner, FruitMachinePlatformType.MPU4, input, isPressed: false, CancellationToken.None);

        Assert.True(wrote);
        Assert.Single(processRunner.Commands);
        Assert.Equal("set_input_value COINS 1 0", processRunner.Commands[0]);
    }

    private sealed class FakeMameProcessRunner : IMameProcessRunner
    {
        public List<string> Commands { get; } = [];

        public Task StartAsync(System.Diagnostics.ProcessStartInfo startInfo, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task WriteStandardInputAsync(string command, CancellationToken cancellationToken)
        {
            Commands.Add(command);
            return Task.CompletedTask;
        }
    }
}
