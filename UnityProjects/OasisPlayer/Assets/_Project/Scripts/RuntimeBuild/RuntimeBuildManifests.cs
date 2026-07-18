using System;
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

    public sealed class ResolvedRuntimeBuild
    {
        public ResolvedRuntimeBuild(string buildRoot, MachineRuntimeManifest machine, string cabinetManifestPath, CabinetRuntimeManifest cabinet, string glbPath)
        {
            BuildRoot = buildRoot;
            Machine = machine;
            CabinetManifestPath = cabinetManifestPath;
            Cabinet = cabinet;
            GlbPath = glbPath;
        }

        public string BuildRoot { get; }
        public MachineRuntimeManifest Machine { get; }
        public string CabinetManifestPath { get; }
        public CabinetRuntimeManifest Cabinet { get; }
        public string GlbPath { get; }
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

            if (machine == null || machine.schema != MachineSchema || machine.schemaVersion != 1)
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

            build = new ResolvedRuntimeBuild(root, machine, cabinetPath, cabinet, glbPath);
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
