using System.IO;
using Oasis.Scripting;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Security.Cryptography;
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
    public const string ObjectsDirectoryName = "objects";
    public const string ObjectManifestFileName = "object.runtime.json";
    public const string ObjectGlbFileName = "object.glb";
    public const string MachineSchema = "oasis.machine.runtime";
    public const string CabinetSchema = "oasis.cabinet.runtime";
    public const string ObjectSchema = "oasis.object3d.runtime";
    public const int MachineSchemaVersion = 9;
    public const int CabinetSchemaVersion = 5;
    public const int ObjectSchemaVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly ProjectAssetPathService _pathService;
    private readonly FaceRuntimeExportService _faceRuntimeExportService;
    private readonly string _libraryRoot;
    private readonly AssetReferenceResolver _assetResolver = new();

    public MachineRuntimeBuildService(ProjectAssetPathService? pathService = null, FaceRuntimeExportService? faceRuntimeExportService = null, string? libraryRoot = null)
    {
        _pathService = pathService ?? new ProjectAssetPathService();
        _faceRuntimeExportService = faceRuntimeExportService ?? new FaceRuntimeExportService();
        _libraryRoot = libraryRoot ?? new EditorPreferencesStore().Load().AssetLibrary.RootPath;
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
        try { MachineDocumentStorage.Validate(machineDocument); }
        catch (InvalidOperationException exception) { return MachineRuntimeBuildResult.Fail(exception.Message); }
        var machineAssetName = ProjectAssetPathService.GetPackageAssetNameFromManifestPath(machineManifestPath, EditorAssetType.Machine);
        if (string.IsNullOrWhiteSpace(machineAssetName)) return MachineRuntimeBuildResult.Fail("Machine manifests must be stored as Assets/Machines/<Name>/asset.machine before building for Oasis Player.");
        if (machineDocument.CabinetAsset is null) return MachineRuntimeBuildResult.Fail($"Machine '{machineDocument.DisplayName}' has no Cabinet asset assigned.");
        string cabinetManifestPath;
        try { cabinetManifestPath = _assetResolver.Resolve(project, _libraryRoot, machineDocument.CabinetAsset); }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException) { return MachineRuntimeBuildResult.Fail($"Machine '{machineDocument.DisplayName}' cannot resolve Cabinet {machineDocument.CabinetAsset}: {exception.Message}"); }
        if (!File.Exists(cabinetManifestPath)) return MachineRuntimeBuildResult.Fail($"Machine '{machineDocument.DisplayName}' references a missing Cabinet {machineDocument.CabinetAsset}. Library root: '{_libraryRoot}'.");
        if (!CabinetDocumentStorage.TryRead(File.ReadAllText(cabinetManifestPath), out var cabinetDocument)) return MachineRuntimeBuildResult.Fail($"Machine '{machineDocument.DisplayName}' references an invalid Cabinet: {machineDocument.CabinetAsset}");
        var cabinetAssetName = Path.GetFileName(Path.GetDirectoryName(cabinetManifestPath));
        string sourceGlb;
        try { sourceGlb = ResolveCabinetModelPath(cabinetManifestPath, cabinetDocument.Model.Path); }
        catch (InvalidOperationException exception) { return MachineRuntimeBuildResult.Fail($"Machine '{machineDocument.DisplayName}' references an invalid Cabinet {machineDocument.CabinetAsset}: {exception.Message}"); }
        if (!File.Exists(sourceGlb)) return MachineRuntimeBuildResult.Fail($"Machine '{machineDocument.DisplayName}' references Cabinet {machineDocument.CabinetAsset}, whose package GLB was not found: {sourceGlb}");
        var buildRoot = GetBuildRoot(project, machineAssetName);
        var stagingRoot = buildRoot + ".staging";
        try
        {
            progress.Report(0.05, "Preparing build output...");
            // Read and validate the persisted source again; never trust the Editor buffer.
            var behaviorSource = ValidateBehavior(project, machineManifestPath, machineDocument);
            ReplaceEmptyDirectory(stagingRoot);
            if (behaviorSource is not null)
            {
                Directory.CreateDirectory(Path.Combine(stagingRoot, "behavior"));
                File.WriteAllText(Path.Combine(stagingRoot, "behavior", "behavior.oasis"), behaviorSource);
            }
            cancellationToken.ThrowIfCancellationRequested();
            var cabinetRoot = Path.Combine(stagingRoot, CabinetDirectoryName);
            Directory.CreateDirectory(cabinetRoot);
            var cabinetAssetIdentity = machineDocument.CabinetAsset.ToString();
            progress.Report(0.12, "Validating cabinet semantic geometry...");
            new GlbCabinetSemanticGeometryValidator().Validate(sourceGlb, cabinetAssetIdentity, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            progress.Report(0.17, "Copying cabinet model...");
            File.Copy(sourceGlb, Path.Combine(cabinetRoot, CabinetGlbFileName), overwrite: true);
            cancellationToken.ThrowIfCancellationRequested();
            ValidateFaceAssignmentTargets(machineDocument.DisplayName, machineDocument.SurfaceAssignments, sourceGlb, cabinetAssetIdentity, cancellationToken);
            var faceReferences = ExportReferencedFaces(project, stagingRoot, machineDocument, cabinetDocument, cabinetAssetIdentity, progress.CreateChild(0.2, 0.7), cancellationToken);
            var objectInstances = ExportReferencedObjects(project, stagingRoot, machineDocument, cancellationToken);
            progress.Report(0.72, "Validating cabinet reflections...");
            cancellationToken.ThrowIfCancellationRequested();
            ValidateReflections(cabinetDocument.Reflections ?? [], GlbCabinetReflectionReceiverDiscovery.Discover(sourceGlb), faceReferences);
            var cabinetManifest = new CabinetRuntimeManifest(CabinetSchema, CabinetSchemaVersion, cabinetAssetName, CabinetGlbFileName, cabinetDocument.Model.Scale, cabinetDocument.Model.UpAxis, ExportReflections(cabinetManifestPath, cabinetRoot, cabinetDocument.Reflections ?? [], cancellationToken));
            progress.Report(0.85, "Writing runtime manifests...");
            cancellationToken.ThrowIfCancellationRequested();
            File.WriteAllText(Path.Combine(cabinetRoot, CabinetManifestFileName), JsonSerializer.Serialize(cabinetManifest, JsonOptions));
            var anchors = machineDocument.Anchors.Select(MachineRuntimeAnchor.From).ToArray();
            var machineManifest = new MachineRuntimeManifest(MachineSchema, MachineSchemaVersion, machineDocument.Id, machineDocument.DisplayName, ProjectAssetPathService.NormalizeProjectRelativePath(Path.Combine(CabinetDirectoryName, CabinetManifestFileName)), faceReferences, objectInstances, anchors, MachineRuntimeManifestDefinition.From(machineDocument.Runtime), machineDocument.InputDefinitions);
            File.WriteAllText(Path.Combine(stagingRoot, MachineManifestFileName), JsonSerializer.Serialize(machineManifest, JsonOptions));
            progress.Report(0.95, "Finalising Oasis Player machine...");
            cancellationToken.ThrowIfCancellationRequested();
            ReplaceFinalDirectory(stagingRoot, buildRoot);
            progress.Report(1, "Oasis Player machine build complete.");
            return MachineRuntimeBuildResult.Ok(buildRoot);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or InvalidOperationException)
        {
            return MachineRuntimeBuildResult.Fail($"Failed to build Oasis Player runtime output: {ex.Message}");
        }
        finally
        {
            if (Directory.Exists(stagingRoot)) Directory.Delete(stagingRoot, recursive: true);
        }
    }

    private string? ValidateBehavior(EditorProject project, string manifestPath, MachineDocument machine)
    {
        if (machine.Runtime is not OasisRuntimeDefinition) return null;
        var sourcePath = Path.Combine(Path.GetDirectoryName(manifestPath)!, MachineBehaviorDefinition.CanonicalSourcePath);
        if (!File.Exists(sourcePath)) throw new InvalidOperationException($"Machine '{machine.DisplayName}', behavior.oasis: source file is missing.");
        var source = File.ReadAllText(sourcePath);
        var compilation = OasisScriptCompiler.Compile(source, MachineBehaviorDefinition.CanonicalSourcePath);
        if (!compilation.Success) throw new InvalidOperationException($"Machine '{machine.DisplayName}': {string.Join(Environment.NewLine, compilation.Diagnostics)}");
        var index = new OasisScriptMachineReferenceIndexBuilder().Build(project, _libraryRoot, machine);
        var diagnostics = index.Diagnostics.Concat(OasisScriptMachineValidator.Validate(compilation.Program!, index.References)).ToArray();
        if (diagnostics.Any(x => x.Severity == OasisScriptDiagnosticSeverity.Error))
            throw new InvalidOperationException($"Machine '{machine.DisplayName}': {string.Join(Environment.NewLine, diagnostics.Select(x => x.ToString()))}");
        return source;
    }

    private IReadOnlyList<MachineRuntimeObjectInstance> ExportReferencedObjects(EditorProject project, string stagingRoot, MachineDocument machine, CancellationToken cancellationToken)
    {
        var instances = new List<MachineRuntimeObjectInstance>();
        var definitions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var instance in machine.ObjectInstances)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var reference = instance.ObjectAsset ?? throw new InvalidOperationException($"Machine Object3D instance '{instance.Id}' requires an Object3D asset reference.");
            string manifestPath;
            try { manifestPath = _assetResolver.Resolve(project, _libraryRoot, reference); }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
            { throw new InvalidOperationException($"Machine Object3D instance '{instance.Id}' cannot resolve Object3D asset {reference}: {exception.Message}", exception); }
            if (!File.Exists(manifestPath)) throw new InvalidOperationException($"Machine Object3D instance '{instance.Id}' references missing Object3D asset {reference}.");
            if (!string.Equals(Path.GetFileName(manifestPath), ProjectAssetPathService.Object3DManifestFileName, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Machine Object3D instance '{instance.Id}' reference {reference} is not an Object3D package manifest.");
            if (!Object3DDocumentStorage.TryRead(File.ReadAllText(manifestPath), out var document, out var error))
                throw new InvalidOperationException($"Machine Object3D instance '{instance.Id}' references invalid Object3D asset {reference}: {error}");
            try { Object3DValidationService.Validate(document, Path.GetDirectoryName(manifestPath)); }
            catch (InvalidOperationException exception)
            { throw new InvalidOperationException($"Machine Object3D instance '{instance.Id}', Object3D asset '{document.DisplayName}' is invalid: {exception.Message}", exception); }

            var definitionId = Guid.Parse(document.Id).ToString("D").ToLowerInvariant();
            var sourceModel = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(manifestPath)!, document.Model.Path.Replace('/', Path.DirectorySeparatorChar)));
            var fingerprint = Object3DDocumentStorage.Serialize(document) + "\nmodel-sha256:" + Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(sourceModel)));
            if (definitions.TryGetValue(definitionId, out var existingFingerprint))
            {
                if (!string.Equals(existingFingerprint, fingerprint, StringComparison.Ordinal))
                    throw new InvalidOperationException($"Object3D stable ID collision '{definitionId}': resolved packages contain conflicting definitions or model content.");
            }
            else
            {
                definitions.Add(definitionId, fingerprint);
                var destination = Path.Combine(stagingRoot, ObjectsDirectoryName, definitionId);
                Directory.CreateDirectory(destination);
                File.Copy(sourceModel, Path.Combine(destination, ObjectGlbFileName), true);
                var runtimeDefinition = Object3DRuntimeManifest.From(document, definitionId);
                File.WriteAllText(Path.Combine(destination, ObjectManifestFileName), JsonSerializer.Serialize(runtimeDefinition, JsonOptions));
            }
            var definitionManifest = ProjectAssetPathService.NormalizeProjectRelativePath(Path.Combine(ObjectsDirectoryName, definitionId, ObjectManifestFileName));
            instances.Add(new MachineRuntimeObjectInstance(instance.Id, instance.DisplayName, definitionId, definitionManifest, MachineRuntimeTransform.From(instance.Transform)));
        }
        return instances;
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
                var resolvedReels = ResolveFaceReels(project, machineDocument, faceDocument);
                var compositionContext = new FaceRuntimeCompositionContext(machineDocument.ReelAssignments, resolvedReels);
                var exportResult = _faceRuntimeExportService.Export(faceDocument, project, compositionContext, manifestPath);
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

    private IReadOnlyDictionary<MachineObjectReference, ReelDocument> ResolveFaceReels(EditorProject project, MachineDocument machine, FaceDocumentModel face)
    {
        var result = new Dictionary<MachineObjectReference, ReelDocument>();
        var requiredMounts = face.Elements.OfType<FaceReelMount>()
            .Where(mount => mount.LinkedMachineObjectReference is { Kind: MachineObjectKind.Reel })
            .GroupBy(mount => mount.LinkedMachineObjectReference!.Value);
        foreach (var mounts in requiredMounts)
        {
            var reference = mounts.Key;
            var mount = mounts.First();
            var faceName = string.IsNullOrWhiteSpace(face.Title) ? face.Id : face.Title;
            var mountName = string.IsNullOrWhiteSpace(mount.Name) ? mount.ObjectId : mount.Name;
            var context = $"Machine '{machine.DisplayName}', Face '{faceName}' reel mount '{mountName}' -> logical '{reference}'";
            var matches = machine.ReelAssignments.Where(assignment => assignment.MachineReelReference == reference).ToArray();
            if (matches.Length != 1) throw new InvalidOperationException(matches.Length == 0
                ? $"{context} -> no Reel asset assignment."
                : $"{context} -> duplicate Reel asset assignments.");
            var asset = matches[0].ReelAsset;
            var manifestPath = _assetResolver.Resolve(project, _libraryRoot, asset);
            if (!File.Exists(manifestPath)) throw new InvalidOperationException($"{context} -> Reel asset '{asset}' was not found. Library root: '{_libraryRoot}'.");
            if (!string.Equals(Path.GetFileName(manifestPath), ProjectAssetPathService.ReelManifestFileName, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"{context} -> Reel asset '{asset}' is not an asset.reel package manifest.");
            if (!ReelDocumentStorage.TryRead(File.ReadAllText(manifestPath), out var reel, out var error))
                throw new InvalidOperationException($"{context} -> Reel asset '{asset}' is invalid: {error}");
            result.Add(reference, reel);
        }
        return result;
    }

    internal static MachineRuntimeFaceReference CreateRuntimeFaceReference(CabinetDocument cabinetDocument, string faceId, string faceAssetName, string targetId, string manifest)
    {
        var targetSettings = cabinetDocument.GetSurfaceTargetSettings(targetId);
        return new MachineRuntimeFaceReference(faceId, faceAssetName, targetId, targetSettings.FrontSide, targetSettings.FaceRotation, targetSettings.FaceFlipHorizontal, manifest);
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

    public string GetBuildRoot(EditorProject project, string machineAssetName) => Path.Combine(project.GeneratedDirectory, "Builds", _pathService.SanitizePathSegment(machineAssetName));
    private static string ResolveCabinetModelPath(string manifestPath, string modelPath)
    {
        if (Path.IsPathFullyQualified(modelPath)) throw new InvalidOperationException("Cabinet model paths must be package-relative.");
        var packageRoot = Path.GetFullPath(Path.GetDirectoryName(manifestPath) ?? string.Empty).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var resolved = Path.GetFullPath(Path.Combine(packageRoot, modelPath));
        if (!resolved.StartsWith(packageRoot, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Cabinet model path escapes the Cabinet package.");
        return resolved;
    }
    private static void ReplaceEmptyDirectory(string path) { if (Directory.Exists(path)) Directory.Delete(path, true); Directory.CreateDirectory(path); }
    private static void ReplaceFinalDirectory(string stagingRoot, string buildRoot) { if (Directory.Exists(buildRoot)) Directory.Delete(buildRoot, true); Directory.CreateDirectory(Path.GetDirectoryName(buildRoot)!); Directory.Move(stagingRoot, buildRoot); }
}

public sealed record MachineRuntimeBuildResult(bool Success, string? BuildRoot, string? ErrorMessage)
{
    public static MachineRuntimeBuildResult Ok(string buildRoot) => new(true, buildRoot, null);
    public static MachineRuntimeBuildResult Fail(string errorMessage) => new(false, null, errorMessage);
}

public sealed record MachineRuntimeManifest(string Schema, int SchemaVersion, string MachineId, string DisplayName, string CabinetManifest, IReadOnlyList<MachineRuntimeFaceReference> Faces, IReadOnlyList<MachineRuntimeObjectInstance> ObjectInstances, IReadOnlyList<MachineRuntimeAnchor> Anchors, MachineRuntimeManifestDefinition Runtime, IReadOnlyList<InputDefinitionModel> Inputs);

/// <summary>Generated Player contract projection; distinct from the authored RuntimeDefinition.</summary>
[JsonPolymorphic]
[JsonDerivedType(typeof(EmulationMachineRuntimeManifestDefinition))]
[JsonDerivedType(typeof(OasisMachineRuntimeManifestDefinition))]
public abstract record MachineRuntimeManifestDefinition(string Kind)
{
    public static MachineRuntimeManifestDefinition From(RuntimeDefinition runtime) => runtime switch
    {
        EmulationRuntimeDefinition emulation => new EmulationMachineRuntimeManifestDefinition(emulation.Platform.ToString(), JsonSerializer.Serialize(emulation.PlatformSettings, emulation.PlatformSettings.GetType(), new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })),
        OasisRuntimeDefinition => new OasisMachineRuntimeManifestDefinition(new("OasisScript", "behavior/behavior.oasis")),
        _ => throw new InvalidOperationException($"Runtime '{runtime.Kind}' is not supported by the Player build pipeline.")
    };
}
public sealed record EmulationMachineRuntimeManifestDefinition(string Platform, string PlatformSettingsJson) : MachineRuntimeManifestDefinition("Emulation");
public sealed record OasisMachineRuntimeManifestDefinition(MachineRuntimeBehavior Behavior) : MachineRuntimeManifestDefinition("Oasis");
public sealed record MachineRuntimeBehavior(string Kind, string Source);
public sealed record MachineRuntimeFaceReference(string FaceId, string AssetName, string CabinetFaceTargetId, string FrontSide, int FaceRotation, bool FaceFlipHorizontal, string Manifest);
public sealed record RuntimeVector3(double X, double Y, double Z)
{
    public static RuntimeVector3 From(MachineVector3 value) => new(value.X, value.Y, value.Z);
}
public sealed record MachineRuntimeTransform(RuntimeVector3 Position, RuntimeVector3 RotationEulerDegrees, RuntimeVector3 Scale)
{
    public static MachineRuntimeTransform From(MachineObjectTransform value) => new(RuntimeVector3.From(value.Position), RuntimeVector3.From(value.Rotation), RuntimeVector3.From(value.Scale));
}
public sealed record MachineRuntimeObjectInstance(string Id, string DisplayName, string DefinitionId, string DefinitionManifest, MachineRuntimeTransform Transform);
public sealed record MachineRuntimeAnchor(string Id, string DisplayName, RuntimeVector3 Position, RuntimeVector3 RotationEulerDegrees)
{
    public static MachineRuntimeAnchor From(MachineAnchor value) => new(value.Id, value.DisplayName, RuntimeVector3.From(value.Position), RuntimeVector3.From(value.Rotation));
}
public sealed record Object3DRuntimeManifest(string Schema, int SchemaVersion, string DefinitionId, string DisplayName, string Model, double ModelScale, string UpAxis, Object3DRuntimeCollider Collider, Object3DRuntimeRigidbody Rigidbody)
{
    public static Object3DRuntimeManifest From(Object3DDocument document, string definitionId) => new(MachineRuntimeBuildService.ObjectSchema, MachineRuntimeBuildService.ObjectSchemaVersion, definitionId, document.DisplayName, MachineRuntimeBuildService.ObjectGlbFileName, document.Model.Scale, document.Model.UpAxis, Object3DRuntimeCollider.From(document.Physics.Collider), Object3DRuntimeRigidbody.From(document.Physics.Rigidbody));
}
public sealed record Object3DRuntimeCollider(string Kind, double[]? Center = null, double? Radius = null, double[]? Size = null, double? Height = null, string? Axis = null)
{
    public static Object3DRuntimeCollider From(Object3DColliderDefinition value) => value.Kind switch
    {
        Object3DColliderKind.Sphere => new("Sphere", value.Center, value.Radius),
        Object3DColliderKind.Box => new("Box", value.Center, Size: value.Size),
        Object3DColliderKind.Capsule => new("Capsule", value.Center, value.Radius, Height: value.Height, Axis: value.Axis!.Value.ToString()),
        Object3DColliderKind.Mesh => new("Mesh"),
        _ => new("None")
    };
}
public sealed record Object3DRuntimeRigidbody(bool Enabled, double? Mass, bool? UseGravity)
{
    public static Object3DRuntimeRigidbody From(Object3DRigidbodyDefinition value) => value.Enabled ? new(true, value.Mass, value.UseGravity) : new(false, null, null);
}
public sealed record CabinetRuntimeManifest(string Schema, int SchemaVersion, string CabinetId, string Glb, double Scale, string UpAxis, IReadOnlyList<CabinetReflectionDefinition> Reflections);
