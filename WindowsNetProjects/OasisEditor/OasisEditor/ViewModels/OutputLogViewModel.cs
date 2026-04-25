using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace OasisEditor;

public sealed class OutputLogViewModel : INotifyPropertyChanged
{
    private OutputLogEntry? _lastEntry;

    public event PropertyChangedEventHandler? PropertyChanged;

    public OutputLogViewModel()
    {
        OutputEntries = new ObservableCollection<OutputLogEntry>();
        ClearOutputCommand = new RelayCommand(ClearOutput, CanClearOutput);
    }

    public ObservableCollection<OutputLogEntry> OutputEntries { get; }
    public ICommand ClearOutputCommand { get; }

    public OutputLogEntry? LastEntry
    {
        get => _lastEntry;
        private set
        {
            if (ReferenceEquals(_lastEntry, value))
            {
                return;
            }

            _lastEntry = value;
            OnPropertyChanged();
        }
    }

    public void AddOutputEntry(string message, OutputLogStatus status)
    {
        var entry = new OutputLogEntry(DateTime.Now, message, status);
        OutputEntries.Add(entry);
        LastEntry = entry;
        NotifyClearCommand();
    }

    private bool CanClearOutput()
    {
        return OutputEntries.Count > 0;
    }

    private void ClearOutput()
    {
        OutputEntries.Clear();
        LastEntry = null;
        AddOutputEntry("Output log cleared.", OutputLogStatus.Info);
    }

    public void NotifyClearCommand()
    {
        if (ClearOutputCommand is RelayCommand clearRelayCommand)
        {
            clearRelayCommand.RaiseCanExecuteChanged();
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
