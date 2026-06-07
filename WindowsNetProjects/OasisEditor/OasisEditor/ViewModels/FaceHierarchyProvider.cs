namespace OasisEditor;

public sealed class FaceHierarchyProvider : IDocumentHierarchyProvider
{
    public bool CanBuild(DocumentTabViewModel? document)
    {
        return document?.Document.DocumentType == EditorDocumentType.Face;
    }

    public IReadOnlyList<HierarchyItemViewModel> Build(DocumentTabViewModel? document)
    {
        if (!CanBuild(document))
        {
            return [];
        }

        var faceDocument = document!;
        var artwork = faceDocument.GetFaceElements()
            .OfType<FaceArtworkElement>()
            .Select((element, index) => CreateElementItem(element, index, "Artwork", "artwork"))
            .ToArray();

        var lampWindows = faceDocument.GetFaceElements()
            .OfType<FaceLampWindowElement>()
            .Select((element, index) => CreateElementItem(element, index, "Lamp Window", "lampWindow"))
            .ToArray();
        var sevenSegmentDisplays = faceDocument.GetFaceElements()
            .OfType<FaceSevenSegmentDisplayElement>()
            .Select((element, index) => CreateElementItem(element, index, "Seven Segment Display", "sevenSegmentDisplay"))
            .ToArray();
        var alphaDisplays = faceDocument.GetFaceElements()
            .OfType<FaceAlphaDisplayElement>()
            .Select((element, index) => CreateElementItem(element, index, "Alpha Display", "alphaDisplay"))
            .ToArray();
        var buttons = faceDocument.GetFaceElements()
            .OfType<FaceButtonElement>()
            .Select((element, index) => CreateElementItem(element, index, "Button", "button"))
            .ToArray();

        var groups = new List<HierarchyItemViewModel>();
        if (artwork.Length > 0)
        {
            groups.Add(new HierarchyItemViewModel($"Artwork ({artwork.Length})", "group:artwork", isGroup: true, children: artwork));
        }

        if (lampWindows.Length > 0)
        {
            groups.Add(new HierarchyItemViewModel($"Lamp Windows ({lampWindows.Length})", "group:lampWindow", isGroup: true, children: lampWindows));
        }

        if (sevenSegmentDisplays.Length > 0)
        {
            groups.Add(new HierarchyItemViewModel($"Seven Segment Displays ({sevenSegmentDisplays.Length})", "group:sevenSegmentDisplay", isGroup: true, children: sevenSegmentDisplays));
        }

        if (alphaDisplays.Length > 0)
        {
            groups.Add(new HierarchyItemViewModel($"Alpha Displays ({alphaDisplays.Length})", "group:alphaDisplay", isGroup: true, children: alphaDisplays));
        }

        if (buttons.Length > 0)
        {
            groups.Add(new HierarchyItemViewModel($"Buttons ({buttons.Length})", "group:button", isGroup: true, children: buttons));
        }

        return groups;
    }

    private static HierarchyItemViewModel CreateElementItem(FaceElementModel element, int index, string kindName, string token)
    {
        var x = Math.Round(element.X);
        var y = Math.Round(element.Y);
        var width = Math.Round(element.Width);
        var height = Math.Round(element.Height);
        var displayName = string.IsNullOrWhiteSpace(element.Name)
            ? $"{kindName} {index + 1} ({width}×{height} at {x}, {y})"
            : element.Name.Trim();
        if (element.IsLocked)
        {
            displayName += " [Locked]";
        }

        if (!element.IsVisible)
        {
            displayName += " [Hidden]";
        }

        if (element.LinkedMachineObjectReference is MachineObjectReference reference && !reference.IsEmpty)
        {
            displayName += $" [{reference}]";
        }

        return new HierarchyItemViewModel(
            displayName,
            $"{token}:{element.ObjectId}",
            panelSelection: FaceSelectionService.ToSelectionInfo(element));
    }
}
