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

        var panelDocument = document!;
        var elements = panelDocument.GetPanelElements();
        var groups = new List<HierarchyItemViewModel>
        {
            BuildFaceSourceShapeGroup(panelDocument.GetPanelFaceSourceShapes()),
            BuildGroup("Backgrounds", elements, PanelElementKind.Background, "Background"),
            BuildGroup("Lamps", elements, PanelElementKind.Lamp, "Lamp"),
            BuildGroup("Reels", elements, PanelElementKind.Reel, "Reel"),
            BuildGroup("Seven Segments", elements, PanelElementKind.SevenSegment, "7 Segment"),
            BuildGroup("Alphas", elements, PanelElementKind.Alpha, "Alpha"),
            BuildGroup("VFD Dot Matrices", elements, PanelElementKind.VfdDotMatrix, "VFD Dot Matrix"),
            BuildGroup("Labels", elements, PanelElementKind.Label, "Label"),
            BuildGroup("Images", elements, PanelElementKind.Image, "Image"),
            BuildGroup("Rectangles", elements, PanelElementKind.Rectangle, "Rectangle"),
            BuildGroup("Anchors", elements, PanelElementKind.Anchor, "Anchor"),
            BuildGroup("Zones", elements, PanelElementKind.Zone, "Zone")
        };

        return groups;
    }

    private static HierarchyItemViewModel BuildFaceSourceShapeGroup(IReadOnlyList<PanelFaceSourceShapeModel> shapes)
    {
        var matches = shapes.Select((shape, index) =>
        {
            var displayName = string.IsNullOrWhiteSpace(shape.Name) ? $"Face Source Shape {index + 1}" : shape.Name.Trim();
            return new HierarchyItemViewModel(
                displayName,
                $"faceSourceShape:{shape.Id}",
                panelSelection: PanelFaceSourceShapeCommands.ToSelection(shape));
        }).ToArray();
        return new HierarchyItemViewModel($"Face Source Shapes ({matches.Length})", "group:faceSourceShapes", isGroup: true, children: matches);
    }

    private static HierarchyItemViewModel BuildGroup(
        string groupName,
        IReadOnlyList<PanelElementModel> elements,
        PanelElementKind kind,
        string itemPrefix)
    {
        var kindToken = Panel2DDocumentStorage.SerializeElementKind(kind);
        var matches = elements
            .Where(element => element.Kind == kind)
            .Select((element, index) =>
            {
                var x = Math.Round(element.X);
                var y = Math.Round(element.Y);
                var width = Math.Round(element.Width);
                var height = Math.Round(element.Height);
                var displayName = string.IsNullOrWhiteSpace(element.Name)
                    ? $"{itemPrefix} {index + 1} ({width}×{height} at {x}, {y})"
                    : element.Name.Trim();
                if (element.IsLocked)
                {
                    displayName += " [Locked]";
                }

                if (!element.IsVisible)
                {
                    displayName += " [Hidden]";
                }

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
