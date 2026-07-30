using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace OasisEditor;

/// <summary>Bounded, best-effort diagnostics shared by both Amber transports.</summary>
internal sealed class AmberComparisonSession
{
    internal const int AdvanceLimit = 20;
    internal const int SnapshotLimit = 8;
    internal const int AudioLimit = 16;
    internal const int InputLimit = 32;

    private readonly Action<string>? _sink;
    private readonly string _backend;
    private readonly string _id;
    private readonly long _started = Stopwatch.GetTimestamp();
    private readonly object _gate = new();
    private long _sequence;
    private readonly Dictionary<string, int> _boundedCounts = [];
    private string? _previousSnapshot;
    private int _staticSnapshots;

    internal AmberComparisonSession(bool enabled, string backend, Action<string>? sink,
        Func<string>? idFactory = null)
    {
        _sink = enabled ? sink : null;
        _backend = backend;
        _id = (idFactory?.Invoke() ?? Guid.NewGuid().ToString("N")[..8]);
    }

    internal bool Enabled => _sink is not null;
    internal string SessionId => _id;

    internal void Write(string operation, string arguments = "", string result = "success", string summary = "")
    {
        if (_sink is null) return;
        try
        {
            lock (_gate)
            {
                var sequence = ++_sequence;
                var elapsed = (Stopwatch.GetTimestamp() - _started) * 1_000_000_000L / Stopwatch.Frequency;
                _sink($"[AmberCompare] session={_id} backend={_backend} sequence={sequence} elapsed_ns={elapsed} thread={Environment.CurrentManagedThreadId} operation={Safe(operation)} arguments={Safe(arguments)} result={Safe(result)} summary={Safe(summary)}");
            }
        }
        catch
        {
            // Diagnostics must never alter emulation behaviour.
        }
    }

    internal void WriteBounded(string category, int limit, string operation, string arguments = "", string result = "success", string summary = "")
    {
        if (_sink is null) return;
        lock (_gate)
        {
            _boundedCounts.TryGetValue(category, out var count);
            if (count >= limit) return;
            _boundedCounts[category] = count + 1;
        }
        Write(operation, arguments, result, summary);
    }

    internal static string RomSummary(string role, IReadOnlyList<string> paths) =>
        string.Join(",", paths.Select((path, slot) =>
            $"role:{role}|slot:{slot}|configured_index:{slot}|filename:{SafeFileName(path)}|present:{(!string.IsNullOrWhiteSpace(path)).ToString().ToLowerInvariant()}"));

    internal string TrackSnapshot(string fingerprint)
    {
        lock (_gate)
        {
            var changed = _previousSnapshot is null || !string.Equals(_previousSnapshot, fingerprint, StringComparison.Ordinal);
            _staticSnapshots = changed ? 0 : _staticSnapshots + 1;
            _previousSnapshot = fingerprint;
            return $"snapshot_changed:{(changed ? "yes" : "no")}|consecutive_static:{_staticSnapshots}";
        }
    }

    internal static string SafeFileName(string? path) => string.IsNullOrWhiteSpace(path) ? "absent" : Safe(Path.GetFileName(path));
    internal static string Number(long value) => value.ToString(CultureInfo.InvariantCulture);
    private static string Safe(string? value) => string.IsNullOrEmpty(value) ? "none" : value.Replace('\r', ' ').Replace('\n', ' ').Replace(' ', '_');
}
