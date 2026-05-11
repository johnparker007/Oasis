namespace OasisEditor;

public sealed class MameStdoutParser : IMameStdoutParser
{
    private readonly IMameLampStateParser _lampStateParser;
    private readonly IMameLampRuntimeAdapter _lampRuntimeAdapter;
    private readonly Action<string>? _diagnosticLogger;

    public MameStdoutParser(IMameLampStateParser lampStateParser, IMameLampRuntimeAdapter lampRuntimeAdapter, Action<string>? diagnosticLogger = null)
    {
        _lampStateParser = lampStateParser ?? throw new ArgumentNullException(nameof(lampStateParser));
        _lampRuntimeAdapter = lampRuntimeAdapter ?? throw new ArgumentNullException(nameof(lampRuntimeAdapter));
        _diagnosticLogger = diagnosticLogger;
    }

    public void ProcessLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        if (_lampStateParser.TryParse(line, out var lampId, out var lampValue))
        {
            _lampRuntimeAdapter.ApplyLampState(lampId, lampValue);
            return;
        }

        _diagnosticLogger?.Invoke($"[MAME-STDOUT] Unhandled line: {line}");
    }
}
