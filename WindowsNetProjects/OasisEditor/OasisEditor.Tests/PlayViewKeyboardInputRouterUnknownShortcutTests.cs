using Xunit;

namespace OasisEditor.Tests;

public sealed class PlayViewKeyboardInputRouterUnknownShortcutTests
{
    [Fact]
    public async Task TryHandleKeyDownAsync_UnknownShortcut_DoesNotSendCommand()
    {
        var runner = new FakeMameProcessRunner();
        var router = CreateRouter(runner);

        var sent = await router.TryHandleKeyDownAsync(FruitMachinePlatformType.MPU4, "F12", isFocused: true, isRepeat: false, CancellationToken.None);

        Assert.False(sent);
        Assert.False(router.CanResolveShortcut("F12"));
        Assert.Empty(runner.Commands);
    }

    [Fact]
    public async Task TryHandleKeyUpAsync_UnknownShortcut_DoesNotSendRelease()
    {
        var runner = new FakeMameProcessRunner();
        var router = CreateRouter(runner);

        var sent = await router.TryHandleKeyUpAsync(FruitMachinePlatformType.MPU4, "F12", isFocused: true, CancellationToken.None);

        Assert.False(sent);
        Assert.Empty(runner.Commands);
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
