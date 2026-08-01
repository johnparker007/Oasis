using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;

namespace OasisEditor;

public sealed class OutputLogViewModel : INotifyPropertyChanged
{
    private readonly OutputLogDiskWriter _diskWriter;
    private readonly IOutputLogShellLauncher _shellLauncher;
    private OutputLogEntry? _lastEntry;
    private bool _showInfoLogs = true;
    private bool _showWarningLogs = true;
    private bool _showErrorLogs = true;
    private bool _autoScroll = true;
    private string _searchText = string.Empty;
    private string _searchTextNormalized = string.Empty;
    private IReadOnlyList<OutputLogEntry> _selectedEntries = Array.Empty<OutputLogEntry>();
    private Func<IRawInputDiagnosticBackend?>? _rawInputBackendProvider;
    private string _rawSwitchIndexText = "0";
    private bool _isRawSwitchAsserted;

    public event PropertyChangedEventHandler? PropertyChanged;

    public OutputLogViewModel()
        : this(new OutputLogDiskWriter(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OasisEditor", "Logs")), new OutputLogShellLauncher())
    {
    }

    public OutputLogViewModel(OutputLogDiskWriter diskWriter, IOutputLogShellLauncher? shellLauncher = null)
    {
        _diskWriter = diskWriter;
        _shellLauncher = shellLauncher ?? new OutputLogShellLauncher();
        OutputEntries = new ObservableCollection<OutputLogEntry>();
        FilteredEntries = CollectionViewSource.GetDefaultView(OutputEntries);
        FilteredEntries.Filter = ShouldShowEntry;
        ClearOutputCommand = new RelayCommand(ClearOutput, CanClearOutput);
        PressRawSwitchCommand = new RelayCommand(() => SetRawSwitch(true), CanUseRawSwitchProbe);
        ReleaseRawSwitchCommand = new RelayCommand(() => SetRawSwitch(false), CanUseRawSwitchProbe);
        PulseRawSwitchCommand = new RelayCommand(PulseRawSwitch, CanUseRawSwitchProbe);
        _diskWriter.Initialize();
    }

    public ObservableCollection<OutputLogEntry> OutputEntries { get; }
    public ICollectionView FilteredEntries { get; }
    public ICommand ClearOutputCommand { get; }
    public ICommand PressRawSwitchCommand { get; }
    public ICommand ReleaseRawSwitchCommand { get; }
    public ICommand PulseRawSwitchCommand { get; }
    public string RawSwitchIndexText
    {
        get => _rawSwitchIndexText;
        set
        {
            if (string.Equals(_rawSwitchIndexText, value, StringComparison.Ordinal)) return;
            _rawSwitchIndexText = value ?? string.Empty;
            IsRawSwitchAsserted = false;
            OnPropertyChanged();
            NotifyRawInputAvailabilityChanged();
        }
    }
    public bool IsRawSwitchAsserted
    {
        get => _isRawSwitchAsserted;
        private set { if (_isRawSwitchAsserted != value) { _isRawSwitchAsserted = value; OnPropertyChanged(); } }
    }
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

    public string SearchText
    {
        get => _searchText;
        set
        {
            var normalized = value?.Trim() ?? string.Empty;
            if (string.Equals(_searchText, normalized, StringComparison.Ordinal))
            {
                return;
            }

            _searchText = normalized;
            _searchTextNormalized = normalized.ToUpperInvariant();
            OnPropertyChanged();
            FilteredEntries.Refresh();
        }
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

    public bool AutoScroll
    {
        get => _autoScroll;
        set
        {
            if (_autoScroll == value)
            {
                return;
            }

            _autoScroll = value;
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

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            dispatcher.Invoke(() => AddEntryOnUiThread(entry));
        }
        else
        {
            AddEntryOnUiThread(entry);
        }

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

    public void ConfigureRawInputProbe(Func<IRawInputDiagnosticBackend?> backendProvider)
    {
        _rawInputBackendProvider = backendProvider ?? throw new ArgumentNullException(nameof(backendProvider));
        NotifyRawInputAvailabilityChanged();
    }

    public void NotifyRawInputAvailabilityChanged()
    {
        (PressRawSwitchCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ReleaseRawSwitchCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (PulseRawSwitchCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    private bool CanUseRawSwitchProbe() => TryGetRawSwitchIndex(out _)
        && _rawInputBackendProvider?.Invoke()?.IsRawInputDiagnosticAvailable == true;

    private bool TryGetRawSwitchIndex(out int switchIndex) =>
        int.TryParse(RawSwitchIndexText, out switchIndex) && switchIndex is >= 0 and <= byte.MaxValue;

    private async void SetRawSwitch(bool pressed)
    {
        var backend = _rawInputBackendProvider?.Invoke();
        if (backend is null || !TryGetRawSwitchIndex(out var switchIndex)) return;
        AddOutputEntry($"[Raw Input] switch={switchIndex} action={(pressed ? "press" : "release")}", OutputLogStatus.Info);
        try
        {
            await backend.SetRawInputStateAsync(switchIndex, pressed, CancellationToken.None);
            IsRawSwitchAsserted = pressed;
        }
        catch (Exception exception)
        {
            AddOutputEntry($"[Raw Input] switch={switchIndex} failed: {exception.Message}", OutputLogStatus.Error);
        }
    }

    private async void PulseRawSwitch()
    {
        var backend = _rawInputBackendProvider?.Invoke();
        if (backend is null || !TryGetRawSwitchIndex(out var switchIndex)) return;
        AddOutputEntry($"[Raw Input] switch={switchIndex} action=pulse", OutputLogStatus.Info);
        try
        {
            await backend.PulseRawInputAsync(switchIndex, CancellationToken.None);
            IsRawSwitchAsserted = false;
        }
        catch (Exception exception)
        {
            AddOutputEntry($"[Raw Input] switch={switchIndex} failed: {exception.Message}", OutputLogStatus.Error);
        }
    }

    public void UpdateSelectedEntries(IEnumerable<OutputLogEntry> selectedEntries)
    {
        var selectedSet = selectedEntries
            .Where(entry => ShouldShowEntry(entry))
            .ToHashSet();

        if (selectedSet.Count == 0)
        {
            SelectedEntries = Array.Empty<OutputLogEntry>();
            return;
        }

        var orderedSelection = FilteredEntries
            .Cast<OutputLogEntry>()
            .Where(selectedSet.Contains)
            .ToList();

        SelectedEntries = orderedSelection;
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

        return _shellLauncher.TryLaunch(new ProcessStartInfo(CurrentLogPath) { UseShellExecute = true }, out failureReason);
    }

    public bool TryShowLogInExplorer(out string? failureReason)
    {
        failureReason = null;
        if (!Directory.Exists(LogDirectoryPath))
        {
            failureReason = $"Cannot show log directory; directory does not exist: {LogDirectoryPath}";
            return false;
        }

        return _shellLauncher.TryLaunch(new ProcessStartInfo("explorer.exe", $"\"{LogDirectoryPath}\"") { UseShellExecute = true }, out failureReason);
    }


    private void AddEntryOnUiThread(OutputLogEntry entry)
    {
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

    private bool ShouldShowEntry(object item)
    {
        if (item is not OutputLogEntry entry)
        {
            return false;
        }

        var matchesSeverity = entry.Status switch
        {
            OutputLogStatus.Info => ShowInfoLogs,
            OutputLogStatus.Warning => ShowWarningLogs,
            OutputLogStatus.Error => ShowErrorLogs,
            _ => true
        };

        if (!matchesSeverity)
        {
            return false;
        }

        if (string.IsNullOrEmpty(_searchTextNormalized))
        {
            return true;
        }

        return entry.Message.Contains(_searchTextNormalized, StringComparison.OrdinalIgnoreCase);
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
