using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using OasisEditor.Features.CabinetEditor.Models;

namespace OasisEditor;

public interface IMachineRuntimeBuildService
{
    MachineRuntimeBuildResult BuildFromCabinetDocument(EditorProject project, string cabinetManifestPath);
}

public sealed class MachineRuntimeBuildService : IMachineRuntimeBuildService
{
    public const string MachineManifestFileName = "machine.runtime.json";
    public const string CabinetDirectoryName = "cabinet";
    public const string CabinetManifestFileName = "cabinet.runtime.json";
    public const string CabinetGlbFileName = "cabinet.glb";
    public const string MachineSchema = "oasis.machine.runtime";
    public const string CabinetSchema = "oasis.cabinet.runtime";
    public const int MachineSchemaVersion = 1;
    public const int CabinetSchemaVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly ProjectAssetPathService _pathService;

    public MachineRuntimeBuildService(ProjectAssetPathService? pathService = null)
    {
        _pathService = pathService ?? new ProjectAssetPathService();
    }

    public MachineRuntimeBuildResult BuildFromCabinetDocument(EditorProject project, string cabinetManifestPath)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (string.IsNullOrWhiteSpace(cabinetManifestPath)) return MachineRuntimeBuildResult.Fail("A saved Cabinet3D asset must be selected before building for Oasis Player.");
        if (!File.Exists(cabinetManifestPath)) return MachineRuntimeBuildResult.Fail($"Cabinet3D manifest was not found: {cabinetManifestPath}");
        if (!CabinetDocumentStorage.TryRead(File.ReadAllText(cabinetManifestPath), out var cabinetDocument)) return MachineRuntimeBuildResult.Fail($"Cabinet3D manifest is invalid or missing model.path: {cabinetManifestPath}");
        var cabinetAssetName = ProjectAssetPathService.GetPackageAssetNameFromManifestPath(cabinetManifestPath, EditorAssetType.Cabinet3D);
        if (string.IsNullOrWhiteSpace(cabinetAssetName)) return MachineRuntimeBuildResult.Fail("Cabinet3D manifests must be stored as Assets/Cabinet3D/<AssetName>/asset.cabinet3d before building for Oasis Player.");
        var sourceGlb = ResolveCabinetModelPath(cabinetManifestPath, cabinetDocument.Model.Path);
        if (!File.Exists(sourceGlb)) return MachineRuntimeBuildResult.Fail($"Cabinet3D GLB model was not found: {sourceGlb}");
        var buildRoot = GetBuildRoot(project, cabinetAssetName);
        var stagingRoot = buildRoot + ".staging";
        try
        {
            ReplaceEmptyDirectory(stagingRoot);
            var cabinetRoot = Path.Combine(stagingRoot, CabinetDirectoryName);
            Directory.CreateDirectory(cabinetRoot);
            File.Copy(sourceGlb, Path.Combine(cabinetRoot, CabinetGlbFileName), overwrite: true);
            var cabinetManifest = new CabinetRuntimeManifest(CabinetSchema, CabinetSchemaVersion, cabinetAssetName, CabinetGlbFileName, cabinetDocument.Model.Scale, cabinetDocument.Model.UpAxis);
            File.WriteAllText(Path.Combine(cabinetRoot, CabinetManifestFileName), JsonSerializer.Serialize(cabinetManifest, JsonOptions));
            var machineManifest = new MachineRuntimeManifest(MachineSchema, MachineSchemaVersion, project.Name, project.Name, ProjectAssetPathService.NormalizeProjectRelativePath(Path.Combine(CabinetDirectoryName, CabinetManifestFileName)));
            File.WriteAllText(Path.Combine(stagingRoot, MachineManifestFileName), JsonSerializer.Serialize(machineManifest, JsonOptions));
            ReplaceFinalDirectory(stagingRoot, buildRoot);
            return MachineRuntimeBuildResult.Ok(buildRoot);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return MachineRuntimeBuildResult.Fail($"Failed to build Oasis Player runtime output: {ex.Message}");
        }
        finally
        {
            if (Directory.Exists(stagingRoot)) Directory.Delete(stagingRoot, recursive: true);
        }
    }

    public string GetBuildRoot(EditorProject project, string machineName) => Path.Combine(project.GeneratedDirectory, "Builds", _pathService.SanitizePathSegment(machineName));
    private static string ResolveCabinetModelPath(string manifestPath, string modelPath) => Path.IsPathFullyQualified(modelPath) ? Path.GetFullPath(modelPath) : Path.GetFullPath(Path.Combine(Path.GetDirectoryName(manifestPath) ?? string.Empty, modelPath));
    private static void ReplaceEmptyDirectory(string path) { if (Directory.Exists(path)) Directory.Delete(path, true); Directory.CreateDirectory(path); }
    private static void ReplaceFinalDirectory(string stagingRoot, string buildRoot) { if (Directory.Exists(buildRoot)) Directory.Delete(buildRoot, true); Directory.CreateDirectory(Path.GetDirectoryName(buildRoot)!); Directory.Move(stagingRoot, buildRoot); }
}

public sealed record MachineRuntimeBuildResult(bool Success, string? BuildRoot, string? ErrorMessage)
{
    public static MachineRuntimeBuildResult Ok(string buildRoot) => new(true, buildRoot, null);
    public static MachineRuntimeBuildResult Fail(string errorMessage) => new(false, null, errorMessage);
}

public sealed record MachineRuntimeManifest(string Schema, int SchemaVersion, string MachineId, string DisplayName, string CabinetManifest);
public sealed record CabinetRuntimeManifest(string Schema, int SchemaVersion, string CabinetId, string Glb, double Scale, string UpAxis);
