namespace OasisEditor;

/// <summary>
/// Applies Input Map deletions to the project model while preserving surviving row order.
/// </summary>
public static class InputMapDeletionService
{
    public static int DeleteSelected(
        IList<InputDefinitionModel> inputs,
        IEnumerable<InputDefinitionModel> selectedInputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(selectedInputs);

        var selected = selectedInputs.ToHashSet(ReferenceEqualityComparer.Instance);
        var indexes = Enumerable.Range(0, inputs.Count)
            .Where(index => selected.Contains(inputs[index]))
            .OrderDescending()
            .ToArray();

        foreach (var index in indexes)
        {
            inputs.RemoveAt(index);
        }

        return indexes.Length;
    }
}
