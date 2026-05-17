namespace OasisEditor;

public sealed class PlayViewInputRouter
{
    private readonly IMameInputCommandService _commandService;
    private readonly IMameProcessRunner _processRunner;
    private readonly HashSet<string> _activeInputIds = [];

    public PlayViewInputRouter(IMameInputCommandService commandService, IMameProcessRunner processRunner)
    {
        _commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));
        _processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
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

        var wrote = await _commandService.TrySendInputStateAsync(_processRunner, platform, inputDefinition, isPressed: true, cancellationToken).ConfigureAwait(false);
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

        return await _commandService.TrySendInputStateAsync(_processRunner, platform, inputDefinition, isPressed: false, cancellationToken).ConfigureAwait(false);
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
                continue;
            }

            _activeInputIds.Remove(inputId);
            var wrote = await _commandService.TrySendInputStateAsync(_processRunner, platform, definition, isPressed: false, cancellationToken).ConfigureAwait(false);
            if (wrote)
            {
                released++;
            }
        }

        return released;
    }
}
