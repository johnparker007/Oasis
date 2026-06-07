using Xunit;

namespace OasisEditor.Tests;

public sealed class PlayViewInputDispatcherTests
{
    [Fact]
    public async Task TryHandlePointerDownAsync_PanelVisualTarget_RoutesThroughSharedInputPath()
    {
        var runner = new FakeMameProcessRunner();
        var visualId = Guid.NewGuid();
        var dispatcher = CreateDispatcher(
            runner,
            new InputDefinitionModel
            {
                Id = "panel-start",
                Kind = InputDefinitionKind.Button,
                ButtonNumber = "2",
                LinkedVisualElementId = visualId
            });

        var pressed = await dispatcher.TryHandlePointerDownAsync(
            FruitMachinePlatformType.MPU4,
            PlayInputTarget.ForPanelVisualElement(visualId),
            isFocused: true,
            CancellationToken.None);

        Assert.True(pressed);
        Assert.Equal(new[] { "set_input_value ORANGE1 4 1" }, runner.Commands);
    }

    [Fact]
    public async Task TryHandlePointerDownAsync_FaceMachineInputTarget_RoutesThroughSharedInputPath()
    {
        var runner = new FakeMameProcessRunner();
        var dispatcher = CreateDispatcher(
            runner,
            new InputDefinitionModel
            {
                Id = "face-start",
                Kind = InputDefinitionKind.Button,
                ButtonNumber = "2"
            });

        var pressed = await dispatcher.TryHandlePointerDownAsync(
            FruitMachinePlatformType.MPU4,
            PlayInputTarget.ForMachineInput(MachineInputReference.FromInputId("face-start")),
            isFocused: true,
            CancellationToken.None);

        Assert.True(pressed);
        Assert.Equal(new[] { "set_input_value ORANGE1 4 1" }, runner.Commands);
    }

    [Fact]
    public async Task TryHandleKeyDownAndUpAsync_UsesNormalizedShortcutForFaceAndPanelDocuments()
    {
        var runner = new FakeMameProcessRunner();
        var dispatcher = CreateDispatcher(
            runner,
            new InputDefinitionModel
            {
                Id = "keyboard-start",
                Kind = InputDefinitionKind.Button,
                ButtonNumber = "2",
                KeyboardShortcut = "Space"
            });

        var pressed = await dispatcher.TryHandleKeyDownAsync(FruitMachinePlatformType.MPU4, "SPACE", isFocused: true, isRepeat: false, CancellationToken.None);
        var released = await dispatcher.TryHandleKeyUpAsync(FruitMachinePlatformType.MPU4, "space", isFocused: true, CancellationToken.None);

        Assert.True(pressed);
        Assert.True(released);
        Assert.True(dispatcher.CanResolveShortcut("SPACE"));
        Assert.Equal(new[] { "set_input_value ORANGE1 4 1", "set_input_value ORANGE1 4 0" }, runner.Commands);
    }

    [Fact]
    public async Task ReleaseAllActiveAsync_ReleasesKeyboardAndPointerInputsOnFocusLoss()
    {
        var runner = new FakeMameProcessRunner();
        var visualId = Guid.NewGuid();
        var dispatcher = CreateDispatcher(
            runner,
            new InputDefinitionModel
            {
                Id = "panel-start",
                Kind = InputDefinitionKind.Button,
                ButtonNumber = "2",
                LinkedVisualElementId = visualId
            },
            new InputDefinitionModel
            {
                Id = "keyboard-collect",
                Kind = InputDefinitionKind.Button,
                ButtonNumber = "3",
                KeyboardShortcut = "C"
            });

        await dispatcher.TryHandlePointerDownAsync(FruitMachinePlatformType.MPU4, PlayInputTarget.ForPanelVisualElement(visualId), isFocused: true, CancellationToken.None);
        await dispatcher.TryHandleKeyDownAsync(FruitMachinePlatformType.MPU4, "C", isFocused: true, isRepeat: false, CancellationToken.None);

        var released = await dispatcher.ReleaseAllActiveAsync(FruitMachinePlatformType.MPU4, CancellationToken.None);

        Assert.Equal(2, released);
        Assert.Equal(4, runner.Commands.Count);
        Assert.Contains("set_input_value ORANGE1 4 1", runner.Commands);
        Assert.Contains("set_input_value ORANGE1 8 1", runner.Commands);
        Assert.Contains("set_input_value ORANGE1 4 0", runner.Commands);
        Assert.Contains("set_input_value ORANGE1 8 0", runner.Commands);
    }

    private static PlayViewInputDispatcher CreateDispatcher(FakeMameProcessRunner runner, params InputDefinitionModel[] inputDefinitions)
    {
        var inputRouter = new PlayViewInputRouter(new MameInputCommandService(new MameInputPortResolver()), runner);
        return new PlayViewInputDispatcher(inputRouter, inputDefinitions);
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
