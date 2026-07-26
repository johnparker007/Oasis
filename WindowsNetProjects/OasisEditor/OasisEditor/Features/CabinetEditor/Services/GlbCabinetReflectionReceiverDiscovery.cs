using OasisEditor.Features.CabinetEditor.Models;
using SharpGLTF.Schema2;

namespace OasisEditor.Features.CabinetEditor.Services;

public static class GlbCabinetReflectionReceiverDiscovery
{
    public static IReadOnlyList<CabinetReflectionReceiverTarget> Discover(string modelPath)
    {
        var root = ModelRoot.Load(modelPath); var scene = root.DefaultScene ?? root.LogicalScenes.First(); var result = new List<CabinetReflectionReceiverTarget>();
        foreach (var node in scene.VisualChildren) Visit(node, string.Empty, result);
        return result;
    }

    private static void Visit(Node node, string parentPath, ICollection<CabinetReflectionReceiverTarget> result)
    {
        var name = string.IsNullOrWhiteSpace(node.Name) ? $"Node{node.LogicalIndex}" : node.Name;
        var path = string.IsNullOrEmpty(parentPath) ? name : parentPath + "/" + name;
        if (node.Mesh is not null)
        {
            var slots = node.Mesh.Primitives.Select((primitive, index) => new CabinetReflectionMaterialSlot(index, primitive.Material?.Name ?? $"Material {index}", "glTF PBR")).ToArray();
            if (slots.Length > 0) result.Add(new CabinetReflectionReceiverTarget(path, name, node.Mesh.Name ?? $"Mesh{node.Mesh.LogicalIndex}", slots));
        }
        foreach (var child in node.VisualChildren) Visit(child, path, result);
    }
}
