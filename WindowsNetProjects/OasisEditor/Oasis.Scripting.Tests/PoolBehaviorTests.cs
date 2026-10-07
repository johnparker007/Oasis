using Oasis.Scripting;
using Xunit;
using Host = Oasis.Scripting.Tests.OasisScriptSessionTests.RecordingHost;

namespace Oasis.Scripting.Tests;

public sealed class PoolBehaviorTests
{
    private static OasisScriptSession Create(Host host)
    {
        var source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Pool/behavior.oasis"));
        var result = OasisScriptCompiler.Compile(source, "Pool/behavior.oasis");
        Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
        return new(result.Program!, host);
    }
    private static void Dispatch(OasisScriptSession session, OasisScriptEvent value) =>
        Assert.True(session.Dispatch(value), session.LastRuntimeDiagnostic?.ToString());
    private static OasisScriptEvent Pocket(string ball, int pocket = 0) => OasisScriptEvent.TriggerEntered(
        OasisScriptReferenceValue.Trigger(Pockets[pocket]), OasisScriptReferenceValue.Object(ball));
    private static readonly string[] Pockets = { "PocketLeftCorner", "PocketLeftMiddle", "PocketLeftFarCorner", "PocketRightCorner", "PocketRightMiddle", "PocketRightFarCorner" };
    private static double Count(OasisScriptSession s)
    { Assert.True(s.TryGetState("trayCount", out var value)); return Assert.IsType<OasisScriptNumberValue>(value).Value; }
    private static void Placement(Host host, string ball, string anchor, bool activate)
    {
        Assert.Equal(activate ? 4 : 3, host.Calls.Count);
        var offset = activate ? 1 : 0;
        if (activate) { Assert.Equal("SetActive", host.Calls[0].Name); Assert.True(Assert.IsType<OasisScriptBoolValue>(host.Calls[0].Values[1]).Value); }
        Assert.Equal("Teleport", host.Calls[offset].Name);
        Assert.Equal(ball, Assert.IsType<OasisScriptReferenceValue>(host.Calls[offset].Values[0]).Id);
        Assert.Equal(anchor, Assert.IsType<OasisScriptReferenceValue>(host.Calls[offset].Values[1]).Id);
        Assert.Equal(new[] { "SetVelocity", "SetAngularVelocity" }, host.Calls.Skip(offset + 1).Select(c => c.Name));
        foreach (var call in host.Calls.Skip(offset + 1))
        { var v = Assert.IsType<OasisScriptVec3Value>(call.Values[1]); Assert.Equal((0d, 0d, 0d), (v.X, v.Y, v.Z)); }
    }
    private static void Reset(OasisScriptSession s, Host host, OasisScriptEvent value)
    {
        host.Calls.Clear(); Dispatch(s, value);
        Assert.Equal(64, host.Calls.Count); Assert.Equal(0, Count(s));
        var calls = host.Calls.ToArray();
        for (var i = 0; i < 16; i++)
        {
            host.Calls.Clear(); host.Calls.AddRange(calls.Skip(i * 4).Take(4));
            Placement(host, i == 15 ? "cueBall" : $"ball{i + 1:00}", i == 15 ? "rackCueBall" : $"rackBall{i + 1:00}", true);
        }
        Assert.True(s.TryGetState("collected", out var flags));
        Assert.All(Assert.IsType<OasisScriptListValue>(flags).Values, flag => Assert.False(Assert.IsType<OasisScriptBoolValue>(flag).Value));
        host.Calls.Clear();
    }

    [Fact] public void StartupAndEveryPocketCollectConsecutivelyWithoutDuplicateEffectsOrOverflow()
    {
        var host = new Host(); var s = Create(host); Reset(s, host, OasisScriptEvent.MachineStarted());
        // Deliberately different from ball-number order.
        var order = new[] { 8, 1, 15, 2, 14, 3, 13, 4, 12, 5, 11, 6, 10, 7, 9 };
        for (var i = 0; i < 15; i++)
        {
            host.Calls.Clear(); var ball = $"ball{order[i]:00}";
            Dispatch(s, Pocket(ball, i % 6)); Placement(host, ball, $"traySlot{i + 1:00}", false);
            Assert.Equal(i + 1, Count(s)); host.Calls.Clear();
            Dispatch(s, Pocket(ball, i % 6)); Dispatch(s, Pocket(ball, (i + 1) % 6));
            Assert.Empty(host.Calls); Assert.Equal(i + 1, Count(s));
        }
        foreach (var ball in order) Dispatch(s, Pocket($"ball{ball:00}"));
        Assert.Empty(host.Calls); Assert.Equal(15, Count(s)); Assert.False(s.IsFaulted);
    }

    [Fact] public void UnknownPayloadsAndCueScratchesDoNotConsumeSlotsOrChangeFlags()
    {
        var host = new Host(); var s = Create(host); Reset(s, host, OasisScriptEvent.MachineStarted());
        Dispatch(s, Pocket("ball08")); host.Calls.Clear();
        Dispatch(s, Pocket("decoration"));
        Dispatch(s, OasisScriptEvent.TriggerEntered(OasisScriptReferenceValue.Trigger("other"), OasisScriptReferenceValue.Object("ball01")));
        Dispatch(s, OasisScriptEvent.TriggerEntered(OasisScriptReferenceValue.Trigger("other"), OasisScriptReferenceValue.Object("cueBall")));
        Assert.Empty(host.Calls);
        Assert.True(s.TryGetState("collected", out var before));
        for (var i = 0; i < 6; i++)
        { host.Calls.Clear(); Dispatch(s, Pocket("cueBall", i)); Placement(host, "cueBall", "rackCueBall", true); Assert.Equal(1, Count(s)); }
        Assert.True(s.TryGetState("collected", out var after)); Assert.Same(before, after);
        host.Calls.Clear(); Dispatch(s, Pocket("ball01")); Placement(host, "ball01", "traySlot02", false);
    }

    [Theory] [InlineData("rerack", 3)] [InlineData("rerack", 15)] [InlineData("newGame", 3)] [InlineData("newGame", 15)]
    public void BothInputsResetPartialAndFullCollectionsAndPermitRecollection(string input, int balls)
    {
        var host = new Host(); var s = Create(host); Reset(s, host, OasisScriptEvent.MachineStarted());
        for (var i = 1; i <= balls; i++) Dispatch(s, Pocket($"ball{i:00}"));
        Reset(s, host, OasisScriptEvent.InputPressed(OasisScriptReferenceValue.Input(input)));
        Dispatch(s, Pocket("ball01")); Placement(host, "ball01", "traySlot01", false); Assert.Equal(1, Count(s));
    }

    [Fact] public void ReloadSessionHasFreshFlagsAndTrayOccupancy()
    {
        var host = new Host(); var old = Create(host); Dispatch(old, Pocket("ball08"));
        var fresh = Create(host); Assert.Equal(0, Count(fresh)); Reset(fresh, host, OasisScriptEvent.MachineStarted());
        Dispatch(fresh, Pocket("ball08")); Placement(host, "ball08", "traySlot01", false); Assert.Equal(1, Count(old));
    }
}
