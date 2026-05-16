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
        var parser = new MameStdoutParser(new MameLampStateParser(), lampAdapter, new MameReelStateParser(), reelAdapter);

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
        var parser = new MameStdoutParser(new MameLampStateParser(), lampAdapter, new MameReelStateParser(), reelAdapter);

        parser.ProcessLine("reel3 = 94");

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
        string? diagnostic = null;
        var parser = new MameStdoutParser(new MameLampStateParser(), lampAdapter, new MameReelStateParser(), reelAdapter, message => diagnostic = message);

        parser.ProcessLine("unknown output");

        Assert.Empty(lampAdapter.Calls);
        Assert.Empty(reelAdapter.Calls);
        Assert.NotNull(diagnostic);
        Assert.Contains("Unhandled line", diagnostic);
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
}
