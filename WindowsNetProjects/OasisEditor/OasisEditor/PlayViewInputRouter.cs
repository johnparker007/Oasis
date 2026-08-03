namespace OasisEditor;

public sealed class PlayViewInputRouter
{
    private readonly IEmulationBackend _backend;
    private readonly HashSet<string> _activeInputIds = [];

    public PlayViewInputRouter(IEmulationBackend backend)
    {
        _backend = backend ?? throw new ArgumentNullException(nameof(backend));
    }

    public async Task<bool> TryPressAsync(FruitMachinePlatformType platform, InputDefinitionModel inputDefinition, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(inputDefinition);

        if (string.IsNullOrWhiteSpace(inputDefinition.Id))
        {
            return false;
        }

        if (!_activeInputIds.Add(inputDefinition.Id))
        {
            return false;
        }

        var wrote = await TrySendInputStateAsync(platform, inputDefinition, isPressed: true, cancellationToken).ConfigureAwait(false);
        if (!wrote)
        {
            _activeInputIds.Remove(inputDefinition.Id);
        }

        return wrote;
    }

    public async Task<bool> TryReleaseAsync(FruitMachinePlatformType platform, InputDefinitionModel inputDefinition, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(inputDefinition);

        if (string.IsNullOrWhiteSpace(inputDefinition.Id) || !_activeInputIds.Remove(inputDefinition.Id))
        {
            return false;
        }

        return await TrySendInputStateAsync(platform, inputDefinition, isPressed: false, cancellationToken).ConfigureAwait(false);
    }

    public async Task<int> ReleaseAllAsync(FruitMachinePlatformType platform, IReadOnlyDictionary<string, InputDefinitionModel> inputDefinitionsById, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(inputDefinitionsById);

        if (_activeInputIds.Count == 0)
        {
            return 0;
        }

        var released = 0;
        foreach (var inputId in _activeInputIds.ToArray())
        {
            if (!inputDefinitionsById.TryGetValue(inputId, out var definition))
            {
                _activeInputIds.Remove(inputId);
                continue;
            }

            _activeInputIds.Remove(inputId);
            var wrote = await TrySendInputStateAsync(platform, definition, isPressed: false, cancellationToken).ConfigureAwait(false);
            if (wrote)
            {
                released++;
            }
        }

        return released;
    }

    private async Task<bool> TrySendInputStateAsync(FruitMachinePlatformType platform, InputDefinitionModel inputDefinition, bool isPressed, CancellationToken cancellationToken)
    {
        try
        {
            if (inputDefinition.CoinInput)
            {
                if (inputDefinition.CoinChannel is not (>= 0 and <= 5) || inputDefinition.CoinValue is not (>= 0 and <= 12))
                    return false;
                if (isPressed)
                    await _backend.InsertCoinAsync(inputDefinition, cancellationToken).ConfigureAwait(false);
                else
                    await _backend.ReleaseCoinAsync(inputDefinition, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await _backend.SetInputStateAsync(inputDefinition, isPressed, cancellationToken).ConfigureAwait(false);
            }
            return true;
        }
        catch
        {
            return false;
        }
    }
}
