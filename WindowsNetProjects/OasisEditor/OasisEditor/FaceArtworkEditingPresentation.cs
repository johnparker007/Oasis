namespace OasisEditor;

/// <summary>Editor-only visibility rules used while the primary Face selection is Artwork.</summary>
internal static class FaceArtworkEditingPresentation
{
    public static bool IsArtworkPrimarySelection(DocumentTabViewModel document)
    {
        if (document.SelectionState.PrimaryItem is not { Domain: EditorSelectionDomain.FaceElement } primary) return false;
        return document.TryGetFaceElementByObjectId(primary.ObjectId, out var element) && element is FaceArtworkElement;
    }

    public static bool IsSuppressed(DocumentTabViewModel document, FaceElementModel element) =>
        IsArtworkPrimarySelection(document) && element is FaceLampWindowElement;

    public static IEnumerable<FaceElementModel> GetViewportElements(DocumentTabViewModel document) =>
        document.GetFaceElements().Where(element => !IsSuppressed(document, element));
}
