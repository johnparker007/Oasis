using System.Collections.Generic;

namespace OasisEditor.Commands;

/// <summary>
/// Tracks executed commands and the current position in history.
/// Undo/redo execution is handled by higher-level services.
/// </summary>
public sealed class CommandHistory
{
    private readonly List<ICommand> _entries = new();
    private int _nextIndex;

    /// <summary>
    /// Gets a snapshot view of all recorded commands in execution order.
    /// </summary>
    public IReadOnlyList<ICommand> Entries => _entries;

    /// <summary>
    /// Gets the number of commands currently recorded in history.
    /// </summary>
    public int Count => _entries.Count;

    /// <summary>
    /// Gets the index where the next executed command would be inserted.
    /// </summary>
    public int NextIndex => _nextIndex;

    public bool CanUndo => _nextIndex > 0;

    public bool CanRedo => _nextIndex < _entries.Count;

    /// <summary>
    /// Records a command that has already been executed.
    /// Any redo branch is discarded before appending.
    /// </summary>
    public void RecordExecuted(ICommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (_nextIndex < _entries.Count)
        {
            _entries.RemoveRange(_nextIndex, _entries.Count - _nextIndex);
        }

        _entries.Add(command);
        _nextIndex = _entries.Count;
    }

    public ICommand GetUndoCandidate()
    {
        if (!CanUndo)
        {
            throw new InvalidOperationException("No commands are available to undo.");
        }

        return _entries[_nextIndex - 1];
    }

    public bool TryGetUndoCandidate(out ICommand? command)
    {
        if (!CanUndo)
        {
            command = null;
            return false;
        }

        command = _entries[_nextIndex - 1];
        return true;
    }

    public ICommand GetRedoCandidate()
    {
        if (!CanRedo)
        {
            throw new InvalidOperationException("No commands are available to redo.");
        }

        return _entries[_nextIndex];
    }

    public bool TryGetRedoCandidate(out ICommand? command)
    {
        if (!CanRedo)
        {
            command = null;
            return false;
        }

        command = _entries[_nextIndex];
        return true;
    }

    public void MarkUndone()
    {
        if (!CanUndo)
        {
            throw new InvalidOperationException("No commands are available to undo.");
        }

        _nextIndex--;
    }

    public void MarkRedone()
    {
        if (!CanRedo)
        {
            throw new InvalidOperationException("No commands are available to redo.");
        }

        _nextIndex++;
    }

    public void Clear()
    {
        _entries.Clear();
        _nextIndex = 0;
    }
}
