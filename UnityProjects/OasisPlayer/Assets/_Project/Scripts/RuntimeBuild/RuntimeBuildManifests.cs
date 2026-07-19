using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace OasisPlayer.RuntimeBuild
{
    [Serializable]
    public sealed class MachineRuntimeManifest
    {
        public string schema = string.Empty;
        public int schemaVersion;
        public string machineId = string.Empty;
        public string displayName = string.Empty;
        public string cabinetManifest = string.Empty;
        public MachineRuntimeFaceReference[] faces = Array.Empty<MachineRuntimeFaceReference>();
    }

    [Serializable]
    public sealed class MachineRuntimeFaceReference
    {
        public string faceId = string.Empty;
        public string assetName = string.Empty;
        public string cabinetFaceTargetId = string.Empty;
        public string frontSide = RuntimeFaceFrontSideExtensions.NormalValue;
        public string manifest = string.Empty;

        public string ResolvedManifestPath
        {
            get { return string.IsNullOrWhiteSpace(manifest) ? string.Empty : manifest.Trim(); }
        }
    }

    public enum RuntimeFaceFrontSide
    {
        Normal,
        Inverted
    }

    public static class RuntimeFaceFrontSideExtensions
    {
        public const string NormalValue = "normal";
        public const string InvertedValue = "inverted";

        public static RuntimeFaceFrontSide Parse(string value)
        {
            return string.Equals(value != null ? value.Trim() : string.Empty, InvertedValue, StringComparison.OrdinalIgnoreCase)
                ? RuntimeFaceFrontSide.Inverted
                : RuntimeFaceFrontSide.Normal;
        }

        public static bool IsInverted(this MachineRuntimeFaceReference reference)
        {
            return reference != null && Parse(reference.frontSide) == RuntimeFaceFrontSide.Inverted;
        }
    }

    [Serializable]
    public sealed class CabinetRuntimeManifest
    {
        public string schema = string.Empty;
        public int schemaVersion;
        public string cabinetId = string.Empty;
        public string glb = string.Empty;
        public float scale = 1f;
        public string upAxis = "Y";
    }

    [Serializable]
    public sealed class FaceRuntimeManifest
    {
        public int schemaVersion;
        public string faceId = string.Empty;
        public int width;
        public int height;
        public string artwork = string.Empty;
        public string mask = string.Empty;
        public string trayId = string.Empty;
        public string lampIds0 = string.Empty;
        public string lampWeights0 = string.Empty;
        public string lampIds1 = string.Empty;
        public string lampWeights1 = string.Empty;
        public string trayIdDebug = string.Empty;
        public string lampWeightsDebug = string.Empty;
        public FaceRuntimeLampManifestEntry[] lamps = Array.Empty<FaceRuntimeLampManifestEntry>();
        public FaceRuntimeElementManifestEntry[] trays = Array.Empty<FaceRuntimeElementManifestEntry>();
        public FaceRuntimeElementManifestEntry[] reels = Array.Empty<FaceRuntimeElementManifestEntry>();
        public FaceRuntimeElementManifestEntry[] sevenSegmentDisplays = Array.Empty<FaceRuntimeElementManifestEntry>();
        public FaceRuntimeElementManifestEntry[] alphaDisplays = Array.Empty<FaceRuntimeElementManifestEntry>();
        public FaceRuntimeButtonManifestEntry[] buttons = Array.Empty<FaceRuntimeButtonManifestEntry>();
    }

    [Serializable]
    public class FaceRuntimeElementManifestEntry
    {
        public string objectId = string.Empty;
        public string machineReference = string.Empty;
        public string name = string.Empty;
        public float x;
        public float y;
        public float width;
        public float height;
    }

    [Serializable]
    public sealed class FaceRuntimeLampManifestEntry : FaceRuntimeElementManifestEntry
    {
        public string sourceLampWindowObjectId = string.Empty;
        public int lampId;
        public int trayId;
    }

    [Serializable]
    public sealed class FaceRuntimeButtonManifestEntry : FaceRuntimeElementManifestEntry
    {
        public string inputReference = string.Empty;
    }

    public sealed class ResolvedRuntimeBuild
    {
        public ResolvedRuntimeBuild(string buildRoot, MachineRuntimeManifest machine, string cabinetManifestPath, CabinetRuntimeManifest cabinet, string glbPath, MachineRuntimeFaceReference[] faces)
        {
            BuildRoot = buildRoot;
            Machine = machine;
            CabinetManifestPath = cabinetManifestPath;
            Cabinet = cabinet;
            GlbPath = glbPath;
            Faces = faces ?? Array.Empty<MachineRuntimeFaceReference>();
        }

        public string BuildRoot { get; }
        public MachineRuntimeManifest Machine { get; }
        public string CabinetManifestPath { get; }
        public CabinetRuntimeManifest Cabinet { get; }
        public string GlbPath { get; }
        public IReadOnlyList<MachineRuntimeFaceReference> Faces { get; }
    }

    public static class RuntimeBuildLoader
    {
        public const string MachineSchema = "oasis.machine.runtime";
        public const string CabinetSchema = "oasis.cabinet.runtime";

        public static bool TryLoad(string buildDirectory, out ResolvedRuntimeBuild build, out string error)
        {
            build = null;
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(buildDirectory))
            {
                error = "Build directory is required.";
                return false;
            }

            var root = Path.GetFullPath(buildDirectory);
            if (!Directory.Exists(root))
            {
                error = $"Build directory does not exist: {root}";
                return false;
            }

            var machinePath = Path.Combine(root, "machine.runtime.json");
            if (!File.Exists(machinePath))
            {
                error = $"Machine runtime manifest is missing: {machinePath}";
                return false;
            }

            MachineRuntimeManifest machine;
            try
            {
                machine = JsonUtility.FromJson<MachineRuntimeManifest>(File.ReadAllText(machinePath));
            }
            catch (Exception ex)
            {
                error = $"Machine manifest is invalid JSON: {machinePath}. {ex.Message}";
                return false;
            }

            if (machine == null || machine.schema != MachineSchema || (machine.schemaVersion != 3))
            {
                error = $"Unsupported machine manifest schema/version in {machinePath}.";
                return false;
            }

            if (!TryResolveContained(root, root, machine.cabinetManifest, out var cabinetPath, out error))
            {
                error = $"Invalid cabinet manifest path in {machinePath}: {error}";
                return false;
            }

            if (!File.Exists(cabinetPath))
            {
                error = $"Cabinet runtime manifest is missing: {cabinetPath}";
                return false;
            }

            CabinetRuntimeManifest cabinet;
            try
            {
                cabinet = JsonUtility.FromJson<CabinetRuntimeManifest>(File.ReadAllText(cabinetPath));
            }
            catch (Exception ex)
            {
                error = $"Cabinet manifest is invalid JSON: {cabinetPath}. {ex.Message}";
                return false;
            }

            if (cabinet == null || cabinet.schema != CabinetSchema || cabinet.schemaVersion != 1)
            {
                error = $"Unsupported cabinet manifest schema/version in {cabinetPath}.";
                return false;
            }

            if (!TryResolveContained(root, Path.GetDirectoryName(cabinetPath), cabinet.glb, out var glbPath, out error))
            {
                error = $"Invalid cabinet GLB path in {cabinetPath}: {error}";
                return false;
            }

            if (!File.Exists(glbPath))
            {
                error = $"Cabinet GLB is missing: {glbPath}";
                return false;
            }

            build = new ResolvedRuntimeBuild(root, machine, cabinetPath, cabinet, glbPath, machine.faces);
            return true;
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
