namespace OasisEditor;

public sealed class PlayViewInputRouter
{
    private readonly IEmulationBackend _backend;
    private readonly HashSet<string> _activeInputIds = [];
    private readonly Action<string>? _inputLogger;

    public PlayViewInputRouter(IEmulationBackend backend, Action<string>? inputLogger = null)
    {
        _backend = backend ?? throw new ArgumentNullException(nameof(backend));
        _inputLogger = inputLogger;
    }

    public async Task<bool> TryPressAsync(FruitMachinePlatformType platform, InputDefinitionModel inputDefinition, CancellationToken cancellationToken, string source = "shared")
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
        if (wrote)
            LogRoute(source, inputDefinition, true);
        if (!wrote)
        {
            _activeInputIds.Remove(inputDefinition.Id);
        }

        return wrote;
    }

    public async Task<bool> TryReleaseAsync(FruitMachinePlatformType platform, InputDefinitionModel inputDefinition, CancellationToken cancellationToken, string source = "shared")
    {
        ArgumentNullException.ThrowIfNull(inputDefinition);

        if (string.IsNullOrWhiteSpace(inputDefinition.Id) || !_activeInputIds.Remove(inputDefinition.Id))
        {
            return false;
        }

        var wrote = await TrySendInputStateAsync(platform, inputDefinition, isPressed: false, cancellationToken).ConfigureAwait(false);
        if (wrote)
            LogRoute(source, inputDefinition, false);
        return wrote;
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
            await _backend.SetInputStateAsync(inputDefinition, isPressed, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void LogRoute(string source, InputDefinitionModel input, bool pressed) =>
        _inputLogger?.Invoke($"[Input] source={source} name=\"{input.Name}\" coin={input.CoinInput.ToString().ToLowerInvariant()} switch={input.ButtonNumber} state={(pressed ? "pressed" : "released")}");
}
