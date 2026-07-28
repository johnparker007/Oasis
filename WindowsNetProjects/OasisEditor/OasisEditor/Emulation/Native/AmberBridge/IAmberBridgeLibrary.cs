namespace OasisEditor;

public interface IAmberBridgeLibrary : IDisposable
{
    AmberBridgeDetails BridgeDetails { get; }
    uint NegotiatedApiVersion { get; }
    uint NegotiatedApiTableSize { get; }
    AmberBridgeCapabilities GetCapabilities();
    void Initialise(IReadOnlyList<string> programRomPaths, IReadOnlyList<string>? soundRomPaths = null);
    void Reset();
    int Run(uint cycles);
    void SetSwitchState(uint switchIndex, bool isOn);
    void GetOutputSnapshot(AmberOutputSnapshotBuffer destination);
    AmberAudioFormat GetAudioFormat();
    uint FillAudioFrames(Span<short> interleavedSamples, uint frameCapacity);
    void ConfigureReels(AmberReelConfiguration configuration);
    void ConfigureCoins(AmberCoinConfiguration configuration);
    void SetPercentageSwitch(uint rawValue);
    void Shutdown();
}

public sealed record AmberBridgeDetails(uint ApiVersion, string Name, string BridgeVersion);
