using System.IO;

namespace OasisEditor;

public interface IEmulationBackendFactory
{
    IEmulationBackend? CreateBackend(FruitMachinePlatformType platform);
}

public sealed class EmulationBackendFactory : IEmulationBackendFactory
{
    private readonly Func<string?> _fabricRuntimePathProvider;
    private readonly Func<string?> _productionAmberPathProvider;
    private readonly Func<string?> _mpu5AmberPathProvider;
    private readonly Func<string?> _epochAmberPathProvider;
    private readonly Func<string?> _mpu3AmberPathProvider;
    private readonly Func<string?> _m1AmberPathProvider;
    private readonly Func<int> _audioBufferLengthMillisecondsProvider;
    private readonly Func<string, IFabricRuntimeLibrary> _runtimeFactory;
    private readonly Func<int, IEmulationAudioSink> _audioSinkFactory;
    private readonly IFabricClock _clock;
    private readonly Action<string>? _errorLogger;
    private readonly Action<string>? _infoLogger;

    public EmulationBackendFactory(
        Func<string?> fabricRuntimePathProvider,
        Func<string?> productionAmberPathProvider,
        Func<int>? audioBufferLengthMillisecondsProvider = null,
        Action<string>? errorLogger = null,
        Action<string>? infoLogger = null,
        Func<string?>? mpu5AmberPathProvider = null,
        Func<string?>? epochAmberPathProvider = null,
        Func<string?>? mpu3AmberPathProvider = null,
        Func<string?>? m1AmberPathProvider = null)
        : this(
            fabricRuntimePathProvider,
            productionAmberPathProvider,
            mpu5AmberPathProvider ?? productionAmberPathProvider,
            epochAmberPathProvider ?? productionAmberPathProvider,
            mpu3AmberPathProvider ?? productionAmberPathProvider,
            m1AmberPathProvider ?? (() => null),
            audioBufferLengthMillisecondsProvider,
            static path => new FabricRuntimeLibrary(path),
            static bufferLength => new NAudioEmulationAudioSink(bufferLength),
            new StopwatchFabricClock(),
            errorLogger,
            infoLogger)
    {
    }

    internal EmulationBackendFactory(
        Func<string?> fabricRuntimePathProvider, Func<string?> productionAmberPathProvider,
        Func<int>? audioBufferLengthMillisecondsProvider, Func<string, IFabricRuntimeLibrary> runtimeFactory,
        Func<int, IEmulationAudioSink> audioSinkFactory, IFabricClock clock,
        Action<string>? errorLogger, Action<string>? infoLogger = null)
        : this(fabricRuntimePathProvider, productionAmberPathProvider, productionAmberPathProvider, productionAmberPathProvider, productionAmberPathProvider, () => null,
            audioBufferLengthMillisecondsProvider, runtimeFactory, audioSinkFactory, clock, errorLogger, infoLogger) { }

    internal EmulationBackendFactory(
        Func<string?> fabricRuntimePathProvider,
        Func<string?> productionAmberPathProvider,
        Func<string?> mpu5AmberPathProvider,
        Func<string?> epochAmberPathProvider,
        Func<string?> mpu3AmberPathProvider,
        Func<string?> m1AmberPathProvider,
        Func<int>? audioBufferLengthMillisecondsProvider,
        Func<string, IFabricRuntimeLibrary> runtimeFactory,
        Func<int, IEmulationAudioSink> audioSinkFactory,
        IFabricClock clock,
        Action<string>? errorLogger,
        Action<string>? infoLogger = null)
    {
        _fabricRuntimePathProvider = fabricRuntimePathProvider ?? throw new ArgumentNullException(nameof(fabricRuntimePathProvider));
        _productionAmberPathProvider = productionAmberPathProvider ?? throw new ArgumentNullException(nameof(productionAmberPathProvider));
        _mpu5AmberPathProvider = mpu5AmberPathProvider ?? throw new ArgumentNullException(nameof(mpu5AmberPathProvider));
        _epochAmberPathProvider = epochAmberPathProvider ?? throw new ArgumentNullException(nameof(epochAmberPathProvider));
        _mpu3AmberPathProvider = mpu3AmberPathProvider ?? throw new ArgumentNullException(nameof(mpu3AmberPathProvider));
        _m1AmberPathProvider = m1AmberPathProvider ?? throw new ArgumentNullException(nameof(m1AmberPathProvider));
        _audioBufferLengthMillisecondsProvider = audioBufferLengthMillisecondsProvider ?? (() => NativeEmulationPreferences.DefaultAudioBufferLengthMilliseconds);
        _runtimeFactory = runtimeFactory ?? throw new ArgumentNullException(nameof(runtimeFactory));
        _audioSinkFactory = audioSinkFactory ?? throw new ArgumentNullException(nameof(audioSinkFactory));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _errorLogger = errorLogger;
        _infoLogger = infoLogger;
    }

    public IEmulationBackend? CreateBackend(FruitMachinePlatformType platform)
    {
        return platform switch
        {
            FruitMachinePlatformType.None => null,
            FruitMachinePlatformType.Impact => CreateFabricBackend(_productionAmberPathProvider, "JPM System 6 production Amber provider DLL"),
            FruitMachinePlatformType.MPU5 => CreateFabricBackend(_mpu5AmberPathProvider, "Barcrest MPU5 Amber provider DLL"),
            FruitMachinePlatformType.Epoch => CreateFabricBackend(_epochAmberPathProvider, "Maygay Epoch Amber provider DLL"),
            FruitMachinePlatformType.MPU3 => CreateFabricBackend(_mpu3AmberPathProvider, "Barcrest MPU3 Amber provider DLL"),
            FruitMachinePlatformType.MaygayM1 => CreateFabricBackend(_m1AmberPathProvider, "Maygay M1 Amber provider DLL"),
            _ => throw new NotSupportedException($"Platform '{platform}' is not supported by Fabric Amber emulation.")
        };
    }

    private IEmulationBackend CreateFabricBackend(Func<string?> providerPath, string providerLabel)
    {
        var runtimePath = _fabricRuntimePathProvider();
        if (string.IsNullOrWhiteSpace(runtimePath))
            throw new InvalidOperationException("Fabric runtime DLL path is not configured. Configure FabricRuntime.dll under Preferences > Fabric Emulation.");
        if (!File.Exists(runtimePath))
            throw new FileNotFoundException("FabricRuntime.dll was not found at the configured Fabric runtime DLL path. Select a valid runtime under Preferences > Fabric Emulation.", runtimePath);

        var amberPath = providerPath();
        if (string.IsNullOrWhiteSpace(amberPath))
            throw new InvalidOperationException($"{providerLabel} path is not configured. Configure it under Preferences > Fabric Emulation.");
        if (!File.Exists(amberPath))
            throw new FileNotFoundException($"The {providerLabel} was not found at the configured path. Select a valid provider under Preferences > Fabric Emulation.", amberPath);

        return new FabricEmulationBackend(runtimePath, amberPath, _runtimeFactory,
            _audioSinkFactory(_audioBufferLengthMillisecondsProvider()), _clock, _errorLogger, _infoLogger);
    }
}
