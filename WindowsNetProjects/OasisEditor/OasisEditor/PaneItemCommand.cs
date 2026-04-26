using System.Windows.Input;

namespace OasisEditor;

public sealed class PaneItemCommand<TItem> : ICommand
    where TItem : class
{
    private readonly Func<TItem?> _selectedItemAccessor;
    private readonly Action<TItem> _execute;
    private readonly Predicate<TItem>? _canExecuteItem;

    public PaneItemCommand(
        Func<TItem?> selectedItemAccessor,
        Action<TItem> execute,
        Predicate<TItem>? canExecuteItem = null)
    {
        _selectedItemAccessor = selectedItemAccessor;
        _execute = execute;
        _canExecuteItem = canExecuteItem;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter)
    {
        var item = parameter as TItem ?? _selectedItemAccessor();
        return item is not null && (_canExecuteItem?.Invoke(item) ?? true);
    }

    public void Execute(object? parameter)
    {
        var item = parameter as TItem ?? _selectedItemAccessor();
        if (item is null)
        {
            return;
        }

        if (_canExecuteItem is not null && !_canExecuteItem(item))
        {
            return;
        }

        _execute(item);
    }

    public void RaiseCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
