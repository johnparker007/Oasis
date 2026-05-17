namespace OasisEditor;

public interface IMameInputCommandService
{
    Task<bool> TrySendInputStateAsync(IMameProcessRunner processRunner, FruitMachinePlatformType platform, InputDefinitionModel inputDefinition, bool isPressed, CancellationToken cancellationToken);
}

public sealed class MameInputCommandService : IMameInputCommandService
{
    private readonly IMameInputPortResolver _inputPortResolver;

    public MameInputCommandService(IMameInputPortResolver inputPortResolver)
    {
        _inputPortResolver = inputPortResolver ?? throw new ArgumentNullException(nameof(inputPortResolver));
    }

    public async Task<bool> TrySendInputStateAsync(IMameProcessRunner processRunner, FruitMachinePlatformType platform, InputDefinitionModel inputDefinition, bool isPressed, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(processRunner);
        ArgumentNullException.ThrowIfNull(inputDefinition);

        if (!_inputPortResolver.TryResolve(platform, inputDefinition, out var target))
        {
            return false;
        }

        var state = isPressed ? "1" : "0";
        var command = $"set_input_value {target.Tag} {target.Mask} {state}";
        await processRunner.WriteStandardInputAsync(command, cancellationToken).ConfigureAwait(false);
        return true;
    }
}
