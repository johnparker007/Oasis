using OasisEditor.Progress;

namespace OasisEditor.Tests;

internal sealed class RecordingEditorProgressReporter : IEditorProgressReporter
{
    private readonly List<(double? Value, string Message)> _reports;
    private readonly double _start;
    private readonly double _end;

    public RecordingEditorProgressReporter()
        : this([], 0d, 1d)
    {
    }

    private RecordingEditorProgressReporter(List<(double? Value, string Message)> reports, double start, double end)
    {
        _reports = reports;
        _start = start;
        _end = end;
    }

    public IReadOnlyList<(double? Value, string Message)> Reports => _reports;

    public void Report(double normalizedProgress, string message)
    {
        var clamped = Math.Clamp(normalizedProgress, 0d, 1d);
        var mapped = _start + ((_end - _start) * clamped);
        _reports.Add((mapped, message));
    }

    public void ReportIndeterminate(string message)
    {
        _reports.Add((null, message));
    }

    public void ReportMessage(string message)
    {
        _reports.Add((_reports.LastOrDefault().Value, message));
    }

    public IEditorProgressReporter CreateChild(double start, double end, string? defaultMessagePrefix = null)
    {
        var childStart = _start + ((_end - _start) * Math.Clamp(start, 0d, 1d));
        var childEnd = _start + ((_end - _start) * Math.Clamp(end, 0d, 1d));
        return new RecordingEditorProgressReporter(_reports, childStart, childEnd);
    }
}
