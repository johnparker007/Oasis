using Oasis.Scripting;
using Xunit;

namespace Oasis.Scripting.Tests;

public sealed class AlgorithmicPoolTests
{
    private static OasisScriptSession Session(PoolHost host)
    {
        var source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Pool/algorithmic.oasis"));
        var compiled = OasisScriptCompiler.Compile(source, "Pool/algorithmic.oasis");
        Assert.True(compiled.Success, string.Join(Environment.NewLine, compiled.Diagnostics));
        return new(compiled.Program!, host);
    }
    private static OasisScriptReferenceValue[] Refs(OasisScriptSession session, string name)
    {
        Assert.True(session.TryGetState(name, out var value));
        return Assert.IsType<OasisScriptListValue>(value).Values.Cast<OasisScriptReferenceValue>().ToArray();
    }
    private static void Dispatch(OasisScriptSession session, OasisScriptEvent value) => Assert.True(session.Dispatch(value), session.LastRuntimeDiagnostic?.ToString());
    private static OasisScriptEvent Pocket(OasisScriptReferenceValue ball, int index = 0) => OasisScriptEvent.TriggerEntered(
        OasisScriptReferenceValue.Trigger(new[] { "Pocket_XNeg_Middle", "Pocket_XNeg_YNeg", "Pocket_XNeg_YPos", "Pocket_XPos_Middle", "Pocket_XPos_YNeg", "Pocket_XPos_YPos" }[index]), ball);

    [Theory]
    [InlineData(0)]
    [InlineData(30)]
    public void StartupGeneratesFiveRowsAndSeparateCueInMeasuredFrames(double yaw)
    {
        var host = new PoolHost { Yaw = yaw };
        var session = Session(host);
        Dispatch(session, OasisScriptEvent.MachineStarted());
        var balls = Refs(session, "balls");
        Assert.Equal(15, balls.Length);
        Assert.Single(Refs(session, "cue"));
        Assert.Equal(16, host.Live.Count);
        Assert.Equal(16, host.Live.Keys.Distinct().Count());
        for (var row = 0; row < 5; row++)
            for (var col = 0; col <= row; col++)
            {
                var ball = host.Live[balls[row * (row + 1) / 2 + col].Id];
                var local = host.ToLocal(ball.Position);
                Assert.Equal((col - row / 2d) * .0485, local.X, 8);
                Assert.Equal(.82704955, local.Y, 8);
                Assert.Equal(.15 + row * .0485 * Math.Sqrt(3) / 2, local.Z, 8);
            }
        var positions = host.Live.Values.Select(b => b.Position).ToArray();
        for (var first = 0; first < positions.Length; first++)
            for (var second = first + 1; second < positions.Length; second++)
                Assert.True(Distance(positions[first], positions[second]) >= .04849);
    }

    [Fact]
    public void CollectionIsIdempotentCueReturnsAndRerackRetainsIdentities()
    {
        var host = new PoolHost(); var session = Session(host);
        Dispatch(session, OasisScriptEvent.MachineStarted());
        var balls = Refs(session, "balls"); var cue = Assert.Single(Refs(session, "cue"));
        for (var index = 0; index < balls.Length; index++)
        {
            Dispatch(session, Pocket(balls[index], index % 6));
            var point = host.ToLocal(host.Live[balls[index].Id].Position);
            Assert.Equal((index - 7) * .0485, point.X, 8);
            Assert.Equal(-.65, point.Z, 8);
            host.Live[balls[index].Id].Position = new(7, 8, 9);
            Dispatch(session, Pocket(balls[index]));
            Assert.Equal(7, host.Live[balls[index].Id].Position.X);
        }
        Assert.Equal(15, Refs(session, "collected").Length);
        host.Live[cue.Id].Position = new(7, 8, 9);
        Dispatch(session, Pocket(cue));
        Assert.Equal(-.35, host.ToLocal(host.Live[cue.Id].Position).Z, 8);
        Assert.Equal(15, Refs(session, "collected").Length);
        Dispatch(session, OasisScriptEvent.InputPressed(OasisScriptReferenceValue.Input("rerack")));
        Assert.Equal(balls.Select(b => b.Id), Refs(session, "balls").Select(b => b.Id));
        Assert.Equal(cue.Id, Assert.Single(Refs(session, "cue")).Id);
        Assert.Empty(Refs(session, "collected"));
        Assert.All(host.Live.Values, ball => Assert.True(ball.MotionCleared));
        Dispatch(session, Pocket(balls[0]));
        Assert.Single(Refs(session, "collected"));
    }

    [Fact]
    public void NewGameReplacesEveryEntityWithoutIncreasingLiveCount()
    {
        var host = new PoolHost(); var session = Session(host);
        Dispatch(session, OasisScriptEvent.MachineStarted());
        for (var repeat = 0; repeat < 8; repeat++)
        {
            var old = host.Live.Keys.ToHashSet();
            Dispatch(session, OasisScriptEvent.InputPressed(OasisScriptReferenceValue.Input("newGame")));
            Assert.Equal(16, host.Live.Count);
            Assert.False(old.Overlaps(host.Live.Keys));
            Assert.Empty(Refs(session, "collected"));
        }
    }

    [Fact]
    public void InvalidRackIsRejectedBeforeAllocatingOrDestroyingAnyBall()
    {
        var host = new PoolHost { Width = .05 }; var session = Session(host);
        Assert.False(session.Dispatch(OasisScriptEvent.MachineStarted()));
        Assert.Contains("rack", session.LastRuntimeDiagnostic!.ToString());
        Assert.Empty(host.Live);
        Assert.Equal(0, host.Allocations);
    }

    private static double Distance(OasisScriptVec3Value a, OasisScriptVec3Value b) => Math.Sqrt(Math.Pow(a.X-b.X,2)+Math.Pow(a.Y-b.Y,2)+Math.Pow(a.Z-b.Z,2));
    private sealed class Ball(OasisScriptReferenceValue reference, OasisScriptVec3Value position)
    {
        public OasisScriptReferenceValue Reference = reference;
        public OasisScriptVec3Value Position = position;
        public bool MotionCleared;
    }
    private sealed class PoolHost : IOasisScriptValueHost
    {
        public readonly Dictionary<string, Ball> Live = [];
        public double Yaw;
        public double Width = .778;
        public int Allocations;
        public OasisScriptVec3Value ToLocal(OasisScriptVec3Value value) => Rotate(value, -Yaw);
        private static OasisScriptVec3Value Rotate(OasisScriptVec3Value p, double yaw)
        {
            var c = Math.Cos(yaw * Math.PI / 180); var s = Math.Sin(yaw * Math.PI / 180);
            return new(p.X*c + p.Z*s, p.Y, -p.X*s + p.Z*c);
        }
        private OasisScriptVec3Value Frame(string id, OasisScriptVec3Value offset)
        {
            var origin = id switch { "rackOrigin" => new OasisScriptVec3Value(0,.80154955,.15), "collectionTray" => new(0,.80154955,-.65), "cueStart" => new(0,.80154955,-.35), _ => throw new InvalidOperationException(id) };
            return Rotate(new(origin.X+offset.X,origin.Y+offset.Y,origin.Z+offset.Z), Yaw);
        }
        public OasisScriptHostResult ResolveObject(string id) => Live.TryGetValue(id, out var ball) ? OasisScriptHostResult.Return(ball.Reference) : OasisScriptHostResult.Fail("Missing live object.");
        private Ball Get(OasisScriptReferenceValue reference)
        {
            if (!reference.IsLiveObject || reference.SessionId != "pool-test" || !Live.TryGetValue(reference.Id,out var ball) || ball.Reference.Generation != reference.Generation) throw new InvalidOperationException("Stale reference.");
            return ball;
        }
        public OasisScriptHostResult Query(string operation, IReadOnlyList<OasisScriptValue> args)
        {
            var first = args[0];
            switch (operation)
            {
                case "collider.sphere_radius": return OasisScriptHostResult.Return(new OasisScriptNumberValue(.024));
                case "frame.position": return OasisScriptHostResult.Return(Frame(((OasisScriptReferenceValue)first).Id,(OasisScriptVec3Value)args[1]));
                case "frame.rotation": return OasisScriptHostResult.Return(new OasisScriptVec3Value(0,Yaw,0));
                case "surface.contains_sphere":
                    var p=ToLocal((OasisScriptVec3Value)args[1]); var radius=((OasisScriptNumberValue)args[2]).Value;
                    return OasisScriptHostResult.Return(new OasisScriptBoolValue(Math.Abs(p.X)+radius <= Width/2 && Math.Abs(p.Z)+radius <= .79 && p.Y >= .80154955+radius-.01));
                case "oasis.instantiate":
                    var id="@spawn:"+(++Allocations); var reference=OasisScriptReferenceValue.OasisObject(id,"pool-test",Allocations);
                    Live.Add(id,new(reference,(OasisScriptVec3Value)args[1])); return OasisScriptHostResult.Return(reference);
                case "oasis.destroy": Get((OasisScriptReferenceValue)first); Live.Remove(((OasisScriptReferenceValue)first).Id); return OasisScriptHostResult.Ok;
                case "oasis.is_alive": return OasisScriptHostResult.Return(new OasisScriptBoolValue(Live.ContainsKey(((OasisScriptReferenceValue)first).Id)));
                default: return OasisScriptHostResult.Fail("Unexpected query: "+operation);
            }
        }
        public OasisScriptHostResult SetActive(OasisScriptReferenceValue value,bool active) { Get(value); return OasisScriptHostResult.Ok; }
        public OasisScriptHostResult Teleport(OasisScriptReferenceValue value,OasisScriptReferenceValue anchor) => TeleportPose(value,Frame(anchor.Id,new(0,0,0)),new(0,0,0));
        public OasisScriptHostResult TeleportPose(OasisScriptReferenceValue value,OasisScriptVec3Value position,OasisScriptVec3Value rotation) { Get(value).Position=position; return OasisScriptHostResult.Ok; }
        public OasisScriptHostResult SetVelocity(OasisScriptReferenceValue value,OasisScriptVec3Value velocity) { Get(value).MotionCleared=true; return OasisScriptHostResult.Ok; }
        public OasisScriptHostResult SetAngularVelocity(OasisScriptReferenceValue value,OasisScriptVec3Value velocity) { Get(value); return OasisScriptHostResult.Ok; }
        public OasisScriptHostResult ApplyImpulse(OasisScriptReferenceValue value,OasisScriptVec3Value impulse) { Get(value); return OasisScriptHostResult.Ok; }
        public OasisScriptHostResult ResetObject(OasisScriptReferenceValue value) { Get(value); return OasisScriptHostResult.Ok; }
        public OasisScriptHostResult StartTimer(string id,double seconds) => OasisScriptHostResult.Ok;
        public OasisScriptHostResult StopTimer(string id) => OasisScriptHostResult.Ok;
    }
}
