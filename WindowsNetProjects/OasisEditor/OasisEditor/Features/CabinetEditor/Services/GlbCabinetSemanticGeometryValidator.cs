using System.IO;
using SharpGLTF.Schema2;

namespace OasisEditor.Features.CabinetEditor.Services;

/// <summary>Validates the geometry contract for physics semantics authored in a Cabinet GLB.</summary>
public sealed class GlbCabinetSemanticGeometryValidator
{
    public void Validate(string modelPath, string cabinetAssetPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(cabinetAssetPath);

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
        Validate(scene, cabinetAssetPath, cancellationToken);
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
