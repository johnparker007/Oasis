namespace OasisEditor;

public interface IEmulationBackendFactory
{
    IEmulationBackend? CreateBackend(FruitMachinePlatformType platform);
}

public sealed class EmulationBackendFactory : IEmulationBackendFactory
{
    private readonly Func<IEmulationBackend> _mameBackendFactory;
    private readonly Func<string?> _system6LibraryPathProvider;

    public EmulationBackendFactory(
        Func<IEmulationBackend> mameBackendFactory,
        Func<string?> system6LibraryPathProvider)
    {
        _mameBackendFactory = mameBackendFactory ?? throw new ArgumentNullException(nameof(mameBackendFactory));
        _system6LibraryPathProvider = system6LibraryPathProvider ?? throw new ArgumentNullException(nameof(system6LibraryPathProvider));
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
            : new System6NativeBackend(libraryPath);
    }
}
