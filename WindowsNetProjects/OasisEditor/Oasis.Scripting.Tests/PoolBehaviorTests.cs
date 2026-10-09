using Oasis.Scripting;
using Xunit;
using Host = Oasis.Scripting.Tests.OasisScriptSessionTests.RecordingHost;

namespace Oasis.Scripting.Tests;

public sealed class PoolBehaviorTests
{
    private static readonly string[] Pockets = { "Pocket_XNeg_Middle", "Pocket_XNeg_YNeg", "Pocket_XNeg_YPos", "Pocket_XPos_Middle", "Pocket_XPos_YNeg", "Pocket_XPos_YPos" };

    private static OasisScriptSession Create(Host host)
    {
        var source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Pool/behavior.oasis"));
        var result = OasisScriptCompiler.Compile(source, "Pool/behavior.oasis");
        Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
        return new(result.Program!, host);
    }

    private static void Dispatch(OasisScriptSession session, OasisScriptEvent value) =>
        Assert.True(session.Dispatch(value), session.LastRuntimeDiagnostic?.ToString());

    private static OasisScriptEvent Pocket(string ball = "ball01", int pocket = 0) =>
        OasisScriptEvent.TriggerEntered(OasisScriptReferenceValue.Trigger(Pockets[pocket]), OasisScriptReferenceValue.Object(ball));

    private static bool Collected(OasisScriptSession session)
    {
        Assert.True(session.TryGetState("collected", out var value));
        return Assert.IsType<OasisScriptBoolValue>(value).Value;
    }

    private static void Placement(Host host, string anchor, bool activate)
    {
        Assert.Equal(activate ? 4 : 3, host.Calls.Count);
        var offset = activate ? 1 : 0;
        Assert.All(host.Calls, call => Assert.Equal("ball01", Assert.IsType<OasisScriptReferenceValue>(call.Values[0]).Id));
        if (activate)
        {
            Assert.Equal("SetActive", host.Calls[0].Name);
            Assert.True(Assert.IsType<OasisScriptBoolValue>(host.Calls[0].Values[1]).Value);
        }
        Assert.Equal("Teleport", host.Calls[offset].Name);
        Assert.Equal(anchor, Assert.IsType<OasisScriptReferenceValue>(host.Calls[offset].Values[1]).Id);
        Assert.Equal(new[] { "SetVelocity", "SetAngularVelocity" }, host.Calls.Skip(offset + 1).Select(c => c.Name));
        foreach (var call in host.Calls.Skip(offset + 1))
        {
            var velocity = Assert.IsType<OasisScriptVec3Value>(call.Values[1]);
            Assert.Equal((0d, 0d, 0d), (velocity.X, velocity.Y, velocity.Z));
        }
    }

    [Fact]
    public void StartupActivatesPlacesAndClearsBothVelocities()
    {
        var host = new Host(); var session = Create(host);
        Assert.False(Collected(session));
        Dispatch(session, OasisScriptEvent.MachineStarted());
        Placement(host, "rackBall01", true); Assert.False(Collected(session));
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)]
    public void EveryPocketCollectsOnceAndDuplicatesFromAllPocketsHaveNoEffects(int pocket)
    {
        var host = new Host(); var session = Create(host);
        Dispatch(session, OasisScriptEvent.MachineStarted()); host.Calls.Clear();
        Dispatch(session, Pocket(pocket: pocket));
        Placement(host, "traySlot01", false); Assert.True(Collected(session));
        host.Calls.Clear();
        Dispatch(session, Pocket(pocket: pocket));
        foreach (var other in Enumerable.Range(0, 6)) Dispatch(session, Pocket(pocket: other));
        Assert.Empty(host.Calls); Assert.True(Collected(session)); Assert.False(session.IsFaulted);
    }

    [Fact]
    public void UnrelatedTriggersObjectsAndTriggerExitsAreIgnored()
    {
        var host = new Host(); var session = Create(host);
        Dispatch(session, OasisScriptEvent.MachineStarted()); host.Calls.Clear();
        foreach (var pocket in Enumerable.Range(0, 6)) Dispatch(session, Pocket("decoration", pocket));
        Dispatch(session, OasisScriptEvent.TriggerEntered(OasisScriptReferenceValue.Trigger("other"), OasisScriptReferenceValue.Object("ball01")));
        Dispatch(session, OasisScriptEvent.TriggerExited(OasisScriptReferenceValue.Trigger(Pockets[0]), OasisScriptReferenceValue.Object("ball01")));
        Assert.Empty(host.Calls); Assert.False(Collected(session));
        Dispatch(session, Pocket()); Placement(host, "traySlot01", false); Assert.True(Collected(session));
    }

    [Theory] [InlineData("rerack", false)] [InlineData("rerack", true)]
    [InlineData("newGame", false)] [InlineData("newGame", true)]
    public void BothResetInputsClearStateRestoreBallAndPermitRecollection(string input, bool collectFirst)
    {
        var host = new Host(); var session = Create(host);
        Dispatch(session, OasisScriptEvent.MachineStarted());
        if (collectFirst) Dispatch(session, Pocket());
        Assert.Equal(collectFirst, Collected(session)); host.Calls.Clear();
        Dispatch(session, OasisScriptEvent.InputPressed(OasisScriptReferenceValue.Input(input)));
        Placement(host, "rackBall01", true); Assert.False(Collected(session));
        host.Calls.Clear();
        Dispatch(session, OasisScriptEvent.InputReleased(OasisScriptReferenceValue.Input(input))); Assert.Empty(host.Calls);
        Dispatch(session, Pocket(pocket: 5));
        Placement(host, "traySlot01", false); Assert.True(Collected(session));
    }

    [Fact]
    public void ReloadSessionHasFreshFlagIndependentOfCollectedSession()
    {
        var oldHost = new Host(); var old = Create(oldHost);
        Dispatch(old, OasisScriptEvent.MachineStarted()); Dispatch(old, Pocket()); Assert.True(Collected(old));
        var host = new Host(); var fresh = Create(host);
        Assert.False(Collected(fresh)); Dispatch(fresh, OasisScriptEvent.MachineStarted());
        Placement(host, "rackBall01", true); host.Calls.Clear();
        Dispatch(fresh, Pocket()); Placement(host, "traySlot01", false);
        Assert.True(Collected(old)); Assert.True(Collected(fresh));
    }
}
