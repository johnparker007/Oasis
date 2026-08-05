namespace OasisEditor;

public readonly record struct EmulationAudioFormat(int SampleRate, int Channels, int BitsPerSample);

public readonly record struct EmulationAudioPlaybackStatistics(
    long RingFramesWritten,
    long RingFramesRejected,
    long DeviceFramesDelivered,
    long SilenceFrames,
    long UnderrunEpisodes,
    int MinimumRingFrames,
    int CurrentRingFrames,
    int CapacityFrames,
    int PrebufferThresholdFrames,
    int PrebufferThresholdMilliseconds,
    bool PlaybackStarted);

public interface IEmulationAudioSink : IDisposable
{
    void Start(EmulationAudioFormat format);
    void PushPcm(ReadOnlySpan<byte> pcmBytes);
    void Stop();
    void Clear();
    EmulationAudioPlaybackStatistics GetStatistics();
}
