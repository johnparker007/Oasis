namespace OasisEditor;

public sealed class NativeEmulationPreferences
{
    public const int DefaultAudioBufferLengthMilliseconds = 50;

    public string FabricRuntimeLibraryPath { get; init; } = string.Empty;
    public string ProductionAmberLibraryPath { get; init; } = string.Empty;
    public string Mpu5AmberLibraryPath { get; init; } = string.Empty;
    public string EpochAmberLibraryPath { get; init; } = string.Empty;
    public string Mpu3AmberLibraryPath { get; init; } = string.Empty;
    public string M1AmberLibraryPath { get; init; } = string.Empty;
    public string Scorpion4AmberLibraryPath { get; init; } = string.Empty;
    public int AudioBufferLengthMilliseconds { get; init; } = DefaultAudioBufferLengthMilliseconds;
}
