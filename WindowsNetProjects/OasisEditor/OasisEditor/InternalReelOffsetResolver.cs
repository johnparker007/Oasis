namespace OasisEditor;

internal static class InternalReelOffsetResolver
{
    internal static double ResolveNormalizedOffset(FruitMachinePlatformType platform, int stops)
    {
        var safeStops = Math.Max(1, stops);
        return platform == FruitMachinePlatformType.Impact ? -0.5d / safeStops : 0d;
    }
}
