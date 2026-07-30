using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace OasisEditor;

public sealed class FabricEmulationBackend : IEmulationBackend
{
    private const int PumpDelayMilliseconds = 1;
    private static readonly EmulationBackendCapabilities BackendCapabilities =
        new(true, true, true, true, false, false, false, false);

    private readonly string _runtimePath;
    private readonly string _amberPath;
    private readonly Func<string, IFabricRuntimeLibrary> _runtimeFactory;
    private readonly IEmulationAudioSink _audioSink;
    private readonly IFabricClock _clock;
    private readonly Action<string> _errorLogger;
    private readonly FabricElapsedTime _elapsedTime;
    private readonly Func<bool> _comparisonEnabled;
    private readonly Action<string>? _comparisonSink;
    private AmberComparisonSession? _comparison;
    private long _pumpIteration;
    private readonly SemaphoreSlim _sessionGate = new(1, 1);
    private readonly ConcurrentQueue<InputCommand> _inputCommands = new();
    private readonly HashSet<int> _assertedInputs = [];
    private readonly Dictionary<int, (bool State, float Brightness)> _lamps = [];
    private readonly Dictionary<int, int> _reels = [];
    private readonly Dictionary<DisplayOutputIdentity, ulong> _displayOutputs = [];
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
        Func<bool>? comparisonEnabled = null,
        Action<string>? comparisonSink = null)
    {
        _runtimePath = runtimePath;
        _amberPath = amberPath;
        _runtimeFactory = runtimeFactory;
        _audioSink = audioSink;
        _clock = clock;
        _errorLogger = errorLogger ?? WriteDebugError;
        _elapsedTime = new FabricElapsedTime(clock.Frequency);
        _comparisonEnabled = comparisonEnabled ?? (() => false);
        _comparisonSink = comparisonSink;
    }

    public EmulationBackendKind BackendKind => EmulationBackendKind.NativeSystem6;
    public EmulationBackendCapabilities Capabilities => BackendCapabilities;
    public EmulationBackendState State { get { lock (_stateGate) return _state; } }
    public Exception? LastFailure { get; private set; }

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
        var comparison = new AmberComparisonSession(_comparisonEnabled(), "fabric", _comparisonSink);
        _comparison = comparison.Enabled ? comparison : null;
        _pumpIteration = 0;
        _comparison?.Write("ComparisonStart", $"selected_backend:Fabric_Amber|platform:{request.Platform}|machine:{request.MachineName}|process_id:{Environment.ProcessId}|fabric_runtime:{AmberComparisonSession.SafeFileName(_runtimePath)}|amber_dll:{AmberComparisonSession.SafeFileName(_amberPath)}");
        _comparison?.Write("BackendCreate");
        try
        {
            var settings = request.System6NativeRoms
                ?? throw new InvalidOperationException("Fabric System 6 requires native ROM settings.");
            var resources = BuildRomResources(settings);
            _comparison?.Write("LoadProgramRoms", AmberComparisonSession.RomSummary("program", settings.ProgramRomPaths));
            _comparison?.Write("LoadSoundRoms", AmberComparisonSession.RomSummary("sound", settings.SoundRomPaths));
            WriteConfiguration(settings);
            cancellationToken.ThrowIfCancellationRequested();
            _runtime = _runtimeFactory(_runtimePath);
            _comparison?.Write("LibraryLoad", $"filename:{AmberComparisonSession.SafeFileName(_runtimePath)}");
            _comparison?.Write("RuntimeCreate");
            _session = _runtime.CreateSession(new FabricLaunchRequest(
                "amber-api-v2", "jpm-system6", _amberPath, resources,
                FabricAmberConfiguration.FromSystem6(settings)));
            _comparison?.Write("SessionCreate");

            await _sessionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                _session.Initialise();
                _comparison?.Write("Initialise");
                ConfigureAudio(_session);
                _elapsedTime.Reset(_clock.GetTimestamp());
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
        catch (Exception exception)
        {
            _comparison?.Write("Failure", result: "failure", summary: exception.GetType().Name);
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
        _comparison?.Write("Cancellation", "expected:true|operation:pump");
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
        _comparison?.Write("ComparisonEnd");
        _comparison = null;
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
            _elapsedTime.Reset(_clock.GetTimestamp());
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
            _elapsedTime.Reset(_clock.GetTimestamp());
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
        _comparison?.WriteBounded("input", AmberComparisonSession.InputLimit, "SubmitInput", $"oasis_id:{inputDefinition.Id}|switch_index:{index}|active:{isPressed.ToString().ToLowerInvariant()}|duplicate_suppression:false|shutdown_release:false", "queued");
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
            while (!cancellationToken.IsCancellationRequested)
            {
                if (State != EmulationBackendState.Running)
                {
                    await Task.Delay(PumpDelayMilliseconds, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                await _sessionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    var session = RequireSession();
                    var iteration = ++_pumpIteration;
                    var elapsed = _elapsedTime.AdvanceTo(_clock.GetTimestamp());
                    var started = Stopwatch.GetTimestamp();
                    session.Advance(elapsed);
                    var durationNs = (Stopwatch.GetTimestamp() - started) * 1_000_000_000L / Stopwatch.Frequency;
                    _comparison?.WriteBounded("advance", AmberComparisonSession.AdvanceLimit, "Advance", $"iteration:{iteration}|elapsed_ns:{elapsed}|time_source:monotonic_variable|advance_calls:1|native_return:unavailable|catch_up:false|clamping:false|duration_ns:{durationNs}");
                    ProcessInputs(session);
                    PublishSnapshot(session.GetSnapshot());
                    ReadAudio(session);
                }
                finally
                {
                    _sessionGate.Release();
                }
                await Task.Delay(PumpDelayMilliseconds, cancellationToken).ConfigureAwait(false);
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
        _comparison?.Write("GetAudioFormat", $"sample_rate:{format.SampleRate}|channels:{format.ChannelCount}|bits_per_sample:{format.BitsPerSample}|encoding:pcm_signed|interleaving:{format.Interleaved}");
        if (format.SampleRate == 0 || format.ChannelCount == 0 || format is not
            { BitsPerSample: 16, Interleaved: true, SignedSamples: true, LittleEndian: true })
            throw new NotSupportedException($"Unsupported Fabric audio format: {format}.");
        _audioFormat = format;
        var frameCapacity = Math.Max(1, checked((int)format.SampleRate / 500));
        _audioBuffer = new short[checked(frameCapacity * (int)format.ChannelCount)];
        _audioSink.Start(new(
            checked((int)format.SampleRate),
            checked((int)format.ChannelCount),
            checked((int)format.BitsPerSample)));
        _audioStarted = true;
        _comparison?.Write("AudioSink", "state:started");
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
        if (_comparison is not null)
        {
            var samples = _audioBuffer.AsSpan(0, sampleCount);
            var nonzero = 0; var peak = 0;
            foreach (var sample in samples) { if (sample != 0) nonzero++; peak = Math.Max(peak, Math.Abs((int)sample)); }
            _comparison.WriteBounded("audio", AmberComparisonSession.AudioLimit, "ReadAudio", $"requested_frames:{frameCapacity}|returned_frames:{framesWritten}|nonzero_samples:{nonzero}|peak_absolute_sample:{peak}|submitted_frames:{framesWritten}");
        }
    }

    private void PublishSnapshot(FabricMachineSnapshot snapshot)
    {
        if (_comparison?.Enabled == true)
        {
            var fingerprint = string.Join(',', snapshot.Lamps.Select(x => $"{x.NumericalIndex}:{x.LogicalState}:{x.Brightness}")) + ";" +
                string.Join(',', snapshot.Reels.Select(x => $"{x.NumericalIndex}:{x.Position}")) + ";" +
                string.Join(',', snapshot.CharacterDisplays.SelectMany(x => x.Characters)) + ";" +
                string.Join(',', snapshot.SegmentDisplays.SelectMany(x => x.SegmentMasks));
            var change = _comparison.TrackSnapshot(fingerprint);
            _comparison.WriteBounded("snapshot", AmberComparisonSession.SnapshotLimit, "GetSnapshot", $"sequence:{snapshot.Sequence}|lamp_count:{snapshot.Lamps.Count}|reel_count:{snapshot.Reels.Count}|reel_positions:{string.Join(',', snapshot.Reels.Select(x => x.Position))}|character_display_count:{snapshot.CharacterDisplays.Count}|segment_display_count:{snapshot.SegmentDisplays.Count}|{change}");
        }
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
            if (_reels.TryGetValue(reel.NumericalIndex, out var previous) && previous == reel.Position)
                continue;
            _reels[reel.NumericalIndex] = reel.Position;
            ReelChanged?.Invoke(this, new(reel.NumericalIndex, reel.Position));
        }
        for (var displayOrdinal = 0; displayOrdinal < snapshot.CharacterDisplays.Count; displayOrdinal++)
        {
            var display = snapshot.CharacterDisplays[displayOrdinal];
            for (var position = 0; position < display.Characters.Length; position++)
            {
                var identity = new DisplayOutputIdentity(DisplayOutputFamily.Character, display.Identifier, displayOrdinal, position);
                // Oasis's alpha event carries one integer. Keep the native mask in its low 32 bits and punctuation in bits 16/17.
                var publishedMask = display.Characters[position] | ((uint)display.Attributes[position] << 16);
                if (_displayOutputs.TryGetValue(identity, out var previous) && previous == publishedMask)
                    continue;
                _displayOutputs[identity] = publishedMask;
                var eventIndex = checked(displayOrdinal * FabricAbi.CharacterCapacity + position);
                SegmentChanged?.Invoke(this, new(eventIndex, unchecked((int)publishedMask), MameSegmentOutputType.NativeAlpha));
            }
        }
        for (var displayOrdinal = 0; displayOrdinal < snapshot.SegmentDisplays.Count; displayOrdinal++)
        {
            var display = snapshot.SegmentDisplays[displayOrdinal];
            for (var position = 0; position < display.SegmentMasks.Length; position++)
            {
                var identity = new DisplayOutputIdentity(DisplayOutputFamily.Segment, display.Identifier, displayOrdinal, position);
                var mask = display.SegmentMasks[position];
                if (_displayOutputs.TryGetValue(identity, out var previous) && previous == mask)
                    continue;
                _displayOutputs[identity] = mask;
                var eventIndex = checked(displayOrdinal * FabricAbi.SegmentCapacity + position);
                SegmentChanged?.Invoke(this, new(eventIndex, unchecked((int)mask), MameSegmentOutputType.Digit));
            }
        }
    }

    private void WriteConfiguration(System6NativeRomSettings settings)
    {
        _comparison?.Write("ConfigureReels", string.Join(",", settings.ReelOptos.Select(r => $"index:{r.ReelIndex}|enabled:{r.Enabled}|steps:{r.Steps}|opto_start:{r.OptoStart}|opto_end:{r.OptoEnd}|opto_invert:{r.OptoInvert}|apply:true")));
        _comparison?.Write("ConfigureCoins", string.Join(",", settings.Coins.Select(c => $"index:{c.Num}|enabled:{c.Enabled}|value:{c.CoinValue}|lockout_invert:{c.LockoutInvert}|port_index:{c.PortIndex}|coin_code:{c.Coin}|level:{c.Level}|full_level:{c.FullLevel}")));
        _comparison?.Write("ConfigurePercentage", $"raw_value:{settings.PercentSwitchValue}");
    }

    private async Task CleanupResourcesAsync()
    {
        await _sessionGate.WaitAsync().ConfigureAwait(false);
        List<Exception>? failures = null;
        try
        {
            if (_session is not null)
            {
                var releasedCount = _assertedInputs.Count;
                TryCleanup(ReleaseAssertedInputs, ref failures);
                _comparison?.Write("SubmitInput", "shutdown_release:true", "success", $"released_count:{releasedCount}");
                if (!_shutdown)
                {
                    TryCleanup(_session.Shutdown, ref failures);
                    _comparison?.Write("Shutdown");
                    _shutdown = true;
                }
                TryCleanup(_session.Dispose, ref failures);
                _comparison?.Write("Destroy", "resource:session");
                _session = null;
            }
            if (_audioStarted)
            {
                TryCleanup(_audioSink.Stop, ref failures);
                _comparison?.Write("AudioSink", "state:stopped");
                _audioStarted = false;
            }
            if (_runtime is not null)
            {
                TryCleanup(_runtime.Dispose, ref failures);
                _comparison?.Write("Destroy", "resource:runtime");
            }
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
    private enum DisplayOutputFamily { Character, Segment }
}
