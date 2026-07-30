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
    private readonly Func<int> _system6AudioBufferLengthMillisecondsProvider;
    private readonly Func<(bool Enabled,string? RuntimePath,string? AmberPath)> _fabricConfigurationProvider;
    private readonly Action<string>? _fabricErrorLogger;
    private readonly Func<bool> _amberComparisonEnabledProvider;
    private readonly Action<string>? _amberComparisonLogger;

    public EmulationBackendFactory(
        Func<IEmulationBackend> mameBackendFactory,
        Func<string?> system6LibraryPathProvider,
        Func<int>? system6AudioBufferLengthMillisecondsProvider = null,
        Func<string, IAmberBridgeLibrary>? amberBridgeFactory = null,
        Func<(bool Enabled,string? RuntimePath,string? AmberPath)>? fabricConfigurationProvider = null,
        Action<string>? fabricErrorLogger = null,
        Func<bool>? amberComparisonEnabledProvider = null,
        Action<string>? amberComparisonLogger = null)
    {
        _mameBackendFactory = mameBackendFactory ?? throw new ArgumentNullException(nameof(mameBackendFactory));
        _system6LibraryPathProvider = system6LibraryPathProvider ?? throw new ArgumentNullException(nameof(system6LibraryPathProvider));
        _system6AudioBufferLengthMillisecondsProvider = system6AudioBufferLengthMillisecondsProvider ?? (() => NativeEmulationPreferences.DefaultAudioBufferLengthMilliseconds);
        _amberBridgeFactory = amberBridgeFactory ?? (static path => new AmberBridgeLibrary(path));
        _fabricConfigurationProvider = fabricConfigurationProvider ?? (() => (false,null,null));
        _fabricErrorLogger = fabricErrorLogger;
        _amberComparisonEnabledProvider = amberComparisonEnabledProvider ?? (() => false);
        _amberComparisonLogger = amberComparisonLogger;
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
        var fabric = _fabricConfigurationProvider();
        if (fabric.Enabled)
        {
            if (string.IsNullOrWhiteSpace(fabric.RuntimePath) || string.IsNullOrWhiteSpace(fabric.AmberPath))
                throw new InvalidOperationException("Fabric emulation is enabled, but both the Fabric runtime DLL path and Amber API v2 DLL path must be configured.");
            return new FabricEmulationBackend(fabric.RuntimePath, fabric.AmberPath, path => new FabricRuntimeLibrary(path),
                new NAudioEmulationAudioSink(), new StopwatchFabricClock(), _fabricErrorLogger,
                _amberComparisonEnabledProvider, _amberComparisonLogger);
        }
        var libraryPath = _system6LibraryPathProvider();
        return string.IsNullOrWhiteSpace(libraryPath)
            ? _mameBackendFactory()
            : new System6NativeBackend(libraryPath, _amberBridgeFactory, new NAudioEmulationAudioSink(_system6AudioBufferLengthMillisecondsProvider()),
                _amberComparisonEnabledProvider, _amberComparisonLogger);
    }

}
