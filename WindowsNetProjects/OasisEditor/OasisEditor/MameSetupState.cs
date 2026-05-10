namespace OasisEditor;

public enum MameSetupPhase
{
    NotStarted,
    Validating,
    Ready,
    NeedsAttention,
    Failed
}

public sealed record MameSetupState(
    MameSetupPhase Phase,
    string Summary,
    string? LatestKnownVersion,
    DateTimeOffset TimestampUtc);
