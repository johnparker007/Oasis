using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace OasisPlayer.RuntimeBuild
{
    public sealed class RuntimeCabinetReflectionRenderer
    {
        public void Render(RuntimeMachine machine)
        {
            if (machine == null || machine.Build == null || machine.Build.Cabinet == null) return;
            var definitions = machine.Build.Cabinet.reflections ?? Array.Empty<RuntimeCabinetReflectionDefinition>();
            if (definitions.Length == 0) { if (Debug.isDebugBuild) Debug.Log("Oasis cabinet reflections: no reflection receivers are defined for this cabinet."); return; }
            var claimed = new HashSet<string>(StringComparer.Ordinal);
            var enabled = 0; var resolvedTargets = 0; var convertedMaterials = 0; var resolved = 0;
            foreach (var definition in definitions)
            {
                if (definition == null || definition.settings == null || !definition.settings.enabled) continue;
                enabled++;
                if (!TryResolveRenderer(machine.Cabinet, definition.targetId, out var renderer, out var targetInfo)) { Warn(machine, definition, $"target could not be resolved; available targets: {targetInfo}"); continue; }
                resolvedTargets++;
                var claim = renderer.GetInstanceID() + ":" + definition.materialSlot;
                if (claimed.Contains(claim)) { Warn(machine, definition, "another reflection definition already owns this target/material slot"); continue; }
                var sourceDefinitions = definition.sources ?? Array.Empty<RuntimeCabinetReflectionSourceDefinition>();
                if (sourceDefinitions.Length == 0 || sourceDefinitions.Length > RuntimeCabinetReflectionShaderProperties.MaximumSources) { Warn(machine, definition, $"source count must be between 1 and {RuntimeCabinetReflectionShaderProperties.MaximumSources}"); continue; }
                var sources = new RuntimeCabinetReflectionSource[sourceDefinitions.Length]; var sourceFailure = false;
                for (var sourceIndex = 0; sourceIndex < sourceDefinitions.Length; sourceIndex++) { var source = sourceDefinitions[sourceIndex]; if (source == null || !TryWorldPlane(machine.Cabinet.transform, source.plane, out var plane)) { Warn(machine, definition, $"source {sourceIndex} cabinet-local Face plane is invalid after world transformation"); sourceFailure = true; break; } sources[sourceIndex] = new RuntimeCabinetReflectionSource(source.faceId, plane); }
                if (sourceFailure) continue;
                if (!TryLoadMask(machine, definition, out var mask, out var maskWarning)) { Warn(machine, definition, maskWarning); continue; }
                if (!RuntimeCabinetReflectionBinding.TryCreate(machine, sources, renderer, definition.materialSlot, out var binding, out var warning, definition.settings, mask)) { Warn(machine, definition, warning); continue; }
                convertedMaterials++; claimed.Add(claim); machine.AddCabinetReflectionBinding(binding); resolved++;
            }
            if (Debug.isDebugBuild) Debug.Log($"Oasis cabinet reflections: definitions={definitions.Length}, enabled={enabled}, resolvedTargets={resolvedTargets}, convertedMaterials={convertedMaterials}, bindings={machine.CabinetReflectionBindings.Count}, failed={enabled - resolved}.");
        }

        private static bool TryLoadMask(RuntimeMachine machine, RuntimeCabinetReflectionDefinition definition, out Texture mask, out string warning)
        {
            mask = Texture2D.whiteTexture; warning = string.Empty;
            if (string.IsNullOrWhiteSpace(definition.visibilityMask)) return true;
            var cabinetDir = Path.GetDirectoryName(machine.Build.CabinetManifestPath);
            var root = Path.GetFullPath(machine.Build.BuildRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var path = Path.GetFullPath(Path.Combine(cabinetDir, definition.visibilityMask));
            if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !File.Exists(path)) { warning = $"visibility mask path is invalid or missing: '{definition.visibilityMask}'"; return false; }
            try
            {
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, true) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = "OasisReflectionMask_" + definition.id };
                if (!texture.LoadImage(File.ReadAllBytes(path), false)) { Destroy(texture); warning = "visibility mask could not be decoded"; return false; }
                var asset = new RuntimeTextureAsset(path, texture); machine.AddCabinetReflectionTexture(asset); mask = texture; return true;
            }
            catch (Exception ex) { warning = "visibility mask could not be loaded: " + ex.Message; return false; }
        }

        private static void Destroy(UnityEngine.Object value) { if (Application.isPlaying) UnityEngine.Object.Destroy(value); else UnityEngine.Object.DestroyImmediate(value); }

        public static bool TryWorldPlane(Transform cabinetRoot, RuntimeCabinetReflectionPlaneDefinition source, out RuntimeFaceReflectionPlane plane)
        {
            plane = default;
            if (cabinetRoot == null || source == null || source.origin == null || source.right == null || source.up == null) return false;
            var rightSpan = cabinetRoot.TransformVector(source.right.Value.normalized * source.width);
            var upSpan = cabinetRoot.TransformVector(source.up.Value.normalized * source.height);
            return RuntimeFaceReflectionPlane.TryCreate(cabinetRoot.TransformPoint(source.origin.Value), rightSpan, upSpan, rightSpan.magnitude, upSpan.magnitude, out plane);
        }

        public static bool TryResolveRenderer(GameObject cabinet, string targetId, out Renderer renderer, out string available)
        {
            renderer = null; var exactMatches = new List<Renderer>(); var normalizedMatches = new List<Renderer>(); var names = new List<string>();
            var requested = targetId != null ? targetId.Trim() : string.Empty;
            if (cabinet != null) foreach (var candidate in cabinet.GetComponentsInChildren<Renderer>(true))
            {
                var path = RelativePath(cabinet.transform, candidate.transform); names.Add(path);
                if (string.Equals(path, requested, StringComparison.Ordinal)) exactMatches.Add(candidate);
                else if (string.Equals(RemoveSyntheticScenePrefix(path), requested, StringComparison.Ordinal)) normalizedMatches.Add(candidate);
            }
            available = names.Count == 0 ? "<none>" : string.Join(", ", names);
            if (exactMatches.Count > 1) return false;
            if (exactMatches.Count == 1) { renderer = exactMatches[0]; return true; }
            if (normalizedMatches.Count != 1) return false;
            renderer = normalizedMatches[0];
            if (Debug.isDebugBuild) Debug.Log($"Cabinet reflection target resolved through glTF Scene prefix: authoredTargetId='{requested}', runtimePath='{RelativePath(cabinet.transform, renderer.transform)}'.");
            return true;
        }

        private static string RelativePath(Transform root, Transform target)
        {
            var parts = new List<string>(); for (var current = target; current != null && current != root; current = current.parent) parts.Add(current.name); parts.Reverse(); return string.Join("/", parts);
        }

        public static string RemoveSyntheticScenePrefix(string runtimePath)
        {
            const string prefix = "Scene/";
            if (string.IsNullOrEmpty(runtimePath)) return string.Empty;
            if (runtimePath == "Scene") return string.Empty;
            return runtimePath.StartsWith(prefix, StringComparison.Ordinal) ? runtimePath.Substring(prefix.Length) : runtimePath;
        }

        private static void Warn(RuntimeMachine machine, RuntimeCabinetReflectionDefinition definition, string reason)
        {
            machine.AddWarning($"Cabinet reflection '{definition.id}' failed: targetId='{definition.targetId}', materialSlot={definition.materialSlot}, reason={reason}.");
        }
    }
}
