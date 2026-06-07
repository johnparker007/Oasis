namespace OasisEditor;

public enum PlayInputTargetKind
{
    None = 0,
    PanelVisualElement,
    MachineInput
}

public readonly record struct PlayInputTarget
{
    private PlayInputTarget(PlayInputTargetKind kind, Guid panelVisualElementId, MachineInputReference machineInputReference)
    {
        Kind = kind;
        PanelVisualElementId = panelVisualElementId;
        MachineInputReference = machineInputReference;
    }

    public PlayInputTargetKind Kind { get; }
    public Guid PanelVisualElementId { get; }
    public MachineInputReference MachineInputReference { get; }

    public static PlayInputTarget ForPanelVisualElement(Guid visualElementId)
    {
        return new PlayInputTarget(PlayInputTargetKind.PanelVisualElement, visualElementId, default);
    }

    public static PlayInputTarget ForMachineInput(MachineInputReference inputReference)
    {
        return new PlayInputTarget(PlayInputTargetKind.MachineInput, Guid.Empty, inputReference);
    }

    public override string ToString()
    {
        return Kind switch
        {
            PlayInputTargetKind.PanelVisualElement => $"panel visual '{PanelVisualElementId}'",
            PlayInputTargetKind.MachineInput => $"machine input '{MachineInputReference}'",
            _ => "none"
        };
    }
}

public sealed class PlayViewInputDispatcher
{
    private readonly PlayViewInputRouter _inputRouter;
    private readonly PlayViewKeyboardInputRouter _keyboardInputRouter;
    private readonly PlayViewPointerInputRouter _panelPointerInputRouter;
    private readonly FacePlayViewPointerInputRouter _facePointerInputRouter;
    private readonly Dictionary<string, InputDefinitionModel> _inputDefinitionsById;

    public PlayViewInputDispatcher(PlayViewInputRouter inputRouter, IEnumerable<InputDefinitionModel> inputDefinitions)
    {
        _inputRouter = inputRouter ?? throw new ArgumentNullException(nameof(inputRouter));
        ArgumentNullException.ThrowIfNull(inputDefinitions);

        var definitions = inputDefinitions.Where(input => input is not null).ToArray();
        _keyboardInputRouter = new PlayViewKeyboardInputRouter(_inputRouter, definitions);
        _panelPointerInputRouter = new PlayViewPointerInputRouter(_inputRouter, definitions);
        _facePointerInputRouter = new FacePlayViewPointerInputRouter(_inputRouter, definitions);
        _inputDefinitionsById = definitions
            .Where(definition => !string.IsNullOrWhiteSpace(definition.Id))
            .ToDictionary(definition => definition.Id, definition => definition, StringComparer.Ordinal);
    }

    public bool CanResolveShortcut(string keyboardShortcut)
    {
        return _keyboardInputRouter.CanResolveShortcut(keyboardShortcut);
    }

    public Task<bool> TryHandleKeyDownAsync(FruitMachinePlatformType platform, string keyboardShortcut, bool isFocused, bool isRepeat, CancellationToken cancellationToken)
    {
        return _keyboardInputRouter.TryHandleKeyDownAsync(platform, keyboardShortcut, isFocused, isRepeat, cancellationToken);
    }

    public Task<bool> TryHandleKeyUpAsync(FruitMachinePlatformType platform, string keyboardShortcut, bool isFocused, CancellationToken cancellationToken)
    {
        return _keyboardInputRouter.TryHandleKeyUpAsync(platform, keyboardShortcut, isFocused, cancellationToken);
    }

    public Task<bool> TryHandlePointerDownAsync(FruitMachinePlatformType platform, PlayInputTarget inputTarget, bool isFocused, CancellationToken cancellationToken)
    {
        return inputTarget.Kind switch
        {
            PlayInputTargetKind.PanelVisualElement => _panelPointerInputRouter.TryHandlePointerDownAsync(platform, inputTarget.PanelVisualElementId, isFocused, cancellationToken),
            PlayInputTargetKind.MachineInput => _facePointerInputRouter.TryHandlePointerDownAsync(platform, inputTarget.MachineInputReference, isFocused, cancellationToken),
            _ => Task.FromResult(false)
        };
    }

    public Task<bool> TryHandlePointerUpAsync(FruitMachinePlatformType platform, PlayInputTarget inputTarget, bool isFocused, CancellationToken cancellationToken)
    {
        return inputTarget.Kind switch
        {
            PlayInputTargetKind.PanelVisualElement => _panelPointerInputRouter.TryHandlePointerUpAsync(platform, inputTarget.PanelVisualElementId, isFocused, cancellationToken),
            PlayInputTargetKind.MachineInput => _facePointerInputRouter.TryHandlePointerUpAsync(platform, inputTarget.MachineInputReference, isFocused, cancellationToken),
            _ => Task.FromResult(false)
        };
    }

    public Task<int> ReleaseAllActiveAsync(FruitMachinePlatformType platform, CancellationToken cancellationToken)
    {
        return _inputRouter.ReleaseAllAsync(platform, _inputDefinitionsById, cancellationToken);
    }
}
