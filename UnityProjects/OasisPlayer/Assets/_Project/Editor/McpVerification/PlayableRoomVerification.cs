using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MCPForUnity.Editor.Helpers;
using OasisPlayer.Loading;
using OasisPlayer.RuntimeBuild;
using UnityEngine;

namespace OasisPlayer.Editor.Verification
{
    public static class PlayableRoomVerification
    {
        public static async Task<object> Verify()
        {
            MachinePreviewLoader loader = null;
            GameObject spawn = null, host = null, camera = null;
            var previous = Physics.simulationMode;
            try
            {
                Physics.simulationMode = SimulationMode.Script;
                if (UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Any(t => t.parent == null && t.name == "MachineSpawn"))
                    throw new InvalidOperationException("Existing MachineSpawn; refusing another session.");
                if (!RuntimeBuildLoader.TryLoad(Environment.GetEnvironmentVariable("OASIS_POOL_VERIFY_BUILD"), out var build, out var error)) throw new InvalidDataException(error);
                spawn = new GameObject("MachineSpawn"); spawn.transform.position = new Vector3(1, 0, -.63f);
                loader = new MachinePreviewLoader(new GltfFastCabinetModelLoader());
                var machine = await loader.LoadAsync(build);
                if (loader.OasisBehaviorFault != null || machine.Objects.Count != 16) throw new InvalidOperationException("Expected calculated Pool setup.");
                host = new GameObject("Verification room"); camera = new GameObject("Verification camera");
                var room = host.AddComponent<PlayableMachineRoom>();
                var original = machine.MachineSpace.position;
                room.Initialize(machine, camera.AddComponent<Camera>(), MachineInteractionProfile.Pool, false);
                room.enabled = false; // Verification never reads hardware input or captures the pointer.
                if (machine.MachineSpace.position != original) throw new InvalidOperationException("Room moved the live Machine.");
                foreach (var wall in host.GetComponentsInChildren<BoxCollider>())
                    if (wall.name.Contains("wall") && wall.bounds.Intersects(room.CabinetBounds)) throw new InvalidOperationException("Wall overlaps actual Cabinet.");
                if (!room.EnterMachine()) throw new InvalidOperationException("Could not enter nearby Pool.");
                var keys = machine.MachineSpace.GetComponent<RuntimeInputDevelopmentControls>();
                keys.Poll(k => k == KeyCode.R);
                if (!machine.Inputs["rerack"].IsPressed) throw new InvalidOperationException("Engaged keyboard input was not routed.");
                room.LeaveMachine();
                if (machine.Inputs["rerack"].IsPressed || keys.enabled) throw new InvalidOperationException("Leaving retained held input.");
                for (var i = 0; i < 120; i++) Physics.Simulate(1f / 60);
                var pool = new PoolInteraction(machine);
                if (!pool.TryGetCue(out var cue)) throw new InvalidOperationException("Cue missing.");
                var start = cue.Rigidbody.position;
                if (!pool.TryShoot(Vector3.forward, 2, out error)) throw new InvalidOperationException(error);
                for (var i = 0; i < 12; i++) Physics.Simulate(1f / 60);
                if (cue.Rigidbody.position.z - start.z < .1f) throw new InvalidOperationException("Real impulse did not move cue forward.");
                if (pool.TryShoot(Vector3.forward, 2, out _)) throw new InvalidOperationException("A second shot was admitted while balls moved.");
                machine.SetInputState("newGame", true); machine.SetInputState("newGame", false);
                if (!pool.TryGetCue(out var replacement) || replacement == cue) throw new InvalidOperationException("Pool adapter retained stale cue after newGame.");
                var visitor = host.transform.Find("Arcade visitor").GetComponent<CharacterController>();
                var before = visitor.transform.position;
                for (var i = 0; i < 120; i++) visitor.Move(new Vector3(0, -.01f, -.1f));
                var south = host.transform.Find("South wall").GetComponent<BoxCollider>();
                if (visitor.transform.position.z >= before.z || visitor.transform.position.z < south.bounds.max.z) throw new InvalidOperationException("Visitor did not move or crossed the room wall.");
                return new SuccessResponse("Playable room and Pool physical input checks passed.", new { pending = false,
                    checks = new[] { "Actual Pool bounds fit room without moving live Machine or intersecting walls", "Nearby enter/leave routes then releases held logical input",
                    "Real cue impulse moves forward and second shot is blocked during motion", "New game resolves replacement cue without stale handles",
                    "Actual CharacterController moves and stops at solid room wall" },
                    limitations = "Background physics verification; mouse feel, rendered HUD/camera and manual walking remain unverified." });
            }
            catch (Exception exception) { return new ErrorResponse(exception.ToString()); }
            finally
            {
                if (host != null) UnityEngine.Object.Destroy(host);
                if (camera != null) UnityEngine.Object.Destroy(camera);
                if (loader != null) loader.Unload();
                if (spawn != null) UnityEngine.Object.Destroy(spawn);
                Physics.simulationMode = previous;
            }
        }
    }
}
