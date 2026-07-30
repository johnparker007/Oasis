namespace OasisEditor;

public interface IEmulationBackendFactory
{
    IEmulationBackend? CreateBackend(FruitMachinePlatformType platform);
}

public sealed class EmulationBackendFactory : IEmulationBackendFactory
{
    private readonly Func<string?> _fabricRuntimePathProvider;
    private readonly Func<string?> _productionAmberPathProvider;
    private readonly Func<int> _audioBufferLengthMillisecondsProvider;
    private readonly Func<string, IFabricRuntimeLibrary> _runtimeFactory;
    private readonly Func<int, IEmulationAudioSink> _audioSinkFactory;
    private readonly IFabricClock _clock;
    private readonly Action<string>? _errorLogger;

    public EmulationBackendFactory(
        Func<string?> fabricRuntimePathProvider,
        Func<string?> productionAmberPathProvider,
        Func<int>? audioBufferLengthMillisecondsProvider = null,
        Func<string, IFabricRuntimeLibrary>? runtimeFactory = null,
        Func<int, IEmulationAudioSink>? audioSinkFactory = null,
        IFabricClock? clock = null,
        Action<string>? errorLogger = null)
    {
        _fabricRuntimePathProvider = fabricRuntimePathProvider ?? throw new ArgumentNullException(nameof(fabricRuntimePathProvider));
        _productionAmberPathProvider = productionAmberPathProvider ?? throw new ArgumentNullException(nameof(productionAmberPathProvider));
        _audioBufferLengthMillisecondsProvider = audioBufferLengthMillisecondsProvider ?? (() => NativeEmulationPreferences.DefaultAudioBufferLengthMilliseconds);
        _runtimeFactory = runtimeFactory ?? (static path => new FabricRuntimeLibrary(path));
        _audioSinkFactory = audioSinkFactory ?? (static bufferLength => new NAudioEmulationAudioSink(bufferLength));
        _clock = clock ?? new StopwatchFabricClock();
        _errorLogger = errorLogger;
    }

    public IEmulationBackend? CreateBackend(FruitMachinePlatformType platform)
    {
        return platform switch
        {
            FruitMachinePlatformType.None => null,
            FruitMachinePlatformType.Impact => CreateFabricBackend(),
            _ => throw new NotSupportedException($"Platform '{platform}' is not supported by Fabric Amber emulation.")
        };
    }

    private IEmulationBackend CreateFabricBackend()
    {
        var runtimePath = _fabricRuntimePathProvider();
        if (string.IsNullOrWhiteSpace(runtimePath))
            throw new InvalidOperationException("Fabric runtime DLL path is not configured. Configure FabricRuntime.dll under Preferences > Fabric Emulation.");
        if (!File.Exists(runtimePath))
            throw new FileNotFoundException("FabricRuntime.dll was not found at the configured Fabric runtime DLL path. Select a valid runtime under Preferences > Fabric Emulation.", runtimePath);

        var amberPath = _productionAmberPathProvider();
        if (string.IsNullOrWhiteSpace(amberPath))
            throw new InvalidOperationException("Production Amber API v2 provider DLL path is not configured. Configure it under Preferences > Fabric Emulation.");
        if (!File.Exists(amberPath))
            throw new FileNotFoundException("The production Amber API v2 provider DLL was not found at the configured path. Select a valid provider under Preferences > Fabric Emulation.", amberPath);

        return new FabricEmulationBackend(runtimePath, amberPath, _runtimeFactory,
            _audioSinkFactory(_audioBufferLengthMillisecondsProvider()), _clock, _errorLogger);
    }
}
