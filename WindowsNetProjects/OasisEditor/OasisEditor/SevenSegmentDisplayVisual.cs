namespace OasisEditor;

internal sealed class SevenSegmentDisplayVisual : SegmentDisplayVisualBase
{
    public SevenSegmentDisplayVisual()
        : base(LoadDefinition())
    {
    }

    protected override int GetSegmentMaskForChar(char c)
    {
        return char.ToUpperInvariant(c) switch
        {
            '0' => 0b0111111,
            '1' => 0b0000110,
            '2' => 0b1011011,
            '3' => 0b1001111,
            '4' => 0b1100110,
            '5' => 0b1101101,
            '6' => 0b1111101,
            '7' => 0b0000111,
            '8' => 0b1111111,
            '9' => 0b1101111,
            _ => 0
        };
    }

    private static SegmentDisplayDefinition? LoadDefinition()
    {
        SegmentDisplayDefinitionLoader.TryGetSevenSegmentDefinition(out var definition);
        return definition;
    }
}
