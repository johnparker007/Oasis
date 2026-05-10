namespace OasisEditor;

public sealed record MameSetupState(
    MameSetupPhase Phase,
    string Summary,
    string LatestKnownVersion,
    bool IsInProgress)
{
    public static MameSetupState NotStarted { get; } = new(MameSetupPhase.NotStarted, "Not started.", "Unknown", false);
}
