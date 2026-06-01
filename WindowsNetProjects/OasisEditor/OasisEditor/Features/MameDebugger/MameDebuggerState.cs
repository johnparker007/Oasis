namespace OasisEditor.Features.MameDebugger;

public sealed class MameDebuggerState
{
    private readonly object _gate = new();

    public MameDebuggerExecutionState ExecutionState { get; private set; } = MameDebuggerExecutionState.Unknown;
    public string? CurrentCpu { get; private set; }
    public long? ProgramCounter { get; private set; }

    public void ApplyStatus(MameDebuggerStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);

        lock (_gate)
        {
            ExecutionState = status.ExecutionState;
            CurrentCpu = status.CurrentCpu;
            ProgramCounter = status.ProgramCounter;
        }
    }

    public void ApplyEvent(MameDebuggerEvent debuggerEvent)
    {
        ArgumentNullException.ThrowIfNull(debuggerEvent);

        lock (_gate)
        {
            ExecutionState = debuggerEvent.ExecutionState;
            CurrentCpu = debuggerEvent.Cpu ?? CurrentCpu;
            ProgramCounter = debuggerEvent.ProgramCounter;
        }
    }
}
