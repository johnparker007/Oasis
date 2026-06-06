using Xunit;

namespace OasisEditor.Tests;

public sealed class PlayViewKeyboardInputRouterTests
{
    [Fact]
    public async Task TryHandleKeyDownAsync_RepeatKey_DoesNotSendDuplicateCommand()
    {
        var runner = new FakeMameProcessRunner();
        var keyboardRouter = CreateRouter(runner, out _);

        var first = await keyboardRouter.TryHandleKeyDownAsync(FruitMachinePlatformType.MPU4, "Space", isFocused: true, isRepeat: false, CancellationToken.None);
        var repeat = await keyboardRouter.TryHandleKeyDownAsync(FruitMachinePlatformType.MPU4, "Space", isFocused: true, isRepeat: true, CancellationToken.None);

        Assert.True(first);
        Assert.False(repeat);
        Assert.Single(runner.Commands);
        Assert.Equal("set_input_value ORANGE1 4 1", runner.Commands[0]);
    }

    [Fact]
    public async Task TryHandleKeyDownAsync_NormalizedShortcut_SendsCommand()
    {
        var runner = new FakeMameProcessRunner();
        var keyboardRouter = CreateRouter(runner, out _);

        var sent = await keyboardRouter.TryHandleKeyDownAsync(FruitMachinePlatformType.MPU4, "SPACE", isFocused: true, isRepeat: false, CancellationToken.None);

        Assert.True(sent);
        Assert.True(keyboardRouter.CanResolveShortcut("SPACE"));
        Assert.Single(runner.Commands);
        Assert.Equal("set_input_value ORANGE1 4 1", runner.Commands[0]);
    }

    [Fact]
    public async Task TryHandleKeyUpAsync_FocusedKey_ReleasesInput()
    {
        var runner = new FakeMameProcessRunner();
        var keyboardRouter = CreateRouter(runner, out _);

        await keyboardRouter.TryHandleKeyDownAsync(FruitMachinePlatformType.MPU4, "Space", isFocused: true, isRepeat: false, CancellationToken.None);
        var released = await keyboardRouter.TryHandleKeyUpAsync(FruitMachinePlatformType.MPU4, "Space", isFocused: true, CancellationToken.None);

        Assert.True(released);
        Assert.Equal(2, runner.Commands.Count);
        Assert.Equal("set_input_value ORANGE1 4 0", runner.Commands[1]);
    }

    [Fact]
    public async Task ReleaseAllActiveAsync_ReleasesWhenFocusLost()
    {
        var runner = new FakeMameProcessRunner();
        var keyboardRouter = CreateRouter(runner, out _);

        await keyboardRouter.TryHandleKeyDownAsync(FruitMachinePlatformType.MPU4, "Space", isFocused: true, isRepeat: false, CancellationToken.None);
        var count = await keyboardRouter.ReleaseAllActiveAsync(FruitMachinePlatformType.MPU4, CancellationToken.None);

        Assert.Equal(1, count);
        Assert.Equal(2, runner.Commands.Count);
        Assert.Equal("set_input_value ORANGE1 4 0", runner.Commands[1]);
    }

    private static PlayViewKeyboardInputRouter CreateRouter(FakeMameProcessRunner runner, out InputDefinitionModel input)
    {
        var commandService = new MameInputCommandService(new MameInputPortResolver());
        var inputRouter = new PlayViewInputRouter(commandService, runner);
        input = new InputDefinitionModel { Id = "btn-1", Kind = InputDefinitionKind.Button, ButtonNumber = "2", KeyboardShortcut = "Space" };
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
