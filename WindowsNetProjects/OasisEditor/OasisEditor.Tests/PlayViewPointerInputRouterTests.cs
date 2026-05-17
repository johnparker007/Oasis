using Xunit;

namespace OasisEditor.Tests;

public sealed class PlayViewPointerInputRouterTests
{
    [Fact]
    public async Task TryHandlePointerDownAsync_LinkedVisual_SendsDownCommand()
    {
        var runner = new FakeMameProcessRunner();
        var visualId = Guid.NewGuid();
        var router = CreateRouter(runner, visualId);

        var sent = await router.TryHandlePointerDownAsync(FruitMachinePlatformType.MPU4, visualId, isFocused: true, CancellationToken.None);

        Assert.True(sent);
        Assert.Single(runner.Commands);
        Assert.Equal("set_input_value ORANGE1 4 1", runner.Commands[0]);
    }

    [Fact]
    public async Task TryHandlePointerUpAsync_NotFocused_DoesNotSendRelease()
    {
        var runner = new FakeMameProcessRunner();
        var visualId = Guid.NewGuid();
        var router = CreateRouter(runner, visualId);

        await router.TryHandlePointerDownAsync(FruitMachinePlatformType.MPU4, visualId, isFocused: true, CancellationToken.None);
        var sent = await router.TryHandlePointerUpAsync(FruitMachinePlatformType.MPU4, visualId, isFocused: false, CancellationToken.None);

        Assert.False(sent);
        Assert.Single(runner.Commands);
    }

    [Fact]
    public async Task ReleaseAllActiveAsync_ReleasesPressedPointerInputs()
    {
        var runner = new FakeMameProcessRunner();
        var visualId = Guid.NewGuid();
        var router = CreateRouter(runner, visualId);

        await router.TryHandlePointerDownAsync(FruitMachinePlatformType.MPU4, visualId, isFocused: true, CancellationToken.None);
        var released = await router.ReleaseAllActiveAsync(FruitMachinePlatformType.MPU4, CancellationToken.None);

        Assert.Equal(1, released);
        Assert.Equal(2, runner.Commands.Count);
        Assert.Equal("set_input_value ORANGE1 4 0", runner.Commands[1]);
    }

    private static PlayViewPointerInputRouter CreateRouter(FakeMameProcessRunner runner, Guid visualId)
    {
        var input = new InputDefinitionModel
        {
            Id = "btn-1",
            Kind = InputDefinitionKind.Button,
            ButtonNumber = "2",
            LinkedVisualElementId = visualId
        };

        var inputRouter = new PlayViewInputRouter(new MameInputCommandService(new MameInputPortResolver()), runner);
        return new PlayViewPointerInputRouter(inputRouter, new[] { input });
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
