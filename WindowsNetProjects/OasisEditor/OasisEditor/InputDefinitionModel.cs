namespace OasisEditor;

public sealed class InputDefinitionModel
{
    public required string Id { get; init; }
    public string Name { get; set; } = string.Empty;
    public InputDefinitionKind Kind { get; set; } = InputDefinitionKind.Unknown;
    public string ButtonNumber { get; set; } = string.Empty;
    public bool CoinInput { get; set; }
    public bool Inverted { get; set; }
    public string RawMfmeShortcut { get; set; } = string.Empty;
    public string KeyboardShortcut { get; set; } = string.Empty;
    public Guid? LinkedVisualElementId { get; set; }
    public string MamePortTag { get; set; } = string.Empty;
    public string MameMask { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
}
