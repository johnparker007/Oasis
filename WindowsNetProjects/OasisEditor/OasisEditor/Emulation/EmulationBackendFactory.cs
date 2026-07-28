namespace OasisEditor;

public interface IEmulationBackendFactory
{
    IEmulationBackend? CreateBackend(FruitMachinePlatformType platform);
}

public sealed class EmulationBackendFactory : IEmulationBackendFactory
{
    private readonly Func<IEmulationBackend> _mameBackendFactory;
    private readonly Func<string?> _system6LibraryPathProvider;
    private readonly Func<string, IAmberBridgeLibrary> _amberBridgeFactory;

    public EmulationBackendFactory(
        Func<IEmulationBackend> mameBackendFactory,
        Func<string?> system6LibraryPathProvider,
        Func<int>? system6AudioBufferLengthMillisecondsProvider = null,
        Func<string, IAmberBridgeLibrary>? amberBridgeFactory = null)
    {
        _mameBackendFactory = mameBackendFactory ?? throw new ArgumentNullException(nameof(mameBackendFactory));
        _system6LibraryPathProvider = system6LibraryPathProvider ?? throw new ArgumentNullException(nameof(system6LibraryPathProvider));
        _ = system6AudioBufferLengthMillisecondsProvider; // Retained in the public constructor pending preferences UI cleanup.
        _amberBridgeFactory = amberBridgeFactory ?? (static path => new AmberBridgeLibrary(path));
    }

    public IEmulationBackend? CreateBackend(FruitMachinePlatformType platform)
    {
        return platform switch
        {
            FruitMachinePlatformType.None => null,
            FruitMachinePlatformType.Impact => CreateSystem6BackendOrMameFallback(),
            FruitMachinePlatformType.Epoch => _mameBackendFactory(),
            _ => _mameBackendFactory()
        };
    }

    private IEmulationBackend CreateSystem6BackendOrMameFallback()
    {
        var libraryPath = _system6LibraryPathProvider();
        return string.IsNullOrWhiteSpace(libraryPath)
            ? _mameBackendFactory()
            : new System6NativeBackend(libraryPath, _amberBridgeFactory);
    }

}
