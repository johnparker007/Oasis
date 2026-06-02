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
    public void CreateCommand_PreservesJsonPayloadContainingSpacesAndQuotedStringsAfterDebugPrefix()
    {
        var command = MameDebuggerProtocol.CreateCommand(7, "status", ":main cpu \"A\"");

        Assert.StartsWith("debug ", command);
        var payload = command[(MameDebuggerProtocol.CommandName.Length + 1)..];
        var request = System.Text.Json.JsonSerializer.Deserialize<MameDebuggerRequest>(payload, MameDebuggerProtocol.JsonOptions);

        Assert.NotNull(request);
        Assert.Equal(7, request.Id);
        Assert.Equal("status", request.Op);
        Assert.Equal(":main cpu \"A\"", request.Cpu);
    }

    [Fact]
    public void CreateCommand_DoesNotRequireQuotingJsonPayloadWithSpacesForLuaBridge()
    {
        var command = MameDebuggerProtocol.CreateCommand(8, "status", "cpu with spaces");

        var rawPayloadAfterDebug = command[("debug ".Length)..];

        Assert.Equal("{\"id\":8,\"op\":\"status\",\"cpu\":\"cpu with spaces\"}", rawPayloadAfterDebug);
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

    [Fact]
    public async Task Service_PingAsync_SendsPingAndReturnsAvailability()
    {
        var runner = new RecordingMameProcessRunner();
        var service = new MameDebuggerService(runner);
        var pingTask = service.PingAsync(CancellationToken.None);

        service.ProcessStdoutLine("@OASIS_DEBUG {\"id\":1,\"ok\":true,\"result\":{\"pong\":true,\"available\":true}}");

        var ping = await pingTask;
        Assert.Equal("debug {\"id\":1,\"op\":\"ping\"}", Assert.Single(runner.Commands));
        Assert.True(ping.Pong);
        Assert.True(ping.Available);
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
