using System.Collections.Concurrent;

namespace OasisEditor.Avalonia;

/// <summary>Owns only the backend started by this workspace. Authored documents stay untouched.</summary>
public sealed class NativePreviewSession : IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1);
    private readonly Func<NativeEmulationPreferences, FruitMachinePlatformType, bool, IEmulationBackend> _factory;
    private readonly ConcurrentDictionary<string, InputDefinitionModel> _pressed = new();
    private readonly Dictionary<string, HashSet<string>> _inputSources = [];
    private Func<bool>? _sourceCurrent;
    private MachineDocument? _machine;
    private IEmulationBackend? _backend;
    private bool _disposed;
    private static NativePreviewSession? _nativeOwner;
    private readonly bool _usesNativeBackend;
    private bool _ownsNativeLease;
    public IEmulationBackend? Backend => _backend;
    public EmulationBackendState State => _backend?.State ?? EmulationBackendState.Stopped;
    public MachineDocument? Machine => _machine is null ? null : Clone(_machine);
    public event Action<IEmulationBackend, MachineDocument>? BackendCreated;
    public event Action? Cleared;
    public string? LastError { get; private set; }
    public bool Audible { get; private set; }

    public NativePreviewSession() : this(CreateBackend) { _usesNativeBackend = true; }
    public NativePreviewSession(Func<NativeEmulationPreferences, FruitMachinePlatformType, bool, IEmulationBackend> factory)
        => _factory = factory;

    public async Task StartAsync(MachineDocument machine, NativeEmulationPreferences preferences, bool audible = false,
        Func<bool>? sourceCurrent = null, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_backend is not null) throw new InvalidOperationException("Stop the current preview before starting another.");
            if (sourceCurrent?.Invoke() == false) throw new InvalidOperationException("Save the current Machine and project before previewing.");
            var copy = Clone(machine);
            var request = CreateLaunchRequest(copy);
            if (_usesNativeBackend)
            {
                if (Interlocked.CompareExchange(ref _nativeOwner, this, null) is not null)
                    throw new InvalidOperationException("A native preview is already running in this process. Stop it before starting another.");
                _ownsNativeLease = true;
            }
            try
            {
                var backend = _factory(preferences, request.Platform, audible);
                _sourceCurrent = sourceCurrent; _machine = copy; Audible = audible; LastError = null;
                _backend = backend;
                BackendCreated?.Invoke(backend, Clone(copy));
                // Loading a real native provider may be slow; never block the UI thread.
                await Task.Run(() => backend.StartAsync(request, cancellationToken), cancellationToken).ConfigureAwait(false);
                if (sourceCurrent?.Invoke() == false)
                    throw new InvalidOperationException("The project changed while preview was starting. Preview stopped.");
            }
            catch (Exception error)
            {
                LastError = error.Message;
                await StopCore().ConfigureAwait(false);
                throw;
            }
        }
        finally { _gate.Release(); }
    }

    public async Task ControlAsync(string action, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (action == "stop") { await StopCore().ConfigureAwait(false); return; }
            var backend = RequireCurrent();
            switch (action)
            {
                case "pause": await ReleaseAllCore().ConfigureAwait(false); await backend.PauseAsync(cancellationToken).ConfigureAwait(false); break;
                case "resume": await backend.ResumeAsync(cancellationToken).ConfigureAwait(false); break;
                case "reset": await ReleaseAllCore().ConfigureAwait(false); await backend.ResetAsync(cancellationToken).ConfigureAwait(false); break;
                case "throttle": await backend.SetThrottleEnabledAsync(true, cancellationToken).ConfigureAwait(false); break;
                case "unthrottle": await backend.SetThrottleEnabledAsync(false, cancellationToken).ConfigureAwait(false); break;
                default: throw new ArgumentException("Use pause, resume, reset, throttle, unthrottle or stop.", nameof(action));
            }
        }
        finally { _gate.Release(); }
    }

    public async Task<CoinInputResult?> SetInputAsync(string id, bool pressed, CancellationToken cancellationToken = default, string source = "default")
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var backend = RequireCurrent();
            var input = _machine!.InputDefinitions.SingleOrDefault(i => i.Id == id)
                ?? throw new ArgumentException("Choose an input authored in the running Machine.", nameof(id));
            if (pressed && State != EmulationBackendState.Running) throw new InvalidOperationException("Resume preview before pressing an input.");
            if (pressed)
            {
                if (_pressed.ContainsKey(id)) { _inputSources[id].Add(source); return null; }
                if (input.CoinInput)
                {
                    var result = await backend.InsertCoinAsync(input, cancellationToken).ConfigureAwait(false);
                    if (result == CoinInputResult.Accepted) { _pressed[id] = input; _inputSources[id] = [source]; }
                    return result;
                }
                await backend.SetInputStateAsync(input, true, cancellationToken).ConfigureAwait(false);
                _pressed[id] = input;
                _inputSources[id] = [source];
            }
            else if (_pressed.TryGetValue(id, out var held))
            {
                if (!_inputSources[id].Contains(source)) return null;
                if (_inputSources[id].Count > 1) { _inputSources[id].Remove(source); return null; }
                if (held.CoinInput) await backend.ReleaseCoinAsync(held, cancellationToken).ConfigureAwait(false);
                else await backend.SetInputStateAsync(held, false, cancellationToken).ConfigureAwait(false);
                _pressed.TryRemove(id, out _);
                _inputSources.Remove(id);
            }
            return null;
        }
        finally { _gate.Release(); }
    }

    public async Task ReleaseInputsAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try { await ReleaseAllCore().ConfigureAwait(false); }
        finally { _gate.Release(); }
    }
    private async Task ReleaseAllCore()
    {
        var backend = _backend;
        if (backend is null) { _pressed.Clear(); _inputSources.Clear(); return; }
        var failures = new List<Exception>();
        foreach (var pair in _pressed.ToArray())
        {
            try
            {
                if (pair.Value.CoinInput) await backend.ReleaseCoinAsync(pair.Value, CancellationToken.None).ConfigureAwait(false);
                else await backend.SetInputStateAsync(pair.Value, false, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception error) { failures.Add(error); }
            finally { _pressed.TryRemove(pair.Key, out _); _inputSources.Remove(pair.Key); }
        }
        if (failures.Count != 0) throw new AggregateException("One or more held inputs could not be released.", failures);
    }
    private IEmulationBackend RequireCurrent()
    {
        if (_sourceCurrent?.Invoke() == false) throw new InvalidOperationException("The preview source changed; stop and restart preview.");
        return _backend ?? throw new InvalidOperationException("Start a Machine preview first.");
    }
    private async Task StopCore()
    {
        var backend = _backend;
        try
        {
            if (backend is not null)
            {
                try { await ReleaseAllCore().ConfigureAwait(false); }
                finally { try { await backend.StopAsync(CancellationToken.None).ConfigureAwait(false); } finally { await backend.DisposeAsync().ConfigureAwait(false); } }
            }
        }
        finally
        {
            _backend = null; _machine = null; _sourceCurrent = null; _pressed.Clear();
            if (_ownsNativeLease) { Interlocked.CompareExchange(ref _nativeOwner, null, this); _ownsNativeLease = false; }
            Cleared?.Invoke();
        }
    }
    public async ValueTask DisposeAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try { if (_disposed) return; _disposed = true; await StopCore().ConfigureAwait(false); }
        finally { _gate.Release(); }
    }
    private static MachineDocument Clone(MachineDocument machine)
    {
        if (!MachineDocumentStorage.TryRead(MachineDocumentStorage.Serialize(machine), out var copy, out var error))
            throw new InvalidDataException(error);
        return copy;
    }

    public static EmulationLaunchRequest CreateLaunchRequest(MachineDocument machine)
    {
        if (machine.Runtime is not EmulationRuntimeDefinition runtime)
            throw new InvalidOperationException("Oasis Machines preview in Oasis Player. Select an Emulation Machine for the integrated 2D preview.");
        return runtime.PlatformSettings switch
        {
            System6NativeRomSettings settings when runtime.Platform == FruitMachinePlatformType.Impact => new(settings),
            Mpu5NativeRomSettings settings when runtime.Platform == FruitMachinePlatformType.MPU5 => EmulationLaunchRequest.ForMpu5(settings),
            EpochNativeRomSettings settings when runtime.Platform == FruitMachinePlatformType.Epoch => EmulationLaunchRequest.ForEpoch(settings),
            Mpu3ProjectSettings settings when runtime.Platform == FruitMachinePlatformType.MPU3 => EmulationLaunchRequest.ForMpu3(settings),
            M1ProjectSettings settings when runtime.Platform == FruitMachinePlatformType.MaygayM1 => EmulationLaunchRequest.ForM1(settings),
            Scorpion4ProjectSettings settings when runtime.Platform == FruitMachinePlatformType.Scorpion4 => EmulationLaunchRequest.ForScorpion4(settings),
            _ => throw new InvalidOperationException("Configure a supported Machine emulation platform and its ROMs first.")
        };
    }

    private static IEmulationBackend CreateBackend(NativeEmulationPreferences preferences, FruitMachinePlatformType platform, bool audible)
    {
        var provider = platform switch
        {
            FruitMachinePlatformType.Impact => preferences.ProductionAmberLibraryPath,
            FruitMachinePlatformType.MPU5 => preferences.Mpu5AmberLibraryPath,
            FruitMachinePlatformType.Epoch => preferences.EpochAmberLibraryPath,
            FruitMachinePlatformType.MPU3 => preferences.Mpu3AmberLibraryPath,
            FruitMachinePlatformType.MaygayM1 => preferences.M1AmberLibraryPath,
            FruitMachinePlatformType.Scorpion4 => preferences.Scorpion4AmberLibraryPath,
            _ => throw new InvalidOperationException("No native provider is supported for this platform.")
        };
        foreach (var path in new[] { preferences.FabricRuntimeLibraryPath, provider })
            if (!Path.IsPathFullyQualified(path) || !File.Exists(path))
                throw new FileNotFoundException("Configure an existing absolute Fabric runtime and platform provider library path.", path);
        if (audible && !OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Audible emulation currently requires the Windows audio backend. Silent diagnostic preview supports other native platforms when their providers are installed.");
        if (preferences.AudioBufferLengthMilliseconds is < 10 or > 2000) throw new ArgumentOutOfRangeException(nameof(preferences), "Audio buffer must be 10–2000 ms.");
        return new FabricEmulationBackend(preferences.FabricRuntimeLibraryPath, provider, path => new FabricRuntimeLibrary(path),
            audible ? new NAudioEmulationAudioSink(preferences.AudioBufferLengthMilliseconds) : new DiagnosticAudioSink(),
            new StopwatchFabricClock());
    }
}

/// <summary>Consumes actual provider PCM without opening an audio device; used by headless verification.</summary>
public sealed class DiagnosticAudioSink : IEmulationAudioSink
{
    private EmulationAudioFormat _format;
    private long _frames, _nonZeroSamples;
    public long Frames => Interlocked.Read(ref _frames);
    public long NonZeroSamples => Interlocked.Read(ref _nonZeroSamples);
    public int WritableFrames => CapacityFrames;
    public int CapacityFrames => 65536;
    public void Start(EmulationAudioFormat format) { _format = format; Clear(); }
    public void PushPcm(ReadOnlySpan<byte> pcmBytes)
    {
        if (_format.Channels <= 0) return;
        var samples = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, short>(pcmBytes);
        Interlocked.Add(ref _frames, samples.Length / _format.Channels);
        long nonzero = 0; foreach (var value in samples) if (value != 0) nonzero++;
        Interlocked.Add(ref _nonZeroSamples, nonzero);
    }
    public void Stop() { }
    public void Clear() { Interlocked.Exchange(ref _frames, 0); Interlocked.Exchange(ref _nonZeroSamples, 0); }
    public void Dispose() { }
    public EmulationAudioPlaybackStatistics GetStatistics() => new(Frames, 0, Frames, 0, 0, 0, 0, CapacityFrames, 0, 0, 0, false);
}
