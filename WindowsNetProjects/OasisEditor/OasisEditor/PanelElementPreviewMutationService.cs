namespace OasisEditor;

internal static class PanelElementPreviewMutationService
{
    public static bool TryApplyPreviews(DocumentTabViewModel document, IReadOnlyDictionary<string, PanelElementModel> updatedElements)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(updatedElements);

        if (updatedElements.Count == 0)
        {
            return false;
        }

        var elements = document.GetPanelElements().ToList();
        var elementIndexes = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < elements.Count; i++)
        {
            var element = elements[i];
            if (!string.IsNullOrWhiteSpace(element.ObjectId))
            {
                elementIndexes[element.ObjectId] = i;
            }
        }

        var changedProperties = PanelChangeProperties.None;
        var changed = false;
        foreach (var update in updatedElements)
        {
            if (string.IsNullOrWhiteSpace(update.Key)
                || !elementIndexes.TryGetValue(update.Key, out var index))
            {
                continue;
            }

            var updatedElement = update.Value;
            var existing = elements[index];
            if (!string.Equals(updatedElement.ObjectId, update.Key, StringComparison.Ordinal)
                || updatedElement.Kind != existing.Kind
                || !PanelElementValidation.IsValidForInspectorUpdate(updatedElement)
                || PanelElementModelComparer.AreEquivalent(existing, updatedElement))
            {
                continue;
            }

            changedProperties |= GetChangedProperties(existing, updatedElement);
            elements[index] = PanelElementModelCloner.Clone(updatedElement);
            changed = true;
        }

        if (!changed)
        {
            return false;
        }

        document.SetPanelElements(
            elements,
            new PanelChangeEvent(
                document.DocumentId,
                null,
                changedProperties,
                AffectsCanvas: true,
                AffectsHierarchy: false,
                AffectsInspectorRows: true,
                AffectsPersistence: false));
        return true;
    }

    public static bool TryApplyPreview(DocumentTabViewModel document, string objectId, PanelElementModel updatedElement)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (string.IsNullOrWhiteSpace(objectId))
        {
            return false;
        }

        var elements = document.GetPanelElements().ToList();
        var index = elements.FindIndex(element => string.Equals(element.ObjectId, objectId, StringComparison.Ordinal));
        if (index < 0)
        {
            return false;
        }

        var existing = elements[index];
        if (!string.Equals(updatedElement.ObjectId, objectId, StringComparison.Ordinal)
            || updatedElement.Kind != existing.Kind
            || !PanelElementValidation.IsValidForInspectorUpdate(updatedElement)
            || PanelElementModelComparer.AreEquivalent(existing, updatedElement))
        {
            return false;
        }

        elements[index] = PanelElementModelCloner.Clone(updatedElement);
        document.SetPanelElements(
            elements,
            new PanelChangeEvent(
                document.DocumentId,
                objectId,
                GetChangedProperties(existing, updatedElement),
                AffectsCanvas: true,
                AffectsHierarchy: false,
                AffectsInspectorRows: true,
                AffectsPersistence: false));
        return true;
    }

    private static PanelChangeProperties GetChangedProperties(PanelElementModel before, PanelElementModel after)
    {
        var changed = PanelChangeProperties.None;

        if (before.X != after.X || before.Y != after.Y || before.Width != after.Width || before.Height != after.Height)
        {
            changed |= PanelChangeProperties.Geometry;
        }

        return changed is PanelChangeProperties.None
            ? PanelChangeProperties.Metadata
            : changed;
    }
}
