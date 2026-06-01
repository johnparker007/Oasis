using OasisEditor;
using OasisEditor.Features.MameDebugger;
using Xunit;

namespace OasisEditor.Tests;

public sealed class MameDebuggerProtocolTests
{
    [Fact]
    public void CreateCommand_UsesDebugCommandWithStructuredJsonPayload()
    {
        var command = MameDebuggerProtocol.CreateCommand(42, "status");

        Assert.Equal("debug {\"id\":42,\"op\":\"status\"}", command);
    }

    [Fact]
    public void StdoutParser_DetectsResponseAndEventPrefixes()
    {
        var parser = new MameDebuggerStdoutParser();

        Assert.True(parser.TryParse("@OASIS_DEBUG {\"id\":1,\"ok\":true,\"result\":{}}", out var response));
        Assert.Equal(MameDebuggerStdoutMessageKind.Response, response.Kind);
        Assert.True(parser.TryParse("@OASIS_DEBUG_EVENT {\"event\":\"stopped\"}", out var debuggerEvent));
        Assert.Equal(MameDebuggerStdoutMessageKind.Event, debuggerEvent.Kind);
        Assert.False(parser.TryParse("lamp0=1", out _));
    }

    [Fact]
    public async Task Service_CorrelatesResponsesByRequestId()
    {
        var runner = new RecordingMameProcessRunner();
        var service = new MameDebuggerService(runner);
        var statusTask = service.GetStatusAsync(CancellationToken.None);

        service.ProcessStdoutLine("@OASIS_DEBUG {\"id\":1,\"ok\":true,\"result\":{\"available\":true,\"state\":\"stopped\",\"cpu\":\":maincpu\",\"pc\":4660}}");

        var status = await statusTask;
        Assert.Equal("debug {\"id\":1,\"op\":\"status\"}", Assert.Single(runner.Commands));
        Assert.True(status.Available);
        Assert.Equal(MameDebuggerExecutionState.Stopped, status.State);
        Assert.Equal(4660, status.Pc);
    }

    private sealed class RecordingMameProcessRunner : IMameProcessRunner
    {
        public List<string> Commands { get; } = [];
        public Task StartAsync(System.Diagnostics.ProcessStartInfo startInfo, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task WriteStandardInputAsync(string command, CancellationToken cancellationToken)
        {
            Commands.Add(command);
            return Task.CompletedTask;
        }
    }
}
