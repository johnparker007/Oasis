using System.Windows;

namespace OasisEditor;

public interface IFaceInputTargetResolver
{
    bool TryResolveInputReference(IReadOnlyList<FaceElementModel> elements, Point documentPoint, out MachineInputReference inputReference);
}

public sealed class FaceInputTargetResolver : IFaceInputTargetResolver
{
    public static FaceInputTargetResolver Instance { get; } = new();

    private FaceInputTargetResolver()
    {
    }

    public bool TryResolveInputReference(IReadOnlyList<FaceElementModel> elements, Point documentPoint, out MachineInputReference inputReference)
    {
        ArgumentNullException.ThrowIfNull(elements);
        inputReference = default;

        foreach (var button in elements.OfType<FaceButtonElement>().Reverse())
        {
            if (!button.IsVisible || !Contains(button, documentPoint))
            {
                continue;
            }

            if (TryGetInputReference(button, out inputReference))
            {
                return true;
            }
        }

        inputReference = default;
        return false;
    }

    public static bool TryGetInputReference(FaceButtonElement button, out MachineInputReference inputReference)
    {
        ArgumentNullException.ThrowIfNull(button);

        if (button.LinkedInputReference is MachineInputReference linkedInputReference
            && !linkedInputReference.Reference.IsEmpty
            && linkedInputReference.Reference.Kind == MachineObjectKind.Input)
        {
            inputReference = linkedInputReference;
            return true;
        }

        if (button.LinkedMachineObjectReference is MachineObjectReference linkedMachineObjectReference
            && !linkedMachineObjectReference.IsEmpty
            && linkedMachineObjectReference.Kind == MachineObjectKind.Input)
        {
            inputReference = new MachineInputReference(linkedMachineObjectReference);
            return true;
        }

        inputReference = default;
        return false;
    }

    private static bool Contains(FaceElementModel element, Point documentPoint)
    {
        return documentPoint.X >= element.X
            && documentPoint.X <= element.X + element.Width
            && documentPoint.Y >= element.Y
            && documentPoint.Y <= element.Y + element.Height;
    }
}

public sealed class FacePlayViewPointerInputRouter
{
    private readonly PlayViewInputRouter _inputRouter;
    private readonly Dictionary<string, InputDefinitionModel> _inputsByMachineInputId;

    public FacePlayViewPointerInputRouter(PlayViewInputRouter inputRouter, IEnumerable<InputDefinitionModel> inputDefinitions)
    {
        _inputRouter = inputRouter ?? throw new ArgumentNullException(nameof(inputRouter));
        ArgumentNullException.ThrowIfNull(inputDefinitions);

        _inputsByMachineInputId = new Dictionary<string, InputDefinitionModel>(StringComparer.Ordinal);
        foreach (var input in inputDefinitions)
        {
            if (input is null || string.IsNullOrWhiteSpace(input.Id))
            {
                continue;
            }

            var reference = MachineInputReference.FromInputId(input.Id);
            if (!reference.Reference.IsEmpty && !_inputsByMachineInputId.ContainsKey(reference.Reference.Id))
            {
                _inputsByMachineInputId[reference.Reference.Id] = input;
            }
        }
    }

    public Task<bool> TryHandlePointerDownAsync(FruitMachinePlatformType platform, MachineInputReference inputReference, bool isFocused, CancellationToken cancellationToken)
    {
        if (!isFocused || !TryResolveInput(inputReference, out var input))
        {
            return Task.FromResult(false);
        }

        return _inputRouter.TryPressAsync(platform, input, cancellationToken);
    }

    public Task<bool> TryHandlePointerUpAsync(FruitMachinePlatformType platform, MachineInputReference inputReference, bool isFocused, CancellationToken cancellationToken)
    {
        if (!isFocused || !TryResolveInput(inputReference, out var input))
        {
            return Task.FromResult(false);
        }

        return _inputRouter.TryReleaseAsync(platform, input, cancellationToken);
    }

    private bool TryResolveInput(MachineInputReference inputReference, out InputDefinitionModel input)
    {
        input = null!;
        return inputReference.Reference.Kind == MachineObjectKind.Input
            && !inputReference.Reference.IsEmpty
            && _inputsByMachineInputId.TryGetValue(inputReference.Reference.Id, out input);
    }
}
