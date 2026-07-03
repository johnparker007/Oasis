namespace OasisEditor;

public readonly record struct EmulationAudioFormat(int SampleRate, int Channels, int BitsPerSample);

public interface IEmulationAudioSink : IDisposable
{
    void Start(EmulationAudioFormat format);
    void PushPcm(ReadOnlySpan<byte> pcmBytes);
    void Stop();
    void Clear();
}
