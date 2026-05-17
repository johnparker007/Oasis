using Xunit;

namespace OasisEditor.Tests;

public sealed class PlayViewKeyboardInputRouterFocusTests
{
    [Fact]
    public async Task TryHandleKeyDownAsync_WhenNotFocused_DoesNotSendCommand()
    {
        var runner = new FakeMameProcessRunner();
        var router = CreateRouter(runner);

        var sent = await router.TryHandleKeyDownAsync(
            FruitMachinePlatformType.MPU4,
            "Space",
            isFocused: false,
            isRepeat: false,
            CancellationToken.None);

        Assert.False(sent);
        Assert.Empty(runner.Commands);
    }

    [Fact]
    public async Task TryHandleKeyUpAsync_WhenNotFocused_DoesNotSendRelease()
    {
        var runner = new FakeMameProcessRunner();
        var router = CreateRouter(runner);

        await router.TryHandleKeyDownAsync(FruitMachinePlatformType.MPU4, "Space", isFocused: true, isRepeat: false, CancellationToken.None);
        var sent = await router.TryHandleKeyUpAsync(FruitMachinePlatformType.MPU4, "Space", isFocused: false, CancellationToken.None);

        Assert.False(sent);
        Assert.Single(runner.Commands);
        Assert.Equal("set_input_value ORANGE1 4 1", runner.Commands[0]);
    }

    private static PlayViewKeyboardInputRouter CreateRouter(FakeMameProcessRunner runner)
    {
        var input = new InputDefinitionModel { Id = "btn-1", Kind = InputDefinitionKind.Button, ButtonNumber = "2", KeyboardShortcut = "Space" };
        var inputRouter = new PlayViewInputRouter(new MameInputCommandService(new MameInputPortResolver()), runner);
        return new PlayViewKeyboardInputRouter(inputRouter, new[] { input });
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
