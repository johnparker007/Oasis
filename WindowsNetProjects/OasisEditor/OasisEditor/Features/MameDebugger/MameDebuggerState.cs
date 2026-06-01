namespace OasisEditor.Features.MameDebugger;

public enum MameDebuggerExecutionState
{
    Unknown,
    Running,
    Stopped
}

public sealed class MameDebuggerState
{
    private readonly object _gate = new();

    public bool IsDebuggerLaunchActive { get; private set; }
    public bool IsDebuggerAvailable { get; private set; }
    public MameDebuggerExecutionState ExecutionState { get; private set; } = MameDebuggerExecutionState.Unknown;
    public string? CurrentCpu { get; private set; }
    public long? CurrentPc { get; private set; }

    public void MarkLaunchMode(bool isDebuggerLaunchActive)
    {
        lock (_gate)
        {
            IsDebuggerLaunchActive = isDebuggerLaunchActive;
            if (!isDebuggerLaunchActive)
            {
                IsDebuggerAvailable = false;
                ExecutionState = MameDebuggerExecutionState.Unknown;
                CurrentCpu = null;
                CurrentPc = null;
            }
        }
    }

    public MameDebuggerStateSnapshot Snapshot()
    {
        lock (_gate)
        {
            return new MameDebuggerStateSnapshot(IsDebuggerLaunchActive, IsDebuggerAvailable, ExecutionState, CurrentCpu, CurrentPc);
        }
    }

    public bool ApplyStatus(MameDebuggerStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);
        lock (_gate)
        {
            var changed = IsDebuggerAvailable != status.Available
                || ExecutionState != status.State
                || CurrentCpu != status.Cpu
                || CurrentPc != status.Pc;

            IsDebuggerAvailable = status.Available;
            ExecutionState = status.State;
            CurrentCpu = status.Cpu;
            CurrentPc = status.Pc;
            return changed;
        }
    }

    public bool ApplyEvent(MameDebuggerEvent debuggerEvent)
    {
        ArgumentNullException.ThrowIfNull(debuggerEvent);
        lock (_gate)
        {
            var nextState = debuggerEvent.Event switch
            {
                "running" => MameDebuggerExecutionState.Running,
                "stopped" => MameDebuggerExecutionState.Stopped,
                _ => ExecutionState
            };

            var changed = ExecutionState != nextState
                || (debuggerEvent.Cpu is not null && CurrentCpu != debuggerEvent.Cpu)
                || (debuggerEvent.Pc.HasValue && CurrentPc != debuggerEvent.Pc);

            ExecutionState = nextState;
            if (debuggerEvent.Cpu is not null)
            {
                CurrentCpu = debuggerEvent.Cpu;
            }

            if (debuggerEvent.Pc.HasValue)
            {
                CurrentPc = debuggerEvent.Pc;
            }

            return changed;
        }
    }
}

public sealed record MameDebuggerStateSnapshot(
    bool IsDebuggerLaunchActive,
    bool IsDebuggerAvailable,
    MameDebuggerExecutionState ExecutionState,
    string? CurrentCpu,
    long? CurrentPc);
