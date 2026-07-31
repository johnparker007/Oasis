namespace OasisEditor;

/// <summary>
/// Defines the serialized Alpha <c>Reversed</c> contract. Source cell zero is
/// normally drawn at the left; a reversed display draws it at the right.
/// </summary>
public static class SegmentCellOrder
{
    public static int SourceIndexForVisualCell(int visualIndex, int cellCount, bool reversed)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(visualIndex);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(cellCount);
        if (visualIndex >= cellCount)
        {
            throw new ArgumentOutOfRangeException(nameof(visualIndex));
        }

        return reversed ? cellCount - 1 - visualIndex : visualIndex;
    }
}
