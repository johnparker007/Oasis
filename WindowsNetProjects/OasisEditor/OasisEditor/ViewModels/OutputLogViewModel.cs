using System.Collections.ObjectModel;
using System.Windows.Input;

namespace OasisEditor;

public sealed class OutputLogViewModel
{
    public OutputLogViewModel()
    {
        OutputEntries = new ObservableCollection<string>();
        ClearOutputCommand = new RelayCommand(ClearOutput, CanClearOutput);
    }

    public ObservableCollection<string> OutputEntries { get; }
    public ICommand ClearOutputCommand { get; }

    public void AddOutputEntry(string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        OutputEntries.Add($"[{timestamp}] {message}");
        NotifyClearCommand();
    }

    private bool CanClearOutput()
    {
        return OutputEntries.Count > 0;
    }

    private void ClearOutput()
    {
        OutputEntries.Clear();
        AddOutputEntry("Output log cleared.");
    }

    public void NotifyClearCommand()
    {
        if (ClearOutputCommand is RelayCommand clearRelayCommand)
        {
            clearRelayCommand.RaiseCanExecuteChanged();
        }
    }
}
