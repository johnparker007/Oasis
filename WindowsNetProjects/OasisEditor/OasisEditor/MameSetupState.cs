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
    bool IsInProgress)
{
    public static MameSetupState NotStarted { get; } = new(MameSetupPhase.NotStarted, "Not started.", "Unknown", false);
}
