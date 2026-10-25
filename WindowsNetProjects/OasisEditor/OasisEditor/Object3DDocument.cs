using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using OasisEditor.Features.CabinetEditor.Models;

namespace OasisEditor;

public enum Object3DColliderKind { None, Sphere, Box, Capsule, Mesh }
public enum Object3DCapsuleAxis { X, Y, Z }
public enum Object3DColliderSourceFrame { ModelLocal, NodeLocal }
public enum Object3DRigidbodyMode { Static, Dynamic, Kinematic }

public sealed record Object3DModelDefinition(string Path, double Scale, string UpAxis);
public sealed record Object3DColliderDefinition(
    Object3DColliderKind Kind,
    double[]? Center = null,
    double? Radius = null,
    double[]? Size = null,
    double? Height = null,
    Object3DCapsuleAxis? Axis = null,
    Object3DColliderSourceFrame SourceFrame = Object3DColliderSourceFrame.ModelLocal,
    bool IsTrigger = false)
{
    public static Object3DColliderDefinition None => new(Object3DColliderKind.None);
}

[method: JsonConstructor]
public sealed record Object3DRigidbodyDefinition(Object3DRigidbodyMode Mode, double? Mass = null, bool? UseGravity = false)
{
    // Source compatibility for code-authored documents. This is not a legacy JSON reader.
    public Object3DRigidbodyDefinition(bool enabled, double? mass = null, bool? useGravity = null)
        : this(enabled ? Object3DRigidbodyMode.Dynamic : Object3DRigidbodyMode.Static, mass, useGravity) { }

    [JsonIgnore]
    public bool Enabled
    {
        get => Mode != Object3DRigidbodyMode.Static;
        init => Mode = value ? Object3DRigidbodyMode.Dynamic : Object3DRigidbodyMode.Static;
    }
}

public sealed record Object3DPhysicsDefinition(Object3DColliderDefinition Collider, Object3DRigidbodyDefinition Rigidbody);

/// <summary>A versioned, typed capability entry stored on an authored Object3D node.</summary>
public sealed record Object3DComponentDefinition(
    string ComponentId,
    string Kind,
    int ContractVersion,
    MachineObjectTransform? Transform = null,
    Object3DModelDefinition? Model = null,
    Object3DColliderDefinition? Collider = null,
    Object3DRigidbodyDefinition? Rigidbody = null)
{
    public const int CurrentContractVersion = 1;
    public const string TransformKind = "Transform";
    public const string ModelKind = "Model";
    public const string ColliderKind = "Collider";
    public const string RigidbodyKind = "Rigidbody";

    public static Object3DComponentDefinition CreateTransform(MachineObjectTransform? value = null) => new(NewId(), TransformKind, CurrentContractVersion, Transform: value ?? MachineObjectTransform.Identity);
    public static Object3DComponentDefinition CreateModel(Object3DModelDefinition value) => new(NewId(), ModelKind, CurrentContractVersion, Model: value);
    public static Object3DComponentDefinition CreateCollider(Object3DColliderDefinition value) => new(NewId(), ColliderKind, CurrentContractVersion, Collider: value);
    public static Object3DComponentDefinition CreateRigidbody(Object3DRigidbodyDefinition value) => new(NewId(), RigidbodyKind, CurrentContractVersion, Rigidbody: value);

    private static string NewId() => Guid.NewGuid().ToString("D");
}

/// <summary>An inline node in the reusable Object3D (Prefab) hierarchy.</summary>
public sealed record Object3DNodeDefinition(
    string Id,
    string Name,
    Object3DComponentDefinition[] Components,
    Object3DNodeDefinition[] Children)
{
    [JsonIgnore]
    public MachineObjectTransform Transform
    {
        get => Components.SingleOrDefault(component => component.Kind == Object3DComponentDefinition.TransformKind)?.Transform
            ?? MachineObjectTransform.Identity;
        init => Components = SetComponent(Components, Object3DComponentDefinition.CreateTransform(value));
    }

    public Object3DNodeDefinition WithComponent(Object3DComponentDefinition component)
    {
        ArgumentNullException.ThrowIfNull(component);
        return this with { Components = SetComponent(Components, component) };
    }

    public Object3DNodeDefinition WithoutComponent(string kind) => this with
    {
        Components = Components.Where(component => !string.Equals(component.Kind, kind, StringComparison.Ordinal)).ToArray()
    };

    public Object3DNodeDefinition WithChildren(IEnumerable<Object3DNodeDefinition> children) => this with { Children = children.ToArray() };

    private static Object3DComponentDefinition[] SetComponent(Object3DComponentDefinition[]? components, Object3DComponentDefinition replacement)
    {
        var current = components ?? [];
        var index = Array.FindIndex(current, component => component.Kind == replacement.Kind);
        if (index < 0) return [.. current, replacement];
        var result = current.ToArray();
        result[index] = replacement with { ComponentId = current[index].ComponentId };
        return result;
    }
}

/// <summary>A reusable authored physical 3D object definition, now with an inline node/component tree.</summary>
[method: JsonConstructor]
public sealed record Object3DDocument(
    int SchemaVersion,
    string Id,
    string DisplayName,
    Object3DNodeDefinition Root)
{
    public const int CurrentSchemaVersion = 2;

    // Explicit source reauthoring API. It creates a current-format document and is not used by the JSON reader.
    public Object3DDocument(int schemaVersion, string id, string displayName, Object3DModelDefinition model, Object3DPhysicsDefinition physics)
        : this(schemaVersion, id, displayName, CreateRoot(model, physics)) { }

    [JsonIgnore]
    public Object3DModelDefinition Model
    {
        get => Root.Components.SingleOrDefault(component => component.Kind == Object3DComponentDefinition.ModelKind)?.Model
            ?? new Object3DModelDefinition("model.glb", 1, "Y");
        init => Root = Root.WithComponent(Object3DComponentDefinition.CreateModel(value));
    }

    [JsonIgnore]
    public Object3DPhysicsDefinition Physics
    {
        get => new(
            Root.Components.SingleOrDefault(component => component.Kind == Object3DComponentDefinition.ColliderKind)?.Collider ?? Object3DColliderDefinition.None,
            Root.Components.SingleOrDefault(component => component.Kind == Object3DComponentDefinition.RigidbodyKind)?.Rigidbody ?? new(Object3DRigidbodyMode.Static));
        init => Root = SetPhysics(Root, value);
    }

    public static Object3DDocument Create(string displayName) => ReauthorRoot(
        displayName,
        new Object3DModelDefinition("model.glb", 1, "Y"),
        new Object3DPhysicsDefinition(Object3DColliderDefinition.None, new(Object3DRigidbodyMode.Static)));

    public static Object3DDocument ReauthorRoot(string displayName, Object3DModelDefinition model, Object3DPhysicsDefinition physics, string? id = null) =>
        new(CurrentSchemaVersion, id ?? Guid.NewGuid().ToString("D"), displayName.Trim(), CreateRoot(model, physics));

    public Object3DDocument WithRoot(Object3DNodeDefinition root) => this with { Root = root };

    private static Object3DNodeDefinition CreateRoot(Object3DModelDefinition model, Object3DPhysicsDefinition physics) => new(
        Guid.NewGuid().ToString("D"), "Root",
        [
            Object3DComponentDefinition.CreateTransform(),
            Object3DComponentDefinition.CreateModel(model),
            Object3DComponentDefinition.CreateCollider(physics.Collider),
            Object3DComponentDefinition.CreateRigidbody(physics.Rigidbody)
        ], []);

    private static Object3DNodeDefinition SetPhysics(Object3DNodeDefinition root, Object3DPhysicsDefinition physics) => root
        .WithComponent(Object3DComponentDefinition.CreateCollider(physics.Collider))
        .WithComponent(Object3DComponentDefinition.CreateRigidbody(physics.Rigidbody));
}

public static class Object3DValidationService
{
    public static void Validate(Object3DDocument document, string? packageDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.SchemaVersion != Object3DDocument.CurrentSchemaVersion) throw new InvalidOperationException($"Unsupported Object3D schema version. This editor supports only version {Object3DDocument.CurrentSchemaVersion}.");
        if (!Guid.TryParse(document.Id, out _)) throw new InvalidOperationException("Object3D ID must be a stable GUID.");
        if (string.IsNullOrWhiteSpace(document.DisplayName)) throw new InvalidOperationException("Object3D display name is required.");
        if (document.Root is null) throw new InvalidOperationException("Object3D node 'Root' is required.");

        var nodeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var componentIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var activeNodes = new HashSet<Object3DNodeDefinition>(ReferenceEqualityComparer.Instance);
        var visitedNodes = new HashSet<Object3DNodeDefinition>(ReferenceEqualityComparer.Instance);
        ValidateNode(document, document.Root, "root", nodeIds, componentIds, activeNodes, visitedNodes);
        if (document.Root.Components.Count(component => component.Kind == Object3DComponentDefinition.ModelKind) > 1) throw NodeError(document, document.Root, "duplicate Model components are not supported.");
        ValidatePackageModel(document, packageDirectory);
    }

    private static void ValidateNode(Object3DDocument document, Object3DNodeDefinition? node, string path,
        HashSet<string> nodeIds, HashSet<string> componentIds, HashSet<Object3DNodeDefinition> activeNodes, HashSet<Object3DNodeDefinition> visitedNodes,
        bool hasScaledAncestor = false, int depth = 0)
    {
        if (node is null) throw new InvalidOperationException($"Object3D '{document.DisplayName}' {path}: node is null.");
        var context = $"Object3D '{document.DisplayName}' node '{node.Name}' ({path})";
        if (depth > 60) throw new InvalidOperationException($"{context}: hierarchy exceeds the supported depth of 60.");
        if (!activeNodes.Add(node)) throw new InvalidOperationException($"{context}: hierarchy contains a cycle.");
        if (!visitedNodes.Add(node)) throw new InvalidOperationException($"{context}: node is referenced more than once; hierarchy must be a tree.");
        if (!Guid.TryParse(node.Id, out _) || !nodeIds.Add(node.Id)) throw new InvalidOperationException($"{context}: node ID must be a unique stable GUID.");
        if (string.IsNullOrWhiteSpace(node.Name)) throw new InvalidOperationException($"{context}: node name is required.");
        if (node.Components is null || node.Children is null) throw new InvalidOperationException($"{context}: components and children arrays are required.");

        var kinds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var component in node.Components)
        {
            if (component is null) throw new InvalidOperationException($"{context}: component entry is null.");
            if (!Guid.TryParse(component.ComponentId, out _) || !componentIds.Add(component.ComponentId)) throw new InvalidOperationException($"{context}: component ID must be a unique stable GUID.");
            if (!kinds.Add(component.Kind)) throw new InvalidOperationException($"{context}: duplicate {component.Kind} component.");
            if (component.ContractVersion != Object3DComponentDefinition.CurrentContractVersion) throw new InvalidOperationException($"{context} component '{component.Kind}' ({component.ComponentId}): unsupported contract version {component.ContractVersion}.");
            var payloadCount = (component.Transform is null ? 0 : 1) + (component.Model is null ? 0 : 1) + (component.Collider is null ? 0 : 1) + (component.Rigidbody is null ? 0 : 1);
            var expectedPayload = component.Kind switch
            {
                Object3DComponentDefinition.TransformKind => component.Transform is not null,
                Object3DComponentDefinition.ModelKind => component.Model is not null,
                Object3DComponentDefinition.ColliderKind => component.Collider is not null,
                Object3DComponentDefinition.RigidbodyKind => component.Rigidbody is not null,
                _ => throw new InvalidOperationException($"{context} component '{component.Kind}' ({component.ComponentId}): unknown component kind.")
            };
            if (!expectedPayload || payloadCount != 1) throw new InvalidOperationException($"{context} component '{component.Kind}' ({component.ComponentId}): payload must match its component kind exactly.");
            ValidateComponent(document, node, component);
        }
        if (!kinds.Contains(Object3DComponentDefinition.TransformKind)) throw new InvalidOperationException($"{context}: exactly one Transform component is required.");
        ValidateTransform(document, node, context);
        if (node.Children.Any(child => child is null)) throw new InvalidOperationException($"{context}: child node entry is null.");

        var hasModel = node.Components.Any(component => component.Kind == Object3DComponentDefinition.ModelKind);
        var collider = node.Components.SingleOrDefault(component => component.Kind == Object3DComponentDefinition.ColliderKind)?.Collider;
        if (!hasModel && collider is { Kind: not Object3DColliderKind.None, SourceFrame: not Object3DColliderSourceFrame.NodeLocal })
            throw new InvalidOperationException($"{context} Collider: source frame must be NodeLocal when the node has no Model component.");
        if (!hasModel && collider?.Kind == Object3DColliderKind.Mesh)
            throw new InvalidOperationException($"{context}: Mesh collider requires a Model component.");
        if (hasScaledAncestor && node.Components.Any(c => c.Rigidbody is { Mode: not Object3DRigidbodyMode.Static }))
            throw new InvalidOperationException($"{context}: active Rigidbody beneath a scaled parent is unsupported.");

        foreach (var child in node.Children) ValidateNode(document, child, $"{path}/{child.Name}", nodeIds, componentIds, activeNodes, visitedNodes,
            hasScaledAncestor || node.Transform.Scale != MachineVector3.One, depth + 1);
        var subtreeBodies = node.Components.Count(component => component.Rigidbody is { Mode: not Object3DRigidbodyMode.Static }) + node.Children.Sum(CountBodies);
        if (subtreeBodies > 1) throw new InvalidOperationException($"{context}: only one Rigidbody may own a node subtree.");
        activeNodes.Remove(node);
    }

    private static int CountBodies(Object3DNodeDefinition node) => (node.Components?.Count(component => component?.Rigidbody is { Mode: not Object3DRigidbodyMode.Static }) ?? 0) + (node.Children?.Where(child => child is not null).Sum(CountBodies) ?? 0);

    private static void ValidateTransform(Object3DDocument document, Object3DNodeDefinition node, string context)
    {
        var transform = node.Components?.SingleOrDefault(component => component?.Kind == Object3DComponentDefinition.TransformKind)?.Transform;
        if (transform is null) return; // precise missing-component diagnostic is emitted by ValidateNode.
        ValidateVector(transform.Position, $"{context} Transform position");
        ValidateVector(transform.Rotation, $"{context} Transform rotation");
        ValidateVector(transform.Scale, $"{context} Transform scale");
        if (transform.Scale.X <= 0 || transform.Scale.Y <= 0 || transform.Scale.Z <= 0) throw new InvalidOperationException($"{context} Transform scale values must be positive.");
    }

    private static void ValidateComponent(Object3DDocument document, Object3DNodeDefinition node, Object3DComponentDefinition component)
    {
        var context = $"Object3D '{document.DisplayName}' node '{node.Name}' component '{component.Kind}' ({component.ComponentId})";
        if (component.Model is { } model)
        {
            if (!CabinetDocumentStorage.IsSafePackageRelativePath(model.Path)) throw new InvalidOperationException($"{context}: model path must be a safe package-relative path.");
            if (!string.Equals(Path.GetExtension(model.Path), ".glb", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException($"{context}: model path must identify a GLB file.");
            if (!FinitePositive(model.Scale)) throw new InvalidOperationException($"{context}: intrinsic model scale must be positive and finite.");
            if (model.UpAxis is not ("X" or "Y" or "Z")) throw new InvalidOperationException($"{context}: model up-axis must be X, Y, or Z.");
        }
        if (component.Collider is { } collider) ValidateCollider(context, collider);
        if (component.Rigidbody is { } rigidbody)
        {
            if (!Enum.IsDefined(rigidbody.Mode)) throw new InvalidOperationException($"{context}: Rigidbody mode is unsupported.");
            if (rigidbody.Mode == Object3DRigidbodyMode.Dynamic && !FinitePositive(rigidbody.Mass)) throw new InvalidOperationException($"{context}: dynamic Rigidbody mass must be positive and finite.");
            if (rigidbody.Mode != Object3DRigidbodyMode.Dynamic && rigidbody.Mass is not null) throw new InvalidOperationException($"{context}: only a dynamic Rigidbody may specify mass.");
        }
    }

    private static void ValidateCollider(string context, Object3DColliderDefinition collider)
    {
        if (!Enum.IsDefined(collider.Kind)) throw new InvalidOperationException($"{context}: collider kind is unsupported.");
        if (!Enum.IsDefined(collider.SourceFrame)) throw new InvalidOperationException($"{context}: collider source frame is unsupported.");
        if (collider.Kind == Object3DColliderKind.None && collider.IsTrigger) throw new InvalidOperationException($"{context}: None collider cannot be a trigger.");
        if (collider.Kind is Object3DColliderKind.Sphere or Object3DColliderKind.Box or Object3DColliderKind.Capsule) ValidateVector(collider.Center, $"{context} collider center");
        if (collider.Kind == Object3DColliderKind.Sphere && !FinitePositive(collider.Radius)) throw new InvalidOperationException($"{context}: sphere radius must be positive and finite.");
        if (collider.Kind == Object3DColliderKind.Box)
        {
            ValidateVector(collider.Size, $"{context} Box size");
            if (collider.Size!.Any(value => value <= 0)) throw new InvalidOperationException($"{context}: Box dimensions must be positive.");
        }
        if (collider.Kind == Object3DColliderKind.Capsule)
        {
            if (!FinitePositive(collider.Radius) || !FinitePositive(collider.Height)) throw new InvalidOperationException($"{context}: Capsule radius and height must be positive and finite.");
            if (collider.Height < collider.Radius * 2) throw new InvalidOperationException($"{context}: Capsule height must be at least twice its radius.");
            if (collider.Axis is null || !Enum.IsDefined(collider.Axis.Value)) throw new InvalidOperationException($"{context}: Capsule axis must be X, Y, or Z.");
        }
        if ((collider.Kind is Object3DColliderKind.None or Object3DColliderKind.Mesh) &&
            (collider.Center is not null || collider.Radius is not null || collider.Size is not null || collider.Height is not null || collider.Axis is not null))
            throw new InvalidOperationException($"{context}: {collider.Kind} collider cannot define primitive dimensions.");
        if (collider.Kind == Object3DColliderKind.None && collider.IsTrigger)
            throw new InvalidOperationException($"{context}: a None collider cannot be marked as a trigger.");
    }

    private static void ValidatePackageModel(Object3DDocument document, string? packageDirectory)
    {
        if (packageDirectory is null) return;
        foreach (var model in EnumerateModels(document.Root))
        {
            var root = Path.GetFullPath(packageDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var modelPath = Path.GetFullPath(Path.Combine(root, model.Path.Replace('/', Path.DirectorySeparatorChar)));
            if (!modelPath.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || !File.Exists(modelPath))
                throw new InvalidOperationException($"Object3D '{document.DisplayName}' model '{model.Path}' is missing or outside its package.");
        }
    }

    private static IEnumerable<Object3DModelDefinition> EnumerateModels(Object3DNodeDefinition node)
    {
        foreach (var component in node.Components)
            if (component?.Model is { } model) yield return model;
        foreach (var child in node.Children) foreach (var model in EnumerateModels(child)) yield return model;
    }

    private static InvalidOperationException NodeError(Object3DDocument document, Object3DNodeDefinition node, string message) => new($"Object3D '{document.DisplayName}' node '{node.Name}': {message}");
    private static bool FinitePositive(double? value) => value is > 0 && double.IsFinite(value.Value);
    private static void ValidateVector(MachineVector3? value, string name)
    {
        if (value is null || !double.IsFinite(value.X) || !double.IsFinite(value.Y) || !double.IsFinite(value.Z)) throw new InvalidOperationException($"{name} must contain three finite values.");
    }
    private static void ValidateVector(double[]? value, string name)
    {
        if (value is null || value.Length != 3 || value.Any(component => !double.IsFinite(component))) throw new InvalidOperationException($"{name} must contain three finite values.");
    }
}

public static class Object3DDocumentStorage
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter() }
    };

    public static string Serialize(Object3DDocument document)
    {
        Object3DValidationService.Validate(document);
        return JsonSerializer.Serialize(document, Options);
    }

    public static bool TryRead(string? json, out Object3DDocument document, out string error)
    {
        document = Object3DDocument.Create("Object3D");
        error = string.Empty;
        try
        {
            if (string.IsNullOrWhiteSpace(json)) throw new InvalidOperationException("Object3D document is empty.");
            document = JsonSerializer.Deserialize<Object3DDocument>(json, Options) ?? throw new InvalidOperationException("Object3D document is empty.");
            Object3DValidationService.Validate(document);
            return true;
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or ArgumentException)
        {
            error = $"Invalid Object3D document: {exception.Message}";
            return false;
        }
    }
}
