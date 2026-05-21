namespace OasisEditor;

internal sealed class AlphaSixteenSegmentDisplayVisual : SegmentDisplayVisualBase
{
    public AlphaSixteenSegmentDisplayVisual(string? segmentDisplayType = null)
        : base(LoadDefinition(segmentDisplayType))
    {
    }

    protected override int GetSegmentMaskForChar(char c)
    {
        return char.ToUpperInvariant(c) switch
        {
            >= '0' and <= '9' => 0b0000_1111_1111_1111,
            >= 'A' and <= 'Z' => 0b1111_1111_0000_1111,
            _ => 0
        };
    }

    private static SegmentDisplayDefinition? LoadDefinition(string? segmentDisplayType)
    {
        if (!string.IsNullOrWhiteSpace(segmentDisplayType)
            && SegmentDisplayDefinitionLoader.TryGetDefinitionByType(segmentDisplayType, out var mappedDefinition))
        {
            return mappedDefinition;
        }

        SegmentDisplayDefinitionLoader.TryGetSixteenSegmentDefinition(out var definition);
        return definition;
    }
}
