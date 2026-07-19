using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OasisPlayer.RuntimeBuild;
using UnityEngine;

namespace OasisPlayer.Loading
{
    public interface IRuntimeTextureAssetLoader
    {
        bool TryLoad(string path, RuntimeTextureRole role, out RuntimeTextureAsset asset, out string error);
    }

    public sealed class PngRuntimeTextureAssetLoader : IRuntimeTextureAssetLoader
    {
        public bool TryLoad(string path, RuntimeTextureRole role, out RuntimeTextureAsset asset, out string error)
        {
            asset = null;
            error = string.Empty;
            if (!File.Exists(path))
            {
                error = $"Texture file is missing: {path}";
                return false;
            }

            try
            {
                var bytes = File.ReadAllBytes(path);
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, role != RuntimeTextureRole.Artwork);
                if (!texture.LoadImage(bytes, false))
                {
                    UnityEngine.Object.Destroy(texture);
                    error = $"Texture file could not be decoded as an image: {path}";
                    return false;
                }

                ConfigureTexture(texture, role);
                texture.name = System.IO.Path.GetFileName(path);
                asset = new RuntimeTextureAsset(path, texture);
                return true;
            }
            catch (Exception ex)
            {
                error = $"Texture file could not be read: {path}. {ex.Message}";
                return false;
            }
        }

        private static void ConfigureTexture(Texture2D texture, RuntimeTextureRole role)
        {
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = role == RuntimeTextureRole.LookupData ? FilterMode.Point : FilterMode.Bilinear;
        }
    }

    public enum RuntimeTextureRole
    {
        Artwork,
        Mask,
        LookupData
    }

    public sealed class RuntimeFaceLoader
    {
        private const int FaceSchemaVersion = 1;
        private const string TargetPrefix = "OasisFace_";

        private readonly IRuntimeTextureAssetLoader _assetLoader;

        public RuntimeFaceLoader(IRuntimeTextureAssetLoader assetLoader)
        {
            _assetLoader = assetLoader ?? throw new ArgumentNullException(nameof(assetLoader));
        }

        public void LoadFaces(RuntimeMachine machine)
        {
            if (machine == null) throw new ArgumentNullException(nameof(machine));

            var seenFaceIds = new HashSet<string>(StringComparer.Ordinal);
            var seenTargetIds = new HashSet<string>(StringComparer.Ordinal);
            var targets = FindFaceTargets(machine.Cabinet);

            foreach (var reference in machine.Build.Faces)
            {
                if (reference == null)
                {
                    machine.AddWarning("Machine manifest contains an empty Face reference.");
                    continue;
                }

                var referenceFaceId = Normalize(reference.faceId);
                if (string.IsNullOrEmpty(referenceFaceId))
                {
                    machine.AddWarning("Machine manifest contains a Face reference with an empty faceId.");
                    continue;
                }

                if (!seenFaceIds.Add(referenceFaceId))
                {
                    machine.AddWarning($"Duplicate Face identifier '{referenceFaceId}' was skipped.");
                    continue;
                }

                var targetId = Normalize(reference.cabinetFaceTargetId);
                if (string.IsNullOrEmpty(targetId))
                {
                    machine.AddWarning($"Face '{referenceFaceId}' does not specify a cabinetFaceTargetId.");
                    continue;
                }

                if (!seenTargetIds.Add(targetId))
                {
                    machine.AddWarning($"Duplicate Face assignment for cabinet target '{targetId}' was skipped.");
                    continue;
                }

                if (!TryLoadFaceManifest(machine.Build.BuildRoot, reference.ResolvedManifestPath, referenceFaceId, out var manifest, out var manifestDirectory, out var error))
                {
                    machine.AddWarning(error);
                    continue;
                }

                if (manifest.schemaVersion != FaceSchemaVersion)
                {
                    machine.AddWarning($"Unsupported Face manifest schema version for Face '{referenceFaceId}' in {reference.ResolvedManifestPath}.");
                    continue;
                }

                if (!TryResolveContained(machine.Build.BuildRoot, manifestDirectory, manifest.artwork, out var artworkPath, out error))
                {
                    machine.AddWarning($"Invalid artwork path for Face '{referenceFaceId}': {error}");
                    continue;
                }

                if (!TryResolveContained(machine.Build.BuildRoot, manifestDirectory, manifest.mask, out var maskPath, out error))
                {
                    machine.AddWarning($"Invalid mask path for Face '{referenceFaceId}': {error}");
                    continue;
                }

                if (!targets.TryGetValue(targetId, out var target))
                {
                    machine.AddWarning($"Cabinet target mesh '{targetId}' was not found for Face '{referenceFaceId}'.");
                    continue;
                }

                if (!_assetLoader.TryLoad(artworkPath, RuntimeTextureRole.Artwork, out var artwork, out error))
                {
                    machine.AddWarning($"Artwork for Face '{referenceFaceId}' could not be loaded. {error}");
                    continue;
                }

                if (!_assetLoader.TryLoad(maskPath, RuntimeTextureRole.Mask, out var mask, out error))
                {
                    artwork.Unload();
                    machine.AddWarning($"Mask for Face '{referenceFaceId}' could not be loaded. {error}");
                    continue;
                }

                var trayId = LoadOptionalTexture(machine, manifest, manifest.trayId, manifestDirectory, referenceFaceId, "tray ID", RuntimeTextureRole.LookupData);
                var lampIds0 = LoadOptionalTexture(machine, manifest, manifest.lampIds0, manifestDirectory, referenceFaceId, "lamp IDs 0", RuntimeTextureRole.LookupData);
                var lampWeights0 = LoadOptionalTexture(machine, manifest, manifest.lampWeights0, manifestDirectory, referenceFaceId, "lamp weights 0", RuntimeTextureRole.LookupData);

                machine.RegisterFace(new RuntimeFace(reference, manifest, target, artwork, mask, trayId, lampIds0, lampWeights0));
            }
        }


        private RuntimeTextureAsset LoadOptionalTexture(RuntimeMachine machine, FaceRuntimeManifest manifest, string relativePath, string manifestDirectory, string faceId, string description, RuntimeTextureRole role)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                machine.AddWarning($"Face '{faceId}' does not declare a {description} runtime texture; future dynamic Face rendering data will be unavailable.");
                return null;
            }

            if (!TryResolveContained(machine.Build.BuildRoot, manifestDirectory, relativePath, out var texturePath, out var error))
            {
                machine.AddWarning($"Invalid {description} path for Face '{faceId}': {error}");
                return null;
            }

            if (!_assetLoader.TryLoad(texturePath, role, out var asset, out error))
            {
                machine.AddWarning($"{description} texture for Face '{faceId}' could not be loaded. {error}");
                return null;
            }

            return asset;
        }

        private static bool TryLoadFaceManifest(string buildRoot, string manifestPath, string faceId, out FaceRuntimeManifest manifest, out string manifestDirectory, out string error)
        {
            manifest = null;
            manifestDirectory = string.Empty;
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(manifestPath))
            {
                error = $"Face '{faceId}' does not specify a runtime manifest path.";
                return false;
            }

            if (!TryResolveContained(buildRoot, buildRoot, manifestPath, out var resolvedManifestPath, out error))
            {
                error = $"Invalid Face manifest path for Face '{faceId}': {error}";
                return false;
            }

            if (!File.Exists(resolvedManifestPath))
            {
                error = $"Face runtime manifest is missing for Face '{faceId}': {resolvedManifestPath}";
                return false;
            }

            try
            {
                manifest = JsonUtility.FromJson<FaceRuntimeManifest>(File.ReadAllText(resolvedManifestPath));
            }
            catch (Exception ex)
            {
                error = $"Face manifest is invalid JSON for Face '{faceId}': {resolvedManifestPath}. {ex.Message}";
                return false;
            }

            if (manifest == null)
            {
                error = $"Face manifest could not be parsed for Face '{faceId}': {resolvedManifestPath}";
                return false;
            }

            manifestDirectory = Path.GetDirectoryName(resolvedManifestPath);
            return true;
        }

        private static Dictionary<string, Transform> FindFaceTargets(GameObject cabinet)
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
            return !string.IsNullOrEmpty(name) && name.StartsWith(TargetPrefix, StringComparison.Ordinal);
        }

        private static string CreateStableId(string sourceName)
        {
            var suffix = sourceName.StartsWith(TargetPrefix, StringComparison.Ordinal) ? sourceName.Substring(TargetPrefix.Length) : sourceName;
            var chars = suffix.Where(char.IsLetterOrDigit).ToArray();
            if (chars.Length == 0) return "target";
            var text = new string(chars);
            return char.ToLowerInvariant(text[0]) + text.Substring(1);
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        private static bool TryResolveContained(string root, string baseDir, string relative, out string resolved, out string error)
        {
            resolved = string.Empty;
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(relative))
            {
                error = "path is empty.";
                return false;
            }

            if (Path.IsPathRooted(relative))
            {
                error = "rooted paths are not allowed.";
                return false;
            }

            resolved = Path.GetFullPath(Path.Combine(baseDir, relative.Replace('/', Path.DirectorySeparatorChar)));
            var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!resolved.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
            {
                error = "path traversal outside the build root is not allowed.";
                return false;
            }

            return true;
        }
    }
}
