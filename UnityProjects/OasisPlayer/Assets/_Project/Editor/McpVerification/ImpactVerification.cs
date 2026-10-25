using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MCPForUnity.Editor.Helpers;
using OasisPlayer.Fabric;
using OasisPlayer.Loading;
using OasisPlayer.RuntimeBuild;
using UnityEngine;

namespace OasisPlayer.Editor.Verification
{
    public static class ImpactVerification
    {
        public static async Task<object> Verify()
        {
            MachinePreviewLoader loader = null; GameObject spawn = null;
            try
            {
                if (UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Any(t => t.parent == null && t.name == "MachineSpawn")) throw new InvalidOperationException("Existing MachineSpawn; refusing another verification session.");
                FabricHostOptions.Current = FabricHostOptions.FromEnvironment();
                if (!RuntimeBuildLoader.TryLoad(Environment.GetEnvironmentVariable("OASIS_POOL_VERIFY_BUILD"), out var build, out var error)) throw new InvalidDataException(error);
                if (build.Machine.runtime.platform != "Impact") throw new InvalidDataException("Expected an explicit JPM Impact package.");
                spawn = new GameObject("MachineSpawn"); loader = new MachinePreviewLoader(new GltfFastCabinetModelLoader());
                var machine = await loader.LoadAsync(build); var behavior = machine.FabricBehavior;
                if (behavior == null || loader.HasOasisBehavior) throw new InvalidOperationException("Expected Fabric behavior and no Oasis script session.");
                if (machine.MachineSpace.GetComponent<RuntimeLampDevelopmentControls>() != null || machine.MachineSpace.GetComponent<RuntimeReelDevelopmentControls>() != null)
                    throw new InvalidOperationException("Synthetic diagnostics are attached to a native machine.");
                var runner = behavior.Runner;
                var changes = new HashSet<string>(StringComparer.Ordinal); var clock = Stopwatch.StartNew();
                while (clock.Elapsed.TotalSeconds < 12)
                {
                    await Task.Delay(100); behavior.ApplyLatest();
                    if (runner.Failure != null) throw runner.Failure;
                    var state = runner.LatestSnapshot;
                    if (state != null) changes.Add(Signature(state));
                }
                var snapshot = runner.LatestSnapshot;
                if (snapshot == null || runner.Slices < 2000 || runner.SnapshotCount < 5 || changes.Count < 2) throw new InvalidOperationException("Native core did not advance and produce changing outputs during the bounded observation.");
                if (runner.Audio == null || runner.AudioFrames < 1) throw new InvalidOperationException("Native PCM audio was not drained.");
                foreach (var lamp in snapshot.Lamps)
                {
                    var expected = lamp.LogicalState ? Math.Max(1, (int)Math.Round(lamp.Brightness * 255)) / 255f : 0;
                    if (machine.LampState.IsValidLampNumber(lamp.NumericalIndex) && Math.Abs(machine.LampState.GetBrightness(lamp.NumericalIndex) - expected) > .004f)
                        throw new InvalidOperationException("Native lamps did not reach runtime device state.");
                }
                var submitted = runner.SubmittedInputs;
                machine.SetInputState("serviceDoor", true); await Wait(() => runner.SubmittedInputs > submitted); machine.SetInputState("serviceDoor", false);
                await Wait(() => runner.SubmittedInputs >= submitted + 2);
                var coin = RuntimeFabricBehavior.MapInput(new MachineInputDefinition { id = "verificationCoin", coinInput = true, coinChannel = 0, coinValue = 5 }, true);
                runner.SubmitInput(coin); runner.SubmitInput(new FabricInput("verificationCoin", 0, FabricInputKind.Coin, false, 0, 5));
                await Wait(() => runner.SubmittedInputs >= submitted + 4);
                using (var duplicate = new FabricMachineRunner())
                {
                    try { await duplicate.StartAsync(FabricHostOptions.Current.RuntimeDll, null); throw new InvalidOperationException("Second singleton board was admitted."); }
                    catch (InvalidOperationException exception) { if (!exception.Message.Contains("one active board")) throw; }
                }
                runner.RequestReset(); await Wait(() => runner.ResetCount > 0);
                if (!runner.IsRunning || runner.Failure != null) throw new InvalidOperationException("Native reset faulted the runner.");
                var report = new { slices = runner.Slices, snapshots = runner.SnapshotCount, distinctOutputs = changes.Count,
                    lamps = snapshot.Lamps.Count, mappedLampCapacity = machine.LampState.Capacity, faceLampVisualMaximum = 254, reels = snapshot.Reels.Count, alphas = snapshot.CharacterDisplays.Count, segments = snapshot.SegmentDisplays.Count,
                    sampleRate = runner.AudioFormat.SampleRate, channels = runner.AudioFormat.ChannelCount, audioFrames = runner.AudioFrames,
                    submittedInputs = runner.SubmittedInputs, rejectedInputs = runner.RejectedInputs };
                var cabinetBounds = PlayableMachineRoom.MeasureCabinet(machine);
                loader.Unload();
                if (runner.IsRunning || machine.IsActive) throw new InvalidOperationException("Native worker/session survived unload.");
                machine = await loader.LoadAsync(build);
                if (machine.FabricBehavior == null || !machine.FabricBehavior.Runner.IsRunning) throw new InvalidOperationException("Native restart after unload failed.");
                await Task.Delay(250);
                if (machine.FabricBehavior.Runner.Failure != null) throw machine.FabricBehavior.Runner.Failure;
                loader.Unload();
                var mixedPath = Environment.GetEnvironmentVariable("OASIS_ARCADE_VERIFY_LAYOUT");
                var mixedVerified = false;
                if (!string.IsNullOrWhiteSpace(mixedPath)) { await VerifyMixedArcade(mixedPath); mixedVerified = true; }
                return new SuccessResponse("Real Fabric/Amber JPM Impact Player verification passed.", new { pending = false,
                    checks = new[] { "Current Editor export loaded local Casino Crazy Impact ROMs through Fabric/Amber", "Twelve-second native clock observation produced changing real lamp/reel/display snapshots and PCM audio",
                        "Logical service-door press/release and dedicated coin protocol reached the native session", "Synthetic lamp/reel diagnostics and Oasis scripts are absent",
                        "A second singleton JPM board is rejected before native creation", "Reset, worker stop, native disposal and fresh reload passed" }, native = report, mixedArcadeVerified = mixedVerified,
                    cabinetBounds = new { center = new[] { cabinetBounds.center.x, cabinetBounds.center.y, cabinetBounds.center.z }, size = new[] { cabinetBounds.size.x, cabinetBounds.size.y, cabinetBounds.size.z } },
                    limitations = "Input delivery is not proof of credited play or payout. Coin routing is not game-verified; borrowed artwork differs from ROM game. Headless run does not verify rendering or audible playback." });
            }
            catch (Exception exception) { return new ErrorResponse(exception.ToString()); }
            finally { if (loader != null) loader.Unload(); if (spawn != null) UnityEngine.Object.Destroy(spawn); }
        }
        private static async Task VerifyMixedArcade(string path)
        {
            using (var arcade = new ArcadeSession())
            {
                GameObject roomRoot = null, camera = null;
                try
                {
                    await arcade.LoadAsync(path);
                    var impact = arcade.Stations.Single(s => s.Machine.Build.Machine.runtime.kind == "Emulation");
                    var pool = arcade.Stations.Single(s => s.Machine.Build.Machine.runtime.kind == "Oasis");
                    if (pool.Machine.Objects.Count != 16 || impact.Machine.FabricBehavior == null || impact.Machine.SessionId == pool.Machine.SessionId)
                        throw new InvalidOperationException("Mixed arcade sessions did not load independently.");
                    roomRoot = new GameObject("Mixed arcade verification room"); camera = new GameObject("Mixed arcade verification camera");
                    var room = roomRoot.AddComponent<PlayableMachineRoom>(); room.InitializeArcade(arcade.Stations, camera.AddComponent<Camera>(), arcade.Layout); room.enabled = false;
                    var visitor = roomRoot.transform.Find("Arcade visitor").GetComponent<CharacterController>();
                    var bounds = PlayableMachineRoom.MeasureCabinet(impact.Machine);
                    visitor.enabled = false; visitor.transform.position = new Vector3(bounds.center.x, .05f, bounds.min.z - 1); visitor.enabled = true;
                    if (!room.EnterMachine(impact.Id)) throw new InvalidOperationException("Could not enter Impact station.");
                    var native = impact.Machine.FabricBehavior.Runner; var submitted = native.SubmittedInputs;
                    var impactKeys = impact.Machine.MachineSpace.GetComponent<RuntimeInputDevelopmentControls>();
                    var poolKeys = pool.Machine.MachineSpace.GetComponent<RuntimeInputDevelopmentControls>();
                    impactKeys.Poll(k => k == KeyCode.G); poolKeys.Poll(k => k == KeyCode.R);
                    await Wait(() => native.SubmittedInputs > submitted);
                    if (pool.Machine.Inputs["rerack"].IsPressed) throw new InvalidOperationException("Impact engagement leaked controls into Pool.");
                    room.LeaveMachine(); await Wait(() => native.SubmittedInputs >= submitted + 2);
                    await Task.Delay(500);
                    var control = new PoolInteraction(pool.Machine);
                    if (!control.TryGetCue(out var cue)) throw new InvalidOperationException("Mixed arcade Pool cue missing.");
                    var before = cue.Rigidbody.position; var slices = native.Slices;
                    if (!control.TryShoot(pool.Machine.MachineSpace.forward, 1, out var error)) throw new InvalidOperationException("Mixed arcade Pool shot: " + error);
                    await Task.Delay(200);
                    if (Vector3.Dot(cue.Rigidbody.position - before, pool.Machine.MachineSpace.forward) < .02f || native.Slices <= slices || native.Failure != null)
                        throw new InvalidOperationException("Pool physics and native Impact did not continue together.");
                    arcade.Dispose();
                    if (pool.Machine.IsActive || impact.Machine.IsActive || native.IsRunning) throw new InvalidOperationException("Mixed arcade unload retained a session.");
                }
                finally { if (roomRoot != null) UnityEngine.Object.Destroy(roomRoot); if (camera != null) UnityEngine.Object.Destroy(camera); }
            }
        }
        private static async Task Wait(Func<bool> predicate)
        {
            for (var i = 0; i < 100 && !predicate(); i++) await Task.Delay(10);
            if (!predicate()) throw new TimeoutException("Fabric input/reset did not reach its worker.");
        }
        private static string Signature(FabricMachineSnapshot state)
        {
            return string.Join(",", state.Lamps.Select(l => l.Brightness + ":" + l.LogicalState)) + "/" + string.Join(",", state.Reels.Select(r => r.Position))
                + "/" + string.Join(",", state.CharacterDisplays.SelectMany(a => a.Characters)) + "/" + string.Join(",", state.SegmentDisplays.SelectMany(a => a.SegmentMasks));
        }
    }
}
