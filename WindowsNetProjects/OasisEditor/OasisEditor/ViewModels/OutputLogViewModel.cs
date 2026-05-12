using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Data;
using System.Windows.Input;

namespace OasisEditor;

public sealed class OutputLogViewModel : INotifyPropertyChanged
{
    private readonly OutputLogDiskWriter _diskWriter;
    private OutputLogEntry? _lastEntry;
    private bool _showInfoLogs = true;
    private bool _showWarningLogs = true;
    private bool _showErrorLogs = true;

    public event PropertyChangedEventHandler? PropertyChanged;

    public OutputLogViewModel()
        : this(new OutputLogDiskWriter(MameRuntimePaths.EnsureManagedLogRootDirectory()))
    {
    }

    public OutputLogViewModel(OutputLogDiskWriter diskWriter)
    {
        _diskWriter = diskWriter;
        OutputEntries = new ObservableCollection<OutputLogEntry>();
        FilteredEntries = CollectionViewSource.GetDefaultView(OutputEntries);
        FilteredEntries.Filter = ShouldShowEntry;
        ClearOutputCommand = new RelayCommand(ClearOutput, CanClearOutput);
        _diskWriter.Initialize();
    }

    public ObservableCollection<OutputLogEntry> OutputEntries { get; }
    public ICollectionView FilteredEntries { get; }
    public ICommand ClearOutputCommand { get; }

    public bool ShowInfoLogs
    {
        get => _showInfoLogs;
        set => SetFilter(ref _showInfoLogs, value);
    }

    public bool ShowWarningLogs
    {
        get => _showWarningLogs;
        set => SetFilter(ref _showWarningLogs, value);
    }

    public bool ShowErrorLogs
    {
        get => _showErrorLogs;
        set => SetFilter(ref _showErrorLogs, value);
    }

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

        try
        {
            _diskWriter.Append(entry);
        }
        catch
        {
            // Keep logging failures non-fatal.
        }
    }

    public void NotifyClearCommand()
    {
        if (ClearOutputCommand is RelayCommand clearRelayCommand)
        {
            clearRelayCommand.RaiseCanExecuteChanged();
        }
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

    private bool ShouldShowEntry(object item)
    {
        if (item is not OutputLogEntry entry)
        {
            return false;
        }

        return entry.Status switch
        {
            OutputLogStatus.Info => ShowInfoLogs,
            OutputLogStatus.Warning => ShowWarningLogs,
            OutputLogStatus.Error => ShowErrorLogs,
            _ => true
        };
    }

    private void SetFilter(ref bool field, bool value, [CallerMemberName] string? propertyName = null)
    {
        if (field == value)
        {
            return;
        }

        field = value;
        OnPropertyChanged(propertyName);
        FilteredEntries.Refresh();
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
