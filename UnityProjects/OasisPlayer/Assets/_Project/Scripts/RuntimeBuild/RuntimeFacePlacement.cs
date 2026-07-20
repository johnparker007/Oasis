using System;
using UnityEngine;

namespace OasisPlayer.RuntimeBuild
{
    public struct RuntimeFaceSurfaceGeometry
    {
        public Vector3 Center;
        public Vector3 HorizontalTangent;
        public Vector3 VerticalTangent;
        public Vector3 VisibleNormal;
        public float PhysicalWidth;
        public float PhysicalHeight;

        public Vector3 FacePointToWorld(float x, float y, int faceWidth, int faceHeight)
        {
            var u = x / faceWidth;
            var v = y / faceHeight;
            return Center
                + HorizontalTangent * ((u - 0.5f) * PhysicalWidth)
                + VerticalTangent * ((0.5f - v) * PhysicalHeight);
        }

        public Vector3 FaceRectCenterToWorld(FaceRuntimeElementManifestEntry entry, FaceRuntimeManifest manifest)
        {
            return FacePointToWorld(entry.x + (entry.width * 0.5f), entry.y + (entry.height * 0.5f), manifest.width, manifest.height);
        }

        public Quaternion AlignLocalReelAxesToSurface()
        {
            return Quaternion.LookRotation(VisibleNormal, VerticalTangent) * Quaternion.Euler(0f, 90f, 0f);
        }
    }

    public static class RuntimeFacePlacement
    {
        public const float DefaultSurfaceClearanceMetres = 0.01f;

        public static bool TryResolve(RuntimeFace face, out RuntimeFaceSurfaceGeometry geometry, out string warning)
        {
            geometry = new RuntimeFaceSurfaceGeometry();
            warning = string.Empty;
            var faceId = FaceId(face);
            var targetId = TargetId(face);
            if (face == null) { warning = "Runtime Face placement skipped because the Face was null."; return false; }
            if (face.Manifest == null || face.Manifest.width <= 0 || face.Manifest.height <= 0)
            {
                warning = $"Runtime Face '{faceId}' has invalid manifest dimensions.";
                return false;
            }
            if (face.CabinetTarget == null)
            {
                warning = $"Runtime Face '{faceId}' has no resolved cabinet target '{targetId}' for component placement.";
                return false;
            }

            var filters = face.CabinetTarget.GetComponentsInChildren<MeshFilter>(true);
            MeshFilter usable = null;
            var usableCount = 0;
            for (var i = 0; i < filters.Length; i++)
            {
                var filter = filters[i];
                if (filter == null || filter.sharedMesh == null) continue;
                if (filter.GetComponent<Renderer>() == null) continue;
                usable = filter;
                usableCount++;
            }

            if (usableCount != 1)
            {
                warning = usableCount == 0
                    ? $"Cabinet target '{targetId}' for Runtime Face '{faceId}' has no usable Face surface mesh."
                    : $"Cabinet target '{targetId}' for Runtime Face '{faceId}' has {usableCount} usable Face surface meshes; component placement skipped because the surface is ambiguous.";
                return false;
            }

            var bounds = usable.sharedMesh.bounds;
            var transform = usable.transform;
            if (!TryResolveSurfaceAxes(bounds, transform, out var horizontal, out var vertical, out var physicalWidth, out var physicalHeight))
            {
                warning = $"Cabinet target '{targetId}' for Runtime Face '{faceId}' has degenerate surface dimensions. Mesh bounds size is {bounds.size}.";
                return false;
            }

            var h = horizontal / physicalWidth;
            var v = vertical / physicalHeight;
            var meshNormal = Vector3.Cross(h, v).normalized;
            if (!IsFinite(meshNormal) || meshNormal.sqrMagnitude <= 0.5f)
            {
                warning = $"Cabinet target '{targetId}' for Runtime Face '{faceId}' has degenerate surface normal.";
                return false;
            }

            var frontSide = face.Reference != null ? RuntimeFaceFrontSideExtensions.Parse(face.Reference.frontSide) : RuntimeFaceFrontSide.Normal;
            geometry.Center = transform.TransformPoint(bounds.center);
            geometry.HorizontalTangent = h;
            geometry.VerticalTangent = v;
            geometry.VisibleNormal = frontSide == RuntimeFaceFrontSide.Inverted ? meshNormal : -meshNormal;
            geometry.PhysicalWidth = physicalWidth;
            geometry.PhysicalHeight = physicalHeight;
            if (!IsFinite(geometry.Center))
            {
                warning = $"Cabinet target '{targetId}' for Runtime Face '{faceId}' produced a non-finite surface centre.";
                return false;
            }
            return true;
        }


        private static bool TryResolveSurfaceAxes(Bounds bounds, Transform transform, out Vector3 horizontal, out Vector3 vertical, out float physicalWidth, out float physicalHeight)
        {
            horizontal = Vector3.zero;
            vertical = Vector3.zero;
            physicalWidth = 0f;
            physicalHeight = 0f;
            var size = bounds.size;
            var horizontalAxis = SelectAxis(size, -1, 0, 2, 1);
            if (horizontalAxis < 0) return false;
            var verticalAxis = SelectAxis(size, horizontalAxis, 1, 2, 0);
            if (verticalAxis < 0) return false;

            horizontal = transform.TransformVector(AxisVector(horizontalAxis, AxisSize(size, horizontalAxis)));
            vertical = transform.TransformVector(AxisVector(verticalAxis, AxisSize(size, verticalAxis)));
            physicalWidth = horizontal.magnitude;
            physicalHeight = vertical.magnitude;
            return physicalWidth > 0.0001f && physicalHeight > 0.0001f;
        }

        private static int SelectAxis(Vector3 size, int excludedAxis, params int[] preferenceOrder)
        {
            const float epsilon = 0.000001f;
            for (var i = 0; i < preferenceOrder.Length; i++)
            {
                var axis = preferenceOrder[i];
                if (axis != excludedAxis && AxisSize(size, axis) > epsilon) return axis;
            }
            return -1;
        }

        private static float AxisSize(Vector3 size, int axis)
        {
            if (axis == 0) return size.x;
            if (axis == 1) return size.y;
            return size.z;
        }

        private static Vector3 AxisVector(int axis, float length)
        {
            if (axis == 0) return new Vector3(length, 0f, 0f);
            if (axis == 1) return new Vector3(0f, length, 0f);
            return new Vector3(0f, 0f, length);
        }

        public static bool ValidateComponent(RuntimeFace face, FaceRuntimeElementManifestEntry entry, out string warning)
        {
            warning = string.Empty;
            if (entry == null) { warning = $"Runtime Face '{FaceId(face)}' has a null component entry."; return false; }
            if (entry.width <= 0f || entry.height <= 0f || !IsFinite(new Vector3(entry.x, entry.y, entry.width)) || float.IsNaN(entry.height) || float.IsInfinity(entry.height))
            {
                warning = $"Runtime Face '{FaceId(face)}' component '{ComponentId(entry)}' has invalid Face-space bounds.";
                return false;
            }
            return true;
        }

        public static string FaceId(RuntimeFace face) => face != null && face.Reference != null && !string.IsNullOrWhiteSpace(face.Reference.faceId) ? face.Reference.faceId.Trim() : "<unknown>";
        public static string TargetId(RuntimeFace face) => face != null && face.Reference != null && !string.IsNullOrWhiteSpace(face.Reference.cabinetFaceTargetId) ? face.Reference.cabinetFaceTargetId.Trim() : "<unknown>";
        public static string ComponentId(FaceRuntimeElementManifestEntry entry) => entry != null && !string.IsNullOrWhiteSpace(entry.objectId) ? entry.objectId.Trim() : "<unknown>";
        private static bool IsFinite(Vector3 v) => !(float.IsNaN(v.x) || float.IsInfinity(v.x) || float.IsNaN(v.y) || float.IsInfinity(v.y) || float.IsNaN(v.z) || float.IsInfinity(v.z));
    }
}
