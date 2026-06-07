namespace OasisEditor;

public interface IFaceRuntimeStateResolver
{
    bool TryGetLampReference(FaceLampWindowElement lampWindow, out MachineObjectReference reference);
    double GetLampIntensity(FaceLampWindowElement lampWindow, MachineRuntimeState runtimeState);
    bool TryGetSevenSegmentDisplayReference(FaceSevenSegmentDisplayElement display, out MachineObjectReference reference);
    int[] GetSevenSegmentCellMasks(FaceSevenSegmentDisplayElement display, MachineRuntimeState runtimeState);
    double[] GetSevenSegmentCellBrightness(FaceSevenSegmentDisplayElement display, MachineRuntimeState runtimeState);
}

public sealed class FaceRuntimeStateResolver : IFaceRuntimeStateResolver
{
    public static FaceRuntimeStateResolver Instance { get; } = new();

    private FaceRuntimeStateResolver()
    {
    }

    public bool TryGetLampReference(FaceLampWindowElement lampWindow, out MachineObjectReference reference)
    {
        ArgumentNullException.ThrowIfNull(lampWindow);
        reference = lampWindow.LinkedMachineObjectReference ?? MachineObjectReference.Empty;
        return reference.Kind == MachineObjectKind.Lamp && !reference.IsEmpty;
    }

    public double GetLampIntensity(FaceLampWindowElement lampWindow, MachineRuntimeState runtimeState)
    {
        ArgumentNullException.ThrowIfNull(lampWindow);
        ArgumentNullException.ThrowIfNull(runtimeState);

        return TryGetLampReference(lampWindow, out var reference)
            ? runtimeState.GetLampIntensity(reference)
            : 0d;
    }

    public bool TryGetSevenSegmentDisplayReference(FaceSevenSegmentDisplayElement display, out MachineObjectReference reference)
    {
        ArgumentNullException.ThrowIfNull(display);
        reference = display.LinkedMachineObjectReference ?? MachineObjectReference.Empty;
        return reference.Kind == MachineObjectKind.SevenSegmentDisplay && !reference.IsEmpty;
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
}
