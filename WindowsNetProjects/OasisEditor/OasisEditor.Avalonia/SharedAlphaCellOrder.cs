using OasisEditor;

namespace OasisEditor.SegmentDisplays;

internal static class AlphaCellOrder
{
    internal static int SourceIndexForCanonicalCell(int canonicalIndex, int cellCount, bool sourceAddressingReversed, FruitMachinePlatformType platform)
    {
        var amber = platform is FruitMachinePlatformType.Impact or FruitMachinePlatformType.MPU5 or FruitMachinePlatformType.Epoch
            or FruitMachinePlatformType.MPU3 or FruitMachinePlatformType.MaygayM1 or FruitMachinePlatformType.Scorpion4;
        return (amber ? !sourceAddressingReversed : sourceAddressingReversed) ? cellCount - 1 - canonicalIndex : canonicalIndex;
    }
}
