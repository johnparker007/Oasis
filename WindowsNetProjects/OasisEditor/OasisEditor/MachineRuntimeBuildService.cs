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
    public const string MachineSchema = "oasis.machine.runtime";
    public const string CabinetSchema = "oasis.cabinet.runtime";
    public const string ObjectSchema = "oasis.object3d.runtime";
    public const int MachineSchemaVersion = 13;
    public const int CabinetSchemaVersion = 5;
    public const int ObjectSchemaVersion = 2;

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
        _libraryRoot = libraryRoot ?? string.Empty;
    }

    public MachineRuntimeBuildResult BuildFromMachineDocument(EditorProject project, string machineManifestPath, IEditorProgressReporter progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(progress);
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(machineManifestPath)) return MachineRuntimeBuildResult.Fail("A saved Machine asset must be selected before building for Oasis Player.");
        string secureMachinePath;
        try
        {
            var relative = Path.GetRelativePath(Path.GetFullPath(project.ProjectDirectory), Path.GetFullPath(machineManifestPath));
            secureMachinePath = WorkspacePathPolicy.ResolveConfined(project.ProjectDirectory, relative, requireExists: true);
            if (!string.Equals(Path.GetFullPath(machineManifestPath), secureMachinePath, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
                return MachineRuntimeBuildResult.Fail("Machine manifest must be inside the current project.");
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        { return MachineRuntimeBuildResult.Fail($"Machine manifest path is not safe: {exception.Message}"); }
        machineManifestPath = secureMachinePath;
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
        try { _ = MachineDocumentStorage.Serialize(machineDocument); }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException) { return MachineRuntimeBuildResult.Fail(exception.Message); }
        var machineAssetName = ProjectAssetPathService.GetPackageAssetNameFromManifestPath(machineManifestPath, EditorAssetType.Machine);
        if (string.IsNullOrWhiteSpace(machineAssetName)) return MachineRuntimeBuildResult.Fail("Machine manifests must be stored as Assets/Machines/<Name>/asset.machine before building for Oasis Player.");
        if (machineDocument.CabinetAsset is null) return MachineRuntimeBuildResult.Fail($"Machine '{machineDocument.DisplayName}' has no Cabinet asset assigned.");
        string cabinetManifestPath;
        try { cabinetManifestPath = ResolveAsset(project, machineDocument.CabinetAsset); }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException) { return MachineRuntimeBuildResult.Fail($"Machine '{machineDocument.DisplayName}' cannot resolve Cabinet {machineDocument.CabinetAsset}: {exception.Message}"); }
        if (!File.Exists(cabinetManifestPath)) return MachineRuntimeBuildResult.Fail($"Machine '{machineDocument.DisplayName}' references a missing Cabinet {machineDocument.CabinetAsset}. Library root: '{_libraryRoot}'.");
        if (!CabinetDocumentStorage.TryRead(File.ReadAllText(cabinetManifestPath), out var cabinetDocument)) return MachineRuntimeBuildResult.Fail($"Machine '{machineDocument.DisplayName}' references an invalid Cabinet: {machineDocument.CabinetAsset}");
        var cabinetAssetName = Path.GetFileName(Path.GetDirectoryName(cabinetManifestPath));
        string sourceGlb;
        try { sourceGlb = ResolveCabinetModelPath(cabinetManifestPath, cabinetDocument.Model.Path); }
        catch (Exception exception) when (exception is InvalidOperationException or InvalidDataException) { return MachineRuntimeBuildResult.Fail($"Machine '{machineDocument.DisplayName}' references an invalid Cabinet {machineDocument.CabinetAsset}: {exception.Message}"); }
        if (!File.Exists(sourceGlb)) return MachineRuntimeBuildResult.Fail($"Machine '{machineDocument.DisplayName}' references Cabinet {machineDocument.CabinetAsset}, whose package GLB was not found: {sourceGlb}");
        var buildRoot = GetBuildRoot(project, machineAssetName);
        var stagingRoot = buildRoot + ".staging";
        try
        {
            var projectRoot = Path.GetFullPath(project.ProjectDirectory);
            _ = WorkspacePathPolicy.ResolveConfined(projectRoot, Path.GetRelativePath(projectRoot, buildRoot), requireExists: false);
            _ = WorkspacePathPolicy.ResolveConfined(projectRoot, Path.GetRelativePath(projectRoot, stagingRoot), requireExists: false);
            progress.Report(0.05, "Preparing build output...");
            // Read and validate the persisted source again; never trust the Editor buffer.
            var behaviorSource = ValidateBehavior(project, machineManifestPath, machineDocument);
            ReplaceEmptyDirectory(stagingRoot);
            var fabricRuntime = ExportImpactFabricRuntime(project, stagingRoot, machineDocument.Runtime);
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
            var (prefabDependencies, objectInstances) = ExportReferencedObjects(project, stagingRoot, machineDocument, cancellationToken);
            progress.Report(0.72, "Validating cabinet reflections...");
            cancellationToken.ThrowIfCancellationRequested();
            ValidateReflections(cabinetDocument.Reflections ?? [], GlbCabinetReflectionReceiverDiscovery.Discover(sourceGlb), faceReferences);
            var cabinetManifest = new CabinetRuntimeManifest(CabinetSchema, CabinetSchemaVersion, cabinetAssetName, CabinetGlbFileName, cabinetDocument.Model.Scale, cabinetDocument.Model.UpAxis, ExportReflections(cabinetManifestPath, cabinetRoot, cabinetDocument.Reflections ?? [], cancellationToken));
            progress.Report(0.85, "Writing runtime manifests...");
            cancellationToken.ThrowIfCancellationRequested();
            File.WriteAllText(Path.Combine(cabinetRoot, CabinetManifestFileName), JsonSerializer.Serialize(cabinetManifest, JsonOptions));
            var anchors = machineDocument.Anchors.Select(MachineRuntimeAnchor.From).ToArray();
            var geometrySurfaces = machineDocument.GeometrySurfaces.Select(MachineRuntimeGeometrySurface.From).ToArray();
            var machineManifest = new MachineRuntimeManifest(MachineSchema, MachineSchemaVersion, machineDocument.Id, machineDocument.DisplayName, ProjectAssetPathService.NormalizeProjectRelativePath(Path.Combine(CabinetDirectoryName, CabinetManifestFileName)), faceReferences, objectInstances, anchors, MachineRuntimeManifestDefinition.From(machineDocument.Runtime, fabricRuntime), machineDocument.InputDefinitions, prefabDependencies, geometrySurfaces, MachineRuntimeInteraction.From(machineDocument.Interaction));
            File.WriteAllText(Path.Combine(stagingRoot, MachineManifestFileName), JsonSerializer.Serialize(machineManifest, JsonOptions));
            progress.Report(0.95, "Finalising Oasis Player machine...");
            cancellationToken.ThrowIfCancellationRequested();
            ReplaceFinalDirectory(stagingRoot, buildRoot);
            progress.Report(1, "Oasis Player machine build complete.");
            return MachineRuntimeBuildResult.Ok(buildRoot);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or InvalidOperationException or ArgumentException)
        {
            return MachineRuntimeBuildResult.Fail($"Failed to build Oasis Player runtime output: {ex.Message}");
        }
        finally
        {
            if (Directory.Exists(stagingRoot)) Directory.Delete(stagingRoot, recursive: true);
        }
    }

    private static FabricRuntimeDescriptor? ExportImpactFabricRuntime(EditorProject project, string stagingRoot, RuntimeDefinition runtime)
    {
        if (runtime is not EmulationRuntimeDefinition { Platform: FruitMachinePlatformType.Impact, PlatformSettings: System6NativeRomSettings authoredSettings })
            return null;

        var resolvedSettings = (System6NativeRomSettings)FabricRomResourceResolver.ResolveSettingsPaths(project, FruitMachinePlatformType.Impact, authoredSettings);
        ValidateContiguousRomSlots(resolvedSettings.ProgramRomPaths, "program");
        ValidateContiguousRomSlots(resolvedSettings.SoundRomPaths, "sound");
        if (string.IsNullOrWhiteSpace(resolvedSettings.ProgramRom1Path))
            throw new InvalidOperationException("Impact builds require Program ROM 1.");

        var romResources = FabricRomResourceResolver.Build(resolvedSettings);
        var roms = new List<FabricRuntimeRom>(romResources.Count);
        foreach (var resource in romResources)
        {
            if (!File.Exists(resource.Path))
                throw new InvalidOperationException($"Impact {RomRoleName(resource.Role)} ROM {resource.Slot + 1} was not found: '{resource.Path}'.");
            var fullPath = Path.GetFullPath(resource.Path);
            var source = WorkspacePathPolicy.ResolveConfined(Path.GetDirectoryName(fullPath)!, Path.GetFileName(fullPath), requireExists: true);

            var roleName = RomRoleName(resource.Role);
            var relativePath = $"emulation/roms/{roleName}-{resource.Slot}.bin";
            var destination = Path.Combine(stagingRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(source, destination, overwrite: true);
            roms.Add(new FabricRuntimeRom((int)resource.Role, checked((int)resource.Slot), relativePath));
        }

        byte[] configurationBytes;
        try
        {
            configurationBytes = FabricAmberSystem6Configuration.FromSystem6(authoredSettings).ToNativeBytes();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or OverflowException)
        {
            throw new InvalidOperationException($"Impact Amber configuration is invalid: {exception.Message}", exception);
        }

        const string configurationPath = "emulation/impact.config.bin";
        var configurationDestination = Path.Combine(stagingRoot, configurationPath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(configurationDestination)!);
        File.WriteAllBytes(configurationDestination, configurationBytes);

        return new FabricRuntimeDescriptor("amber", "jpm-system6", configurationPath, roms);
    }

    private static void ValidateContiguousRomSlots(IReadOnlyList<string> paths, string role)
    {
        var gap = false;
        for (var index = 0; index < paths.Count; index++)
        {
            if (string.IsNullOrWhiteSpace(paths[index])) gap = true;
            else if (gap) throw new InvalidOperationException($"Impact {role} ROM slots must be contiguous from ROM 1; ROM {index + 1} is configured after a gap.");
        }
    }

    private static string RomRoleName(FabricRomRole role) => role switch
    {
        FabricRomRole.Program => "program",
        FabricRomRole.Sound => "sound",
        _ => throw new InvalidOperationException($"Impact ROM role '{role}' is unsupported.")
    };

    private string? ValidateBehavior(EditorProject project, string manifestPath, MachineDocument machine)
    {
        if (machine.Runtime is not OasisRuntimeDefinition) return null;
        var sourcePath = WorkspacePathPolicy.ResolveConfined(Path.GetDirectoryName(manifestPath)!, MachineBehaviorDefinition.CanonicalSourcePath, requireExists: false);
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

    private (IReadOnlyList<MachineRuntimePrefabDependency> Dependencies, IReadOnlyList<MachineRuntimeObjectInstance> Instances)
        ExportReferencedObjects(EditorProject project, string stagingRoot, MachineDocument machine, CancellationToken cancellationToken)
    {
        var instances = new List<MachineRuntimeObjectInstance>();
        var dependencies = new List<MachineRuntimePrefabDependency>();
        var definitions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var bodyOwners = new Dictionary<string, bool>(StringComparer.Ordinal);
        var sourceRootScales = new Dictionary<string, MachineVector3>(StringComparer.Ordinal);

        (Object3DDocument Document, string DefinitionId, string DefinitionManifest) ExportDefinition(AssetReference reference, string context)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string manifestPath;
            try { manifestPath = ResolveAsset(project, reference); }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
            { throw new InvalidOperationException($"{context} cannot resolve Object3D asset {reference}: {exception.Message}", exception); }
            if (!File.Exists(manifestPath)) throw new InvalidOperationException($"{context} references missing Object3D asset {reference}.");
            if (!string.Equals(Path.GetFileName(manifestPath), ProjectAssetPathService.Object3DManifestFileName, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"{context} reference {reference} is not an Object3D package manifest.");
            if (!Object3DDocumentStorage.TryRead(File.ReadAllText(manifestPath), out var document, out var error))
                throw new InvalidOperationException($"{context} references invalid Object3D asset {reference}: {error}");
            try { Object3DValidationService.Validate(document, Path.GetDirectoryName(manifestPath)); }
            catch (InvalidOperationException exception)
            { throw new InvalidOperationException($"{context}, Object3D asset '{document.DisplayName}' is invalid: {exception.Message}", exception); }

            var definitionId = Guid.Parse(document.Id).ToString("D").ToLowerInvariant();
            var sourcePackage = Path.GetDirectoryName(manifestPath)!;
            var models = new Dictionary<string, (string Source, string Hash)>(StringComparer.OrdinalIgnoreCase);
            void CollectModels(Object3DNodeDefinition node)
            {
                foreach (var component in node.Components.Where(c => c.Model is not null))
                {
                    var relative = component.Model!.Path;
                    var source = WorkspacePathPolicy.ResolveConfined(sourcePackage, relative, requireExists: true);
                    models[relative] = (source, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(source))).ToLowerInvariant());
                }
                foreach (var child in node.Children) CollectModels(child);
            }
            CollectModels(document.Root);
            var fingerprint = Object3DDocumentStorage.Serialize(document) + string.Concat(models.OrderBy(model => model.Key, StringComparer.Ordinal)
                .Select(model => "\n" + model.Key + ":" + model.Value.Hash));
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
                foreach (var model in models.Values)
                {
                    var output = Path.Combine(destination, "models", model.Hash + ".glb");
                    Directory.CreateDirectory(Path.GetDirectoryName(output)!);
                    if (!File.Exists(output)) File.Copy(model.Source, output, false);
                }
                var runtimeDefinition = Object3DRuntimeManifest.From(document, definitionId, path => "models/" + models[path].Hash + ".glb");
                File.WriteAllText(Path.Combine(destination, ObjectManifestFileName), JsonSerializer.Serialize(runtimeDefinition, JsonOptions));
            }
            return (document, definitionId, ProjectAssetPathService.NormalizeProjectRelativePath(Path.Combine(ObjectsDirectoryName, definitionId, ObjectManifestFileName)));
        }

        // Export declared dependencies first, even when the script only spawns them.
        foreach (var dependency in machine.PrefabDependencies)
        {
            var exported = ExportDefinition(dependency.PrefabAsset, $"Machine Prefab alias '{dependency.Alias}'");
            dependencies.Add(new MachineRuntimePrefabDependency(dependency.Alias, exported.DefinitionId, exported.DefinitionManifest));
        }
        foreach (var instance in machine.ObjectInstances)
        {
            var reference = instance.ObjectAsset ?? throw new InvalidOperationException($"Machine Object3D instance '{instance.Id}' requires an Object3D asset reference.");
            var exported = ExportDefinition(reference, $"Machine Object3D instance '{instance.Id}'");
            var document = exported.Document;
            bool HasBody(Object3DNodeDefinition node) => node.Components.Any(c => c.Rigidbody is { Mode: not Object3DRigidbodyMode.Static }) || node.Children.Any(HasBody);
            bodyOwners[instance.Id] = HasBody(document.Root);
            sourceRootScales[instance.Id] = document.Root.Transform.Scale;
            instances.Add(new MachineRuntimeObjectInstance(instance.Id, instance.DisplayName, exported.DefinitionId, exported.DefinitionManifest, MachineRuntimeTransform.From(instance.Transform), instance.ParentId, instance.Active));
        }
        var authored = machine.ObjectInstances.ToDictionary(i => i.Id, StringComparer.Ordinal);
        foreach (var instance in machine.ObjectInstances.Where(i => bodyOwners[i.Id]))
        {
            var parentId = instance.ParentId;
            while (parentId is not null)
            {
                if (bodyOwners[parentId]) throw new InvalidOperationException($"Machine instance '{instance.Id}' and ancestor '{parentId}' both own a Rigidbody; nested bodies are unsupported.");
                if (authored[parentId].Transform.Scale != MachineVector3.One || sourceRootScales[parentId] != MachineVector3.One)
                    throw new InvalidOperationException($"Machine instance '{instance.Id}' owns a Rigidbody beneath scaled parent '{parentId}'; scaled body parents are unsupported.");
                parentId = authored[parentId].ParentId;
            }
        }
        return (dependencies, instances);
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
            string source;
            try { source = WorkspacePathPolicy.ResolveConfined(Path.GetDirectoryName(cabinetManifestPath)!, definition.VisibilityMask, requireExists: false); }
            catch (InvalidDataException exception) { throw new InvalidOperationException($"Reflection '{definition.Id}' visibility mask escapes the Cabinet asset directory: {exception.Message}", exception); }
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
            catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or InvalidOperationException)
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
            var manifestPath = ResolveAsset(project, asset);
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
        var relative = Path.GetRelativePath(project.ProjectDirectory, fullPath);
        var secure = WorkspacePathPolicy.ResolveConfined(project.ProjectDirectory, relative, requireExists: true);
        return Directory.Exists(secure) ? WorkspacePathPolicy.ResolveConfined(secure, ProjectAssetPathService.FaceManifestFileName, requireExists: true) : secure;
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

    public string GetBuildRoot(EditorProject project, string machineAssetName)
    {
        ArgumentNullException.ThrowIfNull(project);
        var generatedRelative = Path.GetRelativePath(project.ProjectDirectory, project.GeneratedDirectory);
        return WorkspacePathPolicy.ResolveConfined(project.ProjectDirectory,
            Path.Combine(generatedRelative, "Builds", _pathService.SanitizePathSegment(machineAssetName)), requireExists: false);
    }
    private string ResolveAsset(EditorProject project, AssetReference reference)
    {
        try
        {
            var path = _assetResolver.Resolve(project, _libraryRoot, reference);
            var root = reference.Scope == AssetReferenceScope.Project ? project.ProjectDirectory : _libraryRoot;
            // Keep missing assets as unresolved candidates so the caller can add the
            // Machine/object/reel context to its diagnostic. ResolveConfined still
            // walks existing ancestors and rejects reparse points when the leaf is absent.
            return WorkspacePathPolicy.ResolveConfined(root, Path.GetRelativePath(root, path), requireExists: false);
        }
        catch (InvalidDataException exception) { throw new InvalidOperationException($"Asset reference '{reference}' is not safe: {exception.Message}", exception); }
    }
    private static string ResolveCabinetModelPath(string manifestPath, string modelPath)
    {
        if (Path.IsPathFullyQualified(modelPath)) throw new InvalidOperationException("Cabinet model paths must be package-relative.");
        var packageRoot = Path.GetFullPath(Path.GetDirectoryName(manifestPath) ?? string.Empty).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return WorkspacePathPolicy.ResolveConfined(packageRoot, modelPath, requireExists: false);
    }
    private static void ReplaceEmptyDirectory(string path)
    {
        if (Directory.Exists(path)) { if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new IOException($"Build staging directory is a reparse point: {path}"); Directory.Delete(path, true); }
        Directory.CreateDirectory(path);
    }
    private static void ReplaceFinalDirectory(string stagingRoot, string buildRoot)
    {
        if (Directory.Exists(buildRoot)) { if ((File.GetAttributes(buildRoot) & FileAttributes.ReparsePoint) != 0) throw new IOException($"Build output directory is a reparse point: {buildRoot}"); Directory.Delete(buildRoot, true); }
        Directory.CreateDirectory(Path.GetDirectoryName(buildRoot)!); Directory.Move(stagingRoot, buildRoot);
    }
}

public sealed record MachineRuntimeBuildResult(bool Success, string? BuildRoot, string? ErrorMessage)
{
    public static MachineRuntimeBuildResult Ok(string buildRoot) => new(true, buildRoot, null);
    public static MachineRuntimeBuildResult Fail(string errorMessage) => new(false, null, errorMessage);
}

public sealed record MachineRuntimeManifest(string Schema, int SchemaVersion, string MachineId, string DisplayName, string CabinetManifest, IReadOnlyList<MachineRuntimeFaceReference> Faces, IReadOnlyList<MachineRuntimeObjectInstance> ObjectInstances, IReadOnlyList<MachineRuntimeAnchor> Anchors, MachineRuntimeManifestDefinition Runtime, IReadOnlyList<InputDefinitionModel> Inputs, IReadOnlyList<MachineRuntimePrefabDependency> PrefabDependencies, IReadOnlyList<MachineRuntimeGeometrySurface> GeometrySurfaces, MachineRuntimeInteraction Interaction);

public sealed record MachineRuntimeInteraction(string Kind, string? CuePrefabAlias = null, string? PlayingSurfaceId = null)
{
    public static MachineRuntimeInteraction From(MachineInteractionDefinition value) => new(value.Kind, value.CuePrefabAlias, value.PlayingSurfaceId);
}

/// <summary>Generated Player contract projection; distinct from the authored RuntimeDefinition.</summary>
[JsonPolymorphic]
[JsonDerivedType(typeof(EmulationMachineRuntimeManifestDefinition))]
[JsonDerivedType(typeof(OasisMachineRuntimeManifestDefinition))]
public abstract record MachineRuntimeManifestDefinition(string Kind)
{
    public static MachineRuntimeManifestDefinition From(RuntimeDefinition runtime, FabricRuntimeDescriptor? fabric = null) => runtime switch
    {
        EmulationRuntimeDefinition emulation => new EmulationMachineRuntimeManifestDefinition(emulation.Platform.ToString(), SerializePlatformSettings(emulation), fabric),
        OasisRuntimeDefinition => new OasisMachineRuntimeManifestDefinition(new("OasisScript", "behavior/behavior.oasis")),
        _ => throw new InvalidOperationException($"Runtime '{runtime.Kind}' is not supported by the Player build pipeline.")
    };

    private static string SerializePlatformSettings(EmulationRuntimeDefinition emulation)
    {
        if (emulation.Platform != FruitMachinePlatformType.Impact || emulation.PlatformSettings is not System6NativeRomSettings settings)
            return JsonSerializer.Serialize(emulation.PlatformSettings, emulation.PlatformSettings.GetType(), new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        // The Player consumes the copied ROMs through FabricRuntimeDescriptor. Keep the
        // settings useful for inspection without leaking machine-local source paths.
        var sanitized = JsonSerializer.Deserialize<System6NativeRomSettings>(JsonSerializer.Serialize(settings))
            ?? throw new InvalidOperationException("Impact settings could not be copied for runtime metadata.");
        sanitized.ProgramRom1Path = string.Empty; sanitized.ProgramRom2Path = string.Empty;
        sanitized.ProgramRom3Path = string.Empty; sanitized.ProgramRom4Path = string.Empty;
        sanitized.SoundRom1Path = string.Empty; sanitized.SoundRom2Path = string.Empty;
        sanitized.SoundRom3Path = string.Empty; sanitized.SoundRom4Path = string.Empty;
        return JsonSerializer.Serialize(sanitized, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
    }
}
public sealed record EmulationMachineRuntimeManifestDefinition(string Platform, string PlatformSettingsJson, FabricRuntimeDescriptor? Fabric = null) : MachineRuntimeManifestDefinition("Emulation");
public sealed record FabricRuntimeDescriptor(string BackendKind, string MachineIdentifier, string Configuration, IReadOnlyList<FabricRuntimeRom> Roms);
public sealed record FabricRuntimeRom(int Role, int Slot, string Path);
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
public sealed record MachineRuntimeObjectInstance(string Id, string DisplayName, string DefinitionId, string DefinitionManifest, MachineRuntimeTransform Transform, string? ParentId = null, bool Active = true);
public sealed record MachineRuntimeAnchor(string Id, string DisplayName, RuntimeVector3 Position, RuntimeVector3 RotationEulerDegrees)
{
    public static MachineRuntimeAnchor From(MachineAnchor value) => new(value.Id, value.DisplayName, RuntimeVector3.From(value.Position), RuntimeVector3.From(value.Rotation));
}
public sealed record MachineRuntimePrefabDependency(string Alias, string DefinitionId, string DefinitionManifest);
public sealed record MachineRuntimeGeometrySurface(string Id, RuntimeVector3 Position, RuntimeVector3 RotationEulerDegrees, double Width, double Length)
{
    public static MachineRuntimeGeometrySurface From(MachineGeometrySurface value) => new(value.Id, RuntimeVector3.From(value.Position), RuntimeVector3.From(value.Rotation), value.Width, value.Length);
}
public sealed record Object3DRuntimeManifest(string Schema, int SchemaVersion, string DefinitionId, string DisplayName, Object3DRuntimeNode Root)
{
    public static Object3DRuntimeManifest From(Object3DDocument document, string definitionId, Func<string, string> resolveModel) =>
        new(MachineRuntimeBuildService.ObjectSchema, MachineRuntimeBuildService.ObjectSchemaVersion, definitionId, document.DisplayName, Object3DRuntimeNode.From(document.Root, resolveModel));
}
public sealed record Object3DRuntimeNode(string Id, string Name, Object3DRuntimeComponent[] Components, Object3DRuntimeNode[] Children)
{
    public static Object3DRuntimeNode From(Object3DNodeDefinition node, Func<string, string> resolveModel) => new(node.Id, node.Name,
        node.Components.Select(c => new Object3DRuntimeComponent(c.ComponentId, c.Kind, c.ContractVersion,
            c.Transform is null ? null : MachineRuntimeTransform.From(c.Transform),
            c.Model is null ? null : new Object3DRuntimeModel(resolveModel(c.Model.Path), c.Model.Scale, c.Model.UpAxis),
            c.Collider is null ? null : Object3DRuntimeCollider.From(c.Collider),
            c.Rigidbody is null ? null : new Object3DRuntimeRigidbody(c.Rigidbody.Mode.ToString(), c.Rigidbody.Mass, c.Rigidbody.UseGravity ?? false))).ToArray(),
        node.Children.Select(c => From(c, resolveModel)).ToArray());
}
public sealed record Object3DRuntimeComponent(string ComponentId, string Kind, int ContractVersion, MachineRuntimeTransform? Transform,
    Object3DRuntimeModel? Model, Object3DRuntimeCollider? Collider, Object3DRuntimeRigidbody? Rigidbody);
public sealed record Object3DRuntimeModel(string Path, double Scale, string UpAxis);
public sealed record Object3DRuntimeCollider(string Kind, double[]? Center, double? Radius, double[]? Size, double? Height, string? Axis, string SourceFrame, bool IsTrigger)
{
    public static Object3DRuntimeCollider From(Object3DColliderDefinition value) => new(value.Kind.ToString(), value.Center, value.Radius, value.Size, value.Height, value.Axis?.ToString(), value.SourceFrame.ToString(), value.IsTrigger);
}
public sealed record Object3DRuntimeRigidbody(string Mode, double? Mass, bool UseGravity);
public sealed record CabinetRuntimeManifest(string Schema, int SchemaVersion, string CabinetId, string Glb, double Scale, string UpAxis, IReadOnlyList<CabinetReflectionDefinition> Reflections);
