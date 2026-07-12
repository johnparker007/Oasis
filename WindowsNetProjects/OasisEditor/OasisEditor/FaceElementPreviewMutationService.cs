namespace OasisEditor;

internal static class FaceElementPreviewMutationService
{
    public static bool TryApplyPreviews(DocumentTabViewModel document, IReadOnlyDictionary<string, FaceElementModel> updatedElements)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(updatedElements);
        var changed = false;
        foreach (var update in updatedElements) changed |= TryApplyPreview(document, update.Key, update.Value);
        return changed;
    }

    public static bool TryApplyPreview(DocumentTabViewModel document, string objectId, FaceElementModel updatedElement)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (string.IsNullOrWhiteSpace(objectId)) return false;
        var elements = document.GetFaceElements().ToList();
        var index = elements.FindIndex(element => string.Equals(element.ObjectId, objectId, StringComparison.Ordinal));
        if (index < 0) return false;
        var existing = elements[index];
        if (!string.Equals(updatedElement.ObjectId, objectId, StringComparison.Ordinal)
            || updatedElement.GetType() != existing.GetType()
            || !FaceElementValidation.IsValidForInspectorUpdate(updatedElement)
            || FaceElementModelComparer.AreEquivalent(existing, updatedElement)) return false;
        elements[index] = FaceElementModelCloner.Clone(updatedElement);
        document.SetFaceElements(elements, new PanelChangeEvent(document.DocumentId, objectId, PanelChangeProperties.Geometry, true, false, true, false));
        return true;
    }
}
