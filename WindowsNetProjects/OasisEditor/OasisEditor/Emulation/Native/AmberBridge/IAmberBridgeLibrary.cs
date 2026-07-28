namespace OasisEditor;

public interface IAmberBridgeLibrary : IDisposable
{
    AmberBridgeDetails BridgeDetails { get; }
    void Initialise(IReadOnlyList<string> programRomPaths, IReadOnlyList<string>? soundRomPaths = null);
    void Reset();
    int Run(uint cycles);
    void Shutdown();
}

public sealed record AmberBridgeDetails(uint ApiVersion, string Name, string BridgeVersion);
