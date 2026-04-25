namespace OasisEditor.Commands;

/// <summary>
/// Represents a reversible editor mutation that can be tracked by command history.
/// </summary>
public interface ICommand
{
    /// <summary>
    /// A short, user-facing description for logs and undo/redo UI.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Applies the command mutation.
    /// </summary>
    void Execute();

    /// <summary>
    /// Reverts the command mutation.
    /// </summary>
    void Undo();
}

/// <summary>
/// Represents a command scoped to a specific document.
/// </summary>
public interface IDocumentCommand : ICommand
{
    Guid DocumentId { get; }
}

/// <summary>
/// Optional command contract indicating whether the last execution produced a real mutation.
/// Command services may use this to skip recording no-op executions in history.
/// </summary>
public interface IExecutionTrackedCommand : ICommand
{
    bool WasExecuted { get; }
}
