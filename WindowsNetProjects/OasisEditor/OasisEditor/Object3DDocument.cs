using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using OasisEditor.Features.CabinetEditor.Models;

namespace OasisEditor;

public enum Object3DColliderKind { None, Sphere, Box, Capsule, Mesh }
public enum Object3DCapsuleAxis { X, Y, Z }

public sealed record Object3DModelDefinition(string Path, double Scale, string UpAxis);
public sealed record Object3DColliderDefinition(
    Object3DColliderKind Kind,
    double[]? Center = null,
    double? Radius = null,
    double[]? Size = null,
    double? Height = null,
    Object3DCapsuleAxis? Axis = null)
{
    public static Object3DColliderDefinition None => new(Object3DColliderKind.None);
}
public sealed record Object3DRigidbodyDefinition(bool Enabled, double? Mass = null, bool? UseGravity = null);
public sealed record Object3DPhysicsDefinition(Object3DColliderDefinition Collider, Object3DRigidbodyDefinition Rigidbody);

public enum PrefabComponentKind { Transform, Model, Collider, Rigidbody }
public enum PrefabColliderSourceFrame { ModelLocal, NodeLocalSI }
public enum PrefabBodyMode { Static, Dynamic, Kinematic }

/// <summary>Closed, versioned capability entry; exactly one matching payload is permitted.</summary>
public sealed record PrefabComponent(
    string Id, PrefabComponentKind Kind, [property: JsonRequired] int ContractVersion = 1,
    MachineObjectTransform? Transform = null,
    Object3DModelDefinition? Model = null,
    Object3DColliderDefinition? Collider = null,
    PrefabColliderSourceFrame? SourceFrame = null,
    bool? IsTrigger = null,
    Object3DRigidbodyDefinition? Rigidbody = null,
    PrefabBodyMode? Mode = null)
{
    public static PrefabComponent ForTransform() => new(Guid.NewGuid().ToString("D"), PrefabComponentKind.Transform, Transform: MachineObjectTransform.Identity);
    public static PrefabComponent ForModel(Object3DModelDefinition model) => new(Guid.NewGuid().ToString("D"), PrefabComponentKind.Model, Model: model);
    public static PrefabComponent ForCollider(Object3DColliderDefinition collider, PrefabColliderSourceFrame frame) => new(Guid.NewGuid().ToString("D"), PrefabComponentKind.Collider, Collider: collider, SourceFrame: frame, IsTrigger: false);
    public static PrefabComponent ForBody(Object3DRigidbodyDefinition body) => new(Guid.NewGuid().ToString("D"), PrefabComponentKind.Rigidbody, Rigidbody: body, Mode: body.Enabled ? PrefabBodyMode.Dynamic : PrefabBodyMode.Static);
}

public sealed record PrefabNode(string Id, string Name, IReadOnlyList<PrefabComponent> Components, IReadOnlyList<PrefabNode> Children)
{
    public static PrefabNode Create(string name) => new(Guid.NewGuid().ToString("D"), name, [PrefabComponent.ForTransform()], []);
    public PrefabComponent? Component(PrefabComponentKind kind) => Components?.FirstOrDefault(c => c is not null && c.Kind == kind);
    public PrefabNode SetComponent(PrefabComponent component) => this with
    {
        Components = Component(component.Kind) is { } existing
            ? Components.Select(c => c.Kind == component.Kind ? component with { Id = existing.Id } : c).ToArray()
            : [.. Components, component]
    };
}

/// <summary>The evolved Object3D family: authored Prefab schema 2, runtime schema 1 until P2.</summary>
public sealed record Object3DDocument(int SchemaVersion, string Id, string DisplayName, PrefabNode Root)
{
    public const int CurrentSchemaVersion = 2;

    public static Object3DDocument Create(string displayName) => new(CurrentSchemaVersion,
        Guid.NewGuid().ToString("D"), displayName.Trim(), PrefabNode.Create(displayName.Trim())
            .SetComponent(PrefabComponent.ForModel(new("model.glb", 1, "Y"))));

    // In-memory conveniences for existing source consumers. Never serialized, never a legacy reader.
    [JsonIgnore]
    public Object3DModelDefinition Model
    {
        get => Root.Component(PrefabComponentKind.Model)?.Model ?? throw new InvalidOperationException($"Prefab '{DisplayName}', node '{Root.Id}', Model: component is absent.");
        init => Root = Root.SetComponent(PrefabComponent.ForModel(value));
    }
    [JsonIgnore]
    public Object3DPhysicsDefinition Physics
    {
        get => new(Root.Component(PrefabComponentKind.Collider)?.Collider ?? Object3DColliderDefinition.None,
            Root.Component(PrefabComponentKind.Rigidbody)?.Rigidbody ?? new(false));
        init => Root = Root.SetComponent(PrefabComponent.ForCollider(value.Collider, PrefabColliderSourceFrame.ModelLocal))
            .SetComponent(PrefabComponent.ForBody(value.Rigidbody));
    }
}

public static class Object3DValidationService
{
    public static void Validate(Object3DDocument document, string? packageDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.SchemaVersion != Object3DDocument.CurrentSchemaVersion) throw new InvalidOperationException($"Prefab '{document.Id}', schemaVersion: supports only version {Object3DDocument.CurrentSchemaVersion}; explicitly reauthor older packages.");
        if (!Guid.TryParse(document.Id, out _)) throw new InvalidOperationException("Prefab id must be a stable GUID.");
        if (string.IsNullOrWhiteSpace(document.DisplayName)) throw new InvalidOperationException($"Prefab '{document.Id}', displayName: required.");
        var ids = new HashSet<Guid>();
        var ancestors = new HashSet<PrefabNode>(ReferenceEqualityComparer.Instance);
        Visit(document.Root, false, $"Prefab '{document.Id}'", 0);

        void Visit(PrefabNode? node, bool ancestorBody, string path, int depth)
        {
            if (node is null) Fail(path, "node", "required");
            path += $", node '{node!.Id}' ({node.Name})";
            if (depth > 128) Fail(path, "children", "hierarchy exceeds 128 levels");
            if (!ancestors.Add(node)) Fail(path, "children", "hierarchy cycle");
            AddId(node.Id, path, "id");
            if (string.IsNullOrWhiteSpace(node.Name)) Fail(path, "name", "required");
            if (node.Components is null || node.Children is null) Fail(path, "components/children", "required");
            var kinds = new HashSet<PrefabComponentKind>();
            foreach (var component in node.Components!)
            {
                if (component is null) Fail(path, "component", "required");
                var cp = path + $", component '{component!.Id}' ({component.Kind})";
                AddId(component.Id, cp, "id");
                if (!Enum.IsDefined(component.Kind)) Fail(cp, "kind", "unsupported component");
                if (component.ContractVersion != 1) Fail(cp, "contractVersion", "supports only version 1");
                if (!kinds.Add(component.Kind)) Fail(cp, "kind", "at most one component of each kind per node");
                var payloads = new[] { component.Transform is not null, component.Model is not null, component.Collider is not null, component.Rigidbody is not null };
                if (payloads.Count(v => v) != 1 || !payloads[(int)component.Kind]) Fail(cp, "properties", "exactly one payload matching kind is required");
                if (component.Kind != PrefabComponentKind.Collider && (component.SourceFrame is not null || component.IsTrigger is not null)) Fail(cp, "sourceFrame/isTrigger", "only Collider supports these properties");
                if (component.Kind != PrefabComponentKind.Rigidbody && component.Mode is not null) Fail(cp, "mode", "only Rigidbody supports mode");
                switch (component.Kind)
                {
                    case PrefabComponentKind.Transform:
                        var t = component.Transform!;
                        Vector(t.Position, cp, "position"); Vector(t.Rotation, cp, "rotation"); Vector(t.Scale, cp, "scale");
                        if (t.Scale.X <= 0 || t.Scale.Y <= 0 || t.Scale.Z <= 0) Fail(cp, "scale", "all dimensions must be positive");
                        break;
                    case PrefabComponentKind.Model:
                        var model = component.Model!;
                        if (!CabinetDocumentStorage.IsSafePackageRelativePath(model.Path) || !string.Equals(Path.GetExtension(model.Path), ".glb", StringComparison.OrdinalIgnoreCase)) Fail(cp, "path", "must be a safe package-relative GLB path");
                        if (!FinitePositive(model.Scale)) Fail(cp, "scale", "must be positive and finite");
                        if (model.UpAxis is not ("X" or "Y" or "Z")) Fail(cp, "upAxis", "must be X, Y or Z");
                        if (packageDirectory is not null)
                        {
                            var modelPath = Path.Combine(packageDirectory, model.Path);
                            if (!File.Exists(modelPath)) Fail(cp, "path", $"model is missing: {model.Path}");
                            PrefabModelValidation.ValidateMeshSource(node, modelPath, document.Id);
                        }
                        break;
                    case PrefabComponentKind.Collider:
                        ValidateCollider(component, node, cp);
                        break;
                    case PrefabComponentKind.Rigidbody:
                        if (component.Mode is null || !Enum.IsDefined(component.Mode.Value)) Fail(cp, "mode", "must be Static, Dynamic or Kinematic");
                        var body = component.Rigidbody!;
                        if (body.Enabled != (component.Mode != PrefabBodyMode.Static)) Fail(cp, "enabled", "must agree with mode");
                        if (component.Mode != PrefabBodyMode.Static && (!FinitePositive(body.Mass) || body.UseGravity is null)) Fail(cp, "mass/useGravity", "moving bodies require positive finite mass and explicit gravity");
                        if (component.Mode == PrefabBodyMode.Static && (body.Mass is not null || body.UseGravity is not null)) Fail(cp, "mass/useGravity", "static bodies must omit motion settings");
                        if (component.Mode == PrefabBodyMode.Dynamic)
                        {
                            var parent = PrefabAuthoringService.Parent(document.Root, node.Id);
                            var parents = new HashSet<string>(StringComparer.Ordinal);
                            while (parent is not null && parents.Add(parent.Id))
                            {
                                if (parent.Component(PrefabComponentKind.Transform)?.Transform?.Scale is { } scale && scale != MachineVector3.One) Fail(cp, "mode", $"dynamic body beneath scaled parent '{parent.Id}' is unsupported");
                                parent = PrefabAuthoringService.Parent(document.Root, parent.Id);
                            }
                        }
                        if (ancestorBody) Fail(cp, "mode", "nested Rigidbody ownership is unsupported");
                        if (component.Mode == PrefabBodyMode.Dynamic && Descendants(node).Any(n => n.Component(PrefabComponentKind.Collider)?.Collider?.Kind == Object3DColliderKind.Mesh)) Fail(cp, "mode", "dynamic Mesh colliders are unsupported");
                        if (component.Mode == PrefabBodyMode.Dynamic && !Descendants(node).Any(n => n.Component(PrefabComponentKind.Collider) is { IsTrigger: false, Collider.Kind: Object3DColliderKind.Sphere or Object3DColliderKind.Box or Object3DColliderKind.Capsule })) Fail(cp, "mode", "dynamic body requires a non-trigger primitive collider in its owned subtree");
                        break;
                }
            }
            if (!kinds.Contains(PrefabComponentKind.Transform)) Fail(path, "Transform", "exactly one Transform is mandatory");
            foreach (var child in node.Children!) Visit(child, ancestorBody || kinds.Contains(PrefabComponentKind.Rigidbody), path, depth + 1);
            ancestors.Remove(node);
        }

        void AddId(string id, string path, string property)
        {
            if (!Guid.TryParse(id, out var guid) || !ids.Add(guid)) Fail(path, property, "must be a unique asset-local GUID (nodes and components share the ID domain)");
        }
    }

    public static IEnumerable<PrefabNode> Descendants(PrefabNode root)
    {
        // Iterative and reference-aware even for invalid trees, used during validation as well as projection.
        var seen = new HashSet<PrefabNode>(ReferenceEqualityComparer.Instance);
        var stack = new Stack<PrefabNode>(); stack.Push(root);
        while (stack.TryPop(out var node))
        {
            if (node is null || !seen.Add(node)) continue;
            yield return node;
            if (node.Children is not null) foreach (var child in node.Children.Reverse()) stack.Push(child);
        }
    }

    private static void ValidateCollider(PrefabComponent component, PrefabNode node, string path)
    {
        var c = component.Collider!;
        if (!Enum.IsDefined(c.Kind)) Fail(path, "kind", "unsupported collider kind");
        if (component.SourceFrame is null || !Enum.IsDefined(component.SourceFrame.Value)) Fail(path, "sourceFrame", "must explicitly specify ModelLocal or NodeLocalSI");
        if (component.IsTrigger is null) Fail(path, "isTrigger", "explicit role required");
        var hasModel = node.Component(PrefabComponentKind.Model) is not null;
        if (component.SourceFrame == PrefabColliderSourceFrame.ModelLocal && !hasModel) Fail(path, "sourceFrame", "ModelLocal requires Model on the same node; use NodeLocalSI for model-free nodes");
        if (c.Kind == Object3DColliderKind.None && component.IsTrigger == true) Fail(path, "isTrigger", "None cannot have a trigger role");
        if (c.Kind == Object3DColliderKind.Mesh && (!hasModel || component.SourceFrame != PrefabColliderSourceFrame.ModelLocal)) Fail(path, "sourceFrame", "Mesh requires a Model and ModelLocal frame");
        if (c.Kind is Object3DColliderKind.Sphere or Object3DColliderKind.Box or Object3DColliderKind.Capsule) ArrayVector(c.Center, path, "center");
        else if (c.Center is not null) Fail(path, "center", "unsupported for None/Mesh");
        if (c.Kind is Object3DColliderKind.Sphere or Object3DColliderKind.Capsule) { if (!FinitePositive(c.Radius)) Fail(path, "radius", "must be positive and finite"); }
        else if (c.Radius is not null) Fail(path, "radius", "only Sphere/Capsule supports radius");
        if (c.Kind == Object3DColliderKind.Box) { ArrayVector(c.Size, path, "size"); if (c.Size!.Any(v => v <= 0)) Fail(path, "size", "dimensions must be positive"); }
        else if (c.Size is not null) Fail(path, "size", "only Box supports size");
        if (c.Kind == Object3DColliderKind.Capsule)
        {
            if (!FinitePositive(c.Height) || c.Height < c.Radius * 2) Fail(path, "height", "must be finite and at least twice radius");
            if (c.Axis is null || !Enum.IsDefined(c.Axis.Value)) Fail(path, "axis", "must be X, Y or Z");
        }
        else if (c.Height is not null || c.Axis is not null) Fail(path, "height/axis", "only Capsule supports these properties");
    }
    private static bool FinitePositive(double? value) => value is > 0 && double.IsFinite(value.Value);
    private static void Vector(MachineVector3? v, string path, string property)
    {
        if (v is null || !double.IsFinite(v.X) || !double.IsFinite(v.Y) || !double.IsFinite(v.Z)) Fail(path, property, "must contain three finite values");
    }
    private static void ArrayVector(double[]? v, string path, string property)
    {
        if (v is null || v.Length != 3 || v.Any(x => !double.IsFinite(x))) Fail(path, property, "must contain three finite values");
    }
    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    private static void Fail(string path, string property, string message) => throw new InvalidOperationException($"{path}, {property}: {message}.");
}

/// <summary>Admission to the unchanged Player contract. Never silently flatten or drop authored data.</summary>
public static class PrefabRuntimeProjection
{
    public static void Validate(Object3DDocument document)
    {
        Object3DValidationService.Validate(document);
        var root = document.Root;
        var context = $"Prefab '{document.Id}' ({document.DisplayName}), node '{root.Id}'";
        if (root.Children.Count != 0) throw new InvalidOperationException($"{context}, children: runtime schema 1 cannot represent child nodes; P2 is required.");
        var transform = root.Component(PrefabComponentKind.Transform)!;
        if (transform.Transform != MachineObjectTransform.Identity) Reject(transform, "transform", "runtime schema 1 requires identity authored root transform, distinct from Machine placement");
        if (root.Component(PrefabComponentKind.Model) is null) throw new InvalidOperationException($"{context}, Model: runtime schema 1 requires a model.");
        if (root.Component(PrefabComponentKind.Collider) is { } collider)
        {
            if (collider.SourceFrame != PrefabColliderSourceFrame.ModelLocal) Reject(collider, "sourceFrame", "runtime schema 1 supports only ModelLocal");
            if (collider.IsTrigger == true) Reject(collider, "isTrigger", "runtime schema 1 has no trigger role");
        }
        if (root.Component(PrefabComponentKind.Rigidbody) is { Mode: PrefabBodyMode.Kinematic } body) Reject(body, "mode", "runtime schema 1 cannot represent Kinematic");
        void Reject(PrefabComponent component, string property, string reason) => throw new InvalidOperationException($"{context}, component '{component.Id}' ({component.Kind}), {property}: {reason}; P2 is required.");
    }
}

public static class Object3DDocumentStorage
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        MaxDepth = 512,
        RespectRequiredConstructorParameters = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) }
    };

    public static string Serialize(Object3DDocument document)
    {
        Object3DValidationService.Validate(document);
        return JsonSerializer.Serialize(document, Options);
    }

    public static bool TryRead(string? json, out Object3DDocument document, out string error)
    {
        document = Object3DDocument.Create("Prefab");
        error = string.Empty;
        try
        {
            if (string.IsNullOrWhiteSpace(json)) throw new InvalidOperationException("Prefab document is empty.");
            using var parsed = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 512 });
            if (parsed.RootElement.ValueKind != JsonValueKind.Object) throw new InvalidOperationException("Prefab manifest must be an object.");
            foreach (var property in parsed.RootElement.EnumerateObject())
                if (property.Name is not ("schemaVersion" or "id" or "displayName" or "root")) throw new InvalidOperationException($"Prefab manifest property '{property.Name}' is unsupported.");
            document = JsonSerializer.Deserialize<Object3DDocument>(json, Options) ?? throw new InvalidOperationException("Prefab document is empty.");
            Object3DValidationService.Validate(document);
            return true;
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException)
        {
            error = $"Invalid Prefab document: {exception.Message}";
            return false;
        }
    }
}
