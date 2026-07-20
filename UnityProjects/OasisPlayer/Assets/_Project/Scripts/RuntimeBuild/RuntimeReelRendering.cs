using System;
using System.Collections.Generic;
using UnityEngine;

namespace OasisPlayer.RuntimeBuild
{
    public static class RuntimeReelMeshFactory
    {
        private static readonly Dictionary<string, Mesh> Cache = new Dictionary<string, Mesh>();

        public static Mesh GetOrCreate(float width, float radius, int radialSegments)
        {
            width = Mathf.Max(0.0001f, width);
            radius = Mathf.Max(0.0001f, radius);
            radialSegments = Mathf.Max(3, radialSegments);
            var key = width.ToString("R") + ":" + radius.ToString("R") + ":" + radialSegments;
            if (Cache.TryGetValue(key, out var cached) && cached != null) return cached;

            var vertices = new Vector3[(radialSegments + 1) * 2];
            var normals = new Vector3[vertices.Length];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[radialSegments * 6];
            var halfWidth = width * 0.5f;
            for (var i = 0; i <= radialSegments; i++)
            {
                var t = i / (float)radialSegments;
                var angle = (t * Mathf.PI * 2f) + Mathf.PI; // seam at local rear (-Z)
                var y = Mathf.Sin(angle) * radius;
                var z = Mathf.Cos(angle) * radius;
                var normal = new Vector3(0f, Mathf.Sin(angle), Mathf.Cos(angle));
                var left = i * 2;
                vertices[left] = new Vector3(-halfWidth, y, z);
                vertices[left + 1] = new Vector3(halfWidth, y, z);
                normals[left] = normal;
                normals[left + 1] = normal;
                uv[left] = new Vector2(0f, t);
                uv[left + 1] = new Vector2(1f, t);
            }

            for (var i = 0; i < radialSegments; i++)
            {
                var v = i * 2;
                var ti = i * 6;
                triangles[ti] = v;
                triangles[ti + 1] = v + 2;
                triangles[ti + 2] = v + 1;
                triangles[ti + 3] = v + 1;
                triangles[ti + 4] = v + 2;
                triangles[ti + 5] = v + 3;
            }

            var mesh = new Mesh();
            mesh.name = "OasisRuntimeReelCylinder";
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            Cache[key] = mesh;
            return mesh;
        }
    }

    public static class RuntimeReelPositionConverter
    {
        public const float PositionsPerRevolution = 96f;
        public const float BaselineDegrees = 0f;
        public const float DirectionSign = -1f;

        public static Quaternion ToLocalRotation(float effectivePosition, bool isReversed, float bandOffset)
        {
            var adjusted = effectivePosition + (bandOffset * PositionsPerRevolution);
            if (isReversed) adjusted = -adjusted;
            var wrapped = PositiveModulo(adjusted, PositionsPerRevolution);
            var normalized = wrapped / PositionsPerRevolution;
            return Quaternion.Euler(BaselineDegrees + DirectionSign * normalized * 360f, 0f, 0f);
        }

        private static float PositiveModulo(float value, float divisor)
        {
            return ((value % divisor) + divisor) % divisor;
        }
    }

    public sealed class RuntimeReelRenderer
    {
        public void RenderReels(RuntimeMachine machine)
        {
            if (machine == null) throw new ArgumentNullException(nameof(machine));
            foreach (var face in machine.Faces)
            {
                var entries = face.Manifest.reels ?? Array.Empty<FaceRuntimeReelManifestEntry>();
                foreach (var entry in entries)
                {
                    if (!Validate(machine, face, entry)) continue;
                    var go = new GameObject("OasisRuntimeReel_" + entry.objectId);
                    go.transform.SetParent(face.CabinetTarget, false);
                    go.transform.localPosition = Vector3.zero;
                    go.transform.localRotation = RuntimeReelPositionConverter.ToLocalRotation(0f, entry.isReversed, entry.bandOffset);
                    var filter = go.AddComponent<MeshFilter>();
                    filter.sharedMesh = RuntimeReelMeshFactory.GetOrCreate(entry.physicalWidth, entry.physicalRadius, Mathf.Max(16, entry.radialSegments));
                    var renderer = go.AddComponent<MeshRenderer>();
                    renderer.sharedMaterial = CreateMaterial(entry.ReelBandAsset.Texture);
                }
            }
        }

        private static Material CreateMaterial(Texture2D texture)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            var material = new Material(shader);
            material.name = "OasisRuntimeReelMaterial";
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
            return material;
        }

        private static bool Validate(RuntimeMachine machine, RuntimeFace face, FaceRuntimeReelManifestEntry entry)
        {
            if (entry == null) { machine.AddWarning("Face contains an empty reel entry."); return false; }
            if (string.IsNullOrWhiteSpace(entry.objectId)) { machine.AddWarning("Face contains a reel with an empty objectId."); return false; }
            if (entry.ReelBandAsset == null || entry.ReelBandAsset.Texture == null) { machine.AddWarning($"Reel '{entry.objectId}' has no loaded reel-band texture."); return false; }
            if (entry.stopCount <= 0) { machine.AddWarning($"Reel '{entry.objectId}' has invalid stopCount {entry.stopCount}."); return false; }
            if (entry.physicalWidth <= 0f || entry.physicalRadius <= 0f) { machine.AddWarning($"Reel '{entry.objectId}' has invalid physical dimensions."); return false; }
            if (face.CabinetTarget == null) { machine.AddWarning($"Reel '{entry.objectId}' has no cabinet target."); return false; }
            return true;
        }
    }
}
