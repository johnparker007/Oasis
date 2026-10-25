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
    public static class ArcadeVerification
    {
        public static async Task<object> Verify()
        {
            var session = new ArcadeSession(); GameObject host = null, camera = null;
            var previous = Physics.simulationMode;
            try
            {
                Physics.simulationMode = SimulationMode.Script;
                var path = Environment.GetEnvironmentVariable("OASIS_ARCADE_VERIFY_LAYOUT");
                if (string.IsNullOrWhiteSpace(path)) throw new InvalidOperationException("Explicit arcade verification layout required.");
                await session.LoadAsync(path);
                if (session.Stations.Count != 2) throw new InvalidOperationException("Expected two test stations.");
                var first = session.Stations[0].Machine; var second = session.Stations[1].Machine;
                if (first.Objects.Count != 16 || second.Objects.Count != 16 || first.SessionId == second.SessionId) throw new InvalidOperationException("Expected independent calculated Pool sessions.");
                var oldFirst = first.GetObjectReference(first.Objects.Keys.First());
                if (second.IsLiveObjectReference(oldFirst)) throw new InvalidOperationException("Cross-session object reference admitted.");
                host = new GameObject("Arcade verification room"); camera = new GameObject("Arcade verification camera");
                var room = host.AddComponent<PlayableMachineRoom>(); room.InitializeArcade(session.Stations, camera.AddComponent<Camera>(), session.Layout); room.enabled = false;
                var visitor = host.transform.Find("Arcade visitor").GetComponent<CharacterController>();
                var a = first.MachineSpace.GetComponent<RuntimeInputDevelopmentControls>(); var b = second.MachineSpace.GetComponent<RuntimeInputDevelopmentControls>();
                MoveVisitor(visitor, PlayableMachineRoom.MeasureCabinet(first));
                if (!room.EnterMachine(session.Stations[0].Id)) throw new InvalidOperationException("Could not enter first station.");
                a.Poll(key => key == KeyCode.R); b.Poll(key => key == KeyCode.R);
                if (!first.Inputs["rerack"].IsPressed || second.Inputs["rerack"].IsPressed) throw new InvalidOperationException("Input leaked across stations.");
                room.LeaveMachine();
                if (first.Inputs["rerack"].IsPressed) throw new InvalidOperationException("Held input survived leaving.");
                MoveVisitor(visitor, PlayableMachineRoom.MeasureCabinet(second));
                if (!room.EnterMachine(session.Stations[1].Id)) throw new InvalidOperationException("Could not enter second station.");
                a.Poll(key => key == KeyCode.R); b.Poll(key => key == KeyCode.R);
                if (first.Inputs["rerack"].IsPressed || !second.Inputs["rerack"].IsPressed) throw new InvalidOperationException("Switch did not isolate keyboard ownership.");
                room.LeaveMachine();
                for (var i = 0; i < 120; i++) Physics.Simulate(1f / 60);
                var pool = new PoolInteraction(first, first.Build.Machine.interaction.cuePrefabAlias, first.Build.Machine.interaction.playingSurfaceId);
                if (!pool.TryGetCue(out var cue)) throw new InvalidOperationException("First cue missing.");
                var before = cue.Rigidbody.position; var direction = first.MachineSpace.forward;
                var otherPositions = second.Objects.Values.Select(o => o.Rigidbody.position).ToArray();
                if (!pool.TryShoot(direction, 2, out var message)) throw new InvalidOperationException(message);
                for (var i = 0; i < 12; i++) Physics.Simulate(1f / 60);
                if (Vector3.Dot(cue.Rigidbody.position - before, direction) < .1f) throw new InvalidOperationException("Rotated placement shot failed.");
                if (second.Objects.Values.Where((o, i) => Vector3.Distance(o.Rigidbody.position, otherPositions[i]) > .03f).Any()) throw new InvalidOperationException("Other table moved during first table shot.");
                session.Dispose();
                if (first.IsActive || second.IsActive || first.Objects.Count != 0 || second.Objects.Count != 0 || first.IsLiveObjectReference(oldFirst)) throw new InvalidOperationException("Arcade unload retained live state.");
                await Task.Yield();
                await session.LoadAsync(path);
                if (session.Stations.Count != 2 || session.Stations[0].Machine.SessionId == first.SessionId) throw new InvalidOperationException("Reload reused old session.");
                session.Dispose();
                var bad = RuntimeManifestJsonReader.ReadArcade(File.ReadAllText(path)); bad.machines[1].position = bad.machines[0].position;
                var badPath = Path.Combine(Path.GetDirectoryName(path), "verification-overlap.arcade.json");
                File.WriteAllText(badPath, JsonUtility.ToJson(bad));
                try
                {
                    try { await session.LoadAsync(badPath); throw new InvalidOperationException("Overlapping layout was admitted."); }
                    catch (InvalidOperationException exception) { if (!exception.Message.Contains("overlap")) throw; }
                    if (session.Stations.Count != 0) throw new InvalidOperationException("Partial failed layout retained stations.");
                }
                finally { File.Delete(badPath); }
                return new SuccessResponse("Actual two-machine arcade verification passed.", new { pending = false,
                    checks = new[] { "Two independent actual-GLB Pool sessions with distinct live handle scopes", "Shared authored keys route only to the selected station and release before switching",
                        "Rotated placement cue shot moves along Machine forward while the other table stays still", "Unload clears both registries; reload creates new independent sessions", "Overlapping actual Cabinets fail and roll back the entire layout" },
                    limitations = "Two Pool tables prove composition and isolation. Fruit gameplay, mouse feel and rendered arcade inspection remain separate work." });
            }
            catch (Exception exception) { return new ErrorResponse(exception.ToString()); }
            finally
            {
                if (host != null) UnityEngine.Object.Destroy(host);
                if (camera != null) UnityEngine.Object.Destroy(camera);
                session.Dispose(); Physics.simulationMode = previous;
            }
        }
        private static void MoveVisitor(CharacterController visitor, Bounds cabinet)
        {
            visitor.enabled = false; visitor.transform.position = new Vector3(cabinet.center.x, .05f, cabinet.min.z - 1); visitor.enabled = true;
        }
    }
}
