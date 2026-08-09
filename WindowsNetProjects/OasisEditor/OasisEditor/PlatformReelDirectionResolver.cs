namespace OasisEditor;

internal static class PlatformReelDirectionResolver
{
    internal static bool RequiresReversal(FruitMachinePlatformType platform)
    {
        return platform is FruitMachinePlatformType.MPU4 or FruitMachinePlatformType.Epoch or FruitMachinePlatformType.MPU3;
    }
}
