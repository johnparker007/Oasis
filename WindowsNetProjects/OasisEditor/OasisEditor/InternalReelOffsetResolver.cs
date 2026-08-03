namespace OasisEditor;

internal static class InternalReelOffsetResolver
{
    private const double ImpactSixteenStopBaseOffset = -0.08d;
    private const double ImpactSixteenStopBandCorrection = 0.062d;

    internal static double ResolveNormalizedOffset(FruitMachinePlatformType platform, int stops)
    {
        var safeStops = Math.Max(1, stops);
        return platform switch
        {
            FruitMachinePlatformType.MPU4 when safeStops == 16 => -0.05d,
            FruitMachinePlatformType.Impact when safeStops == 12 => -0.025d,
            FruitMachinePlatformType.Impact when safeStops == 16 =>
                ImpactSixteenStopBaseOffset + ImpactSixteenStopBandCorrection,
            FruitMachinePlatformType.Scorpion4 when safeStops == 12 => 0.2d,
            FruitMachinePlatformType.Scorpion4 when safeStops == 16 => 0.671d,
            _ => 0d
        };
    }
}
