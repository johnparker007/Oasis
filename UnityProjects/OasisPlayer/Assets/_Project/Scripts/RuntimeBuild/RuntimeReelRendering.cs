using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
                var angle = (t * Mathf.PI * 2f) + Mathf.PI;
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

    public sealed class RuntimeReelLoader
    {
        private static readonly string[] TargetPrefixes = { "OasisReel_", "OasisFace_" };
        private readonly IRuntimeTextureAssetLoader _assetLoader;

        public RuntimeReelLoader(IRuntimeTextureAssetLoader assetLoader)
        {
            _assetLoader = assetLoader ?? throw new ArgumentNullException(nameof(assetLoader));
        }

        public void LoadReels(RuntimeMachine machine)
        {
            if (machine == null) throw new ArgumentNullException(nameof(machine));
            var reels = machine.Build.Machine.reels ?? Array.Empty<MachineRuntimeReelReference>();
            var targets = FindReelTargets(machine.Cabinet);
            var loadedTextures = 0;
            var resolvedTargets = 0;

            Debug.Log($"Oasis runtime reel manifest entries={reels.Length}.");
            foreach (var reel in reels)
            {
                if (reel == null) { Warn(machine, "Machine manifest contains an empty reel entry."); continue; }
                if (!ValidateManifestReel(machine, reel)) continue;

                if (TryResolveContained(machine.Build.BuildRoot, machine.Build.BuildRoot, reel.reelBand, out var texturePath, out var error)
                    && _assetLoader.TryLoad(texturePath, RuntimeTextureRole.ReelBand, out var asset, out error))
                {
                    reel.ReelBandAsset = asset;
                    loadedTextures++;
                    Debug.Log($"Oasis runtime reel texture loaded: objectId='{reel.objectId}', path='{reel.reelBand}'.");
                }
                else
                {
                    Warn(machine, $"Reel '{reel.objectId}' texture could not be loaded from '{reel.reelBand}': {error}. A diagnostic placeholder will be used.");
                    reel.ReelBandAsset = new RuntimeTextureAsset("<generated reel diagnostic>", CreateDiagnosticTexture());
                }

                var targetId = Normalize(reel.cabinetReelTargetId);
                if (targets.TryGetValue(targetId, out var target))
                {
                    reel.CabinetTarget = target;
                    resolvedTargets++;
                    Debug.Log($"Oasis runtime reel target resolved: objectId='{reel.objectId}', targetId='{targetId}', target='{target.name}'.");
                }
                else
                {
                    Error(machine, $"Reel '{reel.objectId}' target '{targetId}' was not found. Available reel targets: {FormatAvailableTargets(targets)}.");
                }
            }

            Debug.Log($"Oasis runtime reels loaded: manifest entries={reels.Length}, textures loaded={loadedTextures}, targets resolved={resolvedTargets}.");
        }

        private static bool ValidateManifestReel(RuntimeMachine machine, MachineRuntimeReelReference reel)
        {
            if (string.IsNullOrWhiteSpace(reel.objectId)) { Warn(machine, "Machine manifest contains a reel with an empty objectId."); return false; }
            if (string.IsNullOrWhiteSpace(reel.reelBand)) { Warn(machine, $"Reel '{reel.objectId}' has an empty reelBand path."); return false; }
            if (string.IsNullOrWhiteSpace(reel.cabinetReelTargetId)) { Warn(machine, $"Reel '{reel.objectId}' has an empty cabinetReelTargetId."); return false; }
            if (reel.stopCount <= 0) { Warn(machine, $"Reel '{reel.objectId}' has invalid stopCount {reel.stopCount}."); return false; }
            if (reel.physicalWidth <= 0f || reel.physicalRadius <= 0f) { Warn(machine, $"Reel '{reel.objectId}' has invalid dimensions width={reel.physicalWidth}, radius={reel.physicalRadius}."); return false; }
            return true;
        }

        private static Dictionary<string, Transform> FindReelTargets(GameObject cabinet)
        {
            var targets = new Dictionary<string, Transform>(StringComparer.Ordinal);
            if (cabinet == null) return targets;
            foreach (var transform in cabinet.GetComponentsInChildren<Transform>(true))
            {
                var sourceName = IsTargetName(transform.name) ? transform.name : null;
                if (sourceName == null)
                {
                    var meshFilter = transform.GetComponent<MeshFilter>();
                    if (meshFilter != null && meshFilter.sharedMesh != null && IsTargetName(meshFilter.sharedMesh.name)) sourceName = meshFilter.sharedMesh.name;
                }
                if (sourceName == null) continue;
                var targetId = CreateStableId(sourceName);
                if (!targets.ContainsKey(targetId)) targets.Add(targetId, transform);
            }
            return targets;
        }

        private static bool IsTargetName(string name)
        {
            return !string.IsNullOrEmpty(name) && TargetPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal));
        }

        private static string CreateStableId(string sourceName)
        {
            var prefix = TargetPrefixes.FirstOrDefault(candidate => sourceName.StartsWith(candidate, StringComparison.Ordinal));
            var suffix = prefix != null ? sourceName.Substring(prefix.Length) : sourceName;
            var chars = suffix.Where(char.IsLetterOrDigit).ToArray();
            if (chars.Length == 0) return "target";
            var text = new string(chars);
            return char.ToLowerInvariant(text[0]) + text.Substring(1);
        }

        private static string FormatAvailableTargets(Dictionary<string, Transform> targets)
        {
            return targets.Count == 0 ? "<none>" : string.Join(", ", targets.Keys.OrderBy(k => k).Select(k => "'" + k + "'"));
        }

        private static Texture2D CreateDiagnosticTexture()
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
            texture.SetPixels32(new[] { new Color32(255, 0, 255, 255), new Color32(0, 0, 0, 255), new Color32(0, 0, 0, 255), new Color32(255, 0, 255, 255) });
            texture.wrapMode = TextureWrapMode.Repeat;
            texture.filterMode = FilterMode.Point;
            texture.Apply(false, false);
            texture.name = "OasisRuntimeReelDiagnostic";
            return texture;
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        private static void Warn(RuntimeMachine machine, string message)
        {
            machine.AddWarning(message);
            Debug.LogWarning(message);
        }

        private static void Error(RuntimeMachine machine, string message)
        {
            machine.AddWarning(message);
            Debug.LogError(message);
        }

        private static bool TryResolveContained(string root, string baseDir, string relative, out string resolved, out string error)
        {
            resolved = string.Empty;
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(relative)) { error = "path is empty."; return false; }
            if (Path.IsPathRooted(relative)) { error = "rooted paths are not allowed."; return false; }
            resolved = Path.GetFullPath(Path.Combine(baseDir, relative.Replace('/', Path.DirectorySeparatorChar)));
            var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!resolved.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase)) { error = "path traversal outside the build root is not allowed."; return false; }
            return true;
        }
    }

    public sealed class RuntimeReelRenderer
    {
        public int RenderReels(RuntimeMachine machine)
        {
            if (machine == null) throw new ArgumentNullException(nameof(machine));
            var reels = machine.Build.Machine.reels ?? Array.Empty<MachineRuntimeReelReference>();
            var group = new GameObject("Oasis Runtime Reels");
            group.transform.SetParent(machine.Cabinet != null && machine.Cabinet.transform.parent != null ? machine.Cabinet.transform.parent : null, false);
            var created = 0;
            var skipped = 0;
            Debug.Log($"Oasis runtime reel renderer entries={reels.Length}.");
            foreach (var entry in reels)
            {
                if (!ValidateRenderable(machine, entry)) { skipped++; continue; }
                var go = new GameObject("OasisRuntimeReel_" + entry.objectId);
                go.transform.SetParent(group.transform, false);
                go.transform.position = entry.CabinetTarget.position;
                go.transform.rotation = entry.CabinetTarget.rotation * RuntimeReelPositionConverter.ToLocalRotation(0f, entry.isReversed, entry.bandOffset);
                var filter = go.AddComponent<MeshFilter>();
                filter.sharedMesh = RuntimeReelMeshFactory.GetOrCreate(entry.physicalWidth, entry.physicalRadius, Mathf.Max(16, entry.radialSegments));
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = CreateMaterial(entry.ReelBandAsset.Texture);
                created++;
                Debug.Log($"Oasis runtime reel object created: objectId='{entry.objectId}', target='{entry.cabinetReelTargetId}', texture='{entry.reelBand}'.");
            }
            Debug.Log($"Oasis runtime reels: manifest entries={reels.Length}, objects created={created}, skipped={skipped}.");
            return created;
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

        private static bool ValidateRenderable(RuntimeMachine machine, MachineRuntimeReelReference entry)
        {
            if (entry == null) { Warn(machine, "Renderer skipped an empty reel entry."); return false; }
            if (entry.CabinetTarget == null) { Warn(machine, $"Renderer skipped reel '{entry.objectId}' because target '{entry.cabinetReelTargetId}' was not resolved."); return false; }
            if (entry.ReelBandAsset == null || entry.ReelBandAsset.Texture == null) { Warn(machine, $"Renderer skipped reel '{entry.objectId}' because no texture or diagnostic placeholder was loaded."); return false; }
            return true;
        }

        private static void Warn(RuntimeMachine machine, string message)
        {
            machine.AddWarning(message);
            Debug.LogWarning(message);
        }
    }
}
