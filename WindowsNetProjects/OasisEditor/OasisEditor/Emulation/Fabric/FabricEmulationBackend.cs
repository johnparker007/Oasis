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
    private const int MaximumCatchUpSlicesPerLoop = 8;
    private const int MaximumRetainedSchedulingDebtMilliseconds = 125;
    private static readonly TimeSpan PumpInterval = TimeSpan.FromMilliseconds(1);
    private static readonly EmulationBackendCapabilities BackendCapabilities =
        new(true, true, true, true, false, false, false);

    private readonly string _runtimePath;
    private readonly string _amberPath;
    private readonly Func<string, IFabricRuntimeLibrary> _runtimeFactory;
    private readonly IEmulationAudioSink _audioSink;
    private readonly IFabricClock _clock;
    private readonly int _audioBufferLengthMilliseconds;
    private readonly EmulationAudioOutputBackend _audioOutputBackend;
    private readonly AmberFabricAudioDiagnosticSettings _audioDiagnosticSettings;
    private readonly Action<EmulationBackendDiagnosticMessage>? _diagnosticLogger;
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
    private CancellationTokenSource? _pumpCancellation;
    private Task? _pumpTask;
    private FabricAudioFormat? _audioFormat;
    private short[] _audioBuffer = [];
    private EmulationBackendState _state = EmulationBackendState.Stopped;
    private bool _shutdown;
    private bool _audioStarted;
    private bool _disposed;
    private long _scheduleGeneration;
    private long _audioSequence;
    private long _audioFramesRead;
    private long _audioFramesSubmitted;
    private long _catchUpSlicesExecuted;
    private long _maximumCatchUpBatch;
    private long _discardedSchedulingDebtTicks;
    private long _maximumSchedulingDebtTicks;
    private EmulationAudioDiagnostics? _audioDiagnostics;

    public FabricEmulationBackend(string runtimePath, string amberPath)
        : this(runtimePath, amberPath, path => new FabricRuntimeLibrary(path),
            new NAudioEmulationAudioSink(), new StopwatchFabricClock(), NativeEmulationPreferences.DefaultAudioBufferLengthMilliseconds, EmulationAudioOutputBackend.WasapiOut, AmberFabricAudioDiagnosticSettings.Disabled, null, WriteDebugError)
    {
    }

    internal FabricEmulationBackend(
        string runtimePath,
        string amberPath,
        Func<string, IFabricRuntimeLibrary> runtimeFactory,
        IEmulationAudioSink audioSink,
        IFabricClock clock,
        int audioBufferLengthMilliseconds,
        EmulationAudioOutputBackend audioOutputBackend,
        AmberFabricAudioDiagnosticSettings audioDiagnosticSettings,
        Action<EmulationBackendDiagnosticMessage>? diagnosticLogger = null,
        Action<string>? errorLogger = null)
    {
        _runtimePath = runtimePath;
        _amberPath = amberPath;
        _runtimeFactory = runtimeFactory;
        _audioSink = audioSink;
        _clock = clock;
        _audioBufferLengthMilliseconds = audioBufferLengthMilliseconds;
        _audioOutputBackend = audioOutputBackend;
        _audioDiagnosticSettings = audioDiagnosticSettings;
        _diagnosticLogger = diagnosticLogger;
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
                ConfigureAudio(_session);
                _audioDiagnostics = TryStartAudioDiagnostics();
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

    private async Task PumpAsync(CancellationToken cancellationToken)
    {
        try
        {
            var pumpTicks = Math.Max(1L, _clock.Frequency / EmulationPumpHz);
            var maxDebtTicks = Math.Max(pumpTicks, (_clock.Frequency * MaximumRetainedSchedulingDebtMilliseconds) / 1000);
            var nextDeadline = _clock.GetTimestamp();
            var scheduleGeneration = Interlocked.Read(ref _scheduleGeneration);
            long retainedDebtTicks = 0;
            while (!cancellationToken.IsCancellationRequested)
            {
                if (State != EmulationBackendState.Running)
                {
                    await Task.Delay(PumpInterval, cancellationToken).ConfigureAwait(false);
                    nextDeadline = _clock.GetTimestamp();
                    retainedDebtTicks = 0;
                    scheduleGeneration = Interlocked.Read(ref _scheduleGeneration);
                    continue;
                }

                var currentGeneration = Interlocked.Read(ref _scheduleGeneration);
                if (currentGeneration != scheduleGeneration)
                {
                    nextDeadline = _clock.GetTimestamp();
                    retainedDebtTicks = 0;
                    scheduleGeneration = currentGeneration;
                }

                var now = _clock.GetTimestamp();
                if (now > nextDeadline)
                    retainedDebtTicks += now - nextDeadline;
                if (retainedDebtTicks > maxDebtTicks)
                {
                    var discarded = retainedDebtTicks - maxDebtTicks;
                    retainedDebtTicks = maxDebtTicks;
                    Interlocked.Add(ref _discardedSchedulingDebtTicks, discarded);
                    LogDiagnosticWarning($"Fabric audio catch-up discarded excessive scheduling debt: {TicksToMilliseconds(discarded):F1} ms.");
                }
                ObserveMaximum(ref _maximumSchedulingDebtTicks, retainedDebtTicks);

                var requestedSlices = 1 + (int)Math.Min(MaximumCatchUpSlicesPerLoop - 1, retainedDebtTicks / pumpTicks);
                var batch = 0;
                FabricMachineSnapshot? latestSnapshot = null;
                await _sessionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    var session = RequireSession();
                    ProcessInputs(session);
                    for (var slice = 0; slice < requestedSlices; slice++)
                    {
                        session.Advance(NanosecondsPerPump);
                        ReadAudio(session);
                        latestSnapshot = session.GetSnapshot();
                        if (retainedDebtTicks >= pumpTicks)
                            retainedDebtTicks -= pumpTicks;
                        batch++;
                    }
                }
                finally
                {
                    _sessionGate.Release();
                }

                if (latestSnapshot is not null)
                    PublishSnapshot(latestSnapshot);
                Interlocked.Add(ref _catchUpSlicesExecuted, Math.Max(0, batch - 1));
                ObserveMaximum(ref _maximumCatchUpBatch, batch);
                _audioDiagnostics?.RecordTimeline(default, catchUpSlicesExecuted: Interlocked.Read(ref _catchUpSlicesExecuted), maxCatchUpBatch: Interlocked.Read(ref _maximumCatchUpBatch), currentDebtMilliseconds: (int)Math.Round(TicksToMilliseconds(retainedDebtTicks)), maxDebtMilliseconds: (int)Math.Round(TicksToMilliseconds(Interlocked.Read(ref _maximumSchedulingDebtTicks))), discardedDebtMilliseconds: (int)Math.Round(TicksToMilliseconds(Interlocked.Read(ref _discardedSchedulingDebtTicks))));

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

    private double TicksToMilliseconds(long ticks) => (double)ticks * 1000 / _clock.Frequency;

    private static void ObserveMaximum(ref long target, long value)
    {
        long current;
        do
        {
            current = Interlocked.Read(ref target);
            if (value <= current) return;
        } while (Interlocked.CompareExchange(ref target, value, current) != current);
    }

    private EmulationAudioDiagnostics? TryStartAudioDiagnostics()
    {
        if (_audioFormat is not { } format)
        {
            if (_audioSink is IEmulationAudioDiagnosticSink diagnosticSink) diagnosticSink.ConfigureDiagnostics(null);
            LogDiagnosticInfo("Amber audio diagnostics disabled: Fabric session did not expose audio.");
            return null;
        }

        var settings = EmulationAudioDiagnostics.ApplyEnvironmentOverrides(_audioDiagnosticSettings);
        if (!settings.Enabled)
        {
            if (_audioSink is IEmulationAudioDiagnosticSink diagnosticSink) diagnosticSink.ConfigureDiagnostics(null);
            LogDiagnosticInfo("Amber audio diagnostics disabled.");
            return null;
        }

        try
        {
            var diagnostics = EmulationAudioDiagnostics.Start(settings,
                new(AmberBackendKind, JpmSystem6MachineIdentifier, _runtimePath, _amberPath,
                    new(checked((int)format.SampleRate), checked((int)format.ChannelCount), checked((int)format.BitsPerSample)),
                    _audioBufferLengthMilliseconds, _audioOutputBackend));
            diagnostics.FabricManagedRead.SetFormat(checked((int)format.SampleRate), checked((int)format.ChannelCount));
            diagnostics.FabricBackendSubmit.SetFormat(checked((int)format.SampleRate), checked((int)format.ChannelCount));
            diagnostics.NAudioAccepted.SetFormat(checked((int)format.SampleRate), checked((int)format.ChannelCount));
            if (_audioSink is IEmulationAudioDiagnosticSink diagnosticSink)
                diagnosticSink.ConfigureDiagnostics(diagnostics);
            LogDiagnosticInfo($"Amber audio diagnostics enabled. Capture directory: {diagnostics.SessionDirectory}; sampleRate={format.SampleRate}; channels={format.ChannelCount}; queueBlocks={settings.QueueBlockCapacity}; boundaries=FabricManagedRead,FabricBackendSubmit,NAudioAccepted; maximumCaptureSeconds={settings.CaptureDurationSeconds}; outputBackend={_audioOutputBackend}.");
            return diagnostics;
        }
        catch (Exception exception)
        {
            LogDiagnosticWarning($"Amber audio diagnostics could not start and were disabled: {exception.Message}");
            return null;
        }
    }

    private void LogDiagnosticInfo(string message) =>
        _diagnosticLogger?.Invoke(new(EmulationBackendDiagnosticSeverity.Info, message));

    private void LogDiagnosticWarning(string message) =>
        _diagnosticLogger?.Invoke(new(EmulationBackendDiagnosticSeverity.Warning, message));

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
        var sequence = Interlocked.Increment(ref _audioSequence);
        var framesWritten = session.ReadAudio(_audioBuffer, frameCapacity);
        _audioDiagnostics?.FabricManagedRead.ObserveRead(frameCapacity, framesWritten, sequence);
        var sampleCount = checked(framesWritten * format.ChannelCount);
        var samples = _audioBuffer.AsSpan(0, sampleCount);
        var startFrame = Interlocked.Read(ref _audioFramesRead);
        _audioDiagnostics?.Capture(EmulationAudioCaptureBoundary.FabricManagedRead,
            new(checked((int)format.SampleRate), checked((int)format.ChannelCount), checked((int)format.BitsPerSample)),
            sequence, startFrame, samples, framesWritten);
        Interlocked.Add(ref _audioFramesRead, framesWritten);
        var bytes = MemoryMarshal.AsBytes(samples);
        if (!bytes.IsEmpty)
        {
            var submitStartFrame = Interlocked.Read(ref _audioFramesSubmitted);
            _audioDiagnostics?.FabricBackendSubmit.ObserveSubmit(framesWritten, sequence);
            _audioDiagnostics?.Capture(EmulationAudioCaptureBoundary.FabricBackendSubmit,
                new(checked((int)format.SampleRate), checked((int)format.ChannelCount), checked((int)format.BitsPerSample)),
                sequence, submitStartFrame, samples, framesWritten);
            var pushResult = _audioSink.PushPcm(bytes, new(sequence, submitStartFrame, framesWritten, framesWritten == 0, 0));
            _audioDiagnostics?.ObserveSinkPush(pushResult, framesWritten, format);
            Interlocked.Add(ref _audioFramesSubmitted, pushResult.AcceptedBytes / checked((int)format.ChannelCount * sizeof(short)));
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
                _audioDiagnostics?.Complete(true);
                _audioDiagnostics?.WriteSummary(LogDiagnosticInfo);
                _audioDiagnostics?.Dispose();
                _audioDiagnostics = null;
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
