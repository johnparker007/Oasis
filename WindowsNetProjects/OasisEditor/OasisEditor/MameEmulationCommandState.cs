namespace OasisEditor;

public sealed class MameEmulationCommandState
{
    public bool CanStart { get; init; }
    public bool CanStop { get; init; }
    public bool CanPause { get; init; }
    public bool CanResume { get; init; }
}

public static class MameEmulationCommandStateEvaluator
{
    public static MameEmulationCommandState Evaluate(bool hasLoadedProject, MameEmulationState state)
    {
        var canStart = hasLoadedProject && state is MameEmulationState.Stopped or MameEmulationState.Failed;
        var canStop = state is MameEmulationState.Starting or MameEmulationState.Running or MameEmulationState.Paused or MameEmulationState.Stopping;
        var canPause = state == MameEmulationState.Running;
        var canResume = state == MameEmulationState.Paused;

        return new MameEmulationCommandState
        {
            CanStart = canStart,
            CanStop = canStop,
            CanPause = canPause,
            CanResume = canResume
        };
    }
}
