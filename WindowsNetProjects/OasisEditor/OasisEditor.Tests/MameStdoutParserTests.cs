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
    public void ProcessLine_WhenReelLine_AppliesReelState()
    {
        var lampAdapter = new RecordingLampAdapter();
        var reelAdapter = new RecordingReelAdapter();
        var segmentAdapter = new RecordingSegmentAdapter();
        var parser = new MameStdoutParser(new MameLampStateParser(), lampAdapter, new MameReelStateParser(), reelAdapter, new MameSegmentStateParser(), segmentAdapter);

        parser.ProcessLine("reel3 = 94");

        var call = Assert.Single(reelAdapter.Calls);
        Assert.Equal(3, call.ReelId);
        Assert.Equal(94, call.Value);
        Assert.Empty(lampAdapter.Calls);
    }

    [Fact]
    public void ProcessLine_WhenLegacySreelLine_IgnoresLine()
    {
        var lampAdapter = new RecordingLampAdapter();
        var reelAdapter = new RecordingReelAdapter();
        var segmentAdapter = new RecordingSegmentAdapter();
        var parser = new MameStdoutParser(new MameLampStateParser(), lampAdapter, new MameReelStateParser(), reelAdapter, new MameSegmentStateParser(), segmentAdapter);

        parser.ProcessLine("sreel3 = 64170");

        Assert.Empty(reelAdapter.Calls);
    }

    [Fact]
    public void ProcessLine_WhenUnknownLine_IgnoresLineWithoutLoggingDiagnostic()
    {
        var lampAdapter = new RecordingLampAdapter();
        var reelAdapter = new RecordingReelAdapter();
        var segmentAdapter = new RecordingSegmentAdapter();
        var parser = new MameStdoutParser(new MameLampStateParser(), lampAdapter, new MameReelStateParser(), reelAdapter, new MameSegmentStateParser(), segmentAdapter);

        parser.ProcessLine("unknown output");

        Assert.Empty(lampAdapter.Calls);
        Assert.Empty(reelAdapter.Calls);
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
        var parser = new MameStdoutParser(new MameLampStateParser(), lampAdapter, new MameReelStateParser(), reelAdapter, new MameSegmentStateParser(), segmentAdapter, () => FruitMachinePlatformType.MPU4);

        parser.ProcessLine("vfdduty0 = 31");

        Assert.Empty(lampAdapter.Calls);
        Assert.Empty(reelAdapter.Calls);
        Assert.Empty(segmentAdapter.Calls);
        var duty = Assert.Single(segmentAdapter.BrightnessCalls);
        Assert.Equal(0, duty.CellId);
        Assert.Equal(1d, duty.Brightness);
    }

    [Fact]
    public void ProcessLine_WhenVfdDotMatrixLine_AppliesDotState()
    {
        var lampAdapter = new RecordingLampAdapter();
        var reelAdapter = new RecordingReelAdapter();
        var segmentAdapter = new RecordingSegmentAdapter();
        var dotMatrixAdapter = new RecordingVfdDotMatrixAdapter();
        var parser = new MameStdoutParser(new MameLampStateParser(), lampAdapter, new MameReelStateParser(), reelAdapter, new MameSegmentStateParser(), segmentAdapter, vfdDotMatrixRuntimeAdapter: dotMatrixAdapter);

        parser.ProcessLine("vfddotmatrix767 = 1");

        var call = Assert.Single(dotMatrixAdapter.Calls);
        Assert.Equal(767, call.DotIndex);
        Assert.Equal(1, call.Value);
        Assert.Empty(segmentAdapter.Calls);
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

    private sealed class RecordingVfdDotMatrixAdapter : IMameVfdDotMatrixRuntimeAdapter
    {
        public List<(int DotIndex, int Value)> Calls { get; } = [];
        public void ApplyDotState(int dotIndex, int dotValue) => Calls.Add((dotIndex, dotValue));
    }

    private sealed class RecordingSegmentAdapter : IMameSegmentRuntimeAdapter
    {
        public List<(int CellId, int Mask, MameSegmentOutputType OutputType)> Calls { get; } = [];
        public List<(int CellId, double Brightness)> BrightnessCalls { get; } = [];
        public void ApplySegmentState(int cellId, int segmentMask, MameSegmentOutputType outputType) => Calls.Add((cellId, segmentMask, outputType));
        public void ApplyVfdBrightness(int cellId, double normalizedBrightness) => BrightnessCalls.Add((cellId, normalizedBrightness));
    }
}
