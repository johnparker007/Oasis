namespace OasisEditor;

public sealed class Panel2DHierarchyProvider : IDocumentHierarchyProvider
{
    public bool CanBuild(DocumentTabViewModel? document)
    {
        return document?.Document.DocumentType == EditorDocumentType.Panel2D;
    }

    public IReadOnlyList<HierarchyItemViewModel> Build(DocumentTabViewModel? document)
    {
        if (!CanBuild(document))
        {
            return [];
        }

        var elements = Panel2DDocumentStorage.DeserializeLayout(document?.PanelLayoutJson);
        var groups = new List<HierarchyItemViewModel>
        {
            BuildGroup("Images", elements, PanelElementKind.Image, "Image"),
            BuildGroup("Rectangles", elements, PanelElementKind.Rectangle, "Rectangle"),
            BuildGroup("Anchors", elements, PanelElementKind.Anchor, "Anchor"),
            BuildGroup("Zones", elements, PanelElementKind.Zone, "Zone")
        };

        return groups;
    }

    private static HierarchyItemViewModel BuildGroup(
        string groupName,
        IReadOnlyList<PanelElementFile> elements,
        PanelElementKind kind,
        string itemPrefix)
    {
        var kindToken = Panel2DDocumentStorage.SerializeElementKind(kind);
        var matches = elements
            .Where(element => element.ElementKind == kind)
            .Select((element, index) =>
            {
                var x = Math.Round(element.X);
                var y = Math.Round(element.Y);
                var width = Math.Round(element.Width);
                var height = Math.Round(element.Height);
                var displayName = string.IsNullOrWhiteSpace(element.Name)
                    ? $"{itemPrefix} {index + 1} ({width}×{height} at {x}, {y})"
                    : element.Name.Trim();
                return new HierarchyItemViewModel(
                    displayName,
                    $"{kindToken}:{element.ObjectId}",
                    panelSelection: new PanelSelectionInfo(
                        element.ObjectId,
                        kindToken,
                        element.X,
                        element.Y,
                        element.Width,
                        element.Height));
            })
            .ToArray();

        var label = matches.Length > 0 ? $"{groupName} ({matches.Length})" : $"{groupName} (0)";
        return new HierarchyItemViewModel(label, $"group:{kindToken}", isGroup: true, children: matches);
    }
}
