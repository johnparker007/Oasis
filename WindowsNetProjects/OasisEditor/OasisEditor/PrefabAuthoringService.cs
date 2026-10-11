namespace OasisEditor;

/// <summary>Immutable hierarchy operations. IDs never depend on names or sibling order.</summary>
public static class PrefabAuthoringService
{
    public static PrefabNode? Find(PrefabNode root, string id) => Object3DValidationService.Descendants(root).FirstOrDefault(n => n.Id == id);
    public static PrefabNode? Parent(PrefabNode root, string id) => Object3DValidationService.Descendants(root).FirstOrDefault(n => n.Children?.Any(c => c is not null && c.Id == id) == true);

    public static Object3DDocument Edit(Object3DDocument document, string id, Func<PrefabNode, PrefabNode> mutation)
    {
        var node = Find(document.Root, id) ?? throw new InvalidOperationException($"Prefab '{document.Id}', node '{id}': not found.");
        var replacement = mutation(node);
        if (replacement.Id != id) throw new InvalidOperationException($"Prefab '{document.Id}', node '{id}', id: immutable.");
        return Validate(document with { Root = Replace(document.Root, id, replacement) });
    }
    public static Object3DDocument Add(Object3DDocument document, string parentId, PrefabNode node) => Edit(document, parentId, p => p with { Children = [.. p.Children, node] });
    public static Object3DDocument Remove(Object3DDocument document, string id)
    {
        if (document.Root.Id == id) throw new InvalidOperationException($"Prefab '{document.Id}', node '{id}': root cannot be removed.");
        var parent = Parent(document.Root, id) ?? throw new InvalidOperationException($"Prefab '{document.Id}', node '{id}': parent not found.");
        return Edit(document, parent.Id, p => p with { Children = p.Children.Where(n => n.Id != id).ToArray() });
    }
    public static Object3DDocument Move(Object3DDocument document, string id, string parentId, int index)
    {
        var node = Find(document.Root, id) ?? throw new InvalidOperationException($"Prefab '{document.Id}', node '{id}': not found.");
        if (id == document.Root.Id || Find(node, parentId) is not null) throw new InvalidOperationException($"Prefab '{document.Id}', node '{id}', parent: root move or hierarchy cycle is unsupported.");
        var parent = Parent(document.Root, id)!;
        var destination = Find(document.Root, parentId) ?? throw new InvalidOperationException($"Prefab '{document.Id}', node '{id}', parent '{parentId}': not found.");
        var count = destination.Children.Count - (parent.Id == parentId ? 1 : 0);
        if (index < 0 || index > count) throw new InvalidOperationException($"Prefab '{document.Id}', node '{id}', sibling index: out of range.");
        if (parent.Id == parentId && parent.Children.ToList().FindIndex(n => n.Id == id) == index) return document;
        // Validate only the final tree: removing a dynamic body's child collider transiently can be invalid.
        var without = document with { Root = Replace(document.Root, parent.Id, parent with { Children = parent.Children.Where(n => n.Id != id).ToArray() }) };
        return Edit(without, parentId, p => p with { Children = p.Children.Take(index).Concat([node]).Concat(p.Children.Skip(index)).ToArray() });
    }
    public static Object3DDocument Component(Object3DDocument document, string nodeId, PrefabComponentKind kind, bool add) => Edit(document, nodeId, node =>
    {
        if (kind == PrefabComponentKind.Transform) throw new InvalidOperationException($"Prefab '{document.Id}', node '{nodeId}', Transform: mandatory; cannot add or remove.");
        if (!add) return node with { Components = node.Components.Where(c => c.Kind != kind).ToArray() };
        if (node.Component(kind) is not null) return node;
        var component = kind switch
        {
            PrefabComponentKind.Model => PrefabComponent.ForModel(new("model.glb", 1, "Y")),
            PrefabComponentKind.Collider => PrefabComponent.ForCollider(new(Object3DColliderKind.Box, [0,0,0], Size: [1,1,1]), node.Component(PrefabComponentKind.Model) is null ? PrefabColliderSourceFrame.NodeLocalSI : PrefabColliderSourceFrame.ModelLocal),
            PrefabComponentKind.Rigidbody => PrefabComponent.ForBody(new(false)),
            _ => throw new InvalidOperationException("Unsupported Prefab component kind.")
        };
        return node.SetComponent(component);
    });
    private static Object3DDocument Validate(Object3DDocument document) { Object3DValidationService.Validate(document); return document; }
    private static PrefabNode Replace(PrefabNode root, string id, PrefabNode replacement)
    {
        if (root.Id == id) return replacement;
        if (!root.Children.Any(n => Find(n, id) is not null)) return root;
        return root with { Children = root.Children.Select(n => Replace(n, id, replacement)).ToArray() };
    }
}
