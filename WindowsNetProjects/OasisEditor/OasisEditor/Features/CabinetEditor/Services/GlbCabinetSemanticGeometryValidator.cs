using System.IO;
using System.Text;
using System.Text.Json;
using SharpGLTF.Schema2;

namespace OasisEditor.Features.CabinetEditor.Services;

/// <summary>Validates the geometry contract for physics semantics authored in a Cabinet GLB.</summary>
public sealed class GlbCabinetSemanticGeometryValidator
{
    public void Validate(string modelPath, string cabinetAssetPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(cabinetAssetPath);

        // SharpGLTF rejects some malformed primitive declarations before it exposes their
        // node identity. Inspect only the semantic declarations first so diagnostics can
        // still name the authored object; typed accessor/index validation remains below.
        ValidateDeclarations(modelPath, cabinetAssetPath, cancellationToken);

        ModelRoot model;
        try
        {
            model = ModelRoot.Load(modelPath);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new InvalidDataException($"Cabinet asset '{cabinetAssetPath}' GLB could not be read for semantic geometry validation: {exception.Message}", exception);
        }

        var scene = model.DefaultScene ?? model.LogicalScenes.FirstOrDefault();
        if (scene is null) return;
        var inventory = CabinetTriggerInventory.Discover(scene);
        var invalid = inventory.Triggers.FirstOrDefault(trigger => !trigger.IsValid);
        if (invalid is not null) throw new InvalidDataException($"Cabinet asset '{cabinetAssetPath}', semantic Trigger '{invalid.SourceName}': {invalid.Diagnostic}");
        Validate(scene, cabinetAssetPath, cancellationToken);
    }

    private static void ValidateDeclarations(string modelPath, string cabinetAssetPath, CancellationToken cancellationToken)
    {
        using var document = ReadGlbJson(modelPath);
        var root = document.RootElement;
        if (!root.TryGetProperty("nodes", out var nodes) || nodes.ValueKind != JsonValueKind.Array) return;
        if (!TryGetDefaultScene(root, out var scene)
            || !scene.TryGetProperty("nodes", out var sceneNodes)
            || sceneNodes.ValueKind != JsonValueKind.Array) return;

        var visited = new HashSet<int>();
        foreach (var nodeIndex in sceneNodes.EnumerateArray())
        {
            if (nodeIndex.TryGetInt32(out var index)) ValidateDeclarationNode(root, nodes, index, cabinetAssetPath, visited, cancellationToken);
        }
    }

    private static void ValidateDeclarationNode(JsonElement root, JsonElement nodes, int nodeIndex, string cabinetAssetPath, ISet<int> visited, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!visited.Add(nodeIndex) || nodeIndex < 0 || nodeIndex >= nodes.GetArrayLength()) return;
        var node = nodes[nodeIndex];
        var nodeName = node.TryGetProperty("name", out var nodeNameElement) ? nodeNameElement.GetString() : null;
        var meshIndex = -1;
        var hasMeshIndex = node.TryGetProperty("mesh", out var meshIndexElement) && meshIndexElement.TryGetInt32(out meshIndex);
        var mesh = default(JsonElement);
        var hasMesh = hasMeshIndex && TryGetArrayItem(root, "meshes", meshIndex, out mesh);
        var meshName = hasMesh && mesh.TryGetProperty("name", out var meshNameElement) ? meshNameElement.GetString() : null;
        var kind = CabinetSemanticGeometry.Classify(nodeName, meshName);

        if (kind is CabinetSemanticGeometryKind.Collider or CabinetSemanticGeometryKind.Trigger)
        {
            var semanticName = CabinetSemanticGeometry.ClassifyName(nodeName) == kind ? nodeName! : meshName!;
            var context = $"Cabinet asset '{cabinetAssetPath}', semantic {kind} '{semanticName}'";
            if (!hasMesh) throw new InvalidDataException($"{context} does not reference a mesh or references one that cannot be resolved.");
            if (!mesh.TryGetProperty("primitives", out var primitives) || primitives.ValueKind != JsonValueKind.Array || primitives.GetArrayLength() == 0)
                throw new InvalidDataException($"{context} has no mesh primitives.");
            foreach (var primitive in primitives.EnumerateArray())
            {
                if (!primitive.TryGetProperty("attributes", out var attributes)
                    || attributes.ValueKind != JsonValueKind.Object
                    || !attributes.TryGetProperty("POSITION", out _))
                    throw new InvalidDataException($"{context} has a primitive without a POSITION accessor.");
            }
        }

        if (!node.TryGetProperty("children", out var children) || children.ValueKind != JsonValueKind.Array) return;
        foreach (var childIndex in children.EnumerateArray())
        {
            if (childIndex.TryGetInt32(out var index)) ValidateDeclarationNode(root, nodes, index, cabinetAssetPath, visited, cancellationToken);
        }
    }

    private static bool TryGetDefaultScene(JsonElement root, out JsonElement scene)
    {
        var sceneIndex = root.TryGetProperty("scene", out var sceneIndexElement) && sceneIndexElement.TryGetInt32(out var selected) ? selected : 0;
        return TryGetArrayItem(root, "scenes", sceneIndex, out scene);
    }

    private static bool TryGetArrayItem(JsonElement root, string propertyName, int index, out JsonElement item)
    {
        item = default;
        if (!root.TryGetProperty(propertyName, out var array)
            || array.ValueKind != JsonValueKind.Array
            || index < 0
            || index >= array.GetArrayLength()) return false;
        item = array[index];
        return true;
    }

    private static JsonDocument ReadGlbJson(string modelPath)
    {
        using var reader = new BinaryReader(File.OpenRead(modelPath));
        if (reader.ReadUInt32() != 0x46546C67) throw new InvalidDataException("Not a GLB file.");
        reader.ReadUInt32();
        reader.ReadUInt32();
        var jsonLength = reader.ReadInt32();
        if (reader.ReadUInt32() != 0x4E4F534A) throw new InvalidDataException("GLB JSON chunk missing.");
        return JsonDocument.Parse(Encoding.UTF8.GetString(reader.ReadBytes(jsonLength)));
    }

    internal static void Validate(Scene scene, string cabinetAssetPath, CancellationToken cancellationToken)
    {
        foreach (var node in scene.VisualChildren)
        {
            ValidateNode(node, cabinetAssetPath, cancellationToken);
        }
    }

    private static void ValidateNode(Node node, string cabinetAssetPath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var kind = CabinetSemanticGeometry.Classify(node.Name, node.Mesh?.Name);
        if (kind is CabinetSemanticGeometryKind.Collider or CabinetSemanticGeometryKind.Trigger)
        {
            var semanticName = CabinetSemanticGeometry.ClassifyName(node.Name) == kind ? node.Name! : node.Mesh?.Name ?? "<unnamed>";
            ValidatePhysicsGeometry(node, cabinetAssetPath, semanticName, kind);
        }

        foreach (var child in node.VisualChildren)
        {
            ValidateNode(child, cabinetAssetPath, cancellationToken);
        }
    }

    private static void ValidatePhysicsGeometry(Node node, string cabinetAssetPath, string semanticName, CabinetSemanticGeometryKind kind)
    {
        var context = $"Cabinet asset '{cabinetAssetPath}', semantic {kind} '{semanticName}'";
        if (node.Mesh is null) throw new InvalidDataException($"{context} does not reference a mesh.");
        if (node.Mesh.Primitives.Count == 0) throw new InvalidDataException($"{context} has no mesh primitives.");

        foreach (var primitive in node.Mesh.Primitives)
        {
            var positionAccessor = primitive.GetVertexAccessor("POSITION");
            if (positionAccessor is null) throw new InvalidDataException($"{context} has a primitive without a POSITION accessor.");

            try
            {
                var positions = positionAccessor.AsVector3Array();
                if (positions.Count == 0) throw new InvalidDataException("the POSITION accessor is empty");
                foreach (var (a, b, c) in primitive.GetTriangleIndices())
                {
                    ValidateIndex(a, positions.Count);
                    ValidateIndex(b, positions.Count);
                    ValidateIndex(c, positions.Count);
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                throw new InvalidDataException($"{context} has invalid POSITION/index geometry: {exception.Message}", exception);
            }
        }
    }

    private static void ValidateIndex(int index, int positionCount)
    {
        if (index < 0 || index >= positionCount)
        {
            throw new InvalidDataException($"triangle index {index} is outside its {positionCount} POSITION values");
        }
    }
}
