namespace OasisEditor;

internal static class InternalReelOffsetResolver
{
    private const double ImpactTwelveStopBaseOffset = -0.025d;
    private const double ImpactTwelveStopBandCorrection = 0.05d;
    private const double ImpactSixteenStopBaseOffset = -0.08d;
    private const double ImpactSixteenStopBandCorrection = 0.062d;

    internal static double ResolveNormalizedOffset(FruitMachinePlatformType platform, int stops)
    {
        var safeStops = Math.Max(1, stops);
        return platform switch
        {
            FruitMachinePlatformType.MPU4 when safeStops == 16 => -0.05d,
            FruitMachinePlatformType.MPU5 when safeStops == 12 => -0.075d,
            FruitMachinePlatformType.MPU5 when safeStops == 16 => -0.22d,
            FruitMachinePlatformType.Epoch when safeStops == 12 => -0.16d,
            FruitMachinePlatformType.MaygayM1 when safeStops == 12 => -0.045d,
            FruitMachinePlatformType.MaygayM1 when safeStops == 16 => -0.092d,
            FruitMachinePlatformType.Impact when safeStops == 12 =>
                ImpactTwelveStopBaseOffset + ImpactTwelveStopBandCorrection,
            FruitMachinePlatformType.Impact when safeStops == 16 =>
                ImpactSixteenStopBaseOffset + ImpactSixteenStopBandCorrection,
            FruitMachinePlatformType.Scorpion4 when safeStops == 12 => 0.974d,
            FruitMachinePlatformType.Scorpion4 when safeStops == 16 => 0.911d,
            _ => 0d
        };
    }
}
