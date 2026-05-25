using OasisEditor;
using Xunit;

namespace OasisEditor.Tests;

public sealed class MameStdoutParserTests
{
    [Fact]
    public void ProcessLine_WhenLampLine_AppliesLampState()
    {
        var lampAdapter = new RecordingLampAdapter();
        var reelAdapter = new RecordingReelAdapter();
        var segmentAdapter = new RecordingSegmentAdapter();
        var parser = new MameStdoutParser(new MameLampStateParser(), lampAdapter, new MameReelStateParser(), reelAdapter, new MameSegmentStateParser(), segmentAdapter);

        parser.ProcessLine("lamp7 1");

        var call = Assert.Single(lampAdapter.Calls);
        Assert.Equal(7, call.LampId);
        Assert.Equal(1, call.Value);
        Assert.Empty(reelAdapter.Calls);
    }

    [Fact]
    public void ProcessLine_WhenSreelLine_AppliesReelState()
    {
        var lampAdapter = new RecordingLampAdapter();
        var reelAdapter = new RecordingReelAdapter();
        var segmentAdapter = new RecordingSegmentAdapter();
        var parser = new MameStdoutParser(new MameLampStateParser(), lampAdapter, new MameReelStateParser(), reelAdapter, new MameSegmentStateParser(), segmentAdapter);

        parser.ProcessLine("sreel3 = 64170");

        var call = Assert.Single(reelAdapter.Calls);
        Assert.Equal(3, call.ReelId);
        Assert.Equal(94, call.Value);
        Assert.Empty(lampAdapter.Calls);
    }

    [Fact]
    public void ProcessLine_WhenUnknownLine_LogsDiagnostic()
    {
        var lampAdapter = new RecordingLampAdapter();
        var reelAdapter = new RecordingReelAdapter();
        var segmentAdapter = new RecordingSegmentAdapter();
        string? diagnostic = null;
        var parser = new MameStdoutParser(new MameLampStateParser(), lampAdapter, new MameReelStateParser(), reelAdapter, new MameSegmentStateParser(), segmentAdapter, diagnosticLogger: message => diagnostic = message);

        parser.ProcessLine("unknown output");

        Assert.Empty(lampAdapter.Calls);
        Assert.Empty(reelAdapter.Calls);
        Assert.NotNull(diagnostic);
        Assert.Contains("Unhandled line", diagnostic);
    }

    [Fact]
    public void ProcessLine_WhenSegmentLine_AppliesSegmentState()
    {
        var lampAdapter = new RecordingLampAdapter();
        var reelAdapter = new RecordingReelAdapter();
        var segmentAdapter = new RecordingSegmentAdapter();
        var parser = new MameStdoutParser(new MameLampStateParser(), lampAdapter, new MameReelStateParser(), reelAdapter, new MameSegmentStateParser(), segmentAdapter);

        parser.ProcessLine("vfd12 = 769");

        var call = Assert.Single(segmentAdapter.Calls);
        Assert.Equal(12, call.CellId);
        Assert.Equal(769, call.Mask);
    }

    [Fact]
    public void ProcessLine_WhenDigitSegmentLine_AppliesSegmentState()
    {
        var lampAdapter = new RecordingLampAdapter();
        var reelAdapter = new RecordingReelAdapter();
        var segmentAdapter = new RecordingSegmentAdapter();
        var parser = new MameStdoutParser(new MameLampStateParser(), lampAdapter, new MameReelStateParser(), reelAdapter, new MameSegmentStateParser(), segmentAdapter);

        parser.ProcessLine("digit3 = 16");

        var call = Assert.Single(segmentAdapter.Calls);
        Assert.Equal(3, call.CellId);
        Assert.Equal(16, call.Mask);
    }


    [Fact]
    public void ProcessLine_WhenVfdDutyLine_DoesNotLogUnhandledDiagnostic()
    {
        var lampAdapter = new RecordingLampAdapter();
        var reelAdapter = new RecordingReelAdapter();
        var segmentAdapter = new RecordingSegmentAdapter();
        string? diagnostic = null;
        var parser = new MameStdoutParser(new MameLampStateParser(), lampAdapter, new MameReelStateParser(), reelAdapter, new MameSegmentStateParser(), segmentAdapter, () => FruitMachinePlatformType.MPU4, message => diagnostic = message);

        parser.ProcessLine("vfdduty0 = 31");

        Assert.Empty(lampAdapter.Calls);
        Assert.Empty(reelAdapter.Calls);
        Assert.Empty(segmentAdapter.Calls);
        var duty = Assert.Single(segmentAdapter.BrightnessCalls);
        Assert.Equal(0, duty.CellId);
        Assert.Equal(1d, duty.Brightness);
        Assert.Null(diagnostic);
    }
    private sealed class RecordingLampAdapter : IMameLampRuntimeAdapter
    {
        public List<(int LampId, int Value)> Calls { get; } = [];
        public void ApplyLampState(int lampId, int lampValue) => Calls.Add((lampId, lampValue));
    }

    private sealed class RecordingReelAdapter : IMameReelRuntimeAdapter
    {
        public List<(int ReelId, int Value)> Calls { get; } = [];
        public void ApplyReelState(int reelId, int reelValue) => Calls.Add((reelId, reelValue));
    }

    private sealed class RecordingSegmentAdapter : IMameSegmentRuntimeAdapter
    {
        public List<(int CellId, int Mask, MameSegmentOutputType OutputType)> Calls { get; } = [];
        public List<(int CellId, double Brightness)> BrightnessCalls { get; } = [];
        public void ApplySegmentState(int cellId, int segmentMask, MameSegmentOutputType outputType) => Calls.Add((cellId, segmentMask, outputType));
        public void ApplyVfdBrightness(int cellId, double normalizedBrightness) => BrightnessCalls.Add((cellId, normalizedBrightness));
    }
}
