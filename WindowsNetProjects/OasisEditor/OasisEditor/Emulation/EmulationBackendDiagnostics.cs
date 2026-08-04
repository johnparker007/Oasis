namespace OasisEditor;

public enum EmulationBackendDiagnosticSeverity
{
    Info,
    Warning,
    Error
}

public readonly record struct EmulationBackendDiagnosticMessage(EmulationBackendDiagnosticSeverity Severity, string Message);

public readonly record struct AmberFabricAudioDiagnosticSettings(
    bool Enabled,
    string CaptureDirectory,
    int QueueBlockCapacity,
    int CaptureDurationSeconds)
{
    public static AmberFabricAudioDiagnosticSettings Disabled { get; } = new(false, string.Empty,
        NativeEmulationPreferences.DefaultAudioDiagnosticQueueBlockCapacity,
        NativeEmulationPreferences.DefaultAudioDiagnosticCaptureDurationSeconds);
}
