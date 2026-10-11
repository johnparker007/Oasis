namespace OasisEditor;

public sealed class PrefabHierarchyProvider : IDocumentHierarchyProvider
{
    public bool CanBuild(DocumentTabViewModel? document) => document?.Document.DocumentType == EditorDocumentType.Object3D;
    public IReadOnlyList<HierarchyItemViewModel> Build(DocumentTabViewModel? document) => CanBuild(document) ? [Create(document!.GetObject3DDocument().Root)] : [];
    private static HierarchyItemViewModel Create(PrefabNode node) => new(node.Name, $"prefab:{node.Id}", children:
        node.Components.Select(c => new HierarchyItemViewModel($"{c.Kind} ({c.Id})", $"component:{c.Id}", isGroup: true))
            .Concat(node.Children.Select(Create)).ToArray(), panelSelection: new PanelSelectionInfo(node.Id, "PrefabNode", 0, 0, 0, 0));
}
