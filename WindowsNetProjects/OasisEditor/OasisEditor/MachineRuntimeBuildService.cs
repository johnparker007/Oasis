using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using OasisEditor.Features.CabinetEditor.Models;
using OasisEditor.Features.CabinetEditor.Services;
using OasisEditor.Features.MachineEditor.Models;
using OasisEditor.Progress;

namespace OasisEditor;

public interface IMachineRuntimeBuildService
{
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
    public const int MachineSchemaVersion = 4;
    public const int CabinetSchemaVersion = 4;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly ProjectAssetPathService _pathService;
    private readonly FaceRuntimeExportService _faceRuntimeExportService;

    public MachineRuntimeBuildService(ProjectAssetPathService? pathService = null, FaceRuntimeExportService? faceRuntimeExportService = null)
    {
        _pathService = pathService ?? new ProjectAssetPathService();
        _faceRuntimeExportService = faceRuntimeExportService ?? new FaceRuntimeExportService();
    }

    public MachineRuntimeBuildResult BuildFromMachineDocument(EditorProject project, string machineManifestPath, MachineDocument machineDocument, IEditorProgressReporter progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(machineDocument);
        ArgumentNullException.ThrowIfNull(progress);
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(machineManifestPath)) return MachineRuntimeBuildResult.Fail("A saved Machine asset must be selected before building for Oasis Player.");
        if (!File.Exists(machineManifestPath)) return MachineRuntimeBuildResult.Fail($"Machine manifest was not found: {machineManifestPath}");

        var machineAssetName = ProjectAssetPathService.GetPackageAssetNameFromManifestPath(machineManifestPath, EditorAssetType.Machine);
        if (string.IsNullOrWhiteSpace(machineAssetName)) return MachineRuntimeBuildResult.Fail("Machine manifests must be stored as Assets/Machines/<AssetName>/asset.machine before building for Oasis Player.");
        if (string.IsNullOrWhiteSpace(machineDocument.CabinetAssetPath)) return MachineRuntimeBuildResult.Fail("Machine has no assigned Cabinet asset.");

        var cabinetManifestPath = ResolveCabinetManifestPath(project, machineDocument.CabinetAssetPath);
        if (!File.Exists(cabinetManifestPath)) return MachineRuntimeBuildResult.Fail($"Cabinet3D manifest was not found: {cabinetManifestPath}");
        if (!CabinetDocumentStorage.TryRead(File.ReadAllText(cabinetManifestPath), out var cabinetDocument)) return MachineRuntimeBuildResult.Fail($"Cabinet3D manifest is invalid or missing model.path: {cabinetManifestPath}");

        var cabinetAssetName = ProjectAssetPathService.GetPackageAssetNameFromManifestPath(cabinetManifestPath, EditorAssetType.Cabinet3D);
        if (string.IsNullOrWhiteSpace(cabinetAssetName)) return MachineRuntimeBuildResult.Fail("Cabinet3D manifests must be stored as Assets/Cabinet3D/<AssetName>/asset.cabinet3d before building for Oasis Player.");

        var sourceGlb = ResolveCabinetModelPath(cabinetManifestPath, cabinetDocument.Model.Path);
        if (!File.Exists(sourceGlb)) return MachineRuntimeBuildResult.Fail($"Cabinet3D GLB model was not found: {sourceGlb}");

        var machineAssetPath = ToProjectRelativePath(project, machineManifestPath);
        var cabinetAssetPath = ToProjectRelativePath(project, cabinetManifestPath);
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
            ValidateSurfaceAssignmentTargets(machineDocument, sourceGlb, machineAssetPath, cancellationToken);
            var compositionContext = new MachineCompositionContext(machineDocument, cabinetDocument, machineAssetPath, cabinetAssetPath, null, null);
            var faceReferences = ExportReferencedFaces(project, stagingRoot, machineDocument, cabinetDocument, cabinetAssetPath, compositionContext, progress.CreateChild(0.2, 0.7), cancellationToken);
            progress.Report(0.72, "Validating cabinet reflections...");
            cancellationToken.ThrowIfCancellationRequested();
            var exportedReflections = ExportReflections(cabinetManifestPath, cabinetRoot, cabinetDocument.Reflections ?? [], cancellationToken);
            ValidateReflections(cabinetDocument.Reflections ?? [], GlbCabinetReflectionReceiverDiscovery.Discover(sourceGlb), faceReferences, machineDocument, exportedReflections);
            var cabinetManifest = new CabinetRuntimeManifest(CabinetSchema, CabinetSchemaVersion, cabinetAssetName, CabinetGlbFileName, cabinetDocument.Model.Scale, cabinetDocument.Model.UpAxis, MapReflectionsForRuntime(exportedReflections, faceReferences, machineDocument));
            progress.Report(0.85, "Writing runtime manifests...");
            cancellationToken.ThrowIfCancellationRequested();
            File.WriteAllText(Path.Combine(cabinetRoot, CabinetManifestFileName), JsonSerializer.Serialize(cabinetManifest, JsonOptions));
            var runtimeDefinition = MachineRuntimeDefinitionDto.FromMachineRuntime(machineDocument.Runtime);
            var machineManifest = new MachineRuntimeManifest(
                MachineSchema,
                MachineSchemaVersion,
                machineDocument.Id,
                machineDocument.Title,
                ProjectAssetPathService.NormalizeProjectRelativePath(Path.Combine(CabinetDirectoryName, CabinetManifestFileName)),
                faceReferences,
                runtimeDefinition);
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

    private static void ValidateSurfaceAssignmentTargets(MachineDocument machine, string sourceGlb, string machineAssetPath, CancellationToken cancellationToken)
    {
        var detected = new GlbCabinetFaceTargetDetector().DetectTargets(sourceGlb, cancellationToken);
        if (detected.Count == 0) return;
        var validIds = detected.Where(value => value.IsValid).Select(value => value.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var assignment in machine.SurfaceAssignments ?? [])
        {
            if (!validIds.Contains(assignment.TargetId)) throw new InvalidOperationException($"Machine asset '{machineAssetPath}' surface assignment target '{assignment.TargetId}' is not a valid detected OasisFace_* target.");
        }
    }

    private static void ValidateReflections(
        IReadOnlyList<CabinetReflectionDefinition> definitions,
        IReadOnlyList<CabinetReflectionReceiverTarget> targets,
        IReadOnlyList<MachineRuntimeFaceReference> faces,
        MachineDocument machine,
        IReadOnlyList<CabinetReflectionDefinition> exportedDefinitions)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var claims = new HashSet<string>(StringComparer.Ordinal);
        var faceIds = faces.GroupBy(face => face.FaceId).ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
        var targetToFaceId = BuildTargetToFaceIdMap(machine, faces);
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
                var targetId = source.SourceSurfaceTargetId?.Trim() ?? string.Empty;
                if (!targetToFaceId.TryGetValue(targetId, out var resolvedFaceId)) throw new InvalidOperationException($"Reflection '{definition.Id}' source surface target '{targetId}' does not resolve to a mounted Face.");
                var display = faceIds.TryGetValue(resolvedFaceId, out var matches) && matches.Length > 0 ? matches[0].AssetName : resolvedFaceId;
                if (!sourceIds.Add(resolvedFaceId)) throw new InvalidOperationException($"Reflection '{definition.Id}' contains duplicate source Face '{display}' ({resolvedFaceId}).");
                if (matches is null || matches.Length != 1) throw new InvalidOperationException($"Reflection '{definition.Id}' source Face '{display}' ({resolvedFaceId}) must resolve uniquely; found {matches?.Length ?? 0} matches.");
                if (!CabinetReflectionPlaneValidation.TryValidate(source.Plane, out var error)) throw new InvalidOperationException($"Reflection '{definition.Id}' source Face '{display}' ({resolvedFaceId}) plane is invalid: {error}");
            }
        }
    }

    private static Dictionary<string, string> BuildTargetToFaceIdMap(MachineDocument machine, IReadOnlyList<MachineRuntimeFaceReference> faces)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var assignment in machine.SurfaceAssignments ?? [])
        {
            var faceRef = faces.FirstOrDefault(face => string.Equals(face.CabinetFaceTargetId, assignment.TargetId, StringComparison.Ordinal));
            if (faceRef is not null)
            {
                map[assignment.TargetId] = faceRef.FaceId;
            }
        }
        return map;
    }

    private static IReadOnlyList<CabinetRuntimeReflectionDefinition> MapReflectionsForRuntime(
        IReadOnlyList<CabinetReflectionDefinition> definitions,
        IReadOnlyList<MachineRuntimeFaceReference> faces,
        MachineDocument machine)
    {
        var targetToFaceId = BuildTargetToFaceIdMap(machine, faces);
        return definitions.Select(definition => new CabinetRuntimeReflectionDefinition(
            definition.Id,
            definition.TargetId,
            definition.MaterialSlot,
            (definition.Sources ?? []).Select(source =>
            {
                var targetId = source.SourceSurfaceTargetId?.Trim() ?? string.Empty;
                var faceId = targetToFaceId.TryGetValue(targetId, out var resolved) ? resolved : string.Empty;
                return new CabinetRuntimeReflectionSource(faceId, source.Plane, source.PlaneSource);
            }).ToArray(),
            definition.Settings,
            definition.VisibilityMask)).ToArray();
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

    private IReadOnlyList<MachineRuntimeFaceReference> ExportReferencedFaces(
        EditorProject project,
        string stagingRoot,
        MachineDocument machineDocument,
        CabinetDocument cabinetDocument,
        string cabinetAssetPath,
        MachineCompositionContext compositionContext,
        IEditorProgressReporter progress,
        CancellationToken cancellationToken)
    {
        var assignments = machineDocument.SurfaceAssignments ?? [];
        var duplicateTargets = assignments.GroupBy(value => value.TargetId, StringComparer.Ordinal).FirstOrDefault(group => group.Count() > 1);
        if (duplicateTargets is not null) throw new InvalidOperationException($"Machine contains duplicate surface assignments for target '{duplicateTargets.Key}'.");

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
                if (!TryResolveTargetOverride(cabinetDocument, assignment.TargetId, out var targetOverride))
                    throw new InvalidOperationException(BuildMissingTargetOverrideMessage(faceDocument.Id, faceAssetName, assignment.TargetId, cabinetAssetPath, cabinetDocument.TargetOverrides));
                var exportResult = _faceRuntimeExportService.Export(faceDocument, project, compositionContext, manifestPath);
                var buildFaceDirectory = Path.Combine(stagingRoot, "faces", _pathService.SanitizePathSegment(faceAssetName));
                CopyDirectory(exportResult.OutputDirectory, buildFaceDirectory, cancellationToken);
                references.Add(new MachineRuntimeFaceReference(faceDocument.Id, faceAssetName, assignment.TargetId, targetOverride.FrontSide, targetOverride.FaceRotation, targetOverride.FaceFlipHorizontal, ProjectAssetPathService.NormalizeProjectRelativePath(Path.Combine("faces", _pathService.SanitizePathSegment(faceAssetName), FaceRuntimeExportService.ManifestFileName))));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
            {
                throw new InvalidOperationException($"Machine surface target '{assignment.TargetId}', referenced Face '{assignment.FaceAssetPath}': {exception.Message}", exception);
            }
        }
        progress.Report(1, assignments.Length == 0 ? "No mounted Faces to export." : $"Exported {references.Count} mounted Faces.");
        return references;
    }

    private static string ResolveCabinetManifestPath(EditorProject project, string assetPath)
    {
        var fullPath = Path.IsPathRooted(assetPath) ? assetPath : Path.Combine(project.ProjectDirectory, assetPath.Replace('/', Path.DirectorySeparatorChar));
        return Directory.Exists(fullPath) ? Path.Combine(fullPath, ProjectAssetPathService.Cabinet3DManifestFileName) : fullPath;
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

    private static string ToProjectRelativePath(EditorProject project, string path)
    {
        if (string.IsNullOrWhiteSpace(project.ProjectDirectory)) return ProjectAssetPathService.NormalizeProjectRelativePath(path);
        return ProjectAssetPathService.NormalizeProjectRelativePath(Path.GetRelativePath(project.ProjectDirectory, path));
    }

    public string GetBuildRoot(EditorProject project, string machineName) => Path.Combine(project.GeneratedDirectory, "Builds", _pathService.SanitizePathSegment(machineName));
    public static string ResolveCabinetModelPath(string manifestPath, string modelPath) => Path.IsPathFullyQualified(modelPath) ? Path.GetFullPath(modelPath) : Path.GetFullPath(Path.Combine(Path.GetDirectoryName(manifestPath) ?? string.Empty, modelPath));
    private static void ReplaceEmptyDirectory(string path) { if (Directory.Exists(path)) Directory.Delete(path, true); Directory.CreateDirectory(path); }
    private static void ReplaceFinalDirectory(string stagingRoot, string buildRoot) { if (Directory.Exists(buildRoot)) Directory.Delete(buildRoot, true); Directory.CreateDirectory(Path.GetDirectoryName(buildRoot)!); Directory.Move(stagingRoot, buildRoot); }
}

public sealed record MachineRuntimeBuildResult(bool Success, string? BuildRoot, string? ErrorMessage)
{
    public static MachineRuntimeBuildResult Ok(string buildRoot) => new(true, buildRoot, null);
    public static MachineRuntimeBuildResult Fail(string errorMessage) => new(false, null, errorMessage);
}

public sealed record MachineRuntimePlatformSettingsDto(
    System6NativeRomSettings System6NativeRoms,
    Mpu5NativeRomSettings Mpu5NativeRoms,
    EpochNativeRomSettings EpochNativeRoms,
    Mpu3ProjectSettings Mpu3Settings,
    M1ProjectSettings M1Settings,
    Scorpion4ProjectSettings Scorpion4Settings);

public sealed record MachineRuntimeDefinitionDto(
    string Kind,
    string Platform,
    MachineRuntimePlatformSettingsDto Settings)
{
    public static MachineRuntimeDefinitionDto FromMachineRuntime(MachineRuntimeDefinition runtime) => new(
        runtime.Kind.ToString(),
        runtime.Platform.ToString(),
        new MachineRuntimePlatformSettingsDto(
            runtime.System6NativeRoms,
            runtime.Mpu5NativeRoms,
            runtime.EpochNativeRoms,
            runtime.Mpu3Settings,
            runtime.M1Settings,
            runtime.Scorpion4Settings));
}

public sealed record MachineRuntimeManifest(
    string Schema,
    int SchemaVersion,
    string MachineId,
    string DisplayName,
    string CabinetManifest,
    IReadOnlyList<MachineRuntimeFaceReference> Faces,
    MachineRuntimeDefinitionDto Runtime);

public sealed record MachineRuntimeFaceReference(string FaceId, string AssetName, string CabinetFaceTargetId, string FrontSide, int FaceRotation, bool FaceFlipHorizontal, string Manifest);
public sealed record CabinetRuntimeReflectionSource(string FaceId, CabinetReflectionPlane Plane, string PlaneSource);
public sealed record CabinetRuntimeReflectionDefinition(string Id, string TargetId, int MaterialSlot, IReadOnlyList<CabinetRuntimeReflectionSource> Sources, CabinetReflectionSettings Settings, string? VisibilityMask = null);
public sealed record CabinetRuntimeManifest(string Schema, int SchemaVersion, string CabinetId, string Glb, double Scale, string UpAxis, IReadOnlyList<CabinetRuntimeReflectionDefinition> Reflections);
