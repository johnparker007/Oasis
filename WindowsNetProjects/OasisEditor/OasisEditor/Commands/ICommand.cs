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
