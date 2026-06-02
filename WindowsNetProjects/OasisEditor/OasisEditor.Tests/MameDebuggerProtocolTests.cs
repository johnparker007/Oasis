using System.Text.Json;
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
    public void CreateCommand_SerializesBreakpointPayloadWithSpacesAndQuotes()
    {
        var command = MameDebuggerProtocol.CreateCommand(
            9,
            "bp.set",
            new MameDebuggerBreakpointRequest(":maincpu", 0x1234, "a == 1", "printf \"hit bp\""));

        Assert.StartsWith("debug ", command);
        var payload = command[("debug ".Length)..];
        var request = System.Text.Json.JsonSerializer.Deserialize<MameDebuggerRequest<MameDebuggerBreakpointRequest>>(payload, MameDebuggerProtocol.JsonOptions);

        Assert.NotNull(request);
        Assert.Equal(9, request.Id);
        Assert.Equal("bp.set", request.Op);
        Assert.Equal(0x1234, request.Payload.Address);
        Assert.Equal("a == 1", request.Payload.Condition);
        Assert.Equal("printf \"hit bp\"", request.Payload.Action);
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
    public void ParseResponse_DeserializesBreakpointModels()
    {
        var response = MameDebuggerProtocol.ParseResponse(
            "{\"id\":3,\"ok\":true,\"result\":[{\"debuggerId\":1,\"mameId\":1,\"cpu\":\":maincpu\",\"address\":4660,\"enabled\":true,\"condition\":\"a == 1\",\"action\":\"printf \\\"hit\\\"\"}]}");

        var breakpoints = response.Result!.Value.Deserialize<List<MameDebuggerBreakpoint>>(MameDebuggerProtocol.JsonOptions)!;

        var breakpoint = Assert.Single(breakpoints);
        Assert.Equal(1, breakpoint.MameId);
        Assert.Equal(4660, breakpoint.Address);
        Assert.Equal("a == 1", breakpoint.Condition);
        Assert.Equal("printf \"hit\"", breakpoint.Action);
    }

    [Fact]
    public void ParseResponse_DeserializesWatchpointModels()
    {
        var response = MameDebuggerProtocol.ParseResponse(
            "{\"id\":4,\"ok\":true,\"result\":[{\"debuggerId\":2,\"mameId\":2,\"cpu\":\":maincpu\",\"address\":8192,\"length\":4,\"type\":\"readWrite\",\"enabled\":false,\"condition\":\"wpdata != 0\",\"action\":\"printf \\\"watch hit\\\"\",\"latestHit\":{\"address\":8193,\"data\":255,\"size\":1}}]}");

        var watchpoints = response.Result!.Value.Deserialize<List<MameDebuggerWatchpoint>>(MameDebuggerProtocol.JsonOptions)!;

        var watchpoint = Assert.Single(watchpoints);
        Assert.Equal(MameDebuggerWatchpointType.ReadWrite, watchpoint.Type);
        Assert.False(watchpoint.Enabled);
        Assert.Equal(8193, watchpoint.LatestHit!.Address);
        Assert.Equal(255, watchpoint.LatestHit.Data);
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
    public async Task Service_BreakpointAndWatchpointRequests_CorrelateByRequestId()
    {
        var runner = new RecordingMameProcessRunner();
        var service = new MameDebuggerService(runner);
        var breakpointTask = service.SetBreakpointAsync(new MameDebuggerBreakpointRequest(":maincpu", 0x1000, Action: "printf \"bp hit\""), CancellationToken.None);
        var watchpointTask = service.SetWatchpointAsync(new MameDebuggerWatchpointRequest(":maincpu", 0x2000, 2, MameDebuggerWatchpointType.Write), CancellationToken.None);

        service.ProcessStdoutLine("@OASIS_DEBUG {\"id\":2,\"ok\":true,\"result\":{\"debuggerId\":7,\"mameId\":7,\"cpu\":\":maincpu\",\"address\":8192,\"length\":2,\"type\":\"write\",\"enabled\":true}}");
        service.ProcessStdoutLine("@OASIS_DEBUG {\"id\":1,\"ok\":true,\"result\":{\"debuggerId\":6,\"mameId\":6,\"cpu\":\":maincpu\",\"address\":4096,\"enabled\":true,\"action\":\"printf \\\"bp hit\\\"\"}}");

        var breakpoint = await breakpointTask;
        var watchpoint = await watchpointTask;

        Assert.Equal(6, breakpoint.MameId);
        Assert.Equal(7, watchpoint.MameId);
        Assert.Equal("debug {\"id\":1,\"op\":\"bp.set\",\"payload\":{\"cpu\":\":maincpu\",\"address\":4096,\"action\":\"printf \\\"bp hit\\\"\"}}", runner.Commands[0]);
        Assert.Equal("debug {\"id\":2,\"op\":\"wp.set\",\"payload\":{\"cpu\":\":maincpu\",\"address\":8192,\"length\":2,\"type\":\"write\"}}", runner.Commands[1]);
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
