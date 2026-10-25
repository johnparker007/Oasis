using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace OasisPlayer.RuntimeBuild
{
    [Serializable] public sealed class ArcadeMachinePlacement
    {
        public string id;
        public string build;
        public RuntimeVector3Definition position;
        public float yaw;
    }

    [Serializable] public sealed class ArcadeLayoutManifest
    {
        public string schema;
        public int schemaVersion;
        public float width, length, height;
        public RuntimeVector3Definition visitorSpawn;
        public float visitorYaw;
        public ArcadeMachinePlacement[] machines;
        public const float Clearance = .75f;

        public void Validate()
        {
            if (schema != "oasis.arcade.runtime" || schemaVersion != 1) throw new InvalidDataException("Unsupported Arcade schema/version.");
            if (!Range(width, 4, 100) || !Range(length, 4, 100) || !Range(height, 2.5f, 12)) throw new InvalidDataException("Arcade room dimensions must be finite: width/length 4–100m, height 2.5–12m.");
            if (visitorSpawn == null || !Finite(visitorSpawn.Value) || Math.Abs(visitorSpawn.y) > .001f || !Range(visitorYaw, -360, 360)
                || Math.Abs(visitorSpawn.x) > width / 2 - .5f || Math.Abs(visitorSpawn.z) > length / 2 - .5f)
                throw new InvalidDataException("Visitor spawn must be on the floor inside the room with capsule clearance and finite yaw.");
            if (machines == null || machines.Length < 1 || machines.Length > 32) throw new InvalidDataException("Arcade requires 1–32 Machine placements.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var placement in machines)
            {
                if (placement == null || !RuntimeIdentity.IsValid(placement.id) || !ids.Add(placement.id)) throw new InvalidDataException("Placement IDs must be valid and unique.");
                if (placement.position == null || !Finite(placement.position.Value) || Math.Abs(placement.position.y) > .001f || !Range(placement.yaw, -360, 360))
                    throw new InvalidDataException("Placement '" + placement.id + "' requires a finite floor position and yaw.");
                ResolveBuildPath(Path.GetTempPath(), placement.build); // Validate relative syntax independently of disk contents.
            }
        }

        public static string ResolveBuildPath(string root, string relative)
        {
            if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative) || relative.Contains(":") || relative.StartsWith("\\") || relative.StartsWith("/"))
                throw new InvalidDataException("Arcade dependencies must use contained relative build paths.");
            var normal = relative.Replace('\\', '/');
            foreach (var part in normal.Split('/')) if (part == ".." || part.Length == 0 || part == ".") throw new InvalidDataException("Arcade dependency path contains an invalid segment.");
            var directory = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var full = Path.GetFullPath(Path.Combine(directory, relative));
            if (!full.StartsWith(directory, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Arcade dependency escapes its package.");
            return full;
        }

        public void ValidateCabinetBounds(IReadOnlyList<Bounds> cabinets)
        {
            if (cabinets == null || cabinets.Count != machines.Length) throw new InvalidDataException("One measured Cabinet is required per placement.");
            for (var i = 0; i < cabinets.Count; i++)
            {
                var bounds = cabinets[i];
                if (!Finite(bounds.center) || !Finite(bounds.size) || bounds.size.sqrMagnitude < .001f ||
                    bounds.min.y < -.02f || bounds.min.y > .1f || bounds.max.y > height - .1f ||
                    bounds.min.x < -width / 2 + Clearance || bounds.max.x > width / 2 - Clearance ||
                    bounds.min.z < -length / 2 + Clearance || bounds.max.z > length / 2 - Clearance)
                    throw new InvalidDataException("Placement '" + machines[i].id + "' does not fit the room with floor/wall clearance.");
                var visitor = visitorSpawn.Value; visitor.y = bounds.center.y;
                if (Vector3.Distance(visitor, bounds.ClosestPoint(visitor)) < .4f) throw new InvalidDataException("Visitor spawn intersects placement '" + machines[i].id + "'.");
                var expanded = bounds; expanded.Expand(new Vector3(Clearance, 0, Clearance));
                for (var j = 0; j < i; j++)
                {
                    var other = cabinets[j]; other.Expand(new Vector3(Clearance, 0, Clearance));
                    if (expanded.Intersects(other)) throw new InvalidDataException("Placements '" + machines[j].id + "' and '" + machines[i].id + "' overlap or lack walking clearance.");
                }
            }
        }
        private static bool Range(float value, float min, float max) { return !float.IsNaN(value) && !float.IsInfinity(value) && value >= min && value <= max; }
        private static bool Finite(Vector3 v) { return Range(v.x, -10000, 10000) && Range(v.y, -10000, 10000) && Range(v.z, -10000, 10000); }
    }
}
