using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using OasisEditor.Features.CabinetEditor.Models;

namespace OasisEditor;

public interface IMachineRuntimeBuildService
{
    MachineRuntimeBuildResult BuildFromCabinetDocument(EditorProject project, string cabinetManifestPath);
    MachineRuntimeBuildResult BuildFromCabinetDocument(EditorProject project, string cabinetManifestPath, CabinetDocument cabinetDocument);
}

public sealed class MachineRuntimeBuildService : IMachineRuntimeBuildService
{
    public const string MachineManifestFileName = "machine.runtime.json";
    public const string CabinetDirectoryName = "cabinet";
    public const string CabinetManifestFileName = "cabinet.runtime.json";
    public const string CabinetGlbFileName = "cabinet.glb";
    public const string MachineSchema = "oasis.machine.runtime";
    public const string CabinetSchema = "oasis.cabinet.runtime";
    public const int MachineSchemaVersion = 3;
    public const int CabinetSchemaVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly ProjectAssetPathService _pathService;
    private readonly FaceRuntimeExportService _faceRuntimeExportService;

    public MachineRuntimeBuildService(ProjectAssetPathService? pathService = null, FaceRuntimeExportService? faceRuntimeExportService = null)
    {
        _pathService = pathService ?? new ProjectAssetPathService();
        _faceRuntimeExportService = faceRuntimeExportService ?? new FaceRuntimeExportService();
    }

    public MachineRuntimeBuildResult BuildFromCabinetDocument(EditorProject project, string cabinetManifestPath)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (string.IsNullOrWhiteSpace(cabinetManifestPath)) return MachineRuntimeBuildResult.Fail("A saved Cabinet3D asset must be selected before building for Oasis Player.");
        if (!File.Exists(cabinetManifestPath)) return MachineRuntimeBuildResult.Fail($"Cabinet3D manifest was not found: {cabinetManifestPath}");
        if (!CabinetDocumentStorage.TryRead(File.ReadAllText(cabinetManifestPath), out var cabinetDocument)) return MachineRuntimeBuildResult.Fail($"Cabinet3D manifest is invalid or missing model.path: {cabinetManifestPath}");
        return BuildFromCabinetDocument(project, cabinetManifestPath, cabinetDocument);
    }

    public MachineRuntimeBuildResult BuildFromCabinetDocument(EditorProject project, string cabinetManifestPath, CabinetDocument cabinetDocument)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(cabinetDocument);
        if (string.IsNullOrWhiteSpace(cabinetManifestPath)) return MachineRuntimeBuildResult.Fail("A saved Cabinet3D asset must be selected before building for Oasis Player.");
        if (!File.Exists(cabinetManifestPath)) return MachineRuntimeBuildResult.Fail($"Cabinet3D manifest was not found: {cabinetManifestPath}");
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
            var faceReferences = ExportReferencedFaces(project, stagingRoot, cabinetDocument);
            var cabinetManifest = new CabinetRuntimeManifest(CabinetSchema, CabinetSchemaVersion, cabinetAssetName, CabinetGlbFileName, cabinetDocument.Model.Scale, cabinetDocument.Model.UpAxis);
            File.WriteAllText(Path.Combine(cabinetRoot, CabinetManifestFileName), JsonSerializer.Serialize(cabinetManifest, JsonOptions));
            var machineManifest = new MachineRuntimeManifest(MachineSchema, MachineSchemaVersion, project.Name, project.Name, ProjectAssetPathService.NormalizeProjectRelativePath(Path.Combine(CabinetDirectoryName, CabinetManifestFileName)), faceReferences);
            File.WriteAllText(Path.Combine(stagingRoot, MachineManifestFileName), JsonSerializer.Serialize(machineManifest, JsonOptions));
            ReplaceFinalDirectory(stagingRoot, buildRoot);
            return MachineRuntimeBuildResult.Ok(buildRoot);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
        {
            return MachineRuntimeBuildResult.Fail($"Failed to build Oasis Player runtime output: {ex.Message}");
        }
        finally
        {
            if (Directory.Exists(stagingRoot)) Directory.Delete(stagingRoot, recursive: true);
        }
    }

    private IReadOnlyList<MachineRuntimeFaceReference> ExportReferencedFaces(EditorProject project, string stagingRoot, CabinetDocument cabinetDocument)
    {
        var faceRoot = _pathService.GetAssetTypeDirectory(project, EditorAssetType.Face);
        if (!Directory.Exists(faceRoot)) return Array.Empty<MachineRuntimeFaceReference>();

        var references = new List<MachineRuntimeFaceReference>();
        foreach (var manifestPath in Directory.EnumerateFiles(faceRoot, ProjectAssetPathService.FaceManifestFileName, SearchOption.AllDirectories).OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            if (!FaceDocumentStorage.TryReadValidated(File.ReadAllText(manifestPath), out var faceFile, out _))
            {
                throw new InvalidOperationException($"Face manifest is invalid: {manifestPath}");
            }

            var faceDocument = FaceDocumentStorage.ToModel(faceFile);
            var targetId = NormalizeOptional(faceDocument.AssignedCabinetFaceTargetId);
            if (targetId is null) continue;

            var faceAssetName = ProjectAssetPathService.GetPackageAssetNameFromManifestPath(manifestPath, EditorAssetType.Face);
            if (string.IsNullOrWhiteSpace(faceAssetName))
            {
                throw new InvalidOperationException($"Face manifests must be stored as Assets/Faces/<AssetName>/{ProjectAssetPathService.FaceManifestFileName}: {manifestPath}");
            }

            var exportResult = _faceRuntimeExportService.Export(faceDocument, project, manifestPath);
            var buildFaceDirectory = Path.Combine(stagingRoot, "faces", _pathService.SanitizePathSegment(faceAssetName));
            CopyDirectory(exportResult.OutputDirectory, buildFaceDirectory);
            if (!TryResolveTargetOverride(cabinetDocument, targetId, out var targetOverride))
            {
                throw new InvalidOperationException(BuildMissingTargetOverrideMessage(faceDocument.Id, faceAssetName, targetId, cabinetDocument.TargetOverrides));
            }
            references.Add(new MachineRuntimeFaceReference(
                faceDocument.Id,
                faceAssetName,
                targetId,
                targetOverride.FrontSide,
                targetOverride.FaceRotation,
                targetOverride.FaceFlipHorizontal,
                ProjectAssetPathService.NormalizeProjectRelativePath(Path.Combine("faces", _pathService.SanitizePathSegment(faceAssetName), FaceRuntimeExportService.ManifestFileName))));
        }

        return references;
    }

    private static void CopyDirectory(string sourceDirectory, string destinationDirectory)
    {
        if (Directory.Exists(destinationDirectory)) Directory.Delete(destinationDirectory, recursive: true);
        Directory.CreateDirectory(destinationDirectory);
        foreach (var file in Directory.EnumerateFiles(sourceDirectory).OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            File.Copy(file, Path.Combine(destinationDirectory, Path.GetFileName(file)), overwrite: true);
        }
    }

    private static bool TryResolveTargetOverride(CabinetDocument cabinetDocument, string targetId, out CabinetTargetOverride targetOverride)
    {
        var normalizedTargetId = targetId.Trim();
        var overrides = cabinetDocument.TargetOverrides ?? Array.Empty<CabinetTargetOverride>();
        var match = overrides.FirstOrDefault(candidate => string.Equals(candidate.TargetId, normalizedTargetId, StringComparison.Ordinal));
        if (match is not null)
        {
            targetOverride = match.Normalized();
            return true;
        }

        if (overrides.Length == 0)
        {
            targetOverride = CabinetTargetOverride.Default(normalizedTargetId);
            return true;
        }

        targetOverride = CabinetTargetOverride.Default(normalizedTargetId);
        return false;
    }

    private static string BuildMissingTargetOverrideMessage(string faceId, string faceAssetName, string targetId, IReadOnlyList<CabinetTargetOverride> targetOverrides)
    {
        var availableIds = targetOverrides.Count == 0
            ? "<none>"
            : string.Join(", ", targetOverrides.Select(targetOverride => $"'{targetOverride.TargetId}'"));
        return $"Face '{faceId}' ({faceAssetName}) is assigned to cabinet target '{targetId}', but no matching CabinetTargetOverride was found. Available target override IDs: {availableIds}.";
    }

    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

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

public sealed record MachineRuntimeManifest(string Schema, int SchemaVersion, string MachineId, string DisplayName, string CabinetManifest, IReadOnlyList<MachineRuntimeFaceReference> Faces);
public sealed record MachineRuntimeFaceReference(string FaceId, string AssetName, string CabinetFaceTargetId, string FrontSide, int FaceRotation, bool FaceFlipHorizontal, string Manifest);
public sealed record CabinetRuntimeManifest(string Schema, int SchemaVersion, string CabinetId, string Glb, double Scale, string UpAxis);
