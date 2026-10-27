namespace OasisEditor;

// Converts source-addressed alpha cells into their canonical display order.
internal static class AlphaCellOrder
{
    internal static int SourceIndexForCanonicalCell(int canonicalIndex, int cellCount, bool sourceAddressingReversed, FruitMachinePlatformType platform)
    {
        var reverseSource = IsAmberBackedPlatform(platform)
            ? !sourceAddressingReversed
            : sourceAddressingReversed;
        return reverseSource ? cellCount - 1 - canonicalIndex : canonicalIndex;
    }

    // Amber providers expose alpha cells using the opposite source-addressing convention.
    // Add future Amber-backed platforms here so they share the same runtime mapping.
    internal static bool IsAmberBackedPlatform(FruitMachinePlatformType platform)
    {
        return platform switch
        {
            FruitMachinePlatformType.Impact => true,
            FruitMachinePlatformType.MPU5 => true,
            FruitMachinePlatformType.Epoch => true,
            FruitMachinePlatformType.MPU3 => true,
            FruitMachinePlatformType.MaygayM1 => true,
            FruitMachinePlatformType.Scorpion4 => true,
            _ => false
        };
    }
}
