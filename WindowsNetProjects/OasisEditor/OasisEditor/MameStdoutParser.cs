namespace OasisEditor;

public sealed class MameStdoutParser : IMameStdoutParser
{
    private readonly IMameLampStateParser _lampStateParser;
    private readonly IMameLampRuntimeAdapter _lampRuntimeAdapter;
    private readonly IMameReelStateParser _reelStateParser;
    private readonly IMameReelRuntimeAdapter _reelRuntimeAdapter;
    private readonly IMameSegmentStateParser _segmentStateParser;
    private readonly IMameSegmentRuntimeAdapter _segmentRuntimeAdapter;
    private readonly MameVfdDutyParser _vfdDutyParser;
    private readonly Func<FruitMachinePlatformType> _platformProvider;
    private readonly Action<string>? _diagnosticLogger;

    public MameStdoutParser(IMameLampStateParser lampStateParser, IMameLampRuntimeAdapter lampRuntimeAdapter, IMameReelStateParser reelStateParser, IMameReelRuntimeAdapter reelRuntimeAdapter, IMameSegmentStateParser segmentStateParser, IMameSegmentRuntimeAdapter segmentRuntimeAdapter, Func<FruitMachinePlatformType>? platformProvider = null, Action<string>? diagnosticLogger = null)
    {
        _lampStateParser = lampStateParser ?? throw new ArgumentNullException(nameof(lampStateParser));
        _lampRuntimeAdapter = lampRuntimeAdapter ?? throw new ArgumentNullException(nameof(lampRuntimeAdapter));
        _reelStateParser = reelStateParser ?? throw new ArgumentNullException(nameof(reelStateParser));
        _reelRuntimeAdapter = reelRuntimeAdapter ?? throw new ArgumentNullException(nameof(reelRuntimeAdapter));
        _segmentStateParser = segmentStateParser ?? throw new ArgumentNullException(nameof(segmentStateParser));
        _segmentRuntimeAdapter = segmentRuntimeAdapter ?? throw new ArgumentNullException(nameof(segmentRuntimeAdapter));
        _vfdDutyParser = new MameVfdDutyParser();
        _platformProvider = platformProvider ?? (() => FruitMachinePlatformType.MPU4);
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

        if (_segmentStateParser.TryParse(line, out var cellId, out var segmentMask, out var outputType))
        {
            _segmentRuntimeAdapter.ApplySegmentState(cellId, segmentMask, outputType);
            return;
        }

        if (_vfdDutyParser.TryParseNormalized(line, _platformProvider(), out _, out _))
        {
            return;
        }

        _diagnosticLogger?.Invoke($"[MAME-STDOUT] Unhandled line: {line}");
    }
}
