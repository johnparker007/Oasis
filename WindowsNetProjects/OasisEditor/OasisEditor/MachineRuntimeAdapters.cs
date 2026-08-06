namespace OasisEditor;

public interface IMachineLampRuntimeAdapter
{
    void ApplyLampState(int lampId, int lampValue);
}

public interface IMachineReelRuntimeAdapter
{
    void ApplyReelState(int reelId, int reelValue, ReelPositionConvention convention = ReelPositionConvention.Oasis);
}

public interface IMachineSegmentRuntimeAdapter
{
    void ApplySegmentState(int cellId, int segmentMask, SegmentOutputType outputType);
    void ApplyVfdBrightness(int cellId, double normalizedBrightness);
}
