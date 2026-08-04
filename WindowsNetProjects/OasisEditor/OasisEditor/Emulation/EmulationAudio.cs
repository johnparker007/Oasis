namespace OasisEditor;

public readonly record struct EmulationAudioFormat(int SampleRate, int Channels, int BitsPerSample);

public readonly record struct EmulationAudioPushContext(long Sequence, long SourceStartFrame, long AcceptedOutputStartFrame, int Frames, bool ZeroFrameRead, long AdvanceLatenessTicks);

public readonly record struct EmulationAudioPushResult(int OfferedBytes, int AcceptedBytes, int DroppedBytes, string? DropReason)
{
    public bool Dropped => DroppedBytes > 0;
}

public interface IEmulationAudioSink : IDisposable
{
    void Start(EmulationAudioFormat format);
    EmulationAudioPushResult PushPcm(ReadOnlySpan<byte> pcmBytes, EmulationAudioPushContext context = default);
    void Stop();
    void Clear();
}

internal interface IEmulationAudioDiagnosticSink
{
    void ConfigureDiagnostics(EmulationAudioDiagnostics? diagnostics);
}
