namespace OasisEditor;

public sealed class PlayViewKeyboardInputRouter
{
    private readonly PlayViewInputRouter _inputRouter;
    private readonly Dictionary<string, string> _shortcutToInputId;
    private readonly Dictionary<string, InputDefinitionModel> _inputById;

    public PlayViewKeyboardInputRouter(PlayViewInputRouter inputRouter, IEnumerable<InputDefinitionModel> inputDefinitions)
    {
        _inputRouter = inputRouter ?? throw new ArgumentNullException(nameof(inputRouter));
        ArgumentNullException.ThrowIfNull(inputDefinitions);

        _shortcutToInputId = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        _inputById = new Dictionary<string, InputDefinitionModel>(StringComparer.Ordinal);

        foreach (var input in inputDefinitions)
        {
            if (input is null || string.IsNullOrWhiteSpace(input.Id))
            {
                continue;
            }

            _inputById[input.Id] = input;

            var normalizedShortcut = MfmeShortcutKeyMapper.NormalizeShortcutForRouting(input.KeyboardShortcut);
            if (!string.IsNullOrWhiteSpace(normalizedShortcut) && !_shortcutToInputId.ContainsKey(normalizedShortcut))
            {
                _shortcutToInputId[normalizedShortcut] = input.Id;
            }
        }
    }

    public Task<bool> TryHandleKeyDownAsync(FruitMachinePlatformType platform, string keyboardShortcut, bool isFocused, bool isRepeat, CancellationToken cancellationToken)
    {
        if (!isFocused || isRepeat || string.IsNullOrWhiteSpace(keyboardShortcut))
        {
            return Task.FromResult(false);
        }

        if (!_shortcutToInputId.TryGetValue(keyboardShortcut, out var inputId) || !_inputById.TryGetValue(inputId, out var input))
        {
            return Task.FromResult(false);
        }

        return _inputRouter.TryPressAsync(platform, input, cancellationToken);
    }

    public Task<bool> TryHandleKeyUpAsync(FruitMachinePlatformType platform, string keyboardShortcut, bool isFocused, CancellationToken cancellationToken)
    {
        if (!isFocused || string.IsNullOrWhiteSpace(keyboardShortcut))
        {
            return Task.FromResult(false);
        }

        if (!_shortcutToInputId.TryGetValue(keyboardShortcut, out var inputId) || !_inputById.TryGetValue(inputId, out var input))
        {
            return Task.FromResult(false);
        }

        return _inputRouter.TryReleaseAsync(platform, input, cancellationToken);
    }

    public Task<int> ReleaseAllActiveAsync(FruitMachinePlatformType platform, CancellationToken cancellationToken)
    {
        return _inputRouter.ReleaseAllAsync(platform, _inputById, cancellationToken);
    }
}
