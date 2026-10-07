using Oasis.Scripting;
using Xunit;

namespace Oasis.Scripting.Tests;

public sealed class OasisScriptSessionTests
{
    private static OasisScriptProgram Compile(string source)
    {
        var result = OasisScriptCompiler.Compile(source, "behavior.oasis");
        Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
        return result.Program!;
    }
    private static OasisScriptSession Session(string source, RecordingHost? host = null, int budget = 10000) => new(Compile(source), host ?? new(), new(budget));
    private static double State(OasisScriptSession session, string name = "score")
    { Assert.True(session.TryGetState(name, out var value)); return Assert.IsType<OasisScriptNumberValue>(value).Value; }

    [Fact] public void InitializationRunsOnceAndSessionsAreIndependent()
    {
        var program = Compile("const start = 2; const next = start + 1; state score = next; on machine.started() { score = score + 1; }");
        var first = new OasisScriptSession(program, new RecordingHost()); var second = new OasisScriptSession(program, new RecordingHost());
        Assert.True(first.TryGetConstant("next", out var constant)); Assert.Equal(3, Assert.IsType<OasisScriptNumberValue>(constant).Value);
        Assert.Equal(3, State(first)); Assert.True(first.Dispatch(OasisScriptEvent.MachineStarted()));
        Assert.True(first.Dispatch(OasisScriptEvent.MachineStarted())); Assert.Equal(5, State(first)); Assert.Equal(3, State(second));
        Assert.False(first.TryGetState("missing", out _));
    }

    [Fact] public void FiltersBindingsAndSourceOrder()
    {
        var host = new RecordingHost();
        var session = Session("state score = 0; on trigger.entered(trigger:pocket, ball) { score = score * 10 + 1; object.reset(ball); } on trigger.entered(pocket, ball) { score = score * 10 + 2; } on timer.elapsed(\"hide\") { score = score * 10 + 3; }", host);
        session.Dispatch(OasisScriptEvent.TriggerEntered(OasisScriptReferenceValue.Trigger("other"), OasisScriptReferenceValue.Object("ball08")));
        Assert.Equal(2, State(session)); Assert.Empty(host.Calls);
        session.Dispatch(OasisScriptEvent.TriggerEntered(OasisScriptReferenceValue.Trigger("pocket"), OasisScriptReferenceValue.Object("ball08")));
        Assert.Equal(212, State(session)); Assert.Equal("ball08", Assert.IsType<OasisScriptReferenceValue>(host.Calls[0].Values[0]).Id);
        session.Dispatch(OasisScriptEvent.TimerElapsed("other")); Assert.Equal(212, State(session));
        session.Dispatch(OasisScriptEvent.TimerElapsed("hide")); Assert.Equal(2123, State(session));
    }

    [Theory]
    [InlineData("1 + 2 * 3", 7)] [InlineData("(9 - 3) / 2", 3)] [InlineData("10 % 3", 1)] [InlineData("-5 + 2", -3)]
    public void Arithmetic(string expression, double expected)
    { var session = Session($"state score = 0; on machine.started() {{ score = {expression}; }}"); Assert.True(session.Dispatch(OasisScriptEvent.MachineStarted())); Assert.Equal(expected, State(session)); }

    [Theory]
    [InlineData("2 < 3")] [InlineData("2 <= 2")] [InlineData("3 > 2")] [InlineData("3 >= 3")]
    [InlineData("2 == 2")] [InlineData("2 != 3")] [InlineData("true == true")] [InlineData("\"a\" == \"a\"")]
    [InlineData("object:a == object:a")] [InlineData("object:a != object:b")]
    [InlineData("vec3(1, 2, 3) == vec3(1, 2, 3)")] [InlineData("[1, 2] == [1, 2]")]
    [InlineData("not false and true")] [InlineData("false or true")]
    public void TypedEqualityAndComparisons(string expression)
    { var session = Session($"state score = 0; on machine.started() {{ if {expression} {{ score = 1; }} else {{ score = 2; }} }}"); Assert.True(session.Dispatch(OasisScriptEvent.MachineStarted())); Assert.Equal(1, State(session)); }

    [Fact] public void LocalsNestedBranchesAndFreshScopes()
    {
        var session = Session("state score = 0; on machine.started() { let yes = true; if yes { let n = 2; if false { score = 99; } else { score = score + n; } } else { score = 88; } } on machine.started() { let yes = false; if yes { score = 77; } else { score = score + 1; } }");
        Assert.True(session.Dispatch(OasisScriptEvent.MachineStarted())); Assert.Equal(3, State(session));
        Assert.True(session.Dispatch(OasisScriptEvent.MachineStarted())); Assert.Equal(6, State(session));
    }
    [Fact] public void ListAndRangeIterationComputedIndexAndImmutability()
    {
        var session = Session("const list = [2, 4, 6]; state score = 0; on machine.started() { for n in list { score = score + n; } for i in range(1, 3) { score = score + list[i]; } let index = 4 / 2; score = score + list[index]; for empty in range(2, 2) { score = 99; } }");
        Assert.True(session.Dispatch(OasisScriptEvent.MachineStarted())); Assert.Equal(28, State(session));
        Assert.True(session.TryGetConstant("list", out var list)); Assert.IsAssignableFrom<IReadOnlyList<OasisScriptValue>>(Assert.IsType<OasisScriptListValue>(list).Values);
        var original = new OasisScriptValue[] { new OasisScriptNumberValue(1) }; var immutable = new OasisScriptListValue(OasisScriptType.Number, original);
        original[0] = new OasisScriptNumberValue(99); Assert.Equal(1, Assert.IsType<OasisScriptNumberValue>(immutable.Values[0]).Value);
        Assert.Throws<NotSupportedException>(() => ((IList<OasisScriptValue>)immutable.Values)[0] = original[0]);
    }
    [Theory]
    [InlineData("false and (1 / 0 > 0)", 2)] [InlineData("true or (1 / 0 > 0)", 1)]
    public void BooleanShortCircuit(string condition, double expected)
    { var session = Session($"state score = 0; on machine.started() {{ if {condition} {{ score = 1; }} else {{ score = 2; }} }}"); Assert.True(session.Dispatch(OasisScriptEvent.MachineStarted())); Assert.Equal(expected, State(session)); }

    [Theory]
    [InlineData("1 / 0", "OSR1002")] [InlineData("1 % 0", "OSR1002")]
    [InlineData("[1][2]", "OSR1004")] [InlineData("[1][-1]", "OSR1004")]
    [InlineData("[1][1 / 2]", "OSR1004")]
    public void NumericErrorsFaultWithLocationAndNoLaterHostCalls(string expression, string code)
    {
        var host = new RecordingHost(); var session = Session($"state score = 0;\non machine.started() {{ score = {expression}; object.reset(object:a); }}", host);
        Assert.False(session.Dispatch(OasisScriptEvent.MachineStarted())); Assert.True(session.IsFaulted);
        var error = session.LastRuntimeDiagnostic!; Assert.Equal(code, error.Code); Assert.Equal("behavior.oasis", error.SourceName); Assert.Equal("machine.started", error.EventName); Assert.Equal(2, error.Line); Assert.True(error.Column > 1);
        Assert.False(session.Dispatch(OasisScriptEvent.MachineStarted())); Assert.Same(error, session.LastRuntimeDiagnostic); Assert.Empty(host.Calls); Assert.Equal(0, State(session));
    }
    [Fact] public void NonFiniteArithmeticFaults()
    {
        var large = "1" + new string('0', 200); var session = Session($"state score = 0; on machine.started() {{ score = {large} * {large}; }}");
        Assert.False(session.Dispatch(OasisScriptEvent.MachineStarted())); Assert.Equal("OSR1003", session.LastRuntimeDiagnostic!.Code);
    }
    [Fact] public void InstructionLimitAndInitializationLimit()
    {
        var host = new RecordingHost(); var session = Session("on machine.started() { for i in range(0, 1000000) { object.reset(object:a); } }", host, 25);
        Assert.False(session.Dispatch(OasisScriptEvent.MachineStarted())); Assert.Equal("OSR1001", session.LastRuntimeDiagnostic!.Code); Assert.InRange(host.Calls.Count, 1, 3);
        var initialization = Session("const values = [1, 2, 3, 4, 5];", budget: 3);
        Assert.True(initialization.IsFaulted); Assert.Equal("initialization", initialization.LastRuntimeDiagnostic!.EventName);
    }
    [Fact] public void FreshBudgetForEveryEvent()
    {
        var session = Session("state score = 0; on machine.started() { score = score + 1; }", budget: 8);
        for (var i = 0; i < 100; i++) Assert.True(session.Dispatch(OasisScriptEvent.MachineStarted()));
        Assert.Equal(100, State(session));
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public void ExplicitAndUnexpectedHostFailuresAreConverted(bool throws)
    {
        var host = new RecordingHost { Failure = true, Throws = throws };
        var session = Session("on machine.started() { object.reset(object:a); timer.stop(\"later\"); }", host);
        Assert.False(session.Dispatch(OasisScriptEvent.MachineStarted())); Assert.Equal("OSR1005", session.LastRuntimeDiagnostic!.Code);
        Assert.Single(host.Calls); Assert.False(session.Dispatch(OasisScriptEvent.MachineStarted())); Assert.Single(host.Calls);
    }
    [Fact] public void EveryHostBuiltinDelegatesTypedValues()
    {
        var host = new RecordingHost(); var session = Session("on machine.started() { object.set_active(object:a, false); object.teleport(object:a, anchor:b); object.teleport_pose(object:a, vec3(1, 2, 3), vec3(4, 5, 6)); object.set_velocity(object:a, vec3(7, 8, 9)); object.set_angular_velocity(object:a, vec3(10, 11, 12)); object.apply_impulse(object:a, vec3(13, 14, 15)); object.reset(object:a); timer.start(\"hide\", 0.5); timer.stop(\"hide\"); }", host);
        Assert.True(session.Dispatch(OasisScriptEvent.MachineStarted()));
        Assert.Equal(new[] { "SetActive", "Teleport", "TeleportPose", "SetVelocity", "SetAngularVelocity", "ApplyImpulse", "ResetObject", "StartTimer", "StopTimer" }, host.Calls.Select(x => x.Name));
        Assert.Equal(OasisScriptType.ObjectRef, host.Calls[1].Values[0].Type); Assert.Equal(OasisScriptType.AnchorRef, host.Calls[1].Values[1].Type);
        Assert.False(Assert.IsType<OasisScriptBoolValue>(host.Calls[0].Values[1]).Value);
        var position = Assert.IsType<OasisScriptVec3Value>(host.Calls[2].Values[1]); Assert.Equal((1d, 2d, 3d), (position.X, position.Y, position.Z));
        Assert.Equal(6, Assert.IsType<OasisScriptVec3Value>(host.Calls[2].Values[2]).Z);
        Assert.Equal(9, Assert.IsType<OasisScriptVec3Value>(host.Calls[3].Values[1]).Z);
        Assert.Equal(12, Assert.IsType<OasisScriptVec3Value>(host.Calls[4].Values[1]).Z);
        Assert.Equal(15, Assert.IsType<OasisScriptVec3Value>(host.Calls[5].Values[1]).Z);
        Assert.Equal("hide", Assert.IsType<OasisScriptStringValue>(host.Calls[7].Values[0]).Value); Assert.Equal(0.5, Assert.IsType<OasisScriptNumberValue>(host.Calls[7].Values[1]).Value);
    }
    [Fact] public void PoolPocketUsesPureHost()
    {
        var host = new RecordingHost(); var session = Session("state pocketed = 0; on trigger.entered(trigger:PocketLeftCorner, ball) { object.teleport(ball, anchor:traySlot01); object.set_velocity(ball, vec3(0, 0, 0)); pocketed = pocketed + 1; }", host);
        Assert.True(session.Dispatch(OasisScriptEvent.TriggerEntered(OasisScriptReferenceValue.Trigger("PocketLeftCorner"), OasisScriptReferenceValue.Object("ball08"))));
        Assert.Equal("ball08", Assert.IsType<OasisScriptReferenceValue>(host.Calls[0].Values[0]).Id); Assert.Equal("traySlot01", Assert.IsType<OasisScriptReferenceValue>(host.Calls[0].Values[1]).Id);
        var velocity = Assert.IsType<OasisScriptVec3Value>(host.Calls[1].Values[1]); Assert.Equal((0d, 0d, 0d), (velocity.X, velocity.Y, velocity.Z)); Assert.Equal(1, State(session, "pocketed"));
    }
    [Fact] public void WhacAMoleUsesPureTimerEvents()
    {
        var host = new RecordingHost(); var session = Session("state raised = false; on machine.started() { object.teleport(object:mole1, anchor:mole1Up); timer.start(\"hideMole\", 0.5); raised = true; } on timer.elapsed(\"hideMole\") { object.teleport(object:mole1, anchor:mole1Down); raised = false; }", host);
        Assert.True(session.Dispatch(OasisScriptEvent.MachineStarted())); session.TryGetState("raised", out var raised); Assert.True(Assert.IsType<OasisScriptBoolValue>(raised).Value);
        Assert.Equal("mole1Up", Assert.IsType<OasisScriptReferenceValue>(host.Calls[0].Values[1]).Id);
        Assert.Equal("hideMole", Assert.IsType<OasisScriptStringValue>(host.Calls[1].Values[0]).Value);
        Assert.True(session.Dispatch(OasisScriptEvent.TimerElapsed("hideMole"))); session.TryGetState("raised", out raised); Assert.False(Assert.IsType<OasisScriptBoolValue>(raised).Value);
        Assert.Equal("mole1Down", Assert.IsType<OasisScriptReferenceValue>(host.Calls[2].Values[1]).Id);
    }
    [Fact] public void EveryEventFactoryDispatchesAndRejectsWrongReferenceDomain()
    {
        var session = Session("state score = 0; on input.pressed(i) { score = score + 1; } on input.released(i) { score = score + 1; } on trigger.exited(t, o) { score = score + 1; } on collision.entered(o, other) { score = score + 1; } on collision.exited(o, other) { score = score + 1; }");
        session.Dispatch(OasisScriptEvent.InputPressed(OasisScriptReferenceValue.Input("add"))); session.Dispatch(OasisScriptEvent.InputReleased(OasisScriptReferenceValue.Input("add")));
        session.Dispatch(OasisScriptEvent.TriggerExited(OasisScriptReferenceValue.Trigger("t"), OasisScriptReferenceValue.Object("o")));
        session.Dispatch(OasisScriptEvent.CollisionEntered(OasisScriptReferenceValue.Object("o"), OasisScriptReferenceValue.Object("other")));
        session.Dispatch(OasisScriptEvent.CollisionExited(OasisScriptReferenceValue.Object("o"), OasisScriptReferenceValue.Object("other")));
        Assert.Equal(5, State(session)); Assert.Throws<ArgumentException>(() => OasisScriptEvent.InputPressed(OasisScriptReferenceValue.Object("wrong")));
    }
    [Fact] public void RangeAdvancesExactlyAboveDoubleUnitResolution()
    {
        var session = Session("state score = 0; on machine.started() { for i in range(10000000000000000, 10000000000000002) { score = score + 1; } }");
        Assert.True(session.Dispatch(OasisScriptEvent.MachineStarted())); Assert.Equal(2, State(session));
        Assert.Throws<ArgumentOutOfRangeException>(() => new OasisScriptRangeValue(double.PositiveInfinity, double.PositiveInfinity));
        Assert.Throws<ArgumentOutOfRangeException>(() => new OasisScriptRangeValue(1.5, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => new OasisScriptRangeValue(2, 1));
    }
    [Fact] public void InitializationNumericFaultUsesStructuredDiagnostic()
    {
        var session = Session("const n = 1 / 0; state score = 0;");
        Assert.True(session.IsFaulted); Assert.Equal("OSR1002", session.LastRuntimeDiagnostic!.Code);
        Assert.Equal("initialization", session.LastRuntimeDiagnostic.EventName);
        Assert.False(session.Dispatch(OasisScriptEvent.MachineStarted()));
    }
    [Fact] public void CompiledProgramCollectionsAreImmutable()
    {
        var program = Compile("const n = 1; on machine.started() { timer.stop(\"id\"); }");
        Assert.Throws<NotSupportedException>(() => ((IList<OasisScriptVariable>)program.Constants).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<OasisScriptDeclarationSyntax>)program.Syntax.Declarations).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<OasisScriptStatementSyntax>)program.EventHandlers[0].Body.Statements).Clear());
    }
    internal sealed record Call(string Name, OasisScriptValue[] Values);
    internal sealed class RecordingHost : IOasisScriptHost
    {
        public List<Call> Calls { get; } = new(); public bool Failure { get; set; } public bool Throws { get; set; }
        private OasisScriptHostResult Record(string name, params OasisScriptValue[] args)
        { Calls.Add(new(name, args)); if (Throws) throw new InvalidOperationException("unexpected host error"); return Failure ? OasisScriptHostResult.Fail("missing object") : OasisScriptHostResult.Ok; }
        public OasisScriptHostResult SetActive(OasisScriptReferenceValue obj, bool active) => Record("SetActive", obj, new OasisScriptBoolValue(active));
        public OasisScriptHostResult Teleport(OasisScriptReferenceValue obj, OasisScriptReferenceValue anchor) => Record("Teleport", obj, anchor);
        public OasisScriptHostResult TeleportPose(OasisScriptReferenceValue obj, OasisScriptVec3Value position, OasisScriptVec3Value rotation) => Record("TeleportPose", obj, position, rotation);
        public OasisScriptHostResult SetVelocity(OasisScriptReferenceValue obj, OasisScriptVec3Value value) => Record("SetVelocity", obj, value);
        public OasisScriptHostResult SetAngularVelocity(OasisScriptReferenceValue obj, OasisScriptVec3Value value) => Record("SetAngularVelocity", obj, value);
        public OasisScriptHostResult ApplyImpulse(OasisScriptReferenceValue obj, OasisScriptVec3Value value) => Record("ApplyImpulse", obj, value);
        public OasisScriptHostResult ResetObject(OasisScriptReferenceValue obj) => Record("ResetObject", obj);
        public OasisScriptHostResult StartTimer(string name, double seconds) => Record("StartTimer", new OasisScriptStringValue(name), new OasisScriptNumberValue(seconds));
        public OasisScriptHostResult StopTimer(string name) => Record("StopTimer", new OasisScriptStringValue(name));
    }
}
