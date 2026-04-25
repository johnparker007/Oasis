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
            BuildGroup("Images", elements, "image", "Image"),
            BuildGroup("Rectangles", elements, "rectangle", "Rectangle"),
            BuildGroup("Anchors", elements, "anchor", "Anchor"),
            BuildGroup("Zones", elements, "zone", "Zone")
        };

        return groups;
    }

    private static HierarchyItemViewModel BuildGroup(
        string groupName,
        IReadOnlyList<PanelElementFile> elements,
        string kind,
        string itemPrefix)
    {
        var matches = elements
            .Where(element => string.Equals(element.Kind, kind, StringComparison.OrdinalIgnoreCase))
            .Select((element, index) =>
            {
                var x = Math.Round(element.X);
                var y = Math.Round(element.Y);
                var width = Math.Round(element.Width);
                var height = Math.Round(element.Height);
                return new HierarchyItemViewModel(
                    $"{itemPrefix} {index + 1} ({width}×{height} at {x}, {y})",
                    $"{kind}:{element.X:0.###}:{element.Y:0.###}:{element.Width:0.###}:{element.Height:0.###}",
                    panelSelection: new PanelSelectionInfo(
                        kind,
                        element.X,
                        element.Y,
                        element.Width,
                        element.Height));
            })
            .ToArray();

        var label = matches.Length > 0 ? $"{groupName} ({matches.Length})" : $"{groupName} (0)";
        return new HierarchyItemViewModel(label, $"group:{kind}", isGroup: true, children: matches);
    }
}
