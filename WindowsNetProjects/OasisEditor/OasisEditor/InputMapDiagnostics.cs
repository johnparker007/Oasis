namespace OasisEditor;

public sealed record InputMapDiagnostic(
    string Code,
    string Message,
    string InputDefinitionId,
    InputMapDiagnosticSeverity Severity);

public enum InputMapDiagnosticSeverity
{
    Info,
    Warning,
    Error
}

public interface IInputMapDiagnosticsService
{
    IReadOnlyList<InputMapDiagnostic> Analyze(FruitMachinePlatformType platform, IReadOnlyList<InputDefinitionModel> inputs);
}

public sealed class InputMapDiagnosticsService : IInputMapDiagnosticsService
{
    private readonly IMameInputPortResolver _resolver;

    public InputMapDiagnosticsService(IMameInputPortResolver resolver)
    {
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
    }

    public IReadOnlyList<InputMapDiagnostic> Analyze(FruitMachinePlatformType platform, IReadOnlyList<InputDefinitionModel> inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);

        var diagnostics = new List<InputMapDiagnostic>();
        var keyToInputs = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var input in inputs)
        {
            if (input is null || string.IsNullOrWhiteSpace(input.Id))
            {
                continue;
            }

            if (!_resolver.TryResolve(platform, input, out _))
            {
                diagnostics.Add(new InputMapDiagnostic(
                    "input.unresolved_target",
                    $"Input '{input.Name}' cannot resolve MAME tag/mask for platform {platform}.",
                    input.Id,
                    InputMapDiagnosticSeverity.Warning));
            }

            if (!string.IsNullOrWhiteSpace(input.KeyboardShortcut))
            {
                if (!keyToInputs.TryGetValue(input.KeyboardShortcut, out var ids))
                {
                    ids = [];
                    keyToInputs[input.KeyboardShortcut] = ids;
                }

                ids.Add(input.Id);
            }
        }

        foreach (var pair in keyToInputs)
        {
            if (pair.Value.Count <= 1)
            {
                continue;
            }

            foreach (var inputId in pair.Value)
            {
                diagnostics.Add(new InputMapDiagnostic(
                    "input.duplicate_shortcut",
                    $"Keyboard shortcut '{pair.Key}' is used by multiple input definitions.",
                    inputId,
                    InputMapDiagnosticSeverity.Warning));
            }
        }

        return diagnostics;
    }
}
