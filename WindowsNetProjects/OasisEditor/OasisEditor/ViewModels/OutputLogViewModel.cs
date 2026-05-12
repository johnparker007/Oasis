using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
    private IReadOnlyList<OutputLogEntry> _selectedEntries = Array.Empty<OutputLogEntry>();

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
    public string CurrentLogPath => _diskWriter.CurrentLogPath;
    public string LogDirectoryPath => Path.GetDirectoryName(CurrentLogPath) ?? string.Empty;
    public string CopySelectionHeader => SelectedEntries.Count == 1 ? "Copy Row" : "Copy Rows";

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

    public IReadOnlyList<OutputLogEntry> SelectedEntries
    {
        get => _selectedEntries;
        private set
        {
            _selectedEntries = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CopySelectionHeader));
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

    public void UpdateSelectedEntries(IEnumerable<OutputLogEntry> selectedEntries)
    {
        SelectedEntries = selectedEntries
            .Where(entry => ShouldShowEntry(entry))
            .ToList();
    }

    public string BuildClipboardTextForSelection()
    {
        return string.Join(Environment.NewLine, SelectedEntries.Select(entry => entry.ToClipboardLine()));
    }

    public bool TryOpenCurrentLog(out string? failureReason)
    {
        failureReason = null;
        if (!File.Exists(CurrentLogPath))
        {
            failureReason = $"Cannot open log; file does not exist: {CurrentLogPath}";
            return false;
        }

        return TryLaunch(new ProcessStartInfo(CurrentLogPath) { UseShellExecute = true }, out failureReason);
    }

    public bool TryShowLogInExplorer(out string? failureReason)
    {
        failureReason = null;
        if (!Directory.Exists(LogDirectoryPath))
        {
            failureReason = $"Cannot show log directory; directory does not exist: {LogDirectoryPath}";
            return false;
        }

        return TryLaunch(new ProcessStartInfo("explorer.exe", $"\"{LogDirectoryPath}\"") { UseShellExecute = true }, out failureReason);
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

    private static bool TryLaunch(ProcessStartInfo startInfo, out string? failureReason)
    {
        failureReason = null;
        try
        {
            Process.Start(startInfo);
            return true;
        }
        catch (Exception ex)
        {
            failureReason = ex.Message;
            return false;
        }
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
