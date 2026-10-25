using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MCPForUnity.Editor.Helpers;
using OasisPlayer.Loading;
using OasisPlayer.RuntimeBuild;
using UnityEngine;

namespace OasisPlayer.Editor.Verification
{
    /// <summary>Actual GLB/script/physics verification for the declared, entirely spawned Pool example.</summary>
    public static class AlgorithmicPoolVerification
    {
        public static async Task<object> Verify()
        {
            var checks = new List<string>();
            MachinePreviewLoader loader = null;
            GameObject spawn = null;
            var previousMode = Physics.simulationMode;
            try
            {
                Physics.simulationMode = SimulationMode.Script;
                if (UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Any(t => t.parent == null && t.name == "MachineSpawn"))
                    throw new InvalidOperationException("Existing MachineSpawn; refusing another scene/session.");
                ResolvedRuntimeBuild build;
                string error;
                if (!RuntimeBuildLoader.TryLoad(Environment.GetEnvironmentVariable("OASIS_POOL_VERIFY_BUILD"), out build, out error)) throw new InvalidDataException(error);
                if (build.Machine.objectInstances.Length != 0 || build.PrefabDependencies.Count != 2) throw new InvalidDataException("Expected no placed balls and exactly two declared aliases.");
                spawn = new GameObject("MachineSpawn");
                loader = new MachinePreviewLoader(new GltfFastCabinetModelLoader());
                var machine = await loader.LoadAsync(build);
                RequireClean(loader, machine);
                var balls = Numbered(machine);
                var cue = Cue(machine);
                if (balls.Length != 15 || machine.Objects.Count != 16 || machine.Objects.Keys.Distinct().Count() != 16) throw new InvalidOperationException("Expected exactly 15 balls and one separate cue.");
                if (machine.Objects.Values.Any(b => !machine.IsDynamicObject(b.Id))) throw new InvalidOperationException("An authored ball leaked into the spawned proving case.");
                checks.Add("Actual generated package loaded two spawn-only aliases and created 15 balls plus a separate cue");
                var initial = balls.Select(b => Position(machine, b)).ToArray();
                var diameter = 2 * ((SphereCollider)balls[0].Collider).radius * balls[0].Collider.transform.lossyScale.x;
                var rack = machine.GetAnchor("rackOrigin");
                var rackRotation = Quaternion.Euler(ToVector(rack.RotationEulerDegrees));
                for (var row = 0; row < 5; row++)
                    for (var col = 0; col <= row; col++)
                    {
                        var expected = ToVector(rack.Position) + rackRotation * new Vector3((col - row / 2f) * (diameter + .0005f), diameter / 2 + .0015f, row * (diameter + .0005f) * Mathf.Sqrt(3) / 2);
                        if (Vector3.Distance(initial[row * (row + 1) / 2 + col], expected) > .0001f) throw new InvalidOperationException("Calculated triangular rack position mismatch.");
                    }
                for (var first = 0; first < balls.Length; first++)
                    for (var second = first + 1; second < balls.Length; second++)
                        if (Vector3.Distance(initial[first], initial[second]) < diameter + .0004f) throw new InvalidOperationException("Initial ball spheres overlap.");
                checks.Add("Five centred rows use measured effective collider diameter, sqrt(3)/2 spacing and explicit clearance without overlap");
                Simulate(120);
                var settled = balls.Select(b => Position(machine, b)).ToArray();
                if (settled.Any(p => p.y < .8f) || settled.Where((p, i) => Vector3.Distance(p, initial[i]) > .03f).Any()) throw new InvalidOperationException("Rack did not remain supported after two seconds of actual physics.");
                checks.Add("All 16 real dynamic bodies remained supported after two seconds of Unity physics");
                var originalIds = machine.Objects.Keys.OrderBy(id => id, StringComparer.Ordinal).ToArray();
                foreach (var ball in machine.Objects.Values) Displace(ball);
                Input(machine, "rerack");
                RequireClean(loader, machine);
                if (!originalIds.SequenceEqual(machine.Objects.Keys.OrderBy(id => id, StringComparer.Ordinal))) throw new InvalidOperationException("Rerack replaced identities.");
                foreach (var ball in machine.Objects.Values) RequireCleared(ball);
                checks.Add("Rerack retained all identities, restored calculated poses and cleared linear/angular motion");
                var relays = spawn.GetComponentsInChildren<RuntimeTriggerRelay>(true);
                if (relays.Length != 6) throw new InvalidOperationException("Expected all six real Cabinet pocket relays.");
                var tray = machine.GetAnchor("collectionTray");
                var trayRotation = Quaternion.Euler(ToVector(tray.RotationEulerDegrees));
                for (var index = 0; index < 15; index++)
                {
                    relays[index % 6].PublishEntered(balls[index].Collider);
                    var expected = ToVector(tray.Position) + trayRotation * new Vector3((index - 7) * (diameter + .0005f), diameter / 2 + .0015f, 0);
                    if (Vector3.Distance(Position(machine, balls[index]), expected) > .0001f) throw new InvalidOperationException("Calculated collection slot mismatch.");
                    Displace(balls[index]);
                    var displaced = Position(machine, balls[index]);
                    relays[(index + 1) % 6].PublishEntered(balls[index].Collider);
                    if (Vector3.Distance(Position(machine, balls[index]), displaced) > .0001f) throw new InvalidOperationException("Duplicate collection moved a ball again.");
                }
                Displace(cue);
                relays[0].PublishEntered(cue.Collider);
                RequireCleared(cue);
                if (Mathf.Abs(Position(machine, cue).z - machine.GetAnchor("cueStart").Position.Z) > .0001f) throw new InvalidOperationException("Cue did not return independently.");
                checks.Add("All 15 calculated collection slots, duplicate guard and separate cue recovery passed through actual runtime relays");
                var naturalPockets = new List<string>();
                foreach (var relay in relays)
                {
                    Input(machine, "rerack");
                    var bounds = relay.GetComponent<Collider>().bounds;
                    var drop = new Vector3(bounds.center.x, bounds.max.y + .1f, bounds.center.z);
                    balls[0].Root.transform.position = drop;
                    balls[0].Rigidbody.position = drop;
                    balls[0].Rigidbody.WakeUp();
                    Simulate(120);
                    var target = ToVector(tray.Position) + trayRotation * new Vector3(-7 * (diameter + .0005f), diameter / 2, 0);
                    if (Vector3.Distance(Position(machine, balls[0]), target) > .02f) throw new InvalidOperationException("Natural pocket drop/collection support failed: " + relay.name);
                    naturalPockets.Add(relay.name);
                }
                checks.Add("Natural gravity drops through all six actual pocket volumes collected into a physically supported slot");
                for (var game = 0; game < 5; game++)
                {
                    var oldReferences = machine.Objects.Values.Select(b => machine.GetObjectReference(b.Id)).ToArray();
                    Input(machine, "newGame");
                    RequireClean(loader, machine);
                    if (machine.Objects.Count != 16 || Numbered(machine).Length != 15 || machine.ObjectNodes.Count != oldReferences.Length * balls[0].Nodes.Count)
                        throw new InvalidOperationException("Repeated newGame grew or lost registry entries.");
                    if (oldReferences.Any(machine.IsLiveObjectReference)) throw new InvalidOperationException("Destroyed references remained alive after newGame.");
                }
                checks.Add("Five new games replaced all entities, invalidated every old handle and kept root/node counts stable");
                var finalNodeCount = machine.ObjectNodes.Count;
                var finalIds = machine.Objects.Keys.ToArray();
                var liveReference = machine.GetObjectReference(finalIds[0]);
                loader.Unload();
                if (machine.Objects.Count != 0 || machine.ObjectNodes.Count != 0 || machine.IsLiveObjectReference(liveReference)) throw new InvalidOperationException("Unload left registry entries or a live handle.");
                await Task.Yield();
                machine = await loader.LoadAsync(build);
                RequireClean(loader, machine);
                if (machine.Objects.Count != 16 || machine.IsLiveObjectReference(liveReference)) throw new InvalidOperationException("Fresh reload reused old session handles.");
                checks.Add("Unload cleared registries/imports and fresh actual GLB reload spawned a new independent 16-ball session");
                return new SuccessResponse("Algorithmic Pool actual Player verification passed.", new { pending = false, checks,
                    objectCount = machine.Objects.Count, nodeCount = finalNodeCount, diameter, initialPositions = initial.Select(p => new[] { p.x,p.y,p.z }).ToArray(),
                    settledPositions = settled.Select(p => new[] { p.x,p.y,p.z }).ToArray(), naturalPockets,
                    limitations = "Real GLBs, script, collision volumes and controlled batch Play Mode physics. No rendered Player gameplay, keyboard-focus test, cue controls, numbering or complete Pool rules." });
            }
            catch (Exception exception) { return new ErrorResponse(exception.ToString()); }
            finally
            {
                if (loader != null) loader.Unload();
                if (spawn != null) UnityEngine.Object.Destroy(spawn);
                Physics.simulationMode = previousMode;
            }
        }
        private static RuntimeObjectInstance[] Numbered(RuntimeMachine machine) => machine.Objects.Values.Where(b => b.DisplayName == "poolBall").OrderBy(b => long.Parse(b.Id.Substring(7))).ToArray();
        private static RuntimeObjectInstance Cue(RuntimeMachine machine) => machine.Objects.Values.Single(b => b.DisplayName == "cueBall");
        private static Vector3 Position(RuntimeMachine machine, RuntimeObjectInstance ball) => machine.MachineSpace.InverseTransformPoint(ball.Root.transform.position);
        private static Vector3 ToVector(RuntimeVector3 value) => new Vector3(value.X,value.Y,value.Z);
        private static void Input(RuntimeMachine machine,string id) { machine.SetInputState(id,true); machine.SetInputState(id,false); }
        private static void Simulate(int steps) { Physics.SyncTransforms(); for (var i=0;i<steps;i++) Physics.Simulate(1f/60f); }
        private static void Displace(RuntimeObjectInstance ball)
        {
            ball.Root.transform.localPosition = new Vector3(3,4,5); ball.Rigidbody.position=ball.Root.transform.position;
            ball.Rigidbody.linearVelocity=Vector3.one; ball.Rigidbody.angularVelocity=Vector3.one;
        }
        private static void RequireCleared(RuntimeObjectInstance ball)
        {
            if (!ball.Root.activeSelf || ball.Rigidbody.linearVelocity.sqrMagnitude>.000001f || ball.Rigidbody.angularVelocity.sqrMagnitude>.000001f) throw new InvalidOperationException("Ball activation or velocity reset failed.");
        }
        private static void RequireClean(MachinePreviewLoader loader, RuntimeMachine machine)
        {
            if (!loader.HasOasisBehavior || loader.OasisBehaviorFault != null || machine.Warnings.Count != 0) throw new InvalidOperationException("Behavior failed or runtime warnings occurred: " + loader.OasisBehaviorFault);
        }
    }
}
