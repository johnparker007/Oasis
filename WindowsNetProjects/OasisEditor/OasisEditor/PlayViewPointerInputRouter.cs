namespace OasisEditor;

public sealed class PlayViewPointerInputRouter
{
    private readonly PlayViewInputRouter _inputRouter;
    private readonly Dictionary<Guid, InputDefinitionModel> _inputsByVisualId;

    public PlayViewPointerInputRouter(PlayViewInputRouter inputRouter, IEnumerable<InputDefinitionModel> inputDefinitions)
    {
        _inputRouter = inputRouter ?? throw new ArgumentNullException(nameof(inputRouter));
        ArgumentNullException.ThrowIfNull(inputDefinitions);

        _inputsByVisualId = new Dictionary<Guid, InputDefinitionModel>();
        foreach (var input in inputDefinitions)
        {
            if (input?.LinkedVisualElementId is null)
            {
                continue;
            }

            if (!_inputsByVisualId.ContainsKey(input.LinkedVisualElementId.Value))
            {
                _inputsByVisualId[input.LinkedVisualElementId.Value] = input;
            }
        }
    }

    public Task<bool> TryHandlePointerDownAsync(FruitMachinePlatformType platform, Guid visualElementId, bool isFocused, CancellationToken cancellationToken)
    {
        if (!isFocused || !_inputsByVisualId.TryGetValue(visualElementId, out var input))
        {
            return Task.FromResult(false);
        }

        return _inputRouter.TryPressAsync(platform, input, cancellationToken);
    }

    public Task<bool> TryHandlePointerUpAsync(FruitMachinePlatformType platform, Guid visualElementId, bool isFocused, CancellationToken cancellationToken)
    {
        if (!isFocused || !_inputsByVisualId.TryGetValue(visualElementId, out var input))
        {
            return Task.FromResult(false);
        }

        return _inputRouter.TryReleaseAsync(platform, input, cancellationToken);
    }

    public Task<int> ReleaseAllActiveAsync(FruitMachinePlatformType platform, CancellationToken cancellationToken)
    {
        var byInputId = _inputsByVisualId.Values
            .Where(v => !string.IsNullOrWhiteSpace(v.Id))
            .GroupBy(v => v.Id, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        return _inputRouter.ReleaseAllAsync(platform, byInputId, cancellationToken);
    }
}
