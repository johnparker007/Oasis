namespace OasisEditor;

internal static class FaceRuntimeDisplayReferenceIndex
{
    public static IReadOnlyDictionary<MachineObjectReference, string[]> GetObjectIdsByReference<TElement>(DocumentTabViewModel document, MachineObjectKind expectedKind)
        where TElement : FaceElementModel
    {
        ArgumentNullException.ThrowIfNull(document);

        return document.GetFaceElements()
            .OfType<TElement>()
            .Select(element => new
            {
                Element = element,
                Reference = element.LinkedMachineObjectReference ?? MachineObjectReference.Empty
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.Element.ObjectId)
                && item.Reference.Kind == expectedKind
                && !item.Reference.IsEmpty)
            .GroupBy(item => item.Reference, item => item.Element.ObjectId, MachineObjectReferenceComparer.Instance)
            .ToDictionary(
                group => group.Key,
                group => group.Distinct(StringComparer.Ordinal).ToArray(),
                MachineObjectReferenceComparer.Instance);
    }

    public static void AddObjectIdsForReference<TElement>(DocumentTabViewModel document, MachineObjectReference reference, ISet<string> destination)
        where TElement : FaceElementModel
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(destination);

        if (reference.IsEmpty)
        {
            return;
        }

        var objectIdsByReference = GetObjectIdsByReference<TElement>(document, reference.Kind);
        if (!objectIdsByReference.TryGetValue(reference, out var objectIds))
        {
            return;
        }

        foreach (var objectId in objectIds)
        {
            destination.Add(objectId);
        }
    }

    private sealed class MachineObjectReferenceComparer : IEqualityComparer<MachineObjectReference>
    {
        public static MachineObjectReferenceComparer Instance { get; } = new();

        public bool Equals(MachineObjectReference x, MachineObjectReference y)
        {
            return x.Kind == y.Kind && string.Equals(x.Id, y.Id, StringComparison.Ordinal);
        }

        public int GetHashCode(MachineObjectReference obj)
        {
            return HashCode.Combine(obj.Kind, StringComparer.Ordinal.GetHashCode(obj.Id ?? string.Empty));
        }
    }
}
