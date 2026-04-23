namespace OasisEditor.Commands;

/// <summary>
/// Executes editor commands and provides undo/redo over recorded history.
/// </summary>
public sealed class CommandService
{
    private readonly CommandHistory _history;

    public CommandService()
        : this(new CommandHistory())
    {
    }

    public CommandService(CommandHistory history)
    {
        _history = history ?? throw new ArgumentNullException(nameof(history));
    }

    public CommandHistory History => _history;

    public bool CanUndo => _history.CanUndo;

    public bool CanRedo => _history.CanRedo;

    public void Execute(ICommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        command.Execute();
        _history.RecordExecuted(command);
    }

    public bool TryUndo()
    {
        if (!CanUndo)
        {
            return false;
        }

        var command = _history.GetUndoCandidate();
        command.Undo();
        _history.MarkUndone();
        return true;
    }

    public bool TryRedo()
    {
        if (!CanRedo)
        {
            return false;
        }

        var command = _history.GetRedoCandidate();
        command.Execute();
        _history.MarkRedone();
        return true;
    }
}
