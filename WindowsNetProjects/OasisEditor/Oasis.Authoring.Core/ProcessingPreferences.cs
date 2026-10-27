namespace OasisEditor;

public enum CpuImageProcessingMode { Auto, Maximum, Custom }

public sealed class ProcessingPreferences
{
    public CpuImageProcessingMode CpuMode { get; init; } = CpuImageProcessingMode.Auto;
    public int CustomMaximumWorkers { get; init; } = 1;
}
