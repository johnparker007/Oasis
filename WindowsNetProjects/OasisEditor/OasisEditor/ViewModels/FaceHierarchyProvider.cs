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
        var lampWindows = faceDocument.GetFaceElements()
            .OfType<FaceLampWindowElement>()
            .Select((element, index) =>
            {
                var x = Math.Round(element.X);
                var y = Math.Round(element.Y);
                var width = Math.Round(element.Width);
                var height = Math.Round(element.Height);
                var displayName = string.IsNullOrWhiteSpace(element.Name)
                    ? $"Lamp Window {index + 1} ({width}×{height} at {x}, {y})"
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
                    $"lampWindow:{element.ObjectId}",
                    panelSelection: FaceSelectionService.ToSelectionInfo(element));
            })
            .ToArray();

        return
        [
            new HierarchyItemViewModel($"Lamp Windows ({lampWindows.Length})", "group:lampWindow", isGroup: true, children: lampWindows)
        ];
    }
}
