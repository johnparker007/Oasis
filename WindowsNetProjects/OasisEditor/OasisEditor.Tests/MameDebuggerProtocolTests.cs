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


    [Fact]
    public void ParseResponse_DeserializesRegisterModels()
    {
        var response = MameDebuggerProtocol.ParseResponse(
            "{\"id\":5,\"ok\":true,\"result\":[{\"cpu\":\":maincpu\",\"name\":\"PC\",\"value\":4660,\"displayValue\":\"0x1234\",\"size\":2,\"bits\":16,\"editable\":true}]}");

        var registers = response.Result!.Value.Deserialize<List<MameDebuggerRegister>>(MameDebuggerProtocol.JsonOptions)!;

        var register = Assert.Single(registers);
        Assert.Equal(":maincpu", register.Cpu);
        Assert.Equal("PC", register.Name);
        Assert.Equal(0x1234, register.Value);
        Assert.Equal("0x1234", register.DisplayValue);
        Assert.Equal(16, register.Bits);
        Assert.True(register.Editable);
    }

    [Fact]
    public void ParseResponse_DeserializesMemoryBlockModels()
    {
        var response = MameDebuggerProtocol.ParseResponse(
            "{\"id\":6,\"ok\":true,\"result\":{\"cpu\":\":maincpu\",\"addressSpace\":\"program\",\"startAddress\":4096,\"length\":4,\"bytes\":[1,32,127,255],\"hex\":\"01 20 7F FF\"}}");

        var block = response.Result!.Value.Deserialize<MameDebuggerMemoryBlock>(MameDebuggerProtocol.JsonOptions)!;

        Assert.Equal(":maincpu", block.Cpu);
        Assert.Equal("program", block.AddressSpace);
        Assert.Equal(0x1000, block.StartAddress);
        Assert.Equal(4, block.Length);
        Assert.Equal(new byte[] { 1, 32, 127, 255 }, block.Bytes);
        Assert.Equal("01 20 7F FF", block.Hex);
    }

    [Fact]
    public void CreateCommand_SerializesRegisterSetRequest()
    {
        var command = MameDebuggerProtocol.CreateCommand(10, "regs.set", new MameDebuggerRegisterRequest(":maincpu", "PC", 0x1234));
        var payload = command[("debug ".Length)..];
        var request = System.Text.Json.JsonSerializer.Deserialize<MameDebuggerRequest<MameDebuggerRegisterRequest>>(payload, MameDebuggerProtocol.JsonOptions);

        Assert.NotNull(request);
        Assert.Equal(10, request.Id);
        Assert.Equal("regs.set", request.Op);
        Assert.Equal(":maincpu", request.Payload.Cpu);
        Assert.Equal("PC", request.Payload.Name);
        Assert.Equal(0x1234, request.Payload.Value);
    }

    [Fact]
    public void CreateCommand_SerializesMemoryWriteRequestWithBytesAndHexEdgeCases()
    {
        var command = MameDebuggerProtocol.CreateCommand(
            11,
            "mem.write",
            new MameDebuggerMemoryWriteRequest(":maincpu", 0x2000, [0, 16, 255], "program", "00 10 FF \"quoted\""));
        var payload = command[("debug ".Length)..];
        var request = System.Text.Json.JsonSerializer.Deserialize<MameDebuggerRequest<MameDebuggerMemoryWriteRequest>>(payload, MameDebuggerProtocol.JsonOptions);

        Assert.NotNull(request);
        Assert.Equal(11, request.Id);
        Assert.Equal("mem.write", request.Op);
        Assert.Equal(0x2000, request.Payload.StartAddress);
        Assert.Equal(new byte[] { 0, 16, 255 }, request.Payload.Bytes);
        Assert.Equal("program", request.Payload.AddressSpace);
        Assert.Equal("00 10 FF \"quoted\"", request.Payload.Hex);
        Assert.Contains("\\\"quoted\\\"", command);
    }

    [Fact]
    public async Task Service_RegisterAndMemoryRequests_CorrelateByRequestId()
    {
        var runner = new RecordingMameProcessRunner();
        var service = new MameDebuggerService(runner);
        var registersTask = service.GetRegistersAsync(new MameDebuggerRegisterRequest(":maincpu"), CancellationToken.None);
        var memoryTask = service.ReadMemoryAsync(new MameDebuggerMemoryReadRequest(":maincpu", 0x1000, 2, "program"), CancellationToken.None);

        service.ProcessStdoutLine("@OASIS_DEBUG {\"id\":2,\"ok\":true,\"result\":{\"cpu\":\":maincpu\",\"addressSpace\":\"program\",\"startAddress\":4096,\"length\":2,\"bytes\":[170,85],\"hex\":\"AA 55\"}}");
        service.ProcessStdoutLine("@OASIS_DEBUG {\"id\":1,\"ok\":true,\"result\":[{\"cpu\":\":maincpu\",\"name\":\"PC\",\"value\":4660,\"displayValue\":\"0x1234\",\"editable\":true}]}");

        var registers = await registersTask;
        var block = await memoryTask;

        Assert.Equal("PC", Assert.Single(registers).Name);
        Assert.Equal(new byte[] { 170, 85 }, block.Bytes);
        Assert.Equal("debug {\"id\":1,\"op\":\"regs.get\",\"payload\":{\"cpu\":\":maincpu\"}}", runner.Commands[0]);
        Assert.Equal("debug {\"id\":2,\"op\":\"mem.read\",\"payload\":{\"cpu\":\":maincpu\",\"startAddress\":4096,\"length\":2,\"addressSpace\":\"program\"}}", runner.Commands[1]);
    }

    [Fact]
    public async Task Service_SetRegisterAndWriteMemoryRequests_CorrelateByRequestId()
    {
        var runner = new RecordingMameProcessRunner();
        var service = new MameDebuggerService(runner);
        var registerTask = service.SetRegisterAsync(new MameDebuggerRegisterRequest(":maincpu", "A", 0x42), CancellationToken.None);
        var writeTask = service.WriteMemoryAsync(new MameDebuggerMemoryWriteRequest(":maincpu", 0x2000, [0x12, 0x34], "program"), CancellationToken.None);

        service.ProcessStdoutLine("@OASIS_DEBUG {\"id\":2,\"ok\":true,\"result\":{\"cpu\":\":maincpu\",\"addressSpace\":\"program\",\"startAddress\":8192,\"length\":2,\"bytes\":[18,52],\"hex\":\"12 34\"}}");
        service.ProcessStdoutLine("@OASIS_DEBUG {\"id\":1,\"ok\":true,\"result\":{\"cpu\":\":maincpu\",\"name\":\"A\",\"value\":66,\"displayValue\":\"0x42\",\"editable\":true}}");

        var register = await registerTask;
        var block = await writeTask;

        Assert.Equal("A", register.Name);
        Assert.Equal(new byte[] { 0x12, 0x34 }, block.Bytes);
        Assert.Equal("debug {\"id\":1,\"op\":\"regs.set\",\"payload\":{\"cpu\":\":maincpu\",\"name\":\"A\",\"value\":66}}", runner.Commands[0]);
        Assert.Equal("debug {\"id\":2,\"op\":\"mem.write\",\"payload\":{\"cpu\":\":maincpu\",\"startAddress\":8192,\"bytes\":[18,52],\"addressSpace\":\"program\"}}", runner.Commands[1]);
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
