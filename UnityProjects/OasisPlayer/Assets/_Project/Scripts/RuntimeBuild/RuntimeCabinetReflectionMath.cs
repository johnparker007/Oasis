using System;
using UnityEngine;

namespace OasisPlayer.RuntimeBuild
{
    /// <summary>
    /// A single visible Face rectangle. Origin is untransformed UV (0,0); Right and Up point
    /// toward increasing U and V, dimensions are measured before Face rotation/flip, and
    /// cross(Right, Up) must point along the visible-side Normal.
    /// </summary>
    public readonly struct RuntimeCabinetReflectionPlane
    {
        public RuntimeCabinetReflectionPlane(Vector3 origin, Vector3 right, Vector3 up, Vector3 normal, float width, float height)
        {
            if (right.sqrMagnitude < 0.000001f || up.sqrMagnitude < 0.000001f || normal.sqrMagnitude < 0.000001f)
                throw new ArgumentException("Face plane axes must be non-zero vectors.");
            if (width <= 0f || height <= 0f) throw new ArgumentOutOfRangeException(nameof(width), "Face plane dimensions must be positive.");

            Origin = origin;
            Right = right.normalized;
            Up = up.normalized;
            Normal = normal.normalized;
            if (Mathf.Abs(Vector3.Dot(Right, Up)) > 0.001f || Vector3.Dot(Vector3.Cross(Right, Up), Normal) < 0.999f)
                throw new ArgumentException("Face plane right/up axes must be orthogonal and their cross product must match the visible-side normal.");
            Width = width;
            Height = height;
        }

        public Vector3 Origin { get; }
        public Vector3 Right { get; }
        public Vector3 Up { get; }
        public Vector3 Normal { get; }
        public float Width { get; }
        public float Height { get; }
    }

    public static class RuntimeCabinetReflectionMath
    {
        public const float ParallelEpsilon = 0.00001f;

        public static bool TryIntersectReflectedRay(Vector3 cameraPosition, Vector3 surfacePosition, Vector3 surfaceNormal, RuntimeCabinetReflectionPlane plane, out Vector2 uv, out float distance)
        {
            uv = Vector2.zero;
            distance = 0f;
            var incident = (surfacePosition - cameraPosition).normalized;
            if (incident == Vector3.zero || surfaceNormal == Vector3.zero) return false;
            var reflected = Vector3.Reflect(incident, surfaceNormal.normalized);
            var denominator = Vector3.Dot(reflected, plane.Normal);
            if (Mathf.Abs(denominator) < ParallelEpsilon) return false;
            distance = Vector3.Dot(plane.Origin - surfacePosition, plane.Normal) / denominator;
            if (distance <= 0f) return false;

            var offset = surfacePosition + reflected * distance - plane.Origin;
            uv = new Vector2(Vector3.Dot(offset, plane.Right) / plane.Width, Vector3.Dot(offset, plane.Up) / plane.Height);
            return uv.x >= 0f && uv.x <= 1f && uv.y >= 0f && uv.y <= 1f;
        }
    }
}
