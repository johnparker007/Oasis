using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using MCPForUnity.Editor.Tools;
using MCPForUnity.Editor.Helpers;
using Newtonsoft.Json.Linq;
using OasisPlayer.Loading;
using OasisPlayer.RuntimeBuild;
using UnityEngine;
using UnityEditor;

namespace OasisPlayer.Editor.Verification
{
    // Opt-in, batch-only integration probe. Never loads/saves the user's scenes.
    [InitializeOnLoad]
    [McpForUnityTool("oasis_verify_pool_build", Description = "Opt-in batch-only Pool integration verification: load the configured disposable Editor build in Play Mode and test its real input/trigger/reset adapters. Never saves scenes.")]
    public static class OasisRuntimeVerification
    {
        private static Task<object> _pending;
        private const string Requested = "Oasis.PoolVerification.Requested";

        static OasisRuntimeVerification()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Requested, false)) _pending = Verify();
            };
        }

        public class Parameters
        {
            [ToolParameter("start or status. start loads the configured disposable build and exercises its single-ball input/trigger script.")]
            public string action { get; set; }
        }

        public static object HandleCommand(JObject parameters)
        {
            if (!Application.isBatchMode || string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OASIS_POOL_VERIFY_BUILD")))
                return new ErrorResponse("Only enabled in an explicitly configured disposable batch verification session.");
            var action = (string)parameters["action"];
            if (action == "start")
            {
                if (_pending != null || SessionState.GetBool(Requested, false)) return new ErrorResponse("One verification per batch process; inspect status.");
                if (EditorApplication.isPlayingOrWillChangePlaymode) return new ErrorResponse("A Play session already exists; refusing to interrupt it.");
                SessionState.SetBool(Requested, true);
                EditorApplication.EnterPlaymode();
                return new SuccessResponse("Verification started; poll status.", new { pending = true });
            }
            if (action != "status") return new ErrorResponse("Use start or status.");
            if (_pending == null) return SessionState.GetBool(Requested, false)
                ? (object)new SuccessResponse("Entering Play Mode.", new { pending = true }) : new ErrorResponse("Start verification first.");
            if (!_pending.IsCompleted) return new SuccessResponse("Verification running.", new { pending = true });
            return _pending.GetAwaiter().GetResult();
        }

        private static async Task<object> Verify()
        {
            if (Environment.GetEnvironmentVariable("OASIS_POOL_VERIFY_MODE") == "algorithmic") return await AlgorithmicPoolVerification.Verify();
            var checks = new List<string>();
            MachinePreviewLoader loader = null;
            GameObject spawn = null;
            var previousSimulationMode = Physics.simulationMode;
            try
            {
                Physics.simulationMode = SimulationMode.Script;
                if (UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Any(t => t.parent == null && t.name == "MachineSpawn"))
                    throw new InvalidOperationException("Existing MachineSpawn detected; refusing to touch another scene/session.");
                var directory = Path.GetFullPath(Environment.GetEnvironmentVariable("OASIS_POOL_VERIFY_BUILD"));
                ResolvedRuntimeBuild build;
                string error;
                if (!RuntimeBuildLoader.TryLoad(directory, out build, out error)) throw new InvalidDataException(error);
                checks.Add("RuntimeBuildLoader accepted the actual Editor-generated build");
                spawn = new GameObject("MachineSpawn");
                loader = new MachinePreviewLoader(new GltfFastCabinetModelLoader());
                var machine = await loader.LoadAsync(build);
                if (!loader.HasOasisBehavior || loader.OasisBehaviorFault != null) throw new InvalidOperationException("Behavior did not start cleanly.");
                var ball = machine.GetObject("ball01");
                var rack = machine.GetAnchor("rackBall01").Position;
                var tray = machine.GetAnchor("traySlot01").Position;
                if (ball.Rigidbody == null || ball.Collider == null) throw new InvalidOperationException("Ball Rigidbody/Collider missing.");
                AssertPose(machine, ball, new Vector3(rack.X, rack.Y, rack.Z));
                checks.Add("machine.started activated/teleported the real loaded ball and cleared its motion");
                foreach (var input in new[] { "rerack", "newGame" })
                {
                    Displace(ball);
                    if (!machine.SetInputState(input, true)) throw new InvalidOperationException("Input missing: " + input);
                    machine.SetInputState(input, false);
                    AssertPose(machine, ball, new Vector3(rack.X, rack.Y, rack.Z));
                    checks.Add(input + " reset the live loaded object and both velocities");
                }
                var relays = spawn.GetComponentsInChildren<RuntimeTriggerRelay>(true);
                if (machine.Triggers.Count != 6 || relays.Length != 6) throw new InvalidOperationException("Expected six registered Pool pocket relays.");
                Displace(ball);
                relays[0].PublishEntered(ball.Collider);
                AssertPose(machine, ball, new Vector3(tray.X, tray.Y, tray.Z));
                checks.Add("Real pocket relay dispatched trigger.entered and collected the live ball");
                Displace(ball);
                var displaced = ball.Root.transform.localPosition;
                relays[0].PublishEntered(ball.Collider);
                if (ball.Root.transform.localPosition != displaced) throw new InvalidOperationException("Duplicate collection was not ignored.");
                checks.Add("Duplicate collection guard prevented a second teleport");
                machine.SetInputState("rerack", true); machine.SetInputState("rerack", false);
                AssertPose(machine, ball, new Vector3(rack.X, rack.Y, rack.Z));
                Displace(ball); relays[0].PublishEntered(ball.Collider);
                AssertPose(machine, ball, new Vector3(tray.X, tray.Y, tray.Z));
                checks.Add("Re-rack cleared collected state and enabled collection again");
                if (loader.OasisBehaviorFault != null) throw new InvalidOperationException("Oasis behavior faulted during verification.");
                var cabinetColliders = machine.Cabinet.GetComponentsInChildren<Collider>(true);
                var ballBounds = ball.Collider.bounds;
                var nearestSurfaceTop = cabinetColliders.Where(c => !c.isTrigger && c.bounds.size.x > 0.3f && c.bounds.size.z > 0.3f)
                    .Select(c => (double)c.bounds.max.y).DefaultIfEmpty(double.NaN).Max();
                var collisionGeometry = cabinetColliders.Select(c => new
                {
                    name = c.name, trigger = c.isTrigger,
                    min = new[] { c.bounds.min.x, c.bounds.min.y, c.bounds.min.z },
                    max = new[] { c.bounds.max.x, c.bounds.max.y, c.bounds.max.z }
                }).ToArray();
                machine.SetInputState("rerack", true); machine.SetInputState("rerack", false);
                Physics.SyncTransforms();
                var physicsStart = MachinePosition(machine, ball);
                for (var step = 0; step < 120; step++) Physics.Simulate(1f / 60f);
                var physicsEnd = MachinePosition(machine, ball);
                checks.Add("Simulated two seconds of Unity physics from the authored rack anchor");
                var supportedAtRack = Vector3.Distance(physicsStart, physicsEnd) < 0.02f;
                var naturalPocketChecks = new List<string>();
                if (supportedAtRack && Vector3.Distance(new Vector3(rack.X, rack.Y, rack.Z), new Vector3(tray.X, tray.Y, tray.Z)) > 0.05f)
                {
                    foreach (var relay in relays)
                    {
                        machine.SetInputState("rerack", true); machine.SetInputState("rerack", false);
                        var pocketCollider = relay.GetComponent<Collider>();
                        if (pocketCollider == null) throw new InvalidOperationException("Pocket relay has no collider: " + relay.name);
                        var pocketBounds = pocketCollider.bounds;
                        var drop = new Vector3(pocketBounds.center.x, pocketBounds.max.y + 0.1f, pocketBounds.center.z);
                        ball.Root.transform.position = drop;
                        ball.Rigidbody.position = drop;
                        ball.Rigidbody.linearVelocity = Vector3.zero;
                        ball.Rigidbody.angularVelocity = Vector3.zero;
                        ball.Rigidbody.WakeUp();
                        Physics.SyncTransforms();
                        for (var step = 0; step < 120; step++) Physics.Simulate(1f / 60f);
                        var final = MachinePosition(machine, ball);
                        if (Vector3.Distance(final, new Vector3(tray.X, tray.Y, tray.Z)) > 0.02f)
                            throw new InvalidOperationException("Natural drop did not reach supported collection anchor for " + relay.name + ": " + final);
                        naturalPocketChecks.Add(relay.name);
                    }
                    checks.Add("Natural physics drop into all six pockets collected the ball onto supported tray anchor");
                }
                return new SuccessResponse("Actual authored Pool build load/input/trigger/reset verification passed.", new
                {
                    pending = false, checks, objectCount = machine.Objects.Count, nodeCount = machine.ObjectNodes.Count,
                    nodeIds = machine.ObjectNodes.Keys.ToArray(), triggerCount = machine.Triggers.Count,
                    warnings = machine.Warnings.ToArray(), rackAnchor = new[] { rack.X, rack.Y, rack.Z }, trayAnchor = new[] { tray.X, tray.Y, tray.Z },
                    anchorsCoincide = rack.X == tray.X && rack.Y == tray.Y && rack.Z == tray.Z,
                    ballColliderMinY = ballBounds.min.y, ballColliderMaxY = ballBounds.max.y, cabinetColliderMaxY = nearestSurfaceTop,
                    collisionGeometry, physicsStart = new[] { physicsStart.x, physicsStart.y, physicsStart.z },
                    physicsEnd = new[] { physicsEnd.x, physicsEnd.y, physicsEnd.z }, ballFellBelowRack = physicsEnd.y < physicsStart.y - 0.1f,
                    supportedAtRack, naturalPocketChecks,
                    limitations = "Batch Play Mode checks real GLB loading, runtime script/command adapters and controlled physics. Checks include relay injection and, when anchors are distinct and rack supported, natural vertical drops into each pocket. Not a rolling shot, keyboard-focus or rendered-gameplay test."
                });
            }
            catch (Exception exception) { return new ErrorResponse(exception.ToString()); }
            finally
            {
                if (loader != null) loader.Unload();
                if (spawn != null) UnityEngine.Object.Destroy(spawn);
                Physics.simulationMode = previousSimulationMode;
            }
        }

        private static Vector3 MachinePosition(RuntimeMachine machine, RuntimeObjectInstance ball)
        {
            return machine.MachineSpace != null ? machine.MachineSpace.InverseTransformPoint(ball.Root.transform.position) : ball.Root.transform.localPosition;
        }
        private static void Displace(RuntimeObjectInstance ball)
        {
            ball.Root.transform.localPosition = new Vector3(0.2f, 2, 0.3f);
            ball.Rigidbody.linearVelocity = new Vector3(1, 2, 3);
            ball.Rigidbody.angularVelocity = new Vector3(3, 2, 1);
        }

        private static void AssertPose(RuntimeMachine machine, RuntimeObjectInstance ball, Vector3 expected)
        {
            if (!ball.Root.activeSelf || Vector3.Distance(MachinePosition(machine, ball), expected) > 0.0001f
                || ball.Rigidbody.linearVelocity.sqrMagnitude > 0.000001f || ball.Rigidbody.angularVelocity.sqrMagnitude > 0.000001f)
                throw new InvalidOperationException("Ball pose/activation/velocity verification failed at " + ball.Root.transform.localPosition);
        }
    }
}
