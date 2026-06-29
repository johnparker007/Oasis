using System.Windows;
using Xunit;

namespace OasisEditor.Tests;

public sealed class FaceInputSystemTests
{
    [Fact]
    public void FaceButtonElement_RoundTripsMachineInputReference()
    {
        var document = new FaceDocumentModel
        {
            Title = "Buttons",
            Elements =
            [
                new FaceButtonElement
                {
                    ObjectId = "face-button-start",
                    Name = "Start",
                    X = 10,
                    Y = 20,
                    Width = 80,
                    Height = 30,
                    LinkedInputReference = MachineInputReference.FromInputId("start"),
                    LinkedMachineObjectReference = MachineObjectReference.Input("start"),
                    LinkedPanel2DElementId = "source-panel-button"
                }
            ]
        };

        var json = FaceDocumentStorage.Serialize(document);
        Assert.True(FaceDocumentStorage.TryReadValidated(json, out var file, out var error), error);
        var model = FaceDocumentStorage.ToModel(file);

        var button = Assert.IsType<FaceButtonElement>(Assert.Single(model.Elements));
        Assert.Equal("input:start", button.LinkedInputReference?.ToString());
        Assert.Equal("input:start", button.LinkedMachineObjectReference?.ToString());
        Assert.Equal("source-panel-button", button.LinkedPanel2DElementId);
    }

    [Fact]
    public void FaceInputTargetResolver_UsesMachineInputReferenceNotPanelProvenance()
    {
        var resolver = FaceInputTargetResolver.Instance;
        var elements = new FaceElementModel[]
        {
            new FaceButtonElement
            {
                ObjectId = "face-button",
                X = 0,
                Y = 0,
                Width = 100,
                Height = 50,
                LinkedInputReference = MachineInputReference.FromInputId("collect"),
                LinkedMachineObjectReference = MachineObjectReference.Input("collect"),
                LinkedPanel2DElementId = "not-used-for-routing"
            }
        };

        var resolved = resolver.TryResolveInputReference(elements, new Point(25, 20), out var inputReference);

        Assert.True(resolved);
        Assert.Equal("input:collect", inputReference.ToString());
    }

    [Fact]
    public void FaceInputTargetResolver_IgnoresButtonsWithoutMachineInputReference()
    {
        var resolver = FaceInputTargetResolver.Instance;
        var elements = new FaceElementModel[]
        {
            new FaceButtonElement
            {
                ObjectId = "face-button",
                X = 0,
                Y = 0,
                Width = 100,
                Height = 50,
                LinkedPanel2DElementId = "panel-button-only"
            }
        };

        var resolved = resolver.TryResolveInputReference(elements, new Point(25, 20), out _);

        Assert.False(resolved);
    }


    [Fact]
    public async Task FacePlayViewPointerInputRouter_RoutesThroughExistingMameInputPath()
    {
        var runner = new FakeMameProcessRunner();
        var inputRouter = new PlayViewInputRouter(new MameInputCommandService(new MameInputPortResolver()), runner);
        var faceRouter = new FacePlayViewPointerInputRouter(
            inputRouter,
            [new InputDefinitionModel { Id = "start", Kind = InputDefinitionKind.Button, ButtonNumber = "2" }]);
        var inputReference = MachineInputReference.FromInputId("start");

        var pressed = await faceRouter.TryHandlePointerDownAsync(FruitMachinePlatformType.MPU4, inputReference, isFocused: true, CancellationToken.None);
        var released = await faceRouter.TryHandlePointerUpAsync(FruitMachinePlatformType.MPU4, inputReference, isFocused: true, CancellationToken.None);

        Assert.True(pressed);
        Assert.True(released);
        Assert.Equal(new[] { "set_input_value ORANGE1 4 1", "set_input_value ORANGE1 4 0" }, runner.Commands);
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
