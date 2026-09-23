using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using OasisEditor.Features.CabinetEditor.Models;
using OasisEditor.Features.CabinetEditor.Services;
using OasisEditor.Progress;

namespace OasisEditor;

public interface IMachineRuntimeBuildService
{
    MachineRuntimeBuildResult BuildFromMachineDocument(EditorProject project, string machineManifestPath, IEditorProgressReporter progress, CancellationToken cancellationToken);
    MachineRuntimeBuildResult BuildFromMachineDocument(EditorProject project, string machineManifestPath, MachineDocument machineDocument, IEditorProgressReporter progress, CancellationToken cancellationToken);
}

public sealed class MachineRuntimeBuildService : IMachineRuntimeBuildService
{
    public const string MachineManifestFileName = "machine.runtime.json";
    public const string CabinetDirectoryName = "cabinet";
    public const string CabinetManifestFileName = "cabinet.runtime.json";
    public const string CabinetGlbFileName = "cabinet.glb";
    public const string MachineSchema = "oasis.machine.runtime";
    public const string CabinetSchema = "oasis.cabinet.runtime";
    public const int MachineSchemaVersion = 5;
    public const int CabinetSchemaVersion = 5;

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

    public MachineRuntimeBuildResult BuildFromMachineDocument(EditorProject project, string machineManifestPath, IEditorProgressReporter progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(progress);
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(machineManifestPath)) return MachineRuntimeBuildResult.Fail("A saved Machine asset must be selected before building for Oasis Player.");
        if (!File.Exists(machineManifestPath)) return MachineRuntimeBuildResult.Fail($"Machine manifest was not found: {machineManifestPath}");
        if (!MachineDocumentStorage.TryRead(File.ReadAllText(machineManifestPath), out var machineDocument, out var error)) return MachineRuntimeBuildResult.Fail($"Machine manifest is invalid: {error}");
        return BuildFromMachineDocument(project, machineManifestPath, machineDocument, progress, cancellationToken);
    }

    public MachineRuntimeBuildResult BuildFromMachineDocument(EditorProject project, string machineManifestPath, MachineDocument machineDocument, IEditorProgressReporter progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(machineDocument);
        ArgumentNullException.ThrowIfNull(progress);
        cancellationToken.ThrowIfCancellationRequested();
        var machineAssetName = ProjectAssetPathService.GetPackageAssetNameFromManifestPath(machineManifestPath, EditorAssetType.Machine);
        if (string.IsNullOrWhiteSpace(machineAssetName)) return MachineRuntimeBuildResult.Fail("Machine manifests must be stored as Assets/Machines/<Name>/asset.machine before building for Oasis Player.");
        if (string.IsNullOrWhiteSpace(machineDocument.CabinetAssetPath)) return MachineRuntimeBuildResult.Fail($"Machine '{machineDocument.DisplayName}' has no Cabinet asset assigned.");
        var cabinetManifestPath = _pathService.ResolveProjectRelativePath(project, machineDocument.CabinetAssetPath);
        if (!File.Exists(cabinetManifestPath)) return MachineRuntimeBuildResult.Fail($"Machine '{machineDocument.DisplayName}' references a missing Cabinet: {machineDocument.CabinetAssetPath}");
        if (!CabinetDocumentStorage.TryRead(File.ReadAllText(cabinetManifestPath), out var cabinetDocument)) return MachineRuntimeBuildResult.Fail($"Machine '{machineDocument.DisplayName}' references an invalid Cabinet: {machineDocument.CabinetAssetPath}");
        var cabinetAssetName = ProjectAssetPathService.GetPackageAssetNameFromManifestPath(cabinetManifestPath, EditorAssetType.Cabinet3D);
        if (string.IsNullOrWhiteSpace(cabinetAssetName)) return MachineRuntimeBuildResult.Fail("Cabinet3D manifests must be stored as Assets/Cabinet3D/<AssetName>/asset.cabinet3d.");
        var sourceGlb = ResolveCabinetModelPath(cabinetManifestPath, cabinetDocument.Model.Path);
        if (!File.Exists(sourceGlb)) return MachineRuntimeBuildResult.Fail($"Cabinet3D GLB model was not found: {sourceGlb}");
        var buildRoot = GetBuildRoot(project, machineAssetName);
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
            ValidateFaceAssignmentTargets(machineDocument.DisplayName, machineDocument.SurfaceAssignments, sourceGlb, cabinetAssetPath, cancellationToken);
            var faceReferences = ExportReferencedFaces(project, stagingRoot, machineDocument, cabinetDocument, cabinetAssetPath, progress.CreateChild(0.2, 0.7), cancellationToken);
            progress.Report(0.72, "Validating cabinet reflections...");
            cancellationToken.ThrowIfCancellationRequested();
            ValidateReflections(cabinetDocument.Reflections ?? [], GlbCabinetReflectionReceiverDiscovery.Discover(sourceGlb), faceReferences);
            var cabinetManifest = new CabinetRuntimeManifest(CabinetSchema, CabinetSchemaVersion, cabinetAssetName, CabinetGlbFileName, cabinetDocument.Model.Scale, cabinetDocument.Model.UpAxis, ExportReflections(cabinetManifestPath, cabinetRoot, cabinetDocument.Reflections ?? [], cancellationToken));
            progress.Report(0.85, "Writing runtime manifests...");
            cancellationToken.ThrowIfCancellationRequested();
            File.WriteAllText(Path.Combine(cabinetRoot, CabinetManifestFileName), JsonSerializer.Serialize(cabinetManifest, JsonOptions));
            var machineManifest = new MachineRuntimeManifest(MachineSchema, MachineSchemaVersion, machineDocument.Id, machineDocument.DisplayName, ProjectAssetPathService.NormalizeProjectRelativePath(Path.Combine(CabinetDirectoryName, CabinetManifestFileName)), faceReferences, MachineRuntimeDefinition.From(machineDocument.Runtime), machineDocument.InputDefinitions);
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

    internal static void ValidateFaceAssignmentTargets(string machineDisplayName, IReadOnlyList<MachineSurfaceAssignment> assignments, string sourceGlb, string cabinetAssetPath, CancellationToken cancellationToken)
    {
        var detected = new GlbCabinetFaceTargetDetector().DetectTargets(sourceGlb, cancellationToken);
        var validIds = detected.Where(value => value.IsValid).Select(value => value.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var assignment in assignments)
        {
            if (!validIds.Contains(assignment.TargetId))
            {
                var detectedDescription = validIds.Count == 0
                    ? "the Cabinet GLB contains no valid OasisFace_* targets"
                    : $"the valid detected targets are: {string.Join(", ", validIds.OrderBy(value => value, StringComparer.Ordinal).Select(value => $"'{value}'"))}";
                throw new InvalidOperationException($"Machine '{machineDisplayName}', Cabinet asset '{cabinetAssetPath}', requested Face target '{assignment.TargetId}' is not a valid detected OasisFace_* target; {detectedDescription}.");
            }
        }
    }

    private static void ValidateReflections(IReadOnlyList<CabinetReflectionDefinition> definitions, IReadOnlyList<CabinetReflectionReceiverTarget> targets, IReadOnlyList<MachineRuntimeFaceReference> faces)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal); var claims = new HashSet<string>(StringComparer.Ordinal); var mountedTargets = faces.ToDictionary(face => face.CabinetFaceTargetId, StringComparer.Ordinal);
        foreach (var definition in definitions.Where(item => item.Settings.Enabled))
        {
            if (string.IsNullOrWhiteSpace(definition.Id) || !ids.Add(definition.Id)) throw new InvalidOperationException($"Enabled cabinet reflection IDs must be non-empty and unique: '{definition.Id}'.");
            var target = targets.SingleOrDefault(item => item.TargetPath == definition.TargetId) ?? throw new InvalidOperationException($"Reflection '{definition.Id}' cabinet renderer target was not found: '{definition.TargetId}'.");
            if (target.MaterialSlots.All(slot => slot.Index != definition.MaterialSlot)) throw new InvalidOperationException($"Reflection '{definition.Id}' material slot {definition.MaterialSlot} is invalid for '{definition.TargetId}'.");
            if (!claims.Add(definition.TargetId + ":" + definition.MaterialSlot)) throw new InvalidOperationException($"Multiple enabled reflections target '{definition.TargetId}' material slot {definition.MaterialSlot}.");
            if (definition.Sources.Length == 0) throw new InvalidOperationException($"Reflection '{definition.Id}' in cabinet requires at least one source surface target.");
            if (definition.Sources.Length > CabinetReflectionContract.MaximumSources) throw new InvalidOperationException($"Reflection '{definition.Id}' has {definition.Sources.Length} sources; the supported maximum is {CabinetReflectionContract.MaximumSources}.");
            var sourceIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var source in definition.Sources)
            {
                var found = mountedTargets.ContainsKey(source.SourceSurfaceTargetId);
                if (!sourceIds.Add(source.SourceSurfaceTargetId)) throw new InvalidOperationException($"Reflection '{definition.Id}' contains duplicate source surface target '{source.SourceSurfaceTargetId}'.");
                if (!found) throw new InvalidOperationException($"Reflection '{definition.Id}' source surface target '{source.SourceSurfaceTargetId}' must resolve uniquely; it is not assigned by the Machine.");
                if (!CabinetReflectionPlaneValidation.TryValidate(source.Plane, out var error)) throw new InvalidOperationException($"Reflection '{definition.Id}' source surface target '{source.SourceSurfaceTargetId}' plane is invalid: {error}");
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

    private IReadOnlyList<MachineRuntimeFaceReference> ExportReferencedFaces(EditorProject project, string stagingRoot, MachineDocument machineDocument, CabinetDocument cabinetDocument, string cabinetAssetPath, IEditorProgressReporter progress, CancellationToken cancellationToken)
    {
        var assignments = machineDocument.SurfaceAssignments ?? [];
        var duplicateTargets = assignments.GroupBy(value => value.TargetId, StringComparer.Ordinal).FirstOrDefault(group => group.Count() > 1);
        if (duplicateTargets is not null) throw new InvalidOperationException($"Cabinet asset '{cabinetAssetPath}' contains duplicate Face assignments for target '{duplicateTargets.Key}'.");

        var references = new List<MachineRuntimeFaceReference>();
        for (var index = 0; index < assignments.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var assignment = assignments[index].Normalized();
            var manifestPath = ResolveFaceManifestPath(project, assignment.FaceAssetPath);
            progress.Report((double)index / Math.Max(1, assignments.Length), $"Exporting Face {index + 1} of {assignments.Length}: {assignment.FaceAssetPath}...");
            try
            {
                if (!File.Exists(manifestPath)) throw new InvalidOperationException("referenced Face manifest was not found");
                if (!FaceDocumentStorage.TryReadValidated(File.ReadAllText(manifestPath), out var faceFile, out var validationError))
                    throw new InvalidOperationException($"referenced Face manifest is invalid: {validationError}");
                var faceDocument = FaceDocumentStorage.ToModel(faceFile);
                var faceAssetName = ProjectAssetPathService.GetPackageAssetNameFromManifestPath(manifestPath, EditorAssetType.Face);
                if (string.IsNullOrWhiteSpace(faceAssetName)) throw new InvalidOperationException("Face must be stored as Assets/Faces/<AssetName>/asset.face");
                var cabinetContext = new FaceCabinetContext(cabinetDocument, cabinetAssetPath, machineDocument.ReelAssignments);
                var exportResult = _faceRuntimeExportService.Export(faceDocument, project, cabinetContext, manifestPath);
                var buildFaceDirectory = Path.Combine(stagingRoot, "faces", _pathService.SanitizePathSegment(faceAssetName));
                CopyDirectory(exportResult.OutputDirectory, buildFaceDirectory, cancellationToken);
                references.Add(CreateRuntimeFaceReference(cabinetDocument, faceDocument.Id, faceAssetName, assignment.TargetId, ProjectAssetPathService.NormalizeProjectRelativePath(Path.Combine("faces", _pathService.SanitizePathSegment(faceAssetName), FaceRuntimeExportService.ManifestFileName))));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
            {
                throw new InvalidOperationException($"Machine '{machineDocument.DisplayName}', Cabinet asset '{cabinetAssetPath}', Face target '{assignment.TargetId}', referenced Face '{assignment.FaceAssetPath}': {exception.Message}", exception);
            }
        }
        progress.Report(1, assignments.Length == 0 ? "No mounted Faces to export." : $"Exported {references.Count} mounted Faces.");
        return references;
    }

    internal static MachineRuntimeFaceReference CreateRuntimeFaceReference(CabinetDocument cabinetDocument, string faceId, string faceAssetName, string targetId, string manifest)
    {
        var targetOverride = cabinetDocument.GetTargetOverride(targetId);
        return new MachineRuntimeFaceReference(faceId, faceAssetName, targetId, targetOverride.FrontSide, targetOverride.FaceRotation, targetOverride.FaceFlipHorizontal, manifest);
    }

    private static string ResolveFaceManifestPath(EditorProject project, string assetPath)
    {
        var fullPath = Path.IsPathRooted(assetPath) ? assetPath : Path.Combine(project.ProjectDirectory, assetPath.Replace('/', Path.DirectorySeparatorChar));
        return Directory.Exists(fullPath) ? Path.Combine(fullPath, ProjectAssetPathService.FaceManifestFileName) : fullPath;
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

    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string ToProjectRelativePath(EditorProject project, string path)
    {
        if (string.IsNullOrWhiteSpace(project.ProjectDirectory)) return ProjectAssetPathService.NormalizeProjectRelativePath(path);
        return ProjectAssetPathService.NormalizeProjectRelativePath(Path.GetRelativePath(project.ProjectDirectory, path));
    }

    public string GetBuildRoot(EditorProject project, string machineAssetName) => Path.Combine(project.GeneratedDirectory, "Builds", _pathService.SanitizePathSegment(machineAssetName));
    private static string ResolveCabinetModelPath(string manifestPath, string modelPath) => Path.IsPathFullyQualified(modelPath) ? Path.GetFullPath(modelPath) : Path.GetFullPath(Path.Combine(Path.GetDirectoryName(manifestPath) ?? string.Empty, modelPath));
    private static void ReplaceEmptyDirectory(string path) { if (Directory.Exists(path)) Directory.Delete(path, true); Directory.CreateDirectory(path); }
    private static void ReplaceFinalDirectory(string stagingRoot, string buildRoot) { if (Directory.Exists(buildRoot)) Directory.Delete(buildRoot, true); Directory.CreateDirectory(Path.GetDirectoryName(buildRoot)!); Directory.Move(stagingRoot, buildRoot); }
}

public sealed record MachineRuntimeBuildResult(bool Success, string? BuildRoot, string? ErrorMessage)
{
    public static MachineRuntimeBuildResult Ok(string buildRoot) => new(true, buildRoot, null);
    public static MachineRuntimeBuildResult Fail(string errorMessage) => new(false, null, errorMessage);
}

public sealed record MachineRuntimeManifest(string Schema, int SchemaVersion, string MachineId, string DisplayName, string CabinetManifest, IReadOnlyList<MachineRuntimeFaceReference> Faces, MachineRuntimeDefinition Runtime, IReadOnlyList<InputDefinitionModel> Inputs);
public sealed record MachineRuntimeDefinition(string Kind, string Platform, string PlatformSettingsJson, bool ExecutionSupportedByPlayer)
{
    public static MachineRuntimeDefinition From(MachineEmulationRuntime runtime) => new(MachineEmulationRuntime.Kind, runtime.Platform.ToString(), JsonSerializer.Serialize(runtime.Settings, runtime.Settings.GetType(), new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }), false);
}
public sealed record MachineRuntimeFaceReference(string FaceId, string AssetName, string CabinetFaceTargetId, string FrontSide, int FaceRotation, bool FaceFlipHorizontal, string Manifest);
public sealed record CabinetRuntimeManifest(string Schema, int SchemaVersion, string CabinetId, string Glb, double Scale, string UpAxis, IReadOnlyList<CabinetReflectionDefinition> Reflections);
