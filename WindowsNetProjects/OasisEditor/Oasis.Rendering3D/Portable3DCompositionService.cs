using System.Numerics;
using System.Text.Json;
using OasisEditor;
using OasisEditor.Features.CabinetEditor.Models;
using OasisEditor.Features.CabinetEditor.Services;

namespace Oasis.Rendering3D;

/// <summary>Composes authored Cabinet, Prefab and Machine manifests into a renderer-neutral scene projection.</summary>
public sealed class Portable3DCompositionService
{
    private readonly PortableGlbSceneLoader _loader;
    private readonly AssetReferenceResolver _assetResolver = new();

    public Portable3DCompositionService(PortableGlbSceneLoader? loader = null) => _loader = loader ?? new PortableGlbSceneLoader();

    public async Task<Portable3DCompositionResult> ComposeCabinetAsync(
        EditorProject project,
        string cabinetManifestPath,
        MachineDocument? machine = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        var scene = new CompositionBuilder(_loader);
        if (!CabinetDocumentStorage.TryRead(await File.ReadAllTextAsync(cabinetManifestPath, cancellationToken).ConfigureAwait(false), out var cabinet))
        {
            scene.Diagnostics.Add($"Cabinet manifest could not be read: {cabinetManifestPath}");
            return scene.Build();
        }
        return await ComposeCabinetAsync(project, cabinet, cabinetManifestPath, machine, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Portable3DCompositionResult> ComposeCabinetAsync(
        EditorProject project,
        CabinetDocument cabinet,
        string cabinetManifestPath,
        MachineDocument? machine = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(cabinet);
        var scene = new CompositionBuilder(_loader);
        await AppendCabinetAsync(scene, cabinetManifestPath, cabinet, machine, cancellationToken).ConfigureAwait(false);
        return scene.Build();
    }

    public async Task<Portable3DCompositionResult> ComposePrefabAsync(
        EditorProject project,
        string prefabManifestPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        try
        {
            var definition = await ReadPrefabAsync(prefabManifestPath, cancellationToken).ConfigureAwait(false);
            return await ComposePrefabAsync(project, definition, prefabManifestPath, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (IsContentError(ex))
        {
            var scene = new CompositionBuilder(_loader);
            scene.Diagnostics.Add($"Prefab manifest cannot be read: {ex.Message}");
            return scene.Build();
        }
    }

    public async Task<Portable3DCompositionResult> ComposePrefabAsync(
        EditorProject project,
        Object3DDocument definition,
        string prefabManifestPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(definition);
        var scene = new CompositionBuilder(_loader);
        await AppendPrefabAsync(scene, definition, Path.GetDirectoryName(Path.GetFullPath(prefabManifestPath))!, definition.Id,
            Matrix4x4.Identity, cancellationToken).ConfigureAwait(false);
        return scene.Build();
    }

    public async Task<Portable3DCompositionResult> ComposeMachineAsync(
        EditorProject project,
        MachineDocument machine,
        string libraryRoot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(machine);
        var scene = new CompositionBuilder(_loader);
        if (machine.CabinetAsset is null)
            scene.Diagnostics.Add("Machine has no Cabinet asset assigned.");
        else
        {
            try
            {
                var cabinetManifest = ResolveAsset(project, libraryRoot, machine.CabinetAsset);
                await AppendCabinetAsync(scene, cabinetManifest, null, machine, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (IsContentError(ex))
            {
                scene.Diagnostics.Add($"Cabinet '{machine.CabinetAsset.Path}' cannot be composed: {ex.Message}");
            }
        }

        var instances = machine.ObjectInstances ?? [];
        var byId = instances.GroupBy(item => item.Id, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var definitions = new Dictionary<AssetReference, (Object3DDocument Definition, string Manifest, string PackageRoot)>();
        foreach (var instance in instances)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (instance.ObjectAsset is null)
            {
                scene.Diagnostics.Add($"{instance.Id}: no Object3D asset assigned.");
                continue;
            }
            try
            {
                if (!definitions.TryGetValue(instance.ObjectAsset, out var resolved))
                {
                    var manifest = ResolveAsset(project, libraryRoot, instance.ObjectAsset);
                    var definition = await ReadPrefabAsync(manifest, cancellationToken).ConfigureAwait(false);

                    resolved = (definition, manifest, Path.GetDirectoryName(Path.GetFullPath(manifest))!);
                    definitions.Add(instance.ObjectAsset, resolved);
                }

                if (!IsActive(instance, byId)) continue;
                var machineTransform = MachineInstanceTransform(instance, byId, definitions, project, libraryRoot, scene, cancellationToken);
                await AppendPrefabAsync(scene, resolved.Definition, resolved.PackageRoot, instance.Id, machineTransform, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) when (IsContentError(ex))
            {
                scene.Diagnostics.Add($"{instance.Id}: Object3D '{instance.ObjectAsset.Path}' cannot be composed: {ex.Message}");
            }
        }

        foreach (var anchor in machine.Anchors ?? [])
        {
            var position = Portable3DTransform.ToVector3(anchor.Position);
            var rotation = Portable3DTransform.Rotation(anchor.Rotation);
            var world = rotation * Matrix4x4.CreateTranslation(position);
            scene.Anchors.Add(new Portable3DAnchorPose(anchor.Id, anchor.DisplayName, position, anchor.Rotation, world));
            scene.Include(position);
        }
        return scene.Build();
    }

    private async Task AppendCabinetAsync(CompositionBuilder scene, string manifestPath, CabinetDocument? authoredCabinet, MachineDocument? machine, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        CabinetDocument cabinet;
        if (authoredCabinet is not null) cabinet = authoredCabinet;
        else if (!CabinetDocumentStorage.TryRead(await File.ReadAllTextAsync(manifestPath, token).ConfigureAwait(false), out cabinet))
        {
            scene.Diagnostics.Add($"Cabinet manifest could not be read: {manifestPath}");
            return;
        }
        var modelPath = ResolvePackagePath(manifestPath, cabinet.Model.Path);
        var load = await _loader.LoadAsync(modelPath, token).ConfigureAwait(false);
        if (!load.Succeeded)
        {
            scene.Diagnostics.Add($"Cabinet model cannot be loaded: {load.ErrorMessage}");
            return;
        }
        scene.AddDiagnostics(load.Diagnostics);
        var intrinsic = Portable3DTransform.CreateIntrinsic(cabinet.Model);
        scene.AddScene(load, "cabinet", "cabinet", intrinsic, modelPath);

        if (machine is null) return;
        var assignments = machine.SurfaceAssignments ?? [];
        foreach (var target in load.FaceTargets.Where(target => target.IsValid))
        {
            token.ThrowIfCancellationRequested();
            var assignment = assignments.FirstOrDefault(item => string.Equals(item.TargetId, target.Id, StringComparison.Ordinal));
            if (assignment is null) continue;
            var settings = cabinet.GetSurfaceTargetSettings(target.Id);
            if (PortableFacePlaneGeometryBuilder.TryCreate(target, settings, assignment.FaceAssetPath, intrinsic, out var plane, "cabinet"))
            {
                scene.FacePlanes.Add(plane);
                scene.Include(plane.Positions);
            }
            else
                scene.Diagnostics.Add($"Cabinet Face target '{target.Id}' has no valid quad geometry for its assignment.");
        }
        foreach (var assignment in assignments)
            if (!load.FaceTargets.Any(target => target.IsValid && string.Equals(target.Id, assignment.TargetId, StringComparison.Ordinal)))
                scene.Diagnostics.Add($"Machine Face assignment target '{assignment.TargetId}' was not found in the Cabinet.");
    }

    private async Task AppendPrefabAsync(CompositionBuilder scene, Object3DDocument definition, string packageRoot,
        string ownerId, Matrix4x4 ownerTransform, CancellationToken token)
    {
        var nodePath = new List<Object3DNodeDefinition>();
        async Task Visit(Object3DNodeDefinition node, Matrix4x4 parentTransform)
        {
            token.ThrowIfCancellationRequested();
            nodePath.Add(node);
            var nodeToRoot = Portable3DTransform.CreateNodeTransform(node) * parentTransform;
            var model = node.Components.FirstOrDefault(component => component.Kind == Object3DComponentDefinition.ModelKind)?.Model;
            var collider = node.Components.FirstOrDefault(component => component.Kind == Object3DComponentDefinition.ColliderKind)?.Collider;
            if (model is not null)
            {
                var modelPath = ResolvePackagePath(packageRoot, model.Path);
                var load = await scene.LoadSceneAsync(modelPath, token).ConfigureAwait(false);
                if (load.Succeeded)
                {
                    scene.AddDiagnostics(load.Diagnostics);
                    scene.AddScene(load, ownerId, $"{string.Join('/', nodePath.Select(part => part.Id))}",
                        Portable3DTransform.CreateIntrinsic(model) * nodeToRoot * ownerTransform, modelPath);
                }
                else scene.Diagnostics.Add($"Node '{node.Name}': {load.ErrorMessage}");
            }
            if (collider is { Kind: not Object3DColliderKind.None })
                AddCollider(scene, ownerId, nodePath, nodeToRoot * ownerTransform, model, collider, definition.DisplayName);

            foreach (var child in node.Children ?? []) await Visit(child, nodeToRoot).ConfigureAwait(false);
            nodePath.RemoveAt(nodePath.Count - 1);
        }
        await Visit(definition.Root, Matrix4x4.Identity).ConfigureAwait(false);
    }

    private Matrix4x4 MachineInstanceTransform(MachineObject3DInstance instance,
        IReadOnlyDictionary<string, MachineObject3DInstance> instances,
        Dictionary<AssetReference, (Object3DDocument Definition, string Manifest, string PackageRoot)> definitions,
        EditorProject project, string libraryRoot, CompositionBuilder scene, CancellationToken token)
    {
        var transform = Portable3DTransform.CreatePlacement(instance.Transform);
        var visited = new HashSet<string>(StringComparer.Ordinal) { instance.Id };
        var current = instance;
        while (current.ParentId is { } parentId)
        {
            token.ThrowIfCancellationRequested();
            if (!instances.TryGetValue(parentId, out var parent))
            {
                scene.Diagnostics.Add($"{instance.Id}: parent instance '{parentId}' was not found.");
                break;
            }
            if (!visited.Add(parentId))
            {
                scene.Diagnostics.Add($"{instance.Id}: parent hierarchy contains a cycle at '{parentId}'.");
                break;
            }
            if (parent.ObjectAsset is { } parentAsset)
            {
                if (!definitions.TryGetValue(parentAsset, out var resolved))
                {
                    var manifest = ResolveAsset(project, libraryRoot, parentAsset);
                    var definition = Object3DDocumentStorage.TryRead(File.ReadAllText(manifest), out var parsed, out var error)
                        ? parsed : throw new InvalidDataException(error);
                    resolved = (definition, manifest, Path.GetDirectoryName(Path.GetFullPath(manifest))!);
                    definitions.Add(parentAsset, resolved);
                }
                transform *= Portable3DTransform.CreateNodeTransform(resolved.Definition.Root);
            }
            transform *= Portable3DTransform.CreatePlacement(parent.Transform);
            current = parent;
        }
        return transform;
    }

    private static bool IsActive(MachineObject3DInstance instance, IReadOnlyDictionary<string, MachineObject3DInstance> instances)
    {
        var visited = new HashSet<string>(StringComparer.Ordinal);
        for (var current = instance; current is not null && visited.Add(current.Id);
            current = current.ParentId is { } parentId && instances.TryGetValue(parentId, out var parent) ? parent : null)
            if (!current.Active) return false;
        return true;
    }

    private async Task<Object3DDocument> ReadPrefabAsync(string manifestPath, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (!Object3DDocumentStorage.TryRead(await File.ReadAllTextAsync(manifestPath, token).ConfigureAwait(false), out var definition, out var error))
        {
            throw new InvalidDataException(error);
        }
        return definition;
    }

    private string ResolveAsset(EditorProject project, string libraryRoot, AssetReference reference)
    {
        var path = _assetResolver.Resolve(project, libraryRoot, reference);
        var root = reference.Scope == AssetReferenceScope.Project ? project.ProjectDirectory : libraryRoot;
        return WorkspacePathPolicy.ResolveConfined(root, Path.GetRelativePath(root, path), requireExists: true);
    }

    private static string ResolvePackagePath(string manifestOrPackageRoot, string relativePath)
    {
        var packageRoot = Directory.Exists(manifestOrPackageRoot)
            ? Path.GetFullPath(manifestOrPackageRoot)
            : Path.GetDirectoryName(Path.GetFullPath(manifestOrPackageRoot))!;
        return WorkspacePathPolicy.ResolveConfined(packageRoot, relativePath, requireExists: true);
    }

    private static void AddCollider(CompositionBuilder scene, string ownerId, IReadOnlyList<Object3DNodeDefinition> path,
        Matrix4x4 nodeWorld, Object3DModelDefinition? model, Object3DColliderDefinition collider, string prefabName)
    {
        var node = path[^1];
        if (collider.Kind is Object3DColliderKind.Capsule or Object3DColliderKind.Mesh)
        {
            var kind = collider.Kind.ToString().ToLowerInvariant();
            scene.Diagnostics.Add($"Node '{node.Name}': {kind} collider preview is unavailable; no approximate shape is shown.");
            return;
        }
        var mesh = collider.Kind switch
        {
            Object3DColliderKind.Box => CreateBox(collider),
            Object3DColliderKind.Sphere => CreateSphere(collider),
            _ => null
        };
        if (mesh is null) { scene.Diagnostics.Add($"Node {node.Name}: {collider.Kind} collider preview has invalid dimensions."); return; }
        var colliderWorld = collider.SourceFrame == Object3DColliderSourceFrame.ModelLocal && model is not null
            ? Portable3DTransform.CreateIntrinsic(model) * nodeWorld
            : nodeWorld;
        var semantic = collider.IsTrigger ? CabinetSemanticGeometryKind.Trigger : CabinetSemanticGeometryKind.Collider;
        scene.Meshes.Add(new Portable3DMeshInstance(ownerId, string.Join('/', path.Select(part => part.Id)) + "/Collider",
            $"{prefabName}/{string.Join('/', path.Select(part => part.Name))}/Collider", colliderWorld, semantic,
            collider.IsTrigger ? "PrefabTrigger" : "PrefabCollider", [mesh]));
        scene.Include(mesh.Positions, mesh.TriangleIndices, colliderWorld);
    }

    private static GlbPrimitiveData? CreateBox(Object3DColliderDefinition collider)
    {
        var center = ToVector(collider.Center, Vector3.Zero);
        var size = ToVector(collider.Size, Vector3.One);
        if (!Finite(center) || !Finite(size) || size.X <= 0 || size.Y <= 0 || size.Z <= 0) return null;
        var half = size * .5f;
        var positions = new[]
        {
            center + new Vector3(-half.X,-half.Y,-half.Z), center + new Vector3(half.X,-half.Y,-half.Z),
            center + new Vector3(half.X,half.Y,-half.Z), center + new Vector3(-half.X,half.Y,-half.Z),
            center + new Vector3(-half.X,-half.Y,half.Z), center + new Vector3(half.X,-half.Y,half.Z),
            center + new Vector3(half.X,half.Y,half.Z), center + new Vector3(-half.X,half.Y,half.Z)
        };
        int[] indices = [0,2,1,0,3,2,4,5,6,4,6,7,0,1,5,0,5,4,3,7,6,3,6,2,0,4,7,0,7,3,1,2,6,1,6,5];
        return new GlbPrimitiveData(positions, [], [], indices, -1);
    }

    private static GlbPrimitiveData? CreateSphere(Object3DColliderDefinition collider)
    {
        var center = ToVector(collider.Center, Vector3.Zero);
        var radius = (float)(collider.Radius ?? .5);
        if (!Finite(center) || !float.IsFinite(radius) || radius <= 0) return null;
        const int latitudeSteps = 12;
        const int longitudeSteps = 20;
        var positions = new List<Vector3>();
        for (var latitude = 0; latitude <= latitudeSteps; latitude++)
        {
            var phi = MathF.PI * latitude / latitudeSteps;
            var y = MathF.Cos(phi);
            var ringRadius = MathF.Sin(phi);
            for (var longitude = 0; longitude <= longitudeSteps; longitude++)
            {
                var theta = 2 * MathF.PI * longitude / longitudeSteps;
                positions.Add(center + radius * new Vector3(ringRadius * MathF.Cos(theta), y, ringRadius * MathF.Sin(theta)));
            }
        }
        var indices = new List<int>();
        for (var latitude = 0; latitude < latitudeSteps; latitude++)
            for (var longitude = 0; longitude < longitudeSteps; longitude++)
            {
                var a = latitude * (longitudeSteps + 1) + longitude;
                var b = a + longitudeSteps + 1;
                indices.AddRange([a, b, a + 1, a + 1, b, b + 1]);
            }
        return new GlbPrimitiveData(positions.ToArray(), [], [], indices.ToArray(), -1);
    }

    private static Vector3 ToVector(double[]? value, Vector3 fallback) => value is { Length: 3 }
        ? new Vector3((float)value[0], (float)value[1], (float)value[2]) : fallback;
    private static bool Finite(Vector3 value) => float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
    private static bool IsContentError(Exception ex) => ex is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException or JsonException;
}

public sealed record Portable3DMeshInstance(
    string OwnerId,
    string NodeId,
    string NodePath,
    Matrix4x4 WorldTransform,
    CabinetSemanticGeometryKind SemanticKind,
    string? SemanticId,
    IReadOnlyList<GlbPrimitiveData> Primitives);

public sealed record Portable3DAnchorPose(string Id, string DisplayName, Vector3 Position, MachineVector3 Rotation, Matrix4x4 WorldTransform);

public sealed record Portable3DCompositionResult(
    IReadOnlyList<Portable3DMeshInstance> Meshes,
    IReadOnlyList<GlbMaterialData> Materials,
    IReadOnlyList<PortableFacePlaneGeometry> FacePlanes,
    IReadOnlyList<Portable3DAnchorPose> Anchors,
    BoundingBox3 Bounds,
    IReadOnlyList<string> Diagnostics)
{
    public IReadOnlyList<PortableFacePlaneGeometry> CabinetFaceBindings => FacePlanes;
}

internal sealed class CompositionBuilder(PortableGlbSceneLoader loader)
{
    private readonly Dictionary<string, GlbSceneLoadResult> _sceneCache = new(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
    private readonly Dictionary<(string Path, int Material), int> _materialMap = new();
    public List<Portable3DMeshInstance> Meshes { get; } = [];
    public List<GlbMaterialData> Materials { get; } = [];
    public List<PortableFacePlaneGeometry> FacePlanes { get; } = [];
    public List<Portable3DAnchorPose> Anchors { get; } = [];
    public List<string> Diagnostics { get; } = [];
    private BoundingBox3 _bounds = BoundingBox3.Empty;

    public async Task<GlbSceneLoadResult> LoadSceneAsync(string path, CancellationToken token)
    {
        var fullPath = Path.GetFullPath(path);
        if (_sceneCache.TryGetValue(fullPath, out var cached)) return cached;
        var loaded = await loader.LoadAsync(fullPath, token).ConfigureAwait(false);
        _sceneCache.Add(fullPath, loaded);
        return loaded;
    }

    public void AddScene(GlbSceneLoadResult scene, string ownerId, string nodeIdPrefix, Matrix4x4 parentTransform, string sourcePath)
    {
        if (!scene.Succeeded) { Diagnostics.Add(scene.ErrorMessage ?? "GLB scene could not be loaded."); return; }
        var fullPath = Path.GetFullPath(sourcePath);
        var remap = new Dictionary<int, int>();
        foreach (var material in scene.Materials)
        {
            var key = (fullPath, material.MaterialIndex);
            if (!_materialMap.TryGetValue(key, out var index))
            {
                index = Materials.Count;
                _materialMap.Add(key, index);
                Materials.Add(material with { MaterialIndex = index, Name = $"{Path.GetFileName(fullPath)} / {material.Name}" });
            }
            remap[material.MaterialIndex] = index;
        }
        foreach (var mesh in scene.Meshes)
        {
            var world = mesh.WorldTransform * parentTransform;
            var primitives = mesh.Primitives.Select(primitive => primitive with
            {
                MaterialIndex = remap.TryGetValue(primitive.MaterialIndex, out var index) ? index : 0
            }).ToArray();
            var composed = new Portable3DMeshInstance(ownerId, $"{nodeIdPrefix}/{mesh.NodeIndex}", mesh.NodePath,
                world, mesh.SemanticKind, mesh.SemanticId, primitives);
            Meshes.Add(composed);
            foreach (var primitive in primitives) Include(primitive.Positions, primitive.TriangleIndices, world);
        }
    }

    public void Include(Vector3[] positions, int[] indices, Matrix4x4 transform)
    {
        foreach (var index in indices)
            if ((uint)index < (uint)positions.Length) Include(Vector3.Transform(positions[index], transform));
    }
    public void Include(Vector3 position) => _bounds = _bounds.Include(position);
    public void Include(IEnumerable<Vector3> positions) { foreach (var position in positions) Include(position); }
    public void AddDiagnostics(IEnumerable<string> diagnostics) => Diagnostics.AddRange(diagnostics);
    public Portable3DCompositionResult Build() => new(Meshes.ToArray(), Materials.ToArray(), FacePlanes.ToArray(), Anchors.ToArray(), _bounds,
        Diagnostics.Distinct(StringComparer.Ordinal).ToArray());
}
