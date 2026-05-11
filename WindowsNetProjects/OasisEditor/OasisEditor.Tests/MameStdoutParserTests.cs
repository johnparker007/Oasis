using OasisEditor;
using Xunit;

namespace OasisEditor.Tests;

public sealed class MameStdoutParserTests
{
    [Fact]
    public void ProcessLine_WhenLampLine_AppliesLampState()
    {
        var lampAdapter = new RecordingLampAdapter();
        var parser = new MameStdoutParser(new MameLampStateParser(), lampAdapter);

        parser.ProcessLine("lamp7 1");

        var call = Assert.Single(lampAdapter.Calls);
        Assert.Equal(7, call.LampId);
        Assert.Equal(1, call.Value);
    }

    [Fact]
    public void ProcessLine_WhenUnknownLine_LogsDiagnostic()
    {
        var lampAdapter = new RecordingLampAdapter();
        string? diagnostic = null;
        var parser = new MameStdoutParser(new MameLampStateParser(), lampAdapter, message => diagnostic = message);

        parser.ProcessLine("unknown output");

        Assert.Empty(lampAdapter.Calls);
        Assert.NotNull(diagnostic);
        Assert.Contains("Unhandled line", diagnostic);
    }

    private sealed class RecordingLampAdapter : IMameLampRuntimeAdapter
    {
        public List<(int LampId, int Value)> Calls { get; } = [];

        public void ApplyLampState(int lampId, int lampValue)
        {
            Calls.Add((lampId, lampValue));
        }
    }
}
