namespace OasisEditor.Progress;

public sealed class EditorProgressReporter : IEditorProgressReporter
{
    private readonly Action<EditorProgressState> _report;
    private readonly Func<EditorProgressState> _getCurrentState;
    private EditorProgressState _currentState;
    private readonly double _rangeStart;
    private readonly double _rangeEnd;
    private readonly string? _defaultMessagePrefix;

    public EditorProgressReporter(
        EditorProgressState initialState,
        Action<EditorProgressState> report)
    {
        ArgumentNullException.ThrowIfNull(report);

        _currentState = initialState;
        _getCurrentState = () => _currentState;
        _report = state =>
        {
            _currentState = state;
            report(state);
        };
        _rangeStart = 0d;
        _rangeEnd = 1d;
        _defaultMessagePrefix = null;

        _report(initialState);
    }

    private EditorProgressReporter(
        Func<EditorProgressState> getCurrentState,
        Action<EditorProgressState> report,
        double rangeStart,
        double rangeEnd,
        string? defaultMessagePrefix)
    {
        _currentState = getCurrentState?.Invoke() ?? throw new ArgumentNullException(nameof(getCurrentState));
        _getCurrentState = getCurrentState;
        _report = report ?? throw new ArgumentNullException(nameof(report));
        _rangeStart = EditorProgressState.Clamp(rangeStart);
        _rangeEnd = EditorProgressState.Clamp(rangeEnd);
        _defaultMessagePrefix = string.IsNullOrWhiteSpace(defaultMessagePrefix) ? null : defaultMessagePrefix.Trim();
    }

    public void Report(double normalizedProgress, string message)
    {
        var mappedProgress = MapProgress(normalizedProgress);
        _report(_getCurrentState().WithDeterminateProgress(mappedProgress, FormatMessage(message)));
    }

    public void ReportIndeterminate(string message)
    {
        _report(_getCurrentState().WithIndeterminateMessage(FormatMessage(message)));
    }

    public void ReportMessage(string message)
    {
        _report(_getCurrentState().WithMessage(FormatMessage(message)));
    }

    public IEditorProgressReporter CreateChild(double start, double end, string? defaultMessagePrefix = null)
    {
        var parentStart = Math.Min(_rangeStart, _rangeEnd);
        var parentEnd = Math.Max(_rangeStart, _rangeEnd);
        var childStart = parentStart + ((parentEnd - parentStart) * EditorProgressState.Clamp(start));
        var childEnd = parentStart + ((parentEnd - parentStart) * EditorProgressState.Clamp(end));
        return new EditorProgressReporter(_getCurrentState, _report, childStart, childEnd, defaultMessagePrefix);
    }

    private double MapProgress(double normalizedProgress)
    {
        var progress = EditorProgressState.Clamp(normalizedProgress);
        return _rangeStart + ((_rangeEnd - _rangeStart) * progress);
    }

    private string FormatMessage(string message)
    {
        var trimmed = message?.Trim() ?? string.Empty;
        if (_defaultMessagePrefix is null || trimmed.Length == 0)
        {
            return trimmed;
        }

        return $"{_defaultMessagePrefix}: {trimmed}";
    }
}
