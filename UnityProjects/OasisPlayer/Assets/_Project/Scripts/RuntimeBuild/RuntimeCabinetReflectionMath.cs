using UnityEngine;

namespace OasisPlayer.RuntimeBuild
{
    public readonly struct RuntimeCabinetReflectionSource { public RuntimeCabinetReflectionSource(string faceId, RuntimeFaceReflectionPlane plane) { FaceId = faceId; Plane = plane; } public string FaceId { get; } public RuntimeFaceReflectionPlane Plane { get; } }
    /// <summary>Rectangle origin is UV (0,0); right/up lead to (1,0)/(0,1), and normal is cross(right, up).</summary>
    public readonly struct RuntimeFaceReflectionPlane
    {
        private RuntimeFaceReflectionPlane(Vector3 origin, Vector3 right, Vector3 up, float width, float height)
        {
            OriginWS = origin; RightWS = right; UpWS = up; NormalWS = Vector3.Cross(right, up).normalized; Width = width; Height = height;
        }

        public Vector3 OriginWS { get; }
        public Vector3 RightWS { get; }
        public Vector3 UpWS { get; }
        public Vector3 NormalWS { get; }
        public float Width { get; }
        public float Height { get; }
        public bool IsValid { get { return Width > 0f && Height > 0f && RightWS.sqrMagnitude > 0.99f && UpWS.sqrMagnitude > 0.99f && NormalWS.sqrMagnitude > 0.99f; } }

        public static bool TryCreate(Vector3 origin, Vector3 right, Vector3 up, float width, float height, out RuntimeFaceReflectionPlane plane)
        {
            plane = default;
            if (!Finite(origin) || !Finite(right) || !Finite(up) || !Finite(width) || !Finite(height) || width <= 0f || height <= 0f) return false;
            if (right.sqrMagnitude < 1e-8f || up.sqrMagnitude < 1e-8f) return false;
            right.Normalize(); up.Normalize();
            if (Vector3.Cross(right, up).sqrMagnitude < 1e-8f || Mathf.Abs(Vector3.Dot(right, up)) > 1e-4f) return false;
            plane = new RuntimeFaceReflectionPlane(origin, right, up, width, height);
            return true;
        }

        private static bool Finite(Vector3 value) { return Finite(value.x) && Finite(value.y) && Finite(value.z); }
        private static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
    }

    public static class RuntimeCabinetReflectionMath
    {
        public const float RayEpsilon = 1e-5f;

        public static bool TryIntersectRayWithPlane(Vector3 origin, Vector3 direction, RuntimeFaceReflectionPlane plane, out float distance, out Vector3 hit)
        {
            distance = 0f; hit = default;
            var denominator = Vector3.Dot(direction, plane.NormalWS);
            if (Mathf.Abs(denominator) < RayEpsilon) return false;
            distance = Vector3.Dot(plane.OriginWS - origin, plane.NormalWS) / denominator;
            if (!IsFinite(distance) || distance <= RayEpsilon) { distance = 0f; return false; }
            hit = origin + direction * distance;
            return true;
        }

        public static bool TryWorldPointToFaceUv(Vector3 point, RuntimeFaceReflectionPlane plane, out Vector2 uv)
        {
            uv = default;
            if (plane.Width <= 0f || plane.Height <= 0f) return false;
            var relative = point - plane.OriginWS;
            uv = new Vector2(Vector3.Dot(relative, plane.RightWS) / plane.Width, Vector3.Dot(relative, plane.UpWS) / plane.Height);
            return IsFinite(uv.x) && IsFinite(uv.y);
        }

        public static bool TryReflectToFaceUv(Vector3 camera, Vector3 cabinetPosition, Vector3 cabinetNormal, RuntimeFaceReflectionPlane plane, out Vector2 uv)
        {
            uv = default;
            if (cabinetNormal.sqrMagnitude < 1e-8f || (cabinetPosition - camera).sqrMagnitude < 1e-8f) return false;
            var reflected = Vector3.Reflect((cabinetPosition - camera).normalized, cabinetNormal.normalized);
            return TryIntersectRayWithPlane(cabinetPosition, reflected, plane, out _, out var hit) && TryWorldPointToFaceUv(hit, plane, out uv)
                && uv.x >= 0f && uv.x <= 1f && uv.y >= 0f && uv.y <= 1f;
        }

        public static bool TrySelectNearest(Vector3 origin, Vector3 direction, RuntimeCabinetReflectionSource[] sources, out int sourceIndex, out Vector2 uv)
        {
            sourceIndex = -1; uv = default; var nearest = float.PositiveInfinity;
            if (sources == null || sources.Length > RuntimeCabinetReflectionShaderProperties.MaximumSources) return false;
            for (var index = 0; index < sources.Length; index++)
            {
                if (!TryIntersectRayWithPlane(origin, direction, sources[index].Plane, out var distance, out var hit) || distance >= nearest || !TryWorldPointToFaceUv(hit, sources[index].Plane, out var candidate) || candidate.x < 0f || candidate.x > 1f || candidate.y < 0f || candidate.y > 1f) continue;
                nearest = distance; sourceIndex = index; uv = candidate;
            }
            return sourceIndex >= 0;
        }

        private static bool IsFinite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
    }
}
