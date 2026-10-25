using System;
using System.Linq;
using UnityEngine;

namespace OasisPlayer.RuntimeBuild
{
    public enum MachineInteractionProfile { Buttons, Pool }

    /// <summary>Player physical-input adapter; scoring, collection and resets remain Machine-owned.</summary>
    public sealed class PoolInteraction
    {
        private readonly RuntimeMachine _machine;
        private readonly string _cueAlias;
        private readonly string _surfaceId;
        public PoolInteraction(RuntimeMachine machine, string cueAlias = "cueBall", string surfaceId = "playingSurface")
        {
            _machine = machine ?? throw new ArgumentNullException(nameof(machine));
            _cueAlias = cueAlias;
            _surfaceId = surfaceId;
        }

        public bool TryGetCue(out RuntimeObjectInstance cue)
        {
            cue = null;
            if (!_machine.IsActive || _machine.MachineSpace == null) return false;
            var matches = _machine.Objects.Values.Where(b => b.DisplayName == _cueAlias && b.Root != null && b.Root.activeInHierarchy).ToArray();
            if (matches.Length != 1 || matches[0].Rigidbody == null || matches[0].Rigidbody.isKinematic) return false;
            cue = matches[0];
            return true;
        }

        public bool TryShoot(Vector3 worldDirection, float speed, out string message)
        {
            message = "Wait for the balls to stop.";
            if (!Finite(worldDirection.x) || !Finite(worldDirection.y) || !Finite(worldDirection.z) || !Finite(speed) || speed < .2f || speed > 6f)
            { message = "Shot direction or power is invalid."; return false; }
            if (!TryGetCue(out var cue)) { message = "A single live cue ball is required."; return false; }
            // Horizontal table profile. Tilted surfaces will need a distinct authored adapter contract.
            if (Vector3.Dot(_machine.MachineSpace.up, Vector3.up) < .999f)
            { message = "Pool requires an upright Machine."; return false; }
            worldDirection.y = 0;
            if (worldDirection.sqrMagnitude < .0001f) { message = "Aim along the table."; return false; }
            foreach (var ball in _machine.Objects.Values)
                if (ball.Root != null && ball.Root.activeInHierarchy && ball.Rigidbody != null &&
                    (ball.Rigidbody.linearVelocity.sqrMagnitude > .0025f || ball.Rigidbody.angularVelocity.sqrMagnitude > .25f)) return false;
            var center = _machine.MachineSpace.InverseTransformPoint(cue.Rigidbody.position);
            if (!_machine.Queries.SurfaceContainsSphere(_surfaceId, RuntimeVector3.FromUnity(center), _machine.Queries.SphereRadius(_cueAlias)))
            { message = "The cue ball is outside the playing surface."; return false; }
            cue.Rigidbody.WakeUp();
            cue.Rigidbody.AddForce(worldDirection.normalized * speed * cue.Rigidbody.mass, ForceMode.Impulse);
            message = "Shot taken.";
            return true;
        }
        private static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
    }
}
