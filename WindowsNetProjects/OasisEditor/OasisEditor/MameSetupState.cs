namespace OasisEditor;

public enum MameSetupPhase
{
    NotStarted,
    Validating,
    Ready,
    NeedsAttention
}

public sealed record MameSetupState(
    MameSetupPhase Phase,
    string Summary,
    string? LatestKnownVersion,
    bool IsInProgress,
    IReadOnlyList<string>? Issues = null)
{
    public static MameSetupState NotStarted { get; } = new(MameSetupPhase.NotStarted, "Not started.", "Unknown", false, []);
}
