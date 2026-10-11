using System.IO;
using SharpGLTF.Schema2;

namespace OasisEditor;

public static class PrefabModelValidation
{
    public static void ValidateMeshSource(PrefabNode node, string path, string assetId)
    {
        if (node.Component(PrefabComponentKind.Collider)?.Collider?.Kind != Object3DColliderKind.Mesh) return;
        var context = $"Prefab '{assetId}', node '{node.Id}', component '{node.Component(PrefabComponentKind.Collider)!.Id}', Collider.model";
        try
        {
            var model = ModelRoot.Load(path);
            var scene = model.DefaultScene ?? model.LogicalScenes.FirstOrDefault();
            var count = scene is null ? 0 : scene.VisualChildren.Sum(Count);
            if (count != 1) throw new InvalidOperationException($"{context}: Mesh requires exactly one usable mesh occurrence; found {count}.");
        }
        catch (InvalidOperationException) { throw; }
        catch (Exception ex)
        { throw new InvalidOperationException($"{context}: {ex.Message}", ex); }
    }
    private static int Count(Node node) => (node.Mesh is { } mesh && mesh.Primitives.Any(p => p.GetVertexAccessor("POSITION") is { Count: > 0 }) ? 1 : 0) + node.VisualChildren.Sum(Count);
}
