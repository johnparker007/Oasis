namespace OasisEditor;

public sealed class MameEmulationCommandState
{
    public bool CanStart { get; init; }
    public bool CanStartAndLoadState { get; init; }
    public bool CanStop { get; init; }
    public bool CanSaveStateAndExit { get; init; }
    public bool CanLoadState { get; init; }
    public bool CanSaveState { get; init; }
    public bool CanPause { get; init; }
    public bool CanResume { get; init; }
    public bool CanTogglePause { get; init; }
    public bool CanSetThrottle { get; init; }
    public bool CanReset { get; init; }
}

public static class MameEmulationCommandStateEvaluator
{
    public static MameEmulationCommandState Evaluate(bool hasLoadedProject, MameEmulationState state)
    {
        var canStart = hasLoadedProject && state is MameEmulationState.Stopped or MameEmulationState.Failed;
        var canControlRunningMachine = state is MameEmulationState.Running or MameEmulationState.Paused;
        var canStop = state is MameEmulationState.Starting or MameEmulationState.Running or MameEmulationState.Paused or MameEmulationState.Stopping;
        var canPause = state == MameEmulationState.Running;
        var canResume = state == MameEmulationState.Paused;

        return new MameEmulationCommandState
        {
            CanStart = canStart,
            CanStartAndLoadState = canStart,
            CanStop = canStop,
            CanSaveStateAndExit = canControlRunningMachine,
            CanLoadState = canControlRunningMachine,
            CanSaveState = canControlRunningMachine,
            CanPause = canPause,
            CanResume = canResume,
            CanTogglePause = canControlRunningMachine,
            CanSetThrottle = canControlRunningMachine,
            CanReset = canControlRunningMachine
        };
    }
}
