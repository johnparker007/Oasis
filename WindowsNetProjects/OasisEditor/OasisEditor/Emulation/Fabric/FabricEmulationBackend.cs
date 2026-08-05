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
    private long _catchUpSlices;
    private long _maxCatchUpBatch;
    private long _exceptionalDiscardedTicks;
    private long _fabricAudioFramesRead;
    private long _zeroAudioSlices;
    private long _minFramesPerSlice = long.MaxValue;
    private long _maxFramesPerSlice;
    private long _capacityLimitedCatchUpBatches;
    private long _retainedDebtIterations;
    private long _maxRetainedSlices;
    private long _writableFramesAtLargestCatchUpBatch;
    private long _runnerThreadId;
    private ThreadPriority _runnerThreadPriority = ThreadPriority.Normal;
    private long _singleSliceBatches;
    private long _multiSliceBatches;
    private long _timedWaitCount;
    private long _timedWaitLatenessTicksTotal;
    private long _maxWakeLatenessTicks;
    private long _wakeLateOver2Ms;
    private long _wakeLateOver5Ms;
    private long _wakeLateOver10Ms;
    private long _wakeLateOver20Ms;
    private long _wakeLateOver50Ms;
    private long _wakeLateOver100Ms;
    private long _currentConsecutiveLateWakes;
    private long _longestConsecutiveLateWakes;
    private long _maxAdvanceDurationTicks;
    private long _maxReadAudioDurationTicks;
    private long _maxSnapshotDurationTicks;
    private long _maxPublishDurationTicks;
    private long _maxInputDurationTicks;
    private long _maxSessionGateWaitTicks;
    private long _debtContinuationCount;
    private long _capacityLimitedContinuationCount;
    private long _yieldContinuationCount;
    private string _timingMode = "Unstarted";
    private bool _highResolutionTimerActive;
    private bool _mmcssRegistered;
    private long _advanceCalls;
    private long _advanceDurationTicksTotal;
    private long _advanceOver2Ms;
    private long _advanceOver5Ms;
    private long _advanceOver10Ms;
    private long _advanceOver20Ms;
    private long _advanceOver50Ms;
    private long _advanceOver100Ms;
    private long _readAudioCalls;
    private long _readAudioDurationTicksTotal;
    private long _snapshotCalls;
    private long _snapshotDurationTicksTotal;
    private long _publishCalls;
    private long _publishDurationTicksTotal;
    private readonly ConcurrentQueue<StallTraceRecord> _stallTrace = new();
    private string _lastStallTracePath = string.Empty;

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

    private void Pump(CancellationToken cancellationToken)
    {
        try
        {
            Interlocked.Exchange(ref _runnerThreadId, Environment.CurrentManagedThreadId);
            _runnerThreadPriority = Thread.CurrentThread.Priority;
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

                var waitedForDeadline = WaitUntil(timer, nextDeadline, cancellationToken);
                var now = _clock.GetTimestamp();
                if (waitedForDeadline)
                    ObserveWakeLateness(now - nextDeadline);
                else if (now >= nextDeadline)
                    Interlocked.Increment(ref _debtContinuationCount);
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
                    Interlocked.Increment(ref _capacityLimitedCatchUpBatches);
                    Interlocked.Increment(ref _capacityLimitedContinuationCount);
                    Interlocked.Increment(ref _retainedDebtIterations);
                    UpdateMaxRetainedSlices(availableSlices);
                    timer.Wait(PumpInterval, cancellationToken);
                    continue;
                }
                if (slices < schedulerLimitedSlices)
                {
                    Interlocked.Increment(ref _capacityLimitedCatchUpBatches);
                    Interlocked.Increment(ref _capacityLimitedContinuationCount);
                    Interlocked.Increment(ref _retainedDebtIterations);
                    UpdateMaxRetainedSlices(availableSlices - slices);
                }

                for (var slice = 0; slice < slices; slice++)
                {
                    WaitSessionGate(cancellationToken);
                    try
                    {
                        var session = RequireSession();
                        var advanceStart = _clock.GetTimestamp();
                        session.Advance(NanosecondsPerPump);
                        ObserveOperationDuration("SessionAdvance", _clock.GetTimestamp() - advanceStart, ref _advanceCalls, ref _advanceDurationTicksTotal, ref _maxAdvanceDurationTicks);

                        var inputStart = _clock.GetTimestamp();
                        ProcessInputs(session);
                        UpdateMaxDuration(ref _maxInputDurationTicks, _clock.GetTimestamp() - inputStart);

                        var audioStart = _clock.GetTimestamp();
                        ReadAudio(session);
                        ObserveOperationDuration("ReadAudio", _clock.GetTimestamp() - audioStart, ref _readAudioCalls, ref _readAudioDurationTicksTotal, ref _maxReadAudioDurationTicks);
                    }
                    finally
                    {
                        _sessionGate.Release();
                    }

                    nextDeadline += pumpTicks;
                    slicesSinceSnapshot++;
                }

                Interlocked.Add(ref _totalEmulationSlices, slices);
                if (slices == 1)
                    Interlocked.Increment(ref _singleSliceBatches);
                else
                {
                    Interlocked.Increment(ref _multiSliceBatches);
                    Interlocked.Add(ref _catchUpSlices, slices - 1);
                }
                if (slices > Interlocked.Read(ref _maxCatchUpBatch))
                    Interlocked.Exchange(ref _writableFramesAtLargestCatchUpBatch, writableFrames);
                UpdateMaxCatchUpBatch(slices);

                if (slicesSinceSnapshot >= VisualSnapshotCadenceSlices || slices > 1)
                {
                    PublishLatestSnapshot(cancellationToken);
                    slicesSinceSnapshot = 0;
                }

                if (_clock.GetTimestamp() >= nextDeadline)
                {
                    Interlocked.Increment(ref _yieldContinuationCount);
                    Thread.Yield();
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
            _runnerWake.Set();
            SetState(EmulationBackendState.Failed);
        }
    }

    private bool WaitUntil(FabricRunnerTimer timer, long deadline, CancellationToken cancellationToken)
    {
        var waited = false;
        while (!cancellationToken.IsCancellationRequested)
        {
            var remainingTicks = deadline - _clock.GetTimestamp();
            if (remainingTicks <= 0)
                return waited;
            var remainingMilliseconds = (double)remainingTicks * 1000 / _clock.Frequency;
            if (remainingMilliseconds > 2)
            {
                var wait = TimeSpan.FromMilliseconds(Math.Max(0, remainingMilliseconds - 1));
                waited |= timer.Wait(wait, cancellationToken);
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
            return waited;
        }
        cancellationToken.ThrowIfCancellationRequested();
        return waited;
    }

    private void WaitSessionGate(CancellationToken cancellationToken)
    {
        var start = _clock.GetTimestamp();
        _sessionGate.Wait(cancellationToken);
        var elapsed = _clock.GetTimestamp() - start;
        UpdateMaxDuration(ref _maxSessionGateWaitTicks, elapsed);
        if (TicksToMilliseconds(elapsed) > 10)
            EnqueueStallTrace("SessionGateWait", elapsed);
    }

    private void PublishLatestSnapshot(CancellationToken cancellationToken)
    {
        FabricMachineSnapshot snapshot;
        WaitSessionGate(cancellationToken);
        try
        {
            var session = RequireSession();
            var snapshotStart = _clock.GetTimestamp();
            snapshot = session.GetSnapshot();
            ObserveOperationDuration("GetSnapshot", _clock.GetTimestamp() - snapshotStart, ref _snapshotCalls, ref _snapshotDurationTicksTotal, ref _maxSnapshotDurationTicks);
        }
        finally
        {
            _sessionGate.Release();
        }

        var publishStart = _clock.GetTimestamp();
        PublishSnapshot(snapshot);
        ObserveOperationDuration("PublishSnapshot", _clock.GetTimestamp() - publishStart, ref _publishCalls, ref _publishDurationTicksTotal, ref _maxPublishDurationTicks);
    }

    private void ObserveWakeLateness(long lateTicks)
    {
        Interlocked.Increment(ref _timedWaitCount);
        Interlocked.Add(ref _timedWaitLatenessTicksTotal, Math.Max(0, lateTicks));
        if (lateTicks <= 0)
        {
            Interlocked.Exchange(ref _currentConsecutiveLateWakes, 0);
            return;
        }

        UpdateMax(ref _maxWakeLatenessTicks, lateTicks);
        if (TicksToMilliseconds(lateTicks) > 10)
            EnqueueStallTrace("TimedWait", lateTicks);
        var lateMilliseconds = (double)lateTicks * 1000 / _clock.Frequency;
        if (lateMilliseconds > 2) Interlocked.Increment(ref _wakeLateOver2Ms);
        if (lateMilliseconds > 5) Interlocked.Increment(ref _wakeLateOver5Ms);
        if (lateMilliseconds > 10) Interlocked.Increment(ref _wakeLateOver10Ms);
        if (lateMilliseconds > 20) Interlocked.Increment(ref _wakeLateOver20Ms);
        if (lateMilliseconds > 50) Interlocked.Increment(ref _wakeLateOver50Ms);
        if (lateMilliseconds > 100) Interlocked.Increment(ref _wakeLateOver100Ms);
        var consecutive = Interlocked.Increment(ref _currentConsecutiveLateWakes);
        UpdateMax(ref _longestConsecutiveLateWakes, consecutive);
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
        if (framesWritten == 0)
            Interlocked.Increment(ref _zeroAudioSlices);
        UpdateMinFramesPerSlice(framesWritten);
        UpdateMaxFramesPerSlice(framesWritten);
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
            EmulationAudioPlaybackStatistics audioStatistics = default;
            if (_audioStarted)
            {
                TryCleanup(_audioSink.Stop, ref failures);
                audioStatistics = _audioSink.GetStatistics();
                _audioStarted = false;
            }
            WriteStallTraceFile();
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
        Interlocked.Exchange(ref _catchUpSlices, 0);
        Interlocked.Exchange(ref _maxCatchUpBatch, 0);
        Interlocked.Exchange(ref _exceptionalDiscardedTicks, 0);
        Interlocked.Exchange(ref _fabricAudioFramesRead, 0);
        Interlocked.Exchange(ref _zeroAudioSlices, 0);
        Interlocked.Exchange(ref _minFramesPerSlice, long.MaxValue);
        Interlocked.Exchange(ref _maxFramesPerSlice, 0);
        Interlocked.Exchange(ref _capacityLimitedCatchUpBatches, 0);
        Interlocked.Exchange(ref _retainedDebtIterations, 0);
        Interlocked.Exchange(ref _maxRetainedSlices, 0);
        Interlocked.Exchange(ref _writableFramesAtLargestCatchUpBatch, 0);
        Interlocked.Exchange(ref _runnerThreadId, 0);
        _runnerThreadPriority = ThreadPriority.Normal;
        Interlocked.Exchange(ref _singleSliceBatches, 0);
        Interlocked.Exchange(ref _multiSliceBatches, 0);
        Interlocked.Exchange(ref _maxWakeLatenessTicks, 0);
        Interlocked.Exchange(ref _wakeLateOver2Ms, 0);
        Interlocked.Exchange(ref _wakeLateOver5Ms, 0);
        Interlocked.Exchange(ref _wakeLateOver10Ms, 0);
        Interlocked.Exchange(ref _wakeLateOver20Ms, 0);
        Interlocked.Exchange(ref _wakeLateOver50Ms, 0);
        Interlocked.Exchange(ref _wakeLateOver100Ms, 0);
        Interlocked.Exchange(ref _currentConsecutiveLateWakes, 0);
        Interlocked.Exchange(ref _longestConsecutiveLateWakes, 0);
        Interlocked.Exchange(ref _maxAdvanceDurationTicks, 0);
        Interlocked.Exchange(ref _maxReadAudioDurationTicks, 0);
        Interlocked.Exchange(ref _maxSnapshotDurationTicks, 0);
        Interlocked.Exchange(ref _maxPublishDurationTicks, 0);
        Interlocked.Exchange(ref _maxInputDurationTicks, 0);
        Interlocked.Exchange(ref _maxSessionGateWaitTicks, 0);
        Interlocked.Exchange(ref _timedWaitCount, 0);
        Interlocked.Exchange(ref _timedWaitLatenessTicksTotal, 0);
        Interlocked.Exchange(ref _debtContinuationCount, 0);
        Interlocked.Exchange(ref _capacityLimitedContinuationCount, 0);
        Interlocked.Exchange(ref _yieldContinuationCount, 0);
        _timingMode = "Unstarted";
        _highResolutionTimerActive = false;
        _mmcssRegistered = false;
        Interlocked.Exchange(ref _advanceCalls, 0);
        Interlocked.Exchange(ref _advanceDurationTicksTotal, 0);
        Interlocked.Exchange(ref _advanceOver2Ms, 0);
        Interlocked.Exchange(ref _advanceOver5Ms, 0);
        Interlocked.Exchange(ref _advanceOver10Ms, 0);
        Interlocked.Exchange(ref _advanceOver20Ms, 0);
        Interlocked.Exchange(ref _advanceOver50Ms, 0);
        Interlocked.Exchange(ref _advanceOver100Ms, 0);
        Interlocked.Exchange(ref _readAudioCalls, 0);
        Interlocked.Exchange(ref _readAudioDurationTicksTotal, 0);
        Interlocked.Exchange(ref _snapshotCalls, 0);
        Interlocked.Exchange(ref _snapshotDurationTicksTotal, 0);
        Interlocked.Exchange(ref _publishCalls, 0);
        Interlocked.Exchange(ref _publishDurationTicksTotal, 0);
        while (_stallTrace.TryDequeue(out _)) { }
        _lastStallTracePath = string.Empty;
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

    private void UpdateMinFramesPerSlice(int frames)
    {
        var current = Interlocked.Read(ref _minFramesPerSlice);
        while (frames < current)
        {
            var previous = Interlocked.CompareExchange(ref _minFramesPerSlice, frames, current);
            if (previous == current) return;
            current = previous;
        }
    }


    private void ObserveOperationDuration(string operation, long ticks, ref long calls, ref long totalTicks, ref long maxTicks)
    {
        Interlocked.Increment(ref calls);
        Interlocked.Add(ref totalTicks, ticks);
        UpdateMaxDuration(ref maxTicks, ticks);
        if (operation == "SessionAdvance")
            ObserveAdvanceDurationBuckets(ticks);
        if (TicksToMilliseconds(ticks) > 10)
            EnqueueStallTrace(operation, ticks);
    }

    private void ObserveAdvanceDurationBuckets(long ticks)
    {
        var ms = TicksToMilliseconds(ticks);
        if (ms > 2) Interlocked.Increment(ref _advanceOver2Ms);
        if (ms > 5) Interlocked.Increment(ref _advanceOver5Ms);
        if (ms > 10) Interlocked.Increment(ref _advanceOver10Ms);
        if (ms > 20) Interlocked.Increment(ref _advanceOver20Ms);
        if (ms > 50) Interlocked.Increment(ref _advanceOver50Ms);
        if (ms > 100) Interlocked.Increment(ref _advanceOver100Ms);
    }

    private void EnqueueStallTrace(string operation, long ticks)
    {
        if (_stallTrace.Count >= 500)
            return;
        _stallTrace.Enqueue(new(
            _clock.GetTimestamp(),
            operation,
            TicksToMilliseconds(ticks),
            Environment.CurrentManagedThreadId,
            CalculateAvailableSlices(_clock.GetTimestamp(), _clock.GetTimestamp(), Math.Max(1L, _clock.Frequency / EmulationPumpHz)),
            _audioSink.GetStatistics().CurrentRingFrames,
            _audioSink.WritableFrames,
            Interlocked.Read(ref _singleSliceBatches) + Interlocked.Read(ref _multiSliceBatches),
            null));
    }

    private void UpdateMaxFramesPerSlice(int frames) => UpdateMax(ref _maxFramesPerSlice, frames);
    private void UpdateMaxDuration(ref long target, long ticks) => UpdateMax(ref target, ticks);
    private void UpdateMaxRetainedSlices(long slices) => UpdateMax(ref _maxRetainedSlices, slices);
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

    private double TicksToMilliseconds(long ticks) => (double)ticks * 1000 / _clock.Frequency;
    private double AverageTicksToMilliseconds(long totalTicks, long calls) => calls > 0 ? TicksToMilliseconds(totalTicks) / calls : 0;

    private static double CalculateAverageBatchSize(long slices, long batches) => batches > 0 ? (double)slices / batches : 0;

    private double CalculateAverageBatchSize(long slices) =>
        CalculateAverageBatchSize(slices, Interlocked.Read(ref _singleSliceBatches) + Interlocked.Read(ref _multiSliceBatches));

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
            Interlocked.Read(ref _catchUpSlices),
            Interlocked.Read(ref _maxCatchUpBatch),
            discardedMs,
            Interlocked.Read(ref _fabricAudioFramesRead),
            Interlocked.Read(ref _zeroAudioSlices),
            Interlocked.Read(ref _minFramesPerSlice) == long.MaxValue ? 0 : Interlocked.Read(ref _minFramesPerSlice),
            Interlocked.Read(ref _maxFramesPerSlice),
            Interlocked.Read(ref _capacityLimitedCatchUpBatches),
            Interlocked.Read(ref _retainedDebtIterations),
            Interlocked.Read(ref _maxRetainedSlices),
            Interlocked.Read(ref _writableFramesAtLargestCatchUpBatch),
            Interlocked.Read(ref _runnerThreadId),
            _runnerThreadPriority.ToString(),
            Interlocked.Read(ref _singleSliceBatches),
            Interlocked.Read(ref _multiSliceBatches),
            CalculateAverageBatchSize(slices),
            TicksToMilliseconds(Interlocked.Read(ref _maxWakeLatenessTicks)),
            Interlocked.Read(ref _wakeLateOver2Ms),
            Interlocked.Read(ref _wakeLateOver5Ms),
            Interlocked.Read(ref _wakeLateOver10Ms),
            Interlocked.Read(ref _wakeLateOver20Ms),
            Interlocked.Read(ref _wakeLateOver50Ms),
            Interlocked.Read(ref _wakeLateOver100Ms),
            Interlocked.Read(ref _longestConsecutiveLateWakes),
            TicksToMilliseconds(Interlocked.Read(ref _maxAdvanceDurationTicks)),
            TicksToMilliseconds(Interlocked.Read(ref _maxReadAudioDurationTicks)),
            TicksToMilliseconds(Interlocked.Read(ref _maxSnapshotDurationTicks)),
            TicksToMilliseconds(Interlocked.Read(ref _maxPublishDurationTicks)),
            TicksToMilliseconds(Interlocked.Read(ref _maxInputDurationTicks)),
            TicksToMilliseconds(Interlocked.Read(ref _maxSessionGateWaitTicks)),
            _timingMode,
            _highResolutionTimerActive,
            _mmcssRegistered,
            Interlocked.Read(ref _timedWaitCount),
            Interlocked.Read(ref _debtContinuationCount),
            Interlocked.Read(ref _capacityLimitedContinuationCount),
            Interlocked.Read(ref _yieldContinuationCount),
            AverageTicksToMilliseconds(Interlocked.Read(ref _timedWaitLatenessTicksTotal), Interlocked.Read(ref _timedWaitCount)),
            Interlocked.Read(ref _advanceCalls),
            AverageTicksToMilliseconds(Interlocked.Read(ref _advanceDurationTicksTotal), Interlocked.Read(ref _advanceCalls)),
            Interlocked.Read(ref _advanceOver2Ms),
            Interlocked.Read(ref _advanceOver5Ms),
            Interlocked.Read(ref _advanceOver10Ms),
            Interlocked.Read(ref _advanceOver20Ms),
            Interlocked.Read(ref _advanceOver50Ms),
            Interlocked.Read(ref _advanceOver100Ms),
            Interlocked.Read(ref _readAudioCalls),
            AverageTicksToMilliseconds(Interlocked.Read(ref _readAudioDurationTicksTotal), Interlocked.Read(ref _readAudioCalls)),
            Interlocked.Read(ref _snapshotCalls),
            AverageTicksToMilliseconds(Interlocked.Read(ref _snapshotDurationTicksTotal), Interlocked.Read(ref _snapshotCalls)),
            Interlocked.Read(ref _publishCalls),
            AverageTicksToMilliseconds(Interlocked.Read(ref _publishDurationTicksTotal), Interlocked.Read(ref _publishCalls)),
            _stallTrace.Count,
            _lastStallTracePath,
            audioStatistics));
    }

    internal static string FormatStopSummary(
        double wallMilliseconds,
        double emulatedMilliseconds,
        double ratio,
        long slices,
        long catchUpSlices,
        long maxCatchUpBatch,
        double discardedMilliseconds,
        long fabricAudioFrames,
        long zeroAudioSlices,
        long minFramesPerSlice,
        long maxFramesPerSlice,
        long capacityLimitedCatchUpBatches,
        long retainedDebtIterations,
        long maxRetainedSlices,
        long writableFramesAtLargestCatchUpBatch,
        long runnerThreadId,
        string runnerThreadPriority,
        long singleSliceBatches,
        long multiSliceBatches,
        double averageBatchSize,
        double maxWakeLatenessMilliseconds,
        long wakeLateOver2Ms,
        long wakeLateOver5Ms,
        long wakeLateOver10Ms,
        long wakeLateOver20Ms,
        long wakeLateOver50Ms,
        long wakeLateOver100Ms,
        long longestConsecutiveLateWakes,
        double maxAdvanceDurationMilliseconds,
        double maxReadAudioDurationMilliseconds,
        double maxSnapshotDurationMilliseconds,
        double maxPublishDurationMilliseconds,
        double maxInputDurationMilliseconds,
        double maxSessionGateWaitMilliseconds,
        string timingMode,
        bool highResolutionTimerActive,
        bool mmcssRegistered,
        long timedWaitCount,
        long debtContinuationCount,
        long capacityContinuationCount,
        long yieldContinuationCount,
        double averageTimedWakeLatenessMilliseconds,
        long advanceCalls,
        double averageAdvanceDurationMilliseconds,
        long advanceOver2Ms,
        long advanceOver5Ms,
        long advanceOver10Ms,
        long advanceOver20Ms,
        long advanceOver50Ms,
        long advanceOver100Ms,
        long readAudioCalls,
        double averageReadAudioDurationMilliseconds,
        long snapshotCalls,
        double averageSnapshotDurationMilliseconds,
        long publishCalls,
        double averagePublishDurationMilliseconds,
        int stallTraceCount,
        string stallTracePath,
        EmulationAudioPlaybackStatistics audioStatistics) =>
        $"Fabric stop summary: wallMs={wallMilliseconds:F1}, emulatedMs={emulatedMilliseconds:F1}, ratio={ratio:F5}, slices={slices}, catchUpSlices={catchUpSlices}, maxCatchUpBatch={maxCatchUpBatch}, discardedMs={discardedMilliseconds:F1}, fabricAudioFrames={fabricAudioFrames}, zeroAudioSlices={zeroAudioSlices}, minFramesPerSlice={minFramesPerSlice}, maxFramesPerSlice={maxFramesPerSlice}, startupRingFrames={audioStatistics.StartupRingFrames}, minimumRingFrames={audioStatistics.MinimumRingFrames}, maximumRingFrames={audioStatistics.MaximumRingFrames}, currentRingFrames={audioStatistics.CurrentRingFrames}, ringCapacityFrames={audioStatistics.CapacityFrames}, writableFramesAtLargestCatchUpBatch={writableFramesAtLargestCatchUpBatch}, capacityLimitedCatchUpBatches={capacityLimitedCatchUpBatches}, retainedDebtIterations={retainedDebtIterations}, maxRetainedSlices={maxRetainedSlices}, runnerThreadId={runnerThreadId}, runnerThreadPriority={runnerThreadPriority}, singleSliceBatches={singleSliceBatches}, multiSliceBatches={multiSliceBatches}, averageBatchSize={averageBatchSize:F2}, catchUpSlicePercent={(slices > 0 ? (double)catchUpSlices * 100 / slices : 0):F2}, timingMode={timingMode}, highResolutionTimerActive={highResolutionTimerActive}, mmcssRegistered={mmcssRegistered}, timedWaitCount={timedWaitCount}, debtContinuationCount={debtContinuationCount}, capacityContinuationCount={capacityContinuationCount}, yieldContinuationCount={yieldContinuationCount}, averageTimedWakeLatenessMs={averageTimedWakeLatenessMilliseconds:F3}, maxTimedWakeLatenessMs={maxWakeLatenessMilliseconds:F3}, wakeLateOver2Ms={wakeLateOver2Ms}, wakeLateOver5Ms={wakeLateOver5Ms}, wakeLateOver10Ms={wakeLateOver10Ms}, wakeLateOver20Ms={wakeLateOver20Ms}, wakeLateOver50Ms={wakeLateOver50Ms}, wakeLateOver100Ms={wakeLateOver100Ms}, longestConsecutiveLateWakes={longestConsecutiveLateWakes}, advanceCalls={advanceCalls}, averageAdvanceDurationMs={averageAdvanceDurationMilliseconds:F3}, maxAdvanceDurationMs={maxAdvanceDurationMilliseconds:F3}, advanceOver2Ms={advanceOver2Ms}, advanceOver5Ms={advanceOver5Ms}, advanceOver10Ms={advanceOver10Ms}, advanceOver20Ms={advanceOver20Ms}, advanceOver50Ms={advanceOver50Ms}, advanceOver100Ms={advanceOver100Ms}, readAudioCalls={readAudioCalls}, averageReadAudioDurationMs={averageReadAudioDurationMilliseconds:F3}, snapshotCalls={snapshotCalls}, averageSnapshotDurationMs={averageSnapshotDurationMilliseconds:F3}, publishCalls={publishCalls}, averagePublishDurationMs={averagePublishDurationMilliseconds:F3}, stallTraceCount={stallTraceCount}, stallTracePath={stallTracePath}, maxReadAudioDurationMs={maxReadAudioDurationMilliseconds:F3}, maxSnapshotDurationMs={maxSnapshotDurationMilliseconds:F3}, maxPublishDurationMs={maxPublishDurationMilliseconds:F3}, maxInputDurationMs={maxInputDurationMilliseconds:F3}, maxSessionGateWaitMs={maxSessionGateWaitMilliseconds:F3}, maxFramesGeneratedByOneSlice={maxFramesPerSlice}, ringWritten={audioStatistics.RingFramesWritten}, ringRejected={audioStatistics.RingFramesRejected}, devicePcmFrames={audioStatistics.DeviceFramesDelivered}, silenceFrames={audioStatistics.SilenceFrames}, underrunEpisodes={audioStatistics.UnderrunEpisodes}, minimumRequestedFrames={audioStatistics.MinimumRequestedFrames}, maximumRequestedFrames={audioStatistics.MaximumRequestedFrames}, totalRequestedFrames={audioStatistics.TotalRequestedFrames}, playbackStarted={audioStatistics.PlaybackStarted}.";

    private void WriteStallTraceFile()
    {
        if (_stallTrace.IsEmpty)
            return;
        var path = Path.Combine(Path.GetTempPath(), $"oasis-fabric-runner-stalls-{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}.csv");
        var lines = new List<string>(_stallTrace.Count + 1)
        {
            "timestamp,operation,durationMs,runnerThreadId,currentDebtSlices,ringReadableFrames,ringWritableFrames,batchIndex,applicationActive"
        };
        lines.AddRange(_stallTrace.Select(record =>
            FormattableString.Invariant($"{record.Timestamp},{record.Operation},{record.DurationMilliseconds:F3},{record.RunnerThreadId},{record.CurrentDebtSlices},{record.RingReadableFrames},{record.RingWritableFrames},{record.BatchIndex},{record.ApplicationActive}")));
        File.WriteAllLines(path, lines);
        _lastStallTracePath = path;
    }


    private readonly record struct StallTraceRecord(long Timestamp, string Operation, double DurationMilliseconds, int RunnerThreadId, int CurrentDebtSlices, int RingReadableFrames, int RingWritableFrames, long BatchIndex, bool? ApplicationActive);

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
