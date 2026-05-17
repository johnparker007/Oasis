using Xunit;

namespace OasisEditor.Tests;

public sealed class PlayViewInputRouterTests
{
    [Fact]
    public async Task TryPressAsync_DuplicatePress_DoesNotSendSecondDown()
    {
        var processRunner = new FakeMameProcessRunner();
        var router = new PlayViewInputRouter(new MameInputCommandService(new MameInputPortResolver()), processRunner);
        var input = new InputDefinitionModel { Id = "btn-1", Kind = InputDefinitionKind.Button, ButtonNumber = "2" };

        var first = await router.TryPressAsync(FruitMachinePlatformType.MPU4, input, CancellationToken.None);
        var second = await router.TryPressAsync(FruitMachinePlatformType.MPU4, input, CancellationToken.None);

        Assert.True(first);
        Assert.False(second);
        Assert.Single(processRunner.Commands);
        Assert.Equal("set_input_value ORANGE1 4 1", processRunner.Commands[0]);
    }

    [Fact]
    public async Task ReleaseAllAsync_ReleasesPressedInputs()
    {
        var processRunner = new FakeMameProcessRunner();
        var router = new PlayViewInputRouter(new MameInputCommandService(new MameInputPortResolver()), processRunner);
        var input = new InputDefinitionModel { Id = "btn-1", Kind = InputDefinitionKind.Button, ButtonNumber = "2" };

        await router.TryPressAsync(FruitMachinePlatformType.MPU4, input, CancellationToken.None);

        var released = await router.ReleaseAllAsync(
            FruitMachinePlatformType.MPU4,
            new Dictionary<string, InputDefinitionModel> { [input.Id] = input },
            CancellationToken.None);

        Assert.Equal(1, released);
        Assert.Equal(2, processRunner.Commands.Count);
        Assert.Equal("set_input_value ORANGE1 4 0", processRunner.Commands[1]);
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
