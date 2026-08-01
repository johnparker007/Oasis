using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace OasisEditor;

public sealed class FabricEmulationBackend : IEmulationBackend
{
    private const string AmberBackendKind = "amber";
    private const string JpmSystem6MachineIdentifier = "jpm-system6";
    private const int EmulationPumpHz = 1000;
    private const ulong NanosecondsPerPump = 1_000_000;
    private static readonly TimeSpan PumpInterval = TimeSpan.FromMilliseconds(1);
    private static readonly EmulationBackendCapabilities BackendCapabilities =
        new(true, true, true, true, false, false, false);

    private readonly string _runtimePath;
    private readonly string _amberPath;
    private readonly Func<string, IFabricRuntimeLibrary> _runtimeFactory;
    private readonly IEmulationAudioSink _audioSink;
    private readonly IFabricClock _clock;
    private readonly Action<string> _errorLogger;
    private readonly Action<string> _diagnosticLogger;
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
    private CancellationTokenSource? _pumpCancellation;
    private Task? _pumpTask;
    private FabricAudioFormat? _audioFormat;
    private short[] _audioBuffer = [];
    private EmulationBackendState _state = EmulationBackendState.Stopped;
    private bool _shutdown;
    private bool _audioStarted;
    private bool _disposed;
    private long _scheduleGeneration;

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
        Action<string>? errorLogger = null,
        Action<string>? diagnosticLogger = null)
    {
        _runtimePath = runtimePath;
        _amberPath = amberPath;
        _runtimeFactory = runtimeFactory;
        _audioSink = audioSink;
        _clock = clock;
        _errorLogger = errorLogger ?? WriteDebugError;
        _diagnosticLogger = diagnosticLogger ?? WriteDebugError;
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
            LogCoinConfiguration(settings);
            cancellationToken.ThrowIfCancellationRequested();
            _runtime = _runtimeFactory(_runtimePath);
            _session = _runtime.CreateSession(new FabricLaunchRequest(
                AmberBackendKind, JpmSystem6MachineIdentifier, _amberPath, resources,
                FabricAmberConfiguration.FromSystem6(settings)));

            await _sessionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                _session.Initialise();
                ConfigureAudio(_session);
                Interlocked.Increment(ref _scheduleGeneration);
            }
            finally
            {
                _sessionGate.Release();
            }

            _shutdown = false;
            LastFailure = null;
            _pumpCancellation = new CancellationTokenSource();
            _pumpTask = Task.Run(() => PumpAsync(_pumpCancellation.Token), CancellationToken.None);
            SetState(EmulationBackendState.Running);
        }
        catch
        {
            await CleanupResourcesAsync().ConfigureAwait(false);
            SetState(EmulationBackendState.Failed);
            throw;
        }
    }

    private void LogCoinConfiguration(System6NativeRomSettings settings)
    {
        foreach (var coin in settings.Coins.Where(coin => coin.Enabled))
        {
            _diagnosticLogger(
                $"[Coin Config Source] channelIndex={coin.Num} enabled={FormatBoolean(coin.CoinEnable != 0)} value={coin.CoinValue} " +
                $"lockoutValue={coin.LockoutValue} lockoutInvert={FormatBoolean(coin.LockoutInvert != 0)}");
        }

        var configuration = FabricAmberConfiguration.FromSystem6(settings);
        foreach (var channel in configuration.CoinChannels)
        {
            _diagnosticLogger(
                $"[Coin Config Fabric] channelIndex={channel.Index} enabled={FormatBoolean(channel.Enabled)} value={channel.Value} " +
                $"lockoutValue={channel.LockoutValue} lockoutInvert={FormatBoolean(channel.LockoutInvert)}");
        }
    }

    private static string FormatBoolean(bool value) => value ? "true" : "false";

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_disposed || State == EmulationBackendState.Stopped)
            return;
        if (State != EmulationBackendState.Failed)
            SetState(EmulationBackendState.Stopping);

        if (_pumpCancellation is not null)
            await _pumpCancellation.CancelAsync().ConfigureAwait(false);
        if (_pumpTask is not null && Task.CurrentId != _pumpTask.Id)
        {
            try
            {
                await _pumpTask.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // The caller may stop waiting, but native cleanup below is never abandoned.
            }
            catch
            {
                // LastFailure retains the pump exception; cleanup must continue.
            }
        }
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
            Interlocked.Increment(ref _scheduleGeneration);
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
            Interlocked.Increment(ref _scheduleGeneration);
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

    private async Task PumpAsync(CancellationToken cancellationToken)
    {
        try
        {
            var pumpTicks = Math.Max(1L, _clock.Frequency / EmulationPumpHz);
            var nextDeadline = _clock.GetTimestamp();
            var scheduleGeneration = Interlocked.Read(ref _scheduleGeneration);
            while (!cancellationToken.IsCancellationRequested)
            {
                if (State != EmulationBackendState.Running)
                {
                    await Task.Delay(PumpInterval, cancellationToken).ConfigureAwait(false);
                    nextDeadline = _clock.GetTimestamp();
                    scheduleGeneration = Interlocked.Read(ref _scheduleGeneration);
                    continue;
                }

                var currentGeneration = Interlocked.Read(ref _scheduleGeneration);
                if (currentGeneration != scheduleGeneration)
                {
                    nextDeadline = _clock.GetTimestamp();
                    scheduleGeneration = currentGeneration;
                }

                await _sessionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    var session = RequireSession();
                    session.Advance(NanosecondsPerPump);
                    ProcessInputs(session);
                    PublishSnapshot(session.GetSnapshot());
                    ReadAudio(session);
                }
                finally
                {
                    _sessionGate.Release();
                }

                nextDeadline += pumpTicks;
                var now = _clock.GetTimestamp();
                // Execute the current slice, then abandon catch-up once over three ticks late.
                if (now - nextDeadline > pumpTicks * 3)
                    nextDeadline = now + pumpTicks;

                var remainingTicks = nextDeadline - _clock.GetTimestamp();
                if (remainingTicks > 0)
                {
                    var remaining = TimeSpan.FromSeconds((double)remainingTicks / _clock.Frequency);
                    await Task.Delay(remaining, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    await Task.Yield();
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            LastFailure = exception;
            LogException("Fabric emulation pump failed.", exception);
            _pumpCancellation?.Cancel();
            try
            {
                await CleanupResourcesAsync().ConfigureAwait(false);
            }
            catch (Exception cleanupException)
            {
                LogException("Fabric cleanup after pump failure also failed.", cleanupException);
            }
            SetState(EmulationBackendState.Failed);
        }
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
        var frameCapacity = CalculateAudioFramesPerTick(checked((int)format.SampleRate));
        _audioBuffer = new short[checked(frameCapacity * (int)format.ChannelCount)];
        _audioSink.Start(new(
            checked((int)format.SampleRate),
            checked((int)format.ChannelCount),
            checked((int)format.BitsPerSample)));
        _audioStarted = true;
    }

    internal static int CalculateAudioFramesPerTick(int sampleRate)
    {
        if (sampleRate <= 0)
            throw new ArgumentOutOfRangeException(nameof(sampleRate));
        return checked((sampleRate + EmulationPumpHz - 1) / EmulationPumpHz);
    }

    private void ProcessInputs(IFabricMachineSession session)
    {
        while (_inputCommands.TryDequeue(out var input))
        {
            session.SubmitInput(new($"oasis.switch.{input.Index}", input.Index, input.Active));
            if (input.Active)
                _assertedInputs.Add(input.Index);
            else
                _assertedInputs.Remove(input.Index);
        }
    }

    private void ReadAudio(IFabricMachineSession session)
    {
        if (_audioFormat is not { } format)
            return;
        var frameCapacity = _audioBuffer.Length / format.ChannelCount;
        var framesWritten = session.ReadAudio(_audioBuffer, frameCapacity);
        var sampleCount = checked(framesWritten * format.ChannelCount);
        var bytes = MemoryMarshal.AsBytes(_audioBuffer.AsSpan(0, sampleCount));
        if (!bytes.IsEmpty)
            _audioSink.PushPcm(bytes);
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
            _pumpCancellation?.Dispose();
            _pumpCancellation = null;
            _pumpTask = null;
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
                session.SubmitInput(new($"oasis.switch.{index}", index, false));
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
