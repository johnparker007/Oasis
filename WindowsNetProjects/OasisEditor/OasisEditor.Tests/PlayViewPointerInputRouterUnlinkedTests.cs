using Xunit;

namespace OasisEditor.Tests;

public sealed class PlayViewPointerInputRouterUnlinkedTests
{
    [Fact]
    public async Task TryHandlePointerDownAsync_UnlinkedVisual_DoesNotSendCommand()
    {
        var runner = new FakeMameProcessRunner();
        var linkedVisualId = Guid.NewGuid();
        var unlinkedVisualId = Guid.NewGuid();
        var router = CreateRouter(runner, linkedVisualId);

        var sent = await router.TryHandlePointerDownAsync(FruitMachinePlatformType.MPU4, unlinkedVisualId, isFocused: true, CancellationToken.None);

        Assert.False(sent);
        Assert.Empty(runner.Commands);
    }

    [Fact]
    public async Task TryHandlePointerUpAsync_UnlinkedVisual_DoesNotSendCommand()
    {
        var runner = new FakeMameProcessRunner();
        var linkedVisualId = Guid.NewGuid();
        var unlinkedVisualId = Guid.NewGuid();
        var router = CreateRouter(runner, linkedVisualId);

        var sent = await router.TryHandlePointerUpAsync(FruitMachinePlatformType.MPU4, unlinkedVisualId, isFocused: true, CancellationToken.None);

        Assert.False(sent);
        Assert.Empty(runner.Commands);
    }

    private static PlayViewPointerInputRouter CreateRouter(FakeMameProcessRunner runner, Guid linkedVisualId)
    {
        var input = new InputDefinitionModel { Id = "btn-1", Kind = InputDefinitionKind.Button, ButtonNumber = "2", LinkedVisualElementId = linkedVisualId };
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
