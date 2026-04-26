namespace OasisEditor.Commands;

/// <summary>
/// Executes editor commands and provides undo/redo over recorded history.
/// </summary>
public sealed class CommandService
{
    private readonly CommandHistory _history;
    private readonly Guid? _documentId;

    public CommandService()
        : this(new CommandHistory(), null)
    {
    }

    public CommandService(CommandHistory history, Guid? documentId = null)
    {
        _history = history ?? throw new ArgumentNullException(nameof(history));
        _documentId = documentId;
    }

    public CommandHistory History => _history;
    public Guid? DocumentId => _documentId;

    public bool CanUndo => _history.CanUndo;

    public bool CanRedo => _history.CanRedo;

    public string? UndoDescription =>
        _history.TryGetUndoCandidate(out var command) ? command?.Description : null;

    public string? RedoDescription =>
        _history.TryGetRedoCandidate(out var command) ? command?.Description : null;

    public void Execute(ICommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        ValidateDocumentOwnership(command);

        command.Execute();
        if (command is IExecutionTrackedCommand executionTrackedCommand
            && !executionTrackedCommand.WasExecuted)
        {
            return;
        }

        _history.RecordExecuted(command);
    }

    public bool TryUndo()
    {
        if (!CanUndo)
        {
            return false;
        }

        var command = _history.GetUndoCandidate();
        ValidateDocumentOwnership(command);
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
        ValidateDocumentOwnership(command);
        command.Execute();
        if (command is IExecutionTrackedCommand executionTrackedCommand
            && !executionTrackedCommand.WasExecuted)
        {
            return false;
        }

        _history.MarkRedone();
        return true;
    }

    private void ValidateDocumentOwnership(ICommand command)
    {
        if (_documentId is null)
        {
            return;
        }

        if (command is not IDocumentCommand documentCommand)
        {
            throw new InvalidOperationException("Document-scoped command service can only execute document commands.");
        }

        if (documentCommand.DocumentId != _documentId.Value)
        {
            throw new InvalidOperationException("Command document does not match this command history.");
        }
    }
}
