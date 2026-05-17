using Xunit;

namespace OasisEditor.Tests;

public sealed class PlayViewInputRouterReleaseAllTests
{
    [Fact]
    public async Task ReleaseAllAsync_RemovesMissingDefinitionFromActiveSet()
    {
        var runner = new FakeMameProcessRunner();
        var router = new PlayViewInputRouter(new MameInputCommandService(new MameInputPortResolver()), runner);
        var input = new InputDefinitionModel { Id = "btn-1", Kind = InputDefinitionKind.Button, ButtonNumber = "2" };

        var pressed = await router.TryPressAsync(FruitMachinePlatformType.MPU4, input, CancellationToken.None);
        Assert.True(pressed);
        Assert.Single(runner.Commands);

        var released = await router.ReleaseAllAsync(FruitMachinePlatformType.MPU4, new Dictionary<string, InputDefinitionModel>(), CancellationToken.None);
        Assert.Equal(0, released);

        var pressedAgain = await router.TryPressAsync(FruitMachinePlatformType.MPU4, input, CancellationToken.None);
        Assert.True(pressedAgain);
        Assert.Equal(2, runner.Commands.Count);
        Assert.Equal("set_input_value ORANGE1 4 1", runner.Commands[1]);
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
