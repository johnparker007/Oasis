namespace OasisEditor;

public interface IDocumentHierarchyProvider
{
    bool CanBuild(DocumentTabViewModel? document);

    IReadOnlyList<HierarchyItemViewModel> Build(DocumentTabViewModel? document);
}
