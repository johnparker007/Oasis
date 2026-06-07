namespace OasisEditor;

public interface IFaceRuntimeStateResolver
{
    bool TryGetLampReference(FaceLampWindowElement lampWindow, out MachineObjectReference reference);
    double GetLampIntensity(FaceLampWindowElement lampWindow, MachineRuntimeState runtimeState);
    bool TryGetReelDisplayReference(FaceReelDisplayElement reelDisplay, out MachineObjectReference reference);
    double GetReelPosition(FaceReelDisplayElement reelDisplay, MachineRuntimeState runtimeState);
    bool TryGetSevenSegmentDisplayReference(FaceSevenSegmentDisplayElement display, out MachineObjectReference reference);
    bool TryGetAlphaDisplayReference(FaceAlphaDisplayElement display, out MachineObjectReference reference);
    int[] GetSevenSegmentCellMasks(FaceSevenSegmentDisplayElement display, MachineRuntimeState runtimeState);
    double[] GetSevenSegmentCellBrightness(FaceSevenSegmentDisplayElement display, MachineRuntimeState runtimeState);
    int[] GetAlphaCellMasks(FaceAlphaDisplayElement display, MachineRuntimeState runtimeState);
    double[] GetAlphaCellBrightness(FaceAlphaDisplayElement display, MachineRuntimeState runtimeState);
}

public sealed class FaceRuntimeStateResolver : IFaceRuntimeStateResolver
{
    public static FaceRuntimeStateResolver Instance { get; } = new();

    private FaceRuntimeStateResolver()
    {
    }

    public bool TryGetLampReference(FaceLampWindowElement lampWindow, out MachineObjectReference reference)
    {
        return TryGetReference(lampWindow, MachineObjectKind.Lamp, out reference);
    }

    public double GetLampIntensity(FaceLampWindowElement lampWindow, MachineRuntimeState runtimeState)
    {
        ArgumentNullException.ThrowIfNull(lampWindow);
        ArgumentNullException.ThrowIfNull(runtimeState);

        return TryGetLampReference(lampWindow, out var reference)
            ? runtimeState.GetLampIntensity(reference)
            : 0d;
    }

    public bool TryGetReelDisplayReference(FaceReelDisplayElement reelDisplay, out MachineObjectReference reference)
    {
        return TryGetReference(reelDisplay, MachineObjectKind.Reel, out reference);
    }

    public double GetReelPosition(FaceReelDisplayElement reelDisplay, MachineRuntimeState runtimeState)
    {
        ArgumentNullException.ThrowIfNull(reelDisplay);
        ArgumentNullException.ThrowIfNull(runtimeState);

        var rawPosition = TryGetReelDisplayReference(reelDisplay, out var reference)
            ? runtimeState.GetReelPosition(reference)
            : 0d;

        return ResolveEffectiveReelPosition(
            rawPosition,
            reelDisplay.Stops.GetValueOrDefault(1),
            reelDisplay.IsReversed,
            reelDisplay.BandOffset.GetValueOrDefault(0d),
            runtimeState.FruitMachinePlatform);
    }

    internal static double ResolveEffectiveReelPosition(double rawReelPosition, int stops, bool reelReversed, double reelBandOffset, FruitMachinePlatformType platform)
    {
        const double positionsPerRevolution = 96d;
        var safeStops = Math.Max(1, stops);
        var wrapped = ((rawReelPosition % positionsPerRevolution) + positionsPerRevolution) % positionsPerRevolution;
        var shouldReverse = RequiresPlatformReversal(platform) ^ reelReversed;
        var directionAdjusted = shouldReverse && wrapped != 0d
            ? positionsPerRevolution - wrapped
            : wrapped;
        var platformOffset = MameReelRuntimeAdapter.ResolvePlatformBandOffsetNormalized(platform, safeStops);
        var offsetAdjusted = directionAdjusted + ((platformOffset + reelBandOffset) * positionsPerRevolution);
        return ((offsetAdjusted % positionsPerRevolution) + positionsPerRevolution) % positionsPerRevolution;
    }

    private static bool RequiresPlatformReversal(FruitMachinePlatformType platform)
    {
        return platform == FruitMachinePlatformType.MPU4;
    }

    public bool TryGetSevenSegmentDisplayReference(FaceSevenSegmentDisplayElement display, out MachineObjectReference reference)
    {
        return TryGetReference(display, MachineObjectKind.SevenSegmentDisplay, out reference);
    }

    public bool TryGetAlphaDisplayReference(FaceAlphaDisplayElement display, out MachineObjectReference reference)
    {
        return TryGetReference(display, MachineObjectKind.AlphaDisplay, out reference);
    }

    private static bool TryGetReference(FaceElementModel element, MachineObjectKind expectedKind, out MachineObjectReference reference)
    {
        ArgumentNullException.ThrowIfNull(element);
        reference = element.LinkedMachineObjectReference ?? MachineObjectReference.Empty;
        return reference.Kind == expectedKind && !reference.IsEmpty;
    }

    public int[] GetSevenSegmentCellMasks(FaceSevenSegmentDisplayElement display, MachineRuntimeState runtimeState)
    {
        ArgumentNullException.ThrowIfNull(display);
        ArgumentNullException.ThrowIfNull(runtimeState);

        return TryGetSevenSegmentDisplayReference(display, out var reference)
            ? runtimeState.GetSegmentCellMasks(reference, 1)
            : new int[1];
    }

    public double[] GetSevenSegmentCellBrightness(FaceSevenSegmentDisplayElement display, MachineRuntimeState runtimeState)
    {
        ArgumentNullException.ThrowIfNull(display);
        ArgumentNullException.ThrowIfNull(runtimeState);

        return TryGetSevenSegmentDisplayReference(display, out var reference)
            ? runtimeState.GetSegmentCellBrightness(reference, 1)
            : [1d];
    }

    public int[] GetAlphaCellMasks(FaceAlphaDisplayElement display, MachineRuntimeState runtimeState)
    {
        ArgumentNullException.ThrowIfNull(display);
        ArgumentNullException.ThrowIfNull(runtimeState);

        return TryGetAlphaDisplayReference(display, out var reference)
            ? runtimeState.GetSegmentCellMasks(reference, 16)
            : new int[16];
    }

    public double[] GetAlphaCellBrightness(FaceAlphaDisplayElement display, MachineRuntimeState runtimeState)
    {
        ArgumentNullException.ThrowIfNull(display);
        ArgumentNullException.ThrowIfNull(runtimeState);

        return TryGetAlphaDisplayReference(display, out var reference)
            ? runtimeState.GetSegmentCellBrightness(reference, 16)
            : Enumerable.Repeat(1d, 16).ToArray();
    }
}
