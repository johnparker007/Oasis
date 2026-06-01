using OasisEditor.Features.MameDebugger;
using Xunit;

namespace OasisEditor.Tests;

public sealed class MameDebuggerProtocolTests
{
    [Fact]
    public void Parser_DetectsDebuggerResponsePrefix()
    {
        var parser = new MameDebuggerStdoutParser();

        var parsed = parser.TryParse("@OASIS_DEBUG {\"id\":7,\"ok\":true,\"result\":{\"pong\":\"pong\"}}", out var message);

        Assert.True(parsed);
        Assert.False(message.IsEvent);
        Assert.Equal(7, message.Payload.GetProperty("id").GetInt32());
    }

    [Fact]
    public void Parser_DetectsDebuggerEventBeforeResponsePrefix()
    {
        var parser = new MameDebuggerStdoutParser();

        var parsed = parser.TryParse("@OASIS_DEBUG_EVENT {\"event\":\"stopped\",\"state\":\"stopped\"}", out var message);

        Assert.True(parsed);
        Assert.True(message.IsEvent);
        Assert.Equal("stopped", message.Payload.GetProperty("event").GetString());
    }

    [Fact]
    public void Protocol_CreatesStructuredDebugCommand()
    {
        var line = MameDebuggerProtocol.CreateCommandLine(11, "status");

        Assert.Equal("debug {\"id\":11,\"op\":\"status\"}", line);
    }
}
