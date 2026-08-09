using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace OasisEditor;

public sealed class FabricEmulationBackend : IEmulationBackend
{
    private const string AmberBackendKind = "amber";
    private const string JpmSystem6MachineIdentifier = "jpm-system6";
    private const string BarcrestMpu5MachineIdentifier = "barcrest-mpu5";
    private const int EmulationPumpHz = 1000;
    private const ulong NanosecondsPerPump = 1_000_000;
    private const int MaxCatchUpSlices = 64;
    private const int ExceptionalDelayCapMilliseconds = 500;
    private const int VisualSnapshotCadenceSlices = 16;
    private const long NanosecondsPerMillisecond = 1_000_000;
    private static readonly TimeSpan PumpInterval = TimeSpan.FromMilliseconds(1);
    private static readonly EmulationBackendCapabilities BackendCapabilities =
        new(true, true, true, true, false, false, false);

    private readonly string _runtimePath;
    private readonly string _amberPath;
    private readonly Func<string, IFabricRuntimeLibrary> _runtimeFactory;
    private readonly IEmulationAudioSink _audioSink;
    private readonly IFabricClock _clock;
    private readonly Action<string> _errorLogger;
    private readonly Action<string> _infoLogger;
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
    private Thread? _pumpThread;
    private readonly ManualResetEventSlim _runnerWake = new(false);
    private FabricAudioFormat? _audioFormat;
    private short[] _audioBuffer = [];
    private EmulationBackendState _state = EmulationBackendState.Stopped;
    private bool _shutdown;
    private bool _audioStarted;
    private bool _disposed;
    private long _scheduleGeneration;
    private long _runnerStartTimestamp;
    private long _totalEmulationSlices;
    private long _maxCatchUpBatch;
    private long _exceptionalDiscardedTicks;
    private long _fabricAudioFramesRead;
    private string _timingMode = "Unstarted";
    private bool _highResolutionTimerActive;
    private bool _mmcssRegistered;

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
        Action<string>? infoLogger = null)
    {
        _runtimePath = runtimePath;
        _amberPath = amberPath;
        _runtimeFactory = runtimeFactory;
        _audioSink = audioSink;
        _clock = clock;
        _errorLogger = errorLogger ?? WriteDebugError;
        _infoLogger = infoLogger ?? WriteDebugInfo;
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
            var (machineIdentifier, resources, configuration) = request.Platform switch
            {
                FruitMachinePlatformType.Impact when request.System6Configuration is not null =>
                    (JpmSystem6MachineIdentifier, BuildRomResources(request.System6Configuration), (IFabricBackendConfiguration)FabricAmberSystem6Configuration.FromSystem6(request.System6Configuration)),
                FruitMachinePlatformType.MPU5 when request.Mpu5Configuration is not null =>
                    (BarcrestMpu5MachineIdentifier, BuildRomResources(request.Mpu5Configuration), (IFabricBackendConfiguration?)FabricAmberMpu5Configuration.FromMpu5(request.Mpu5Configuration)),
                _ => throw new InvalidOperationException($"Launch settings do not match Fabric platform '{request.Platform}'.")
            };
            cancellationToken.ThrowIfCancellationRequested();
            _runtime = _runtimeFactory(_runtimePath);
            if (configuration is FabricAmberSystem6Configuration system6Configuration)
                WriteCoinConfigurationDiagnostics(system6Configuration);
            _session = _runtime.CreateSession(new FabricLaunchRequest(
                AmberBackendKind, machineIdentifier, _amberPath, resources,
                configuration));

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
            ResetRunnerStatistics();
            LastFailure = null;
            _pumpCancellation = new CancellationTokenSource();
            _runnerWake.Reset();
            _pumpThread = new Thread(() => Pump(_pumpCancellation.Token))
            {
                Name = "Oasis Fabric Emulation Runner",
                IsBackground = true,
                Priority = ThreadPriority.Normal
            };
            _pumpThread.Start();
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

        _pumpCancellation?.Cancel();
        _runnerWake.Set();
        var thread = _pumpThread;
        if (thread is not null && thread.ManagedThreadId != Environment.CurrentManagedThreadId)
        {
            while (thread.IsAlive)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (thread.Join(25))
                    break;
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
        {
            SetState(EmulationBackendState.Paused);
            _runnerWake.Set();
        }
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
            _runnerWake.Set();
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
            _runnerWake.Set();
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
        _runnerWake.Dispose();
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

    internal static IReadOnlyList<FabricRomResource> BuildRomResources(Mpu5NativeRomSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.ProgramRom1Path))
            throw new InvalidOperationException("Current project settings are missing required MPU5 Program ROM 1.");
        var resources = new List<FabricRomResource>(8);
        AddRomRole(resources, settings.ProgramRomPaths, FabricRomRole.Program);
        AddRomRole(resources, settings.SoundRomPaths, FabricRomRole.Sound);
        return resources;
    }

    internal static IReadOnlyList<string> BuildCoinConfigurationDiagnostics(FabricAmberSystem6Configuration configuration)
    {
        var lines = new List<string>(configuration.CoinChannels.Count + 1)
        {
            $"[Coin Config Fabric v2] size={Marshal.SizeOf<AmberCoinsNative>()} version=2 channelMask=0x{configuration.CoinChannelApplyMask:X8} routeMask=0x{configuration.CoinRouteApplyMask:X8} style={(uint)configuration.CommunicationStyle} invert={(configuration.CommunicationInvert ? 1 : 0)} cycles={configuration.PulseCycles} edc={(configuration.EdcEnabled ? 1 : 0)}"
        };
        lines.AddRange(configuration.CoinChannels.Select(channel =>
            $"[Coin Config Fabric v2] slot={channel.Index} index={channel.Index} enabled={(channel.Enabled ? 1 : 0)} value={channel.Value} lockoutInvert={(channel.LockoutInvert ? 1 : 0)} reserved=0"));
        return lines;
    }

    private static void WriteCoinConfigurationDiagnostics(FabricAmberSystem6Configuration configuration)
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

    private void Pump(CancellationToken cancellationToken)
    {
        try
        {
            using var timer = new FabricRunnerTimer(_runnerWake);
            using var mmcss = FabricRunnerMmcssRegistration.Register("Games");
            _timingMode = timer.TimingMode;
            _highResolutionTimerActive = timer.HighResolutionTimerActive;
            _mmcssRegistered = mmcss.Registered;
            var pumpTicks = Math.Max(1L, _clock.Frequency / EmulationPumpHz);
            var nextDeadline = _clock.GetTimestamp();
            var scheduleGeneration = Interlocked.Read(ref _scheduleGeneration);
            var slicesSinceSnapshot = 0;
            while (!cancellationToken.IsCancellationRequested)
            {
                if (State != EmulationBackendState.Running)
                {
                    timer.Wait(PumpInterval, cancellationToken);
                    nextDeadline = _clock.GetTimestamp();
                    scheduleGeneration = Interlocked.Read(ref _scheduleGeneration);
                    slicesSinceSnapshot = 0;
                    continue;
                }

                var currentGeneration = Interlocked.Read(ref _scheduleGeneration);
                if (currentGeneration != scheduleGeneration)
                {
                    nextDeadline = _clock.GetTimestamp();
                    scheduleGeneration = currentGeneration;
                    slicesSinceSnapshot = 0;
                }

                WaitUntil(timer, nextDeadline, cancellationToken);
                var now = _clock.GetTimestamp();
                var exceptionalCapTicks = pumpTicks * ExceptionalDelayCapMilliseconds;
                if (now - nextDeadline > exceptionalCapTicks)
                {
                    Interlocked.Add(ref _exceptionalDiscardedTicks, now - nextDeadline - exceptionalCapTicks);
                    nextDeadline = now - exceptionalCapTicks;
                }

                var availableSlices = CalculateAvailableSlices(now, nextDeadline, pumpTicks);
                if (availableSlices <= 0)
                    continue;

                var writableFrames = _audioStarted ? _audioSink.WritableFrames : int.MaxValue;
                var capacityLimitedSlices = CalculateAudioCapacityLimitedSlices(writableFrames);
                var schedulerLimitedSlices = Math.Min(availableSlices, MaxCatchUpSlices);
                var slices = Math.Min(schedulerLimitedSlices, capacityLimitedSlices);
                if (slices <= 0)
                {
                    timer.Wait(PumpInterval, cancellationToken);
                    continue;
                }
                for (var slice = 0; slice < slices; slice++)
                {
                    WaitSessionGate(cancellationToken);
                    try
                    {
                        var session = RequireSession();
                        session.Advance(NanosecondsPerPump);
                        ProcessInputs(session);
                        ReadAudio(session);
                    }
                    finally
                    {
                        _sessionGate.Release();
                    }

                    nextDeadline += pumpTicks;
                    slicesSinceSnapshot++;
                }

                Interlocked.Add(ref _totalEmulationSlices, slices);
                UpdateMaxCatchUpBatch(slices);

                if (slicesSinceSnapshot >= VisualSnapshotCadenceSlices || slices > 1)
                {
                    PublishLatestSnapshot(cancellationToken);
                    slicesSinceSnapshot = 0;
                }

                if (_clock.GetTimestamp() >= nextDeadline)
                    Thread.Yield();
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
            _runnerWake.Set();
            try
            {
                CleanupResourcesAsync().GetAwaiter().GetResult();
            }
            catch (Exception cleanupException)
            {
                LogException("Fabric cleanup after pump failure failed.", cleanupException);
            }
            SetState(EmulationBackendState.Failed);
        }
    }

    private void WaitUntil(FabricRunnerTimer timer, long deadline, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var remainingTicks = deadline - _clock.GetTimestamp();
            if (remainingTicks <= 0)
                return;
            var remainingMilliseconds = (double)remainingTicks * 1000 / _clock.Frequency;
            if (remainingMilliseconds > 2)
            {
                var wait = TimeSpan.FromMilliseconds(Math.Max(0, remainingMilliseconds - 1));
                timer.Wait(wait, cancellationToken);
                continue;
            }
            var spin = new SpinWait();
            while (_clock.GetTimestamp() < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (spin.Count < 10)
                    spin.SpinOnce();
                else
                    Thread.Yield();
            }
            return;
        }
        cancellationToken.ThrowIfCancellationRequested();
    }

    private void WaitSessionGate(CancellationToken cancellationToken) =>
        _sessionGate.Wait(cancellationToken);

    private void PublishLatestSnapshot(CancellationToken cancellationToken)
    {
        FabricMachineSnapshot snapshot;
        WaitSessionGate(cancellationToken);
        try
        {
            var session = RequireSession();
            snapshot = session.GetSnapshot();
        }
        finally
        {
            _sessionGate.Release();
        }

        PublishSnapshot(snapshot);
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
        var audioFormat = new EmulationAudioFormat(
            checked((int)format.SampleRate),
            checked((int)format.ChannelCount),
            checked((int)format.BitsPerSample));
        _audioSink.Start(audioFormat);
        _audioStarted = true;
        var statistics = _audioSink.GetStatistics();
        _infoLogger($"Fabric audio startup: sampleRate={audioFormat.SampleRate}, channels={audioFormat.Channels}, ringCapacityFrames={statistics.CapacityFrames}, ringCapacityMs={(double)statistics.CapacityFrames * 1000 / audioFormat.SampleRate:F1}, prebufferFrames={statistics.PrebufferThresholdFrames}, prebufferMs={statistics.PrebufferThresholdMilliseconds}, outputBackend=WasapiOut, wasapiLatencyMs={statistics.WasapiLatencyMilliseconds}.");
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
            session.SubmitInput(new($"oasis.switch.{input.Index}", input.Index, FabricInputKind.Digital, input.Active));
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
        Interlocked.Add(ref _fabricAudioFramesRead, framesWritten);
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
            ReelChanged?.Invoke(this, new(reel.NumericalIndex, reel.Position, ReelPositionConvention.Amber));
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
            EmulationAudioPlaybackStatistics audioStatistics = default;
            if (_audioStarted)
            {
                TryCleanup(_audioSink.Stop, ref failures);
                audioStatistics = _audioSink.GetStatistics();
                _audioStarted = false;
            }
            EmitStopSummary(audioStatistics);
            if (_runtime is not null)
                TryCleanup(_runtime.Dispose, ref failures);
            _runtime = null;
            _audioFormat = null;
            _pumpCancellation?.Dispose();
            _pumpCancellation = null;
            _pumpThread = null;
            ClearPendingInputs();
        }
        finally
        {
            _sessionGate.Release();
        }
        if (failures is { Count: > 0 })
            throw failures.Count == 1 ? failures[0] : new AggregateException("Multiple Fabric cleanup operations failed.", failures);
    }

    private void ResetRunnerStatistics()
    {
        Interlocked.Exchange(ref _runnerStartTimestamp, _clock.GetTimestamp());
        Interlocked.Exchange(ref _totalEmulationSlices, 0);
        Interlocked.Exchange(ref _maxCatchUpBatch, 0);
        Interlocked.Exchange(ref _exceptionalDiscardedTicks, 0);
        Interlocked.Exchange(ref _fabricAudioFramesRead, 0);
        _timingMode = "Unstarted";
        _highResolutionTimerActive = false;
        _mmcssRegistered = false;
    }

    internal static int CalculateSafeFramesPerSlice(int sampleRate)
    {
        if (sampleRate <= 0)
            throw new ArgumentOutOfRangeException(nameof(sampleRate));
        return checked((sampleRate + EmulationPumpHz - 1) / EmulationPumpHz);
    }

    internal int CalculateAudioCapacityLimitedSlices(int writableFrames)
    {
        if (!_audioStarted || _audioFormat is not { } format)
            return MaxCatchUpSlices;
        if (writableFrames <= 0)
            return 0;
        return CalculateAudioCapacityLimitedSlices(checked((int)format.SampleRate), writableFrames);
    }

    internal static int CalculateAudioCapacityLimitedSlices(int sampleRate, int writableFrames)
    {
        if (writableFrames <= 0)
            return 0;
        return writableFrames / CalculateSafeFramesPerSlice(sampleRate);
    }

    internal static int CalculateAvailableSlices(long now, long nextDeadline, long pumpTicks)
    {
        if (now < nextDeadline)
            return 0;
        return checked((int)Math.Min(int.MaxValue, ((now - nextDeadline) / pumpTicks) + 1));
    }

    private static void UpdateMax(ref long target, long value)
    {
        var current = Interlocked.Read(ref target);
        while (value > current)
        {
            var previous = Interlocked.CompareExchange(ref target, value, current);
            if (previous == current) return;
            current = previous;
        }
    }

    private void UpdateMaxCatchUpBatch(int slices)
    {
        var current = Interlocked.Read(ref _maxCatchUpBatch);
        while (slices > current)
        {
            var previous = Interlocked.CompareExchange(ref _maxCatchUpBatch, slices, current);
            if (previous == current)
                return;
            current = previous;
        }
    }

    private void EmitStopSummary(EmulationAudioPlaybackStatistics audioStatistics)
    {
        var start = Interlocked.Read(ref _runnerStartTimestamp);
        var wallMs = start == 0 ? 0 : (double)(_clock.GetTimestamp() - start) * 1000 / _clock.Frequency;
        var slices = Interlocked.Read(ref _totalEmulationSlices);
        var emulatedMs = slices;
        var ratio = wallMs > 0 ? emulatedMs / wallMs : 0;
        var discardedMs = (double)Interlocked.Read(ref _exceptionalDiscardedTicks) * 1000 / _clock.Frequency;
        _infoLogger(FormatStopSummary(
            wallMs,
            emulatedMs,
            ratio,
            slices,
            Interlocked.Read(ref _maxCatchUpBatch),
            discardedMs,
            Interlocked.Read(ref _fabricAudioFramesRead),
            _timingMode,
            _highResolutionTimerActive,
            _mmcssRegistered,
            audioStatistics));
    }

    internal static string FormatStopSummary(
        double wallMilliseconds,
        double emulatedMilliseconds,
        double ratio,
        long slices,
        long maxCatchUpBatch,
        double discardedMilliseconds,
        long fabricAudioFrames,
        string timingMode,
        bool highResolutionTimerActive,
        bool mmcssRegistered,
        EmulationAudioPlaybackStatistics audioStatistics) =>
        $"Fabric stop summary: wallMs={wallMilliseconds:F1}, emulatedMs={emulatedMilliseconds:F1}, ratio={ratio:F5}, slices={slices}, maximumCatchUpBatch={maxCatchUpBatch}, discardedMs={discardedMilliseconds:F1}, fabricAudioFrames={fabricAudioFrames}, ringWritten={audioStatistics.RingFramesWritten}, ringRejected={audioStatistics.RingFramesRejected}, devicePcmFrames={audioStatistics.DeviceFramesDelivered}, silenceFrames={audioStatistics.SilenceFrames}, underrunEpisodes={audioStatistics.UnderrunEpisodes}, minimumRingFrames={audioStatistics.MinimumRingFrames}, ringCapacityFrames={audioStatistics.CapacityFrames}, timingMode={timingMode}, highResolutionTimerActive={highResolutionTimerActive}, mmcssRegistered={mmcssRegistered}.";

    private static void TryCleanup(Action action, ref List<Exception>? failures)
    {
        try { action(); }
        catch (Exception exception) { (failures ??= []).Add(exception); }
    }

    private void LogException(string message, Exception exception) =>
        _errorLogger($"[Error] {message}{Environment.NewLine}{exception}");

    private static void WriteDebugError(string message) => Debug.WriteLine(message);
    private static void WriteDebugInfo(string message) => Debug.WriteLine(message);

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
