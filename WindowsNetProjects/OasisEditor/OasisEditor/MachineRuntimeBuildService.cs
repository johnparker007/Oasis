using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using OasisEditor.Features.CabinetEditor.Models;
using OasisEditor.Features.CabinetEditor.Services;
using OasisEditor.Progress;

namespace OasisEditor;

public interface IMachineRuntimeBuildService
{
    MachineRuntimeBuildResult BuildFromCabinetDocument(EditorProject project, string cabinetManifestPath, IEditorProgressReporter progress, CancellationToken cancellationToken);
    MachineRuntimeBuildResult BuildFromCabinetDocument(EditorProject project, string cabinetManifestPath, CabinetDocument cabinetDocument, IEditorProgressReporter progress, CancellationToken cancellationToken);
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
    public const int CabinetSchemaVersion = 4;

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

    public MachineRuntimeBuildResult BuildFromCabinetDocument(EditorProject project, string cabinetManifestPath, IEditorProgressReporter progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(progress);
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(cabinetManifestPath)) return MachineRuntimeBuildResult.Fail("A saved Cabinet3D asset must be selected before building for Oasis Player.");
        if (!File.Exists(cabinetManifestPath)) return MachineRuntimeBuildResult.Fail($"Cabinet3D manifest was not found: {cabinetManifestPath}");
        if (!CabinetDocumentStorage.TryRead(File.ReadAllText(cabinetManifestPath), out var cabinetDocument)) return MachineRuntimeBuildResult.Fail($"Cabinet3D manifest is invalid or missing model.path: {cabinetManifestPath}");
        return BuildFromCabinetDocument(project, cabinetManifestPath, cabinetDocument, progress, cancellationToken);
    }

    public MachineRuntimeBuildResult BuildFromCabinetDocument(EditorProject project, string cabinetManifestPath, CabinetDocument cabinetDocument, IEditorProgressReporter progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(cabinetDocument);
        ArgumentNullException.ThrowIfNull(progress);
        cancellationToken.ThrowIfCancellationRequested();
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
            progress.Report(0.05, "Preparing build output...");
            ReplaceEmptyDirectory(stagingRoot);
            cancellationToken.ThrowIfCancellationRequested();
            var cabinetRoot = Path.Combine(stagingRoot, CabinetDirectoryName);
            Directory.CreateDirectory(cabinetRoot);
            progress.Report(0.15, "Copying cabinet model...");
            File.Copy(sourceGlb, Path.Combine(cabinetRoot, CabinetGlbFileName), overwrite: true);
            cancellationToken.ThrowIfCancellationRequested();
            var cabinetAssetPath = ToProjectRelativePath(project, cabinetManifestPath);
            var faceReferences = ExportReferencedFaces(project, stagingRoot, cabinetDocument, cabinetAssetPath, progress.CreateChild(0.2, 0.7), cancellationToken);
            progress.Report(0.72, "Validating cabinet reflections...");
            cancellationToken.ThrowIfCancellationRequested();
            ValidateReflections(cabinetDocument.Reflections ?? [], GlbCabinetReflectionReceiverDiscovery.Discover(sourceGlb), faceReferences);
            var cabinetManifest = new CabinetRuntimeManifest(CabinetSchema, CabinetSchemaVersion, cabinetAssetName, CabinetGlbFileName, cabinetDocument.Model.Scale, cabinetDocument.Model.UpAxis, ExportReflections(cabinetManifestPath, cabinetRoot, cabinetDocument.Reflections ?? [], cancellationToken));
            progress.Report(0.85, "Writing runtime manifests...");
            cancellationToken.ThrowIfCancellationRequested();
            File.WriteAllText(Path.Combine(cabinetRoot, CabinetManifestFileName), JsonSerializer.Serialize(cabinetManifest, JsonOptions));
            var machineManifest = new MachineRuntimeManifest(MachineSchema, MachineSchemaVersion, project.Name, project.Name, ProjectAssetPathService.NormalizeProjectRelativePath(Path.Combine(CabinetDirectoryName, CabinetManifestFileName)), faceReferences);
            File.WriteAllText(Path.Combine(stagingRoot, MachineManifestFileName), JsonSerializer.Serialize(machineManifest, JsonOptions));
            progress.Report(0.95, "Finalising Oasis Player machine...");
            cancellationToken.ThrowIfCancellationRequested();
            ReplaceFinalDirectory(stagingRoot, buildRoot);
            progress.Report(1, "Oasis Player machine build complete.");
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

    private static void ValidateReflections(IReadOnlyList<CabinetReflectionDefinition> definitions, IReadOnlyList<CabinetReflectionReceiverTarget> targets, IReadOnlyList<MachineRuntimeFaceReference> faces)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal); var claims = new HashSet<string>(StringComparer.Ordinal); var faceIds = faces.GroupBy(face => face.FaceId).ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
        foreach (var definition in definitions.Where(item => item.Settings.Enabled))
        {
            if (string.IsNullOrWhiteSpace(definition.Id) || !ids.Add(definition.Id)) throw new InvalidOperationException($"Enabled cabinet reflection IDs must be non-empty and unique: '{definition.Id}'.");
            var target = targets.SingleOrDefault(item => item.TargetPath == definition.TargetId) ?? throw new InvalidOperationException($"Reflection '{definition.Id}' cabinet renderer target was not found: '{definition.TargetId}'.");
            if (target.MaterialSlots.All(slot => slot.Index != definition.MaterialSlot)) throw new InvalidOperationException($"Reflection '{definition.Id}' material slot {definition.MaterialSlot} is invalid for '{definition.TargetId}'.");
            if (!claims.Add(definition.TargetId + ":" + definition.MaterialSlot)) throw new InvalidOperationException($"Multiple enabled reflections target '{definition.TargetId}' material slot {definition.MaterialSlot}.");
            if (definition.Sources.Length == 0) throw new InvalidOperationException($"Reflection '{definition.Id}' in cabinet requires at least one source Face.");
            if (definition.Sources.Length > CabinetReflectionContract.MaximumSources) throw new InvalidOperationException($"Reflection '{definition.Id}' has {definition.Sources.Length} sources; the supported maximum is {CabinetReflectionContract.MaximumSources}.");
            var sourceIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var source in definition.Sources)
            {
                var display = faceIds.TryGetValue(source.FaceId, out var matches) && matches.Length > 0 ? matches[0].AssetName : "unknown Face";
                if (!sourceIds.Add(source.FaceId)) throw new InvalidOperationException($"Reflection '{definition.Id}' contains duplicate source Face '{display}' ({source.FaceId}).");
                if (matches is null || matches.Length != 1) throw new InvalidOperationException($"Reflection '{definition.Id}' source Face '{display}' ({source.FaceId}) must resolve uniquely; found {matches?.Length ?? 0} matches.");
                if (!CabinetReflectionPlaneValidation.TryValidate(source.Plane, out var error)) throw new InvalidOperationException($"Reflection '{definition.Id}' source Face '{display}' ({source.FaceId}) plane is invalid: {error}");
            }
        }
    }

    private static IReadOnlyList<CabinetReflectionDefinition> ExportReflections(string cabinetManifestPath, string cabinetRoot, IReadOnlyList<CabinetReflectionDefinition> definitions, CancellationToken cancellationToken)
    {
        var result = new List<CabinetReflectionDefinition>();
        var sourceRoot = Path.GetFullPath(Path.GetDirectoryName(cabinetManifestPath) ?? string.Empty).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        foreach (var definition in definitions)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(definition.VisibilityMask)) { result.Add(definition with { VisibilityMask = null }); continue; }
            var source = Path.GetFullPath(Path.Combine(sourceRoot, definition.VisibilityMask));
            if (!source.StartsWith(sourceRoot, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException($"Reflection '{definition.Id}' visibility mask escapes the Cabinet asset directory.");
            if (!File.Exists(source)) throw new InvalidOperationException($"Reflection '{definition.Id}' visibility mask was not found: {source}");
            var safeId = string.Concat(definition.Id.Where(character => char.IsLetterOrDigit(character) || character is '-' or '_'));
            if (safeId.Length == 0) throw new InvalidOperationException("Reflection visibility masks require an alphanumeric definition ID.");
            var relative = ProjectAssetPathService.NormalizeProjectRelativePath(Path.Combine("reflection-masks", safeId + Path.GetExtension(source)));
            var destination = Path.Combine(cabinetRoot, relative); Directory.CreateDirectory(Path.GetDirectoryName(destination)!); File.Copy(source, destination, true);
            result.Add(definition with { VisibilityMask = relative });
        }
        return result;
    }

    private IReadOnlyList<MachineRuntimeFaceReference> ExportReferencedFaces(EditorProject project, string stagingRoot, CabinetDocument cabinetDocument, string cabinetAssetPath, IEditorProgressReporter progress, CancellationToken cancellationToken)
    {
        var faceRoot = _pathService.GetAssetTypeDirectory(project, EditorAssetType.Face);
        if (!Directory.Exists(faceRoot)) return Array.Empty<MachineRuntimeFaceReference>();

        var manifestPaths = Directory.EnumerateFiles(faceRoot, ProjectAssetPathService.FaceManifestFileName, SearchOption.AllDirectories).OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray();
        var references = new List<MachineRuntimeFaceReference>();
        for (var index = 0; index < manifestPaths.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var manifestPath = manifestPaths[index];
            var fallbackName = Path.GetFileName(Path.GetDirectoryName(manifestPath));
            progress.Report((double)index / Math.Max(1, manifestPaths.Length), $"Inspecting Face {index + 1} of {manifestPaths.Length}: {fallbackName}...");
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

            if (!TryResolveTargetOverride(cabinetDocument, targetId, out var targetOverride))
            {
                throw new InvalidOperationException(BuildMissingTargetOverrideMessage(faceDocument.Id, faceAssetName, targetId, cabinetAssetPath, cabinetDocument.TargetOverrides));
            }

            var cabinetContext = new FaceCabinetContext(cabinetDocument, null, cabinetAssetPath, null, null);
            progress.Report((index + 0.25) / Math.Max(1, manifestPaths.Length), $"Exporting Face {index + 1} of {manifestPaths.Length}: {faceAssetName}...");
            var exportResult = _faceRuntimeExportService.Export(faceDocument, project, cabinetContext, manifestPath);
            var buildFaceDirectory = Path.Combine(stagingRoot, "faces", _pathService.SanitizePathSegment(faceAssetName));
            CopyDirectory(exportResult.OutputDirectory, buildFaceDirectory, cancellationToken);
            references.Add(new MachineRuntimeFaceReference(
                faceDocument.Id,
                faceAssetName,
                targetId,
                targetOverride.FrontSide,
                targetOverride.FaceRotation,
                targetOverride.FaceFlipHorizontal,
                ProjectAssetPathService.NormalizeProjectRelativePath(Path.Combine("faces", _pathService.SanitizePathSegment(faceAssetName), FaceRuntimeExportService.ManifestFileName))));
        }

        progress.Report(1, manifestPaths.Length == 0 ? "No referenced Faces to export." : $"Exported {references.Count} referenced Faces.");
        return references;
    }

    private static void CopyDirectory(string sourceDirectory, string destinationDirectory, CancellationToken cancellationToken)
    {
        if (Directory.Exists(destinationDirectory)) Directory.Delete(destinationDirectory, recursive: true);
        Directory.CreateDirectory(destinationDirectory);
        foreach (var directory in Directory.EnumerateDirectories(sourceDirectory, "*", SearchOption.AllDirectories).OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            Directory.CreateDirectory(Path.Combine(destinationDirectory, Path.GetRelativePath(sourceDirectory, directory)));
        }

        foreach (var file in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories).OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relativePath = Path.GetRelativePath(sourceDirectory, file);
            File.Copy(file, Path.Combine(destinationDirectory, relativePath), overwrite: true);
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

    private static string BuildMissingTargetOverrideMessage(string faceId, string faceAssetName, string targetId, string cabinetAssetPath, IReadOnlyList<CabinetTargetOverride> targetOverrides)
    {
        var availableIds = targetOverrides.Count == 0
            ? "<none>"
            : string.Join(", ", targetOverrides.Select(targetOverride => $"'{targetOverride.TargetId}'"));
        return $"Face '{faceId}' ({faceAssetName}) is assigned to cabinet target '{targetId}', but Cabinet asset '{cabinetAssetPath}' does not contain that target override. Available target override IDs: {availableIds}.";
    }

    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string ToProjectRelativePath(EditorProject project, string path)
    {
        if (string.IsNullOrWhiteSpace(project.ProjectDirectory)) return ProjectAssetPathService.NormalizeProjectRelativePath(path);
        return ProjectAssetPathService.NormalizeProjectRelativePath(Path.GetRelativePath(project.ProjectDirectory, path));
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

public sealed record MachineRuntimeManifest(string Schema, int SchemaVersion, string MachineId, string DisplayName, string CabinetManifest, IReadOnlyList<MachineRuntimeFaceReference> Faces);
public sealed record MachineRuntimeFaceReference(string FaceId, string AssetName, string CabinetFaceTargetId, string FrontSide, int FaceRotation, bool FaceFlipHorizontal, string Manifest);
public sealed record CabinetRuntimeManifest(string Schema, int SchemaVersion, string CabinetId, string Glb, double Scale, string UpAxis, IReadOnlyList<CabinetReflectionDefinition> Reflections);
