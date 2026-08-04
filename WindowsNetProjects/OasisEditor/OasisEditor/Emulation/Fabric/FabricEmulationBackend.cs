using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace OasisEditor;

public sealed class FabricEmulationBackend : IEmulationBackend, IEditorUpdateDrivenEmulationBackend
{
    private const string AmberBackendKind = "amber";
    private const string JpmSystem6MachineIdentifier = "jpm-system6";
    private const double System6FallbackNativeRefreshHz = 50.0;
    private const int AudioDrainMaxReadsPerUpdate = 8;
    private const int AudioDrainMaxDisplayFramesPerUpdate = 2;
    private const int AudioPushChunkMilliseconds = 1;
    private static readonly EmulationBackendCapabilities BackendCapabilities =
        new(true, true, true, true, false, false, false);

    private readonly string _runtimePath;
    private readonly string _amberPath;
    private readonly Func<string, IFabricRuntimeLibrary> _runtimeFactory;
    private readonly IEmulationAudioSink _audioSink;
    private readonly IFabricClock _clock;
    private readonly Action<string> _errorLogger;
    private readonly SemaphoreSlim _sessionGate = new(1, 1);
    private readonly ConcurrentQueue<InputCommand> _inputCommands = new();
    private readonly HashSet<int> _assertedInputs = [];
    private readonly Dictionary<int, (bool State, float Brightness)> _lamps = [];
    private readonly Dictionary<int, int> _reels = [];
    private readonly Dictionary<DisplayOutputIdentity, ulong> _displayOutputs = [];
    private readonly Dictionary<DisplayBrightnessIdentity, float> _displayBrightnessOutputs = [];
    private readonly object _stateGate = new();

    private IFabricRuntimeLibrary? _runtime;
    private IFabricMachineSession? _session;
    private long? _elapsedUpdateBaselineTimestamp;
    private long _nativeDisplayFrameNanoseconds;
    private FabricAudioFormat? _audioFormat;
    private short[] _audioBuffer = [];
    private EmulationBackendState _state = EmulationBackendState.Stopped;
    private bool _shutdown;
    private bool _audioStarted;
    private bool _disposed;
    private long _elapsedAdvanceCount;
    private long _elapsedClampedUpdateCount;
    private long _maxRawElapsedNanoseconds;
    private long _audioFramesReturned;
    private long _audioReadCount;

    public FabricEmulationBackend(string runtimePath, string amberPath)
        : this(runtimePath, amberPath, path => new FabricRuntimeLibrary(path),
            new NAudioEmulationAudioSink(), new StopwatchFabricClock(), WriteDebugError)
    {
    }

    internal FabricEmulationBackend(
        string runtimePath,
        string amberPath,
        Func<string, IFabricRuntimeLibrary> runtimeFactory,
        IEmulationAudioSink audioSink,
        IFabricClock clock,
        Action<string>? errorLogger = null)
    {
        _runtimePath = runtimePath;
        _amberPath = amberPath;
        _runtimeFactory = runtimeFactory;
        _audioSink = audioSink;
        _clock = clock;
        _errorLogger = errorLogger ?? WriteDebugError;
    }

    public EmulationBackendKind BackendKind => EmulationBackendKind.Fabric;
    public EmulationBackendCapabilities Capabilities => BackendCapabilities;
    public EmulationBackendState State { get { lock (_stateGate) return _state; } }
    public Exception? LastFailure { get; private set; }
    internal IEmulationAudioSink AudioSink => _audioSink;

    public event EventHandler<EmulationBackendState>? StateChanged;
    public event EventHandler<MachineLampChangedEventArgs>? LampChanged;
    public event EventHandler<MachineReelChangedEventArgs>? ReelChanged;
    public event EventHandler<MachineSegmentChangedEventArgs>? SegmentChanged;
    public event EventHandler<MachineVfdBrightnessChangedEventArgs>? VfdBrightnessChanged;
    public event EventHandler<MachineDotMatrixChangedEventArgs>? DotMatrixChanged { add { } remove { } }

    public async Task StartAsync(EmulationLaunchRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        if (State != EmulationBackendState.Stopped)
            throw new InvalidOperationException($"Fabric backend cannot start while it is {State}.");

        SetState(EmulationBackendState.Starting);
        try
        {
            var settings = request.System6Configuration;
            var resources = BuildRomResources(settings);
            cancellationToken.ThrowIfCancellationRequested();
            _runtime = _runtimeFactory(_runtimePath);
            var configuration = FabricAmberConfiguration.FromSystem6(settings);
            WriteCoinConfigurationDiagnostics(configuration);
            _session = _runtime.CreateSession(new FabricLaunchRequest(
                AmberBackendKind, JpmSystem6MachineIdentifier, _amberPath, resources,
                configuration));

            await _sessionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                _session.Initialise();
                // Fabric currently does not expose Amber/System 6 native display metadata through the managed ABI.
                // System 6 is a 50 Hz native video platform, so keep this fallback isolated for the
                // editor elapsed-time scheduling experiment until machine metadata can replace it.
                _nativeDisplayFrameNanoseconds = ResolveNativeDisplayFrameNanoseconds(System6FallbackNativeRefreshHz);
                ConfigureAudio(_session);
            }
            finally
            {
                _sessionGate.Release();
            }

            _shutdown = false;
            LastFailure = null;
            ResetElapsedUpdateBaseline();
            ResetElapsedUpdateDiagnostics();
            SetState(EmulationBackendState.Running);
        }
        catch
        {
            await CleanupResourcesAsync().ConfigureAwait(false);
            SetState(EmulationBackendState.Failed);
            throw;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_disposed || State == EmulationBackendState.Stopped)
            return;
        if (State != EmulationBackendState.Failed)
            SetState(EmulationBackendState.Stopping);

        ResetElapsedUpdateBaseline();
        WriteElapsedUpdateStopSummary();
        await CleanupResourcesAsync().ConfigureAwait(false);
        SetState(EmulationBackendState.Stopped);
    }

    public Task PauseAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        if (State == EmulationBackendState.Running)
            SetState(EmulationBackendState.Paused);
        return Task.CompletedTask;
    }

    public Task ResumeAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        if (State == EmulationBackendState.Paused)
        {
            ResetElapsedUpdateBaseline();
            SetState(EmulationBackendState.Running);
        }
        return Task.CompletedTask;
    }

    public async Task ResetAsync(EmulationResetKind resetKind, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        EnsureAcceptingOperations();
        await _sessionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ReleaseAssertedInputs();
            ClearPendingInputs();
            RequireSession().Reset();
            ClearOutputCaches();
            _audioSink.Clear();
            ResetElapsedUpdateBaseline();
        }
        finally
        {
            _sessionGate.Release();
        }
    }

    public Task SetInputStateAsync(InputDefinitionModel inputDefinition, bool isPressed, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(inputDefinition);
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        EnsureAcceptingOperations();
        if (_session?.Capabilities.Has(FabricCapability.DigitalInput) != true)
            throw new NotSupportedException("Fabric session does not support digital input.");
        if (!int.TryParse(inputDefinition.ButtonNumber, out var index))
            throw new InvalidOperationException($"Input '{inputDefinition.Id}' has no numeric switch mapping.");
        _inputCommands.Enqueue(new(index, isPressed));
        return Task.CompletedTask;
    }

    public Task<CoinInputResult> InsertCoinAsync(InputDefinitionModel inputDefinition, CancellationToken cancellationToken) =>
        SetCoinStateAsync(inputDefinition, true, cancellationToken);

    public async Task ReleaseCoinAsync(InputDefinitionModel inputDefinition, CancellationToken cancellationToken) =>
        _ = await SetCoinStateAsync(inputDefinition, false, cancellationToken).ConfigureAwait(false);

    private async Task<CoinInputResult> SetCoinStateAsync(InputDefinitionModel inputDefinition, bool active, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(inputDefinition);
        ThrowIfDisposed();
        EnsureAcceptingOperations();
        var session = RequireSession();
        if (!session.Capabilities.Has(FabricCapability.CoinInput))
            throw new NotSupportedException("Fabric session does not support coin input.");
        if (inputDefinition.CoinChannel is not (>= 0 and <= 5) || inputDefinition.CoinValue is not (>= 0 and <= 12))
            throw new InvalidOperationException($"Coin input '{inputDefinition.Id}' requires a channel from 0 to 5 and denomination from 0 to 12.");

        await _sessionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var channel = checked((byte)inputDefinition.CoinChannel.Value);
            var value = checked((byte)inputDefinition.CoinValue.Value);
            var result = session.SubmitInput(new(inputDefinition.Name, 0, FabricInputKind.Coin, active, channel, value));
            Debug.WriteLine(active
                ? $"[Fabric Coin Input] channel={channel} value={value} result={(result == FabricResult.InputRejected ? "rejected" : "accepted")}"
                : $"[Fabric Coin Input] channel={channel} value={value} state=released");
            return result == FabricResult.InputRejected ? CoinInputResult.Rejected : CoinInputResult.Accepted;
        }
        finally
        {
            _sessionGate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;
        await StopAsync(CancellationToken.None).ConfigureAwait(false);
        _audioSink.Dispose();
        _sessionGate.Dispose();
        _disposed = true;
    }

    internal static IReadOnlyList<FabricRomResource> BuildRomResources(System6NativeRomSettings settings)
    {
        var resources = new List<FabricRomResource>(8);
        AddRomRole(resources, settings.ProgramRomPaths, FabricRomRole.Program);
        AddRomRole(resources, settings.SoundRomPaths, FabricRomRole.Sound);
        return resources;
    }

    internal static IReadOnlyList<string> BuildCoinConfigurationDiagnostics(FabricAmberConfiguration configuration)
    {
        var lines = new List<string>(configuration.CoinChannels.Count + 1)
        {
            $"[Coin Config Fabric v2] size={Marshal.SizeOf<AmberCoinsNative>()} version=2 channelMask=0x{configuration.CoinChannelApplyMask:X8} routeMask=0x{configuration.CoinRouteApplyMask:X8} style={(uint)configuration.CommunicationStyle} invert={(configuration.CommunicationInvert ? 1 : 0)} cycles={configuration.PulseCycles} edc={(configuration.EdcEnabled ? 1 : 0)}"
        };
        lines.AddRange(configuration.CoinChannels.Select(channel =>
            $"[Coin Config Fabric v2] slot={channel.Index} index={channel.Index} enabled={(channel.Enabled ? 1 : 0)} value={channel.Value} lockoutInvert={(channel.LockoutInvert ? 1 : 0)} reserved=0"));
        return lines;
    }

    private static void WriteCoinConfigurationDiagnostics(FabricAmberConfiguration configuration)
    {
        foreach (var line in BuildCoinConfigurationDiagnostics(configuration))
            Debug.WriteLine(line);
    }

    private static void AddRomRole(List<FabricRomResource> resources, IReadOnlyList<string> paths, FabricRomRole role)
    {
        if (paths.Count > 4)
            throw new InvalidOperationException($"Fabric supports at most four {role} ROM slots.");
        var sawBlank = false;
        for (var index = 0; index < paths.Count; index++)
        {
            var path = paths[index];
            if (string.IsNullOrWhiteSpace(path))
            {
                sawBlank = true;
                continue;
            }
            if (sawBlank)
                throw new InvalidOperationException($"Fabric {role} ROM slot {index} is configured after a blank slot; slots must be contiguous from zero.");
            resources.Add(new(role, checked((uint)index), path));
        }
    }

    public async Task UpdateAsync(CancellationToken cancellationToken)
    {
        Exception? failure = null;
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        if (State != EmulationBackendState.Running)
        {
            ResetElapsedUpdateBaseline();
            return;
        }

        await _sessionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (State != EmulationBackendState.Running || _session is null)
            {
                ResetElapsedUpdateBaseline();
                return;
            }

            var now = _clock.GetTimestamp();
            if (_elapsedUpdateBaselineTimestamp is not long previous)
            {
                _elapsedUpdateBaselineTimestamp = now;
                return;
            }

            _elapsedUpdateBaselineTimestamp = now;
            var elapsedTicks = now - previous;
            if (elapsedTicks <= 0)
                return;

            var rawNanoseconds = FabricElapsedTime.TicksToNanoseconds(elapsedTicks, _clock.Frequency);
            var advanceNanoseconds = Math.Min(rawNanoseconds, checked((ulong)_nativeDisplayFrameNanoseconds));
            RecordElapsedAdvanceDiagnostics(rawNanoseconds, advanceNanoseconds);
            if (advanceNanoseconds == 0)
                return;

            var session = RequireSession();
            session.Advance(advanceNanoseconds);
            ProcessInputs(session);
            PublishSnapshot(session.GetSnapshot());
            ReadAvailableAudio(session);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            failure = exception;
        }
        finally
        {
            _sessionGate.Release();
        }

        if (failure is null)
            return;

        LastFailure = failure;
        LogException("Fabric editor elapsed-time update failed.", failure);
        try
        {
            await CleanupResourcesAsync().ConfigureAwait(false);
        }
        catch (Exception cleanupException)
        {
            LogException("Fabric cleanup after update failure also failed.", cleanupException);
        }
        SetState(EmulationBackendState.Failed);
    }

    private void ResetElapsedUpdateBaseline() => _elapsedUpdateBaselineTimestamp = null;

    private void RecordElapsedAdvanceDiagnostics(ulong rawNanoseconds, ulong advanceNanoseconds)
    {
        Interlocked.Increment(ref _elapsedAdvanceCount);
        if (advanceNanoseconds < rawNanoseconds)
            Interlocked.Increment(ref _elapsedClampedUpdateCount);
        var boundedRaw = rawNanoseconds > long.MaxValue ? long.MaxValue : (long)rawNanoseconds;
        long observed;
        do
        {
            observed = Interlocked.Read(ref _maxRawElapsedNanoseconds);
            if (boundedRaw <= observed)
                return;
        } while (Interlocked.CompareExchange(ref _maxRawElapsedNanoseconds, boundedRaw, observed) != observed);
    }

    private void ResetElapsedUpdateDiagnostics()
    {
        Interlocked.Exchange(ref _elapsedAdvanceCount, 0);
        Interlocked.Exchange(ref _elapsedClampedUpdateCount, 0);
        Interlocked.Exchange(ref _maxRawElapsedNanoseconds, 0);
        Interlocked.Exchange(ref _audioFramesReturned, 0);
        Interlocked.Exchange(ref _audioReadCount, 0);
    }

    private void WriteElapsedUpdateStopSummary()
    {
        var advances = Interlocked.Read(ref _elapsedAdvanceCount);
        if (advances == 0)
            return;
        Debug.WriteLine($"Fabric elapsed update summary: advances={advances}, clampedUpdates={Interlocked.Read(ref _elapsedClampedUpdateCount)}, maxRawElapsedMs={_maxRawElapsedNanoseconds / 1_000_000.0:F3}, audioReads={Interlocked.Read(ref _audioReadCount)}, audioFrames={Interlocked.Read(ref _audioFramesReturned)}.");
    }

    private void ConfigureAudio(IFabricMachineSession session)
    {
        if (!session.Capabilities.Has(FabricCapability.Audio))
            return;
        var format = session.GetAudioFormat();
        if (format.SampleRate == 0 || format.ChannelCount == 0 || format is not
            { BitsPerSample: 16, Interleaved: true, SignedSamples: true, LittleEndian: true })
            throw new NotSupportedException($"Unsupported Fabric audio format: {format}.");
        _audioFormat = format;
        var frameCapacity = CalculateAudioFramesPerNativeDisplayFrame(checked((int)format.SampleRate), System6FallbackNativeRefreshHz);
        _audioBuffer = new short[checked(frameCapacity * (int)format.ChannelCount)];
        _audioSink.Start(new(
            checked((int)format.SampleRate),
            checked((int)format.ChannelCount),
            checked((int)format.BitsPerSample)));
        _audioStarted = true;
    }

    internal static int CalculateAudioFramesPerNativeDisplayFrame(int sampleRate, double refreshHz)
    {
        if (sampleRate <= 0)
            throw new ArgumentOutOfRangeException(nameof(sampleRate));
        if (!IsValidRefreshRate(refreshHz))
            throw new ArgumentOutOfRangeException(nameof(refreshHz), "Native display refresh rate must be finite and greater than zero.");
        return checked((int)Math.Ceiling(sampleRate / refreshHz));
    }

    internal static long ResolveNativeDisplayFrameNanoseconds(double refreshHz)
    {
        if (!IsValidRefreshRate(refreshHz))
            throw new ArgumentOutOfRangeException(nameof(refreshHz), "Native display refresh rate must be finite and greater than zero.");
        return checked((long)Math.Ceiling(1_000_000_000.0 / refreshHz));
    }

    internal static int CalculateAudioFramesPerPushChunk(int sampleRate)
    {
        if (sampleRate <= 0)
            throw new ArgumentOutOfRangeException(nameof(sampleRate));
        return checked((sampleRate * AudioPushChunkMilliseconds + 999) / 1000);
    }

    private static bool IsValidRefreshRate(double refreshHz) =>
        !double.IsNaN(refreshHz) && !double.IsInfinity(refreshHz) && refreshHz > 0;

    private void ProcessInputs(IFabricMachineSession session)
    {
        while (_inputCommands.TryDequeue(out var input))
        {
            session.SubmitInput(new($"oasis.switch.{input.Index}", input.Index, FabricInputKind.Digital, input.Active));
            if (input.Active)
                _assertedInputs.Add(input.Index);
            else
                _assertedInputs.Remove(input.Index);
        }
    }

    private void ReadAvailableAudio(IFabricMachineSession session)
    {
        if (_audioFormat is not { } format)
            return;
        var frameCapacity = _audioBuffer.Length / format.ChannelCount;
        var totalFrameLimit = checked(frameCapacity * AudioDrainMaxDisplayFramesPerUpdate);
        var totalFrames = 0;
        for (var read = 0; read < AudioDrainMaxReadsPerUpdate && totalFrames < totalFrameLimit; read++)
        {
            var framesWritten = session.ReadAudio(_audioBuffer, frameCapacity);
            if (framesWritten <= 0)
                break;
            if (framesWritten > frameCapacity)
                throw new InvalidOperationException($"Fabric returned {framesWritten} audio frames for a {frameCapacity}-frame read buffer.");
            PushAudioFramesInSmallChunks(format, framesWritten);
            Interlocked.Increment(ref _audioReadCount);
            Interlocked.Add(ref _audioFramesReturned, framesWritten);
            totalFrames = checked(totalFrames + framesWritten);
            if (framesWritten < frameCapacity)
                break;
        }
    }

    private void PushAudioFramesInSmallChunks(FabricAudioFormat format, int framesWritten)
    {
        var channelCount = format.ChannelCount;
        var frameOffset = 0;
        var framesPerChunk = CalculateAudioFramesPerPushChunk(checked((int)format.SampleRate));
        while (frameOffset < framesWritten)
        {
            var chunkFrames = Math.Min(framesPerChunk, framesWritten - frameOffset);
            var sampleOffset = checked(frameOffset * channelCount);
            var sampleCount = checked(chunkFrames * channelCount);
            var bytes = MemoryMarshal.AsBytes(_audioBuffer.AsSpan(sampleOffset, sampleCount));
            if (!bytes.IsEmpty)
                _audioSink.PushPcm(bytes);
            frameOffset = checked(frameOffset + chunkFrames);
        }
    }

    internal void PublishSnapshot(FabricMachineSnapshot snapshot)
    {
        foreach (var lamp in snapshot.Lamps)
        {
            var value = (lamp.LogicalState, lamp.Brightness);
            if (_lamps.TryGetValue(lamp.NumericalIndex, out var previous) && previous == value)
                continue;
            _lamps[lamp.NumericalIndex] = value;
            LampChanged?.Invoke(this, new(lamp.NumericalIndex,
                lamp.LogicalState ? Math.Max(1, (int)Math.Round(lamp.Brightness * 255)) : 0));
        }
        foreach (var reel in snapshot.Reels)
        {
            // Fabric and Oasis both use the native zero-based reel identifier.
            if (_reels.TryGetValue(reel.NumericalIndex, out var previous) && previous == reel.Position)
                continue;
            _reels[reel.NumericalIndex] = reel.Position;
            ReelChanged?.Invoke(this, new(reel.NumericalIndex, reel.Position));
        }
        for (var displayOrdinal = 0; displayOrdinal < snapshot.CharacterDisplays.Count; displayOrdinal++)
        {
            var display = snapshot.CharacterDisplays[displayOrdinal];
            var brightnessIdentity = new DisplayBrightnessIdentity(
                DisplayOutputFamily.Character, display.Identifier, displayOrdinal);
            if (!_displayBrightnessOutputs.TryGetValue(brightnessIdentity, out var previousBrightness)
                || previousBrightness != display.Brightness)
            {
                _displayBrightnessOutputs[brightnessIdentity] = display.Brightness;
                var displayBaseIndex = checked(displayOrdinal * FabricAbi.CharacterCapacity);
                VfdBrightnessChanged?.Invoke(this, new(displayBaseIndex, display.Brightness));
            }
            for (var position = 0; position < display.Characters.Length; position++)
            {
                var identity = new DisplayOutputIdentity(DisplayOutputFamily.Character, display.Identifier, displayOrdinal, position);
                // Oasis's alpha event carries one integer. Convert Amber's low 16 segment bits,
                // then retain punctuation in bits 16/17.
                var oasisMask = System6AlphaSegmentMapper.MapNativeMaskToOasisMask(
                    unchecked((int)display.Characters[position]));
                var publishedMask = unchecked((uint)oasisMask) | ((uint)display.Attributes[position] << 16);
                if (_displayOutputs.TryGetValue(identity, out var previous) && previous == publishedMask)
                    continue;
                _displayOutputs[identity] = publishedMask;
                var eventIndex = checked(displayOrdinal * FabricAbi.CharacterCapacity + position);
                SegmentChanged?.Invoke(this, new(eventIndex, unchecked((int)publishedMask), SegmentOutputType.NativeAlpha));
            }
        }
        var oasisCellId = 0;
        for (var displayOrdinal = 0; displayOrdinal < snapshot.SegmentDisplays.Count; displayOrdinal++)
        {
            var display = snapshot.SegmentDisplays[displayOrdinal];
            for (var position = 0; position < display.SegmentMasks.Length; position++)
            {
                var identity = new DisplayOutputIdentity(DisplayOutputFamily.Segment, display.Identifier, displayOrdinal, position);
                var oasisMask = System6SevenSegmentMapper.MapNativeMaskToOasisMask(
                    unchecked((int)display.SegmentMasks[position]));
                var publishedMask = unchecked((uint)oasisMask);
                if (_displayOutputs.TryGetValue(identity, out var previous) && previous == publishedMask)
                {
                    oasisCellId++;
                    continue;
                }
                _displayOutputs[identity] = publishedMask;
                SegmentChanged?.Invoke(this, new(oasisCellId, oasisMask, SegmentOutputType.Digit));
                if (Debugger.IsAttached)
                {
                    Debug.WriteLine(
                        $"Fabric segment changed: identifier='{display.Identifier}', ordinal={displayOrdinal}, " +
                        $"position={position}, oasisCellId={oasisCellId}, mask=0x{publishedMask:X}.");
                }
                oasisCellId++;
            }
        }
    }

    private async Task CleanupResourcesAsync()
    {
        await _sessionGate.WaitAsync().ConfigureAwait(false);
        List<Exception>? failures = null;
        try
        {
            if (_session is not null)
            {
                TryCleanup(ReleaseAssertedInputs, ref failures);
                if (!_shutdown)
                {
                    TryCleanup(_session.Shutdown, ref failures);
                    _shutdown = true;
                }
                TryCleanup(_session.Dispose, ref failures);
                _session = null;
            }
            if (_audioStarted)
            {
                TryCleanup(_audioSink.Stop, ref failures);
                _audioStarted = false;
            }
            if (_runtime is not null)
                TryCleanup(_runtime.Dispose, ref failures);
            _runtime = null;
            _audioFormat = null;
            ResetElapsedUpdateBaseline();
            ClearPendingInputs();
        }
        finally
        {
            _sessionGate.Release();
        }
        if (failures is { Count: > 0 })
            throw failures.Count == 1 ? failures[0] : new AggregateException("Multiple Fabric cleanup operations failed.", failures);
    }

    private static void TryCleanup(Action action, ref List<Exception>? failures)
    {
        try { action(); }
        catch (Exception exception) { (failures ??= []).Add(exception); }
    }

    private void LogException(string message, Exception exception) =>
        _errorLogger($"[Error] {message}{Environment.NewLine}{exception}");

    private static void WriteDebugError(string message) => Debug.WriteLine(message);

    private void ReleaseAssertedInputs()
    {
        var session = _session;
        if (session is not null)
            foreach (var index in _assertedInputs)
                session.SubmitInput(new($"oasis.switch.{index}", index, FabricInputKind.Digital, false));
        _assertedInputs.Clear();
    }

    private void ClearPendingInputs()
    {
        while (_inputCommands.TryDequeue(out _)) { }
    }

    private void ClearOutputCaches()
    {
        _lamps.Clear();
        _reels.Clear();
        _displayOutputs.Clear();
        _displayBrightnessOutputs.Clear();
    }

    private IFabricMachineSession RequireSession() =>
        _session ?? throw new InvalidOperationException("Fabric session is not active.");

    private void EnsureAcceptingOperations()
    {
        if (State is not EmulationBackendState.Running and not EmulationBackendState.Paused)
            throw new InvalidOperationException($"Fabric backend does not accept operations while it is {State}.");
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(FabricEmulationBackend));
    }

    private void SetState(EmulationBackendState state)
    {
        lock (_stateGate)
            _state = state;
        StateChanged?.Invoke(this, state);
    }

    private readonly record struct InputCommand(int Index, bool Active);
    private readonly record struct DisplayOutputIdentity(DisplayOutputFamily Family, string Identifier, int DisplayOrdinal, int Position);
    private readonly record struct DisplayBrightnessIdentity(DisplayOutputFamily Family, string Identifier, int DisplayOrdinal);
    private enum DisplayOutputFamily { Character, Segment }
}
