using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using Avalonia.Threading;

namespace OasisEditor.Avalonia;

public enum OutputSeverity { Info, Warning, Error }

public sealed record OutputLogItem(DateTimeOffset Timestamp, OutputSeverity Severity, string Message)
{
    public string FormattedTimestamp => Timestamp.ToLocalTime().ToString("HH:mm:ss.fff");
    public string ClipboardLine => $"[{FormattedTimestamp}] [{Severity}] {Message}";
}

/// <summary>Shared, UI-independent Output pane state. Disk logging is opt-in and requires an explicit path.</summary>
public sealed class OutputLogService : INotifyPropertyChanged, IDisposable
{
    public const int MaximumEntries = 2000;
    private readonly object _diskGate = new();
    private readonly StreamWriter? _diskWriter;
    private string _searchText = string.Empty;
    private bool _showInfo = true, _showWarnings = true, _showErrors = true;
    private bool _disposed;

    public OutputLogService(string? explicitDiskLogPath = null)
    {
        Entries.CollectionChanged += (_, _) => RefreshFiltered();
        if (!string.IsNullOrWhiteSpace(explicitDiskLogPath))
        {
            var path = Path.GetFullPath(explicitDiskLogPath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            _diskWriter = new StreamWriter(new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read), new UTF8Encoding(false)) { AutoFlush = true };
            DiskLogPath = path;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public ObservableCollection<OutputLogItem> Entries { get; } = [];
    public IReadOnlyList<OutputLogItem> FilteredEntries { get; private set; } = [];
    public string? DiskLogPath { get; }
    public bool HasEntries => Entries.Count > 0;

    public string SearchText
    {
        get => _searchText;
        set { var normalized = value?.Trim() ?? string.Empty; if (_searchText == normalized) return; _searchText = normalized; OnPropertyChanged(); RefreshFiltered(); }
    }
    public bool ShowInfo { get => _showInfo; set => SetFilter(ref _showInfo, value); }
    public bool ShowWarnings { get => _showWarnings; set => SetFilter(ref _showWarnings, value); }
    public bool ShowErrors { get => _showErrors; set => SetFilter(ref _showErrors, value); }

    public void Add(string message, OutputSeverity severity = OutputSeverity.Info)
    {
        if (_disposed) return;
        var item = new OutputLogItem(DateTimeOffset.Now, severity, message ?? string.Empty);
        if (_diskWriter is not null)
        {
            lock (_diskGate)
            {
                try { _diskWriter.WriteLine(item.ClipboardLine); }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException or ObjectDisposedException) { }
            }
        }
        if (Dispatcher.UIThread.CheckAccess()) AddOnUiThread(item);
        else Dispatcher.UIThread.Post(() => AddOnUiThread(item));
    }

    public void Clear()
    {
        void ClearOnUiThread()
        {
            Entries.Clear();
            OnPropertyChanged(nameof(HasEntries));
            Add("Output cleared.");
        }
        if (Dispatcher.UIThread.CheckAccess()) ClearOnUiThread(); else Dispatcher.UIThread.Post(ClearOnUiThread);
    }

    public string BuildText(IEnumerable<OutputLogItem> items) => string.Join(Environment.NewLine, items.Select(item => item.ClipboardLine));

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        lock (_diskGate) _diskWriter?.Dispose();
    }

    private void AddOnUiThread(OutputLogItem item)
    {
        if (_disposed) return;
        var previous = Entries.LastOrDefault();
        if (previous is not null && previous.Severity == item.Severity && string.Equals(previous.Message, item.Message, StringComparison.Ordinal)) return;
        Entries.Add(item);
        while (Entries.Count > MaximumEntries) Entries.RemoveAt(0);
        OnPropertyChanged(nameof(HasEntries));
    }

    private void SetFilter(ref bool field, bool value, [CallerMemberName] string? property = null)
    { if (field == value) return; field = value; OnPropertyChanged(property); RefreshFiltered(); }

    private void RefreshFiltered()
    {
        void Apply()
        {
            var query = _searchText;
            FilteredEntries = Entries.Where(item => item.Severity switch
            {
                OutputSeverity.Info => _showInfo,
                OutputSeverity.Warning => _showWarnings,
                OutputSeverity.Error => _showErrors,
                _ => true
            }).Where(item => query.Length == 0 || item.Message.Contains(query, StringComparison.OrdinalIgnoreCase)).ToArray();
            OnPropertyChanged(nameof(FilteredEntries));
        }
        if (Dispatcher.UIThread.CheckAccess()) Apply(); else Dispatcher.UIThread.Post(Apply);
    }

    private void OnPropertyChanged([CallerMemberName] string? property = null) => PropertyChanged?.Invoke(this, new(property));
}
