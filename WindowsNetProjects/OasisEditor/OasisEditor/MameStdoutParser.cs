namespace OasisEditor;

public sealed class MameStdoutParser : IMameStdoutParser
{
    private readonly IMameLampStateParser _lampStateParser;
    private readonly IMameLampRuntimeAdapter _lampRuntimeAdapter;
    private readonly IMameReelStateParser _reelStateParser;
    private readonly IMameReelRuntimeAdapter _reelRuntimeAdapter;
    private readonly Action<string>? _diagnosticLogger;

    public MameStdoutParser(IMameLampStateParser lampStateParser, IMameLampRuntimeAdapter lampRuntimeAdapter, IMameReelStateParser reelStateParser, IMameReelRuntimeAdapter reelRuntimeAdapter, Action<string>? diagnosticLogger = null)
    {
        _lampStateParser = lampStateParser ?? throw new ArgumentNullException(nameof(lampStateParser));
        _lampRuntimeAdapter = lampRuntimeAdapter ?? throw new ArgumentNullException(nameof(lampRuntimeAdapter));
        _reelStateParser = reelStateParser ?? throw new ArgumentNullException(nameof(reelStateParser));
        _reelRuntimeAdapter = reelRuntimeAdapter ?? throw new ArgumentNullException(nameof(reelRuntimeAdapter));
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

        if (_reelStateParser.TryParse(line, out var reelId, out var reelValue))
        {
            _reelRuntimeAdapter.ApplyReelState(reelId, reelValue);
            return;
        }

        _diagnosticLogger?.Invoke($"[MAME-STDOUT] Unhandled line: {line}");
    }
}
