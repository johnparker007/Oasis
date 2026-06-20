namespace OasisEditor;

internal static class InternalReelOffsetResolver
{
    public static double ResolveBackendReelOffsetNormalized(
        EmulationBackendKind backendKind,
        FruitMachinePlatformType platform,
        int stops)
    {
        var safeStops = Math.Max(1, stops);
        return backendKind switch
        {
            EmulationBackendKind.NativeSystem6 => ResolveNativeSystem6ReelOffsetNormalized(platform, safeStops),
            _ => ResolveMameReelOffsetNormalized(platform, safeStops)
        };
    }

    // Backend calibration offsets only. These are deliberately separate from user/layout BandOffset values.
    private static double ResolveMameReelOffsetNormalized(FruitMachinePlatformType platform, int stops)
    {
        return platform switch
        {
            FruitMachinePlatformType.MPU4 when stops == 16 => -0.05d,
            FruitMachinePlatformType.Impact when stops == 12 => -0.025d,
            FruitMachinePlatformType.Impact when stops == 16 => -0.08d,
            FruitMachinePlatformType.Scorpion4 when stops == 12 => 0.2d,
            FruitMachinePlatformType.Scorpion4 when stops == 16 => 0.671d,
            _ => 0d
        };
    }

    private static double ResolveNativeSystem6ReelOffsetNormalized(FruitMachinePlatformType platform, int stops)
    {
        return platform switch
        {
            FruitMachinePlatformType.Impact when stops == 16 => 0.07d,
            _ => 0d
        };
    }
}
