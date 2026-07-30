using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Collections.Concurrent;

namespace OasisEditor;

/// <summary>The active System 6 backend. Native ABI ownership is delegated to Amber Bridge.</summary>
public sealed class System6NativeBackend : IEmulationBackend
{
    private const int EmulationPumpHz = 1000;
    private const int System6ClockHz = 8_000_000;
    private static readonly TimeSpan PumpInterval = TimeSpan.FromMilliseconds(1000d / EmulationPumpHz);
    private static readonly EmulationBackendCapabilities BackendCapabilities = new(true, true, true, true, false, false, false, false);

    private readonly string _bridgePath;
    private readonly Func<string, IAmberBridgeLibrary> _bridgeFactory;
    private readonly IEmulationAudioSink _audioSink;
    private readonly Func<bool> _comparisonEnabled;
    private readonly Action<string>? _comparisonSink;
    private AmberComparisonSession? _comparison;
    private long _pumpIteration;
    private readonly AmberOutputSnapshotBuffer _snapshot = new();
    private readonly ConcurrentQueue<(uint Index, bool IsOn)> _switchCommands = new();
    private readonly HashSet<uint> _assertedSwitches = [];
    private short[] _audioSamples = [];
    private int _audioSampleRate;
    private int _audioChannelCount;
    private int _audioFramesPerPump;
    private AmberBridgeCapabilities? _amberCapabilities;
    private AmberLampState[] _lastLamps = [];
    private int[] _lastReels = [];
    private uint[] _lastSevenSegments = [];
    private ushort[] _lastAlpha = [];
    private bool _firstSnapshot, _firstAudio;
    private readonly object _stateGate = new();
    private IAmberBridgeLibrary? _bridge;
    private CancellationTokenSource? _runCancellation;
    private Task? _runTask;
    private EmulationBackendState _state = EmulationBackendState.Stopped;
    private bool _shutdown;
    private bool _disposed;

    public System6NativeBackend(string bridgePath)
        : this(bridgePath, static path => new AmberBridgeLibrary(path), new NAudioEmulationAudioSink()) { }

    internal System6NativeBackend(string bridgePath, Func<string, IAmberBridgeLibrary> bridgeFactory, IEmulationAudioSink? audioSink = null,
        Func<bool>? comparisonEnabled = null, Action<string>? comparisonSink = null)
    {
        if (string.IsNullOrWhiteSpace(bridgePath))
            throw new ArgumentException("Amber Bridge DLL path must not be empty.", nameof(bridgePath));
        _bridgePath = bridgePath;
        _bridgeFactory = bridgeFactory ?? throw new ArgumentNullException(nameof(bridgeFactory));
        _audioSink = audioSink ?? new NullAudioSink();
        _comparisonEnabled = comparisonEnabled ?? (() => false);
        _comparisonSink = comparisonSink;
    }

    public EmulationBackendKind BackendKind => EmulationBackendKind.NativeSystem6;
    public EmulationBackendCapabilities Capabilities => BackendCapabilities;
    public EmulationBackendState State { get { lock (_stateGate) return _state; } }
    internal int LastCyclesRun { get; private set; }

    public event EventHandler<EmulationBackendState>? StateChanged;
    public event EventHandler<MachineLampChangedEventArgs>? LampChanged;
    public event EventHandler<MachineReelChangedEventArgs>? ReelChanged;
    public event EventHandler<MachineSegmentChangedEventArgs>? SegmentChanged;
    public event EventHandler<MachineVfdBrightnessChangedEventArgs>? VfdBrightnessChanged;
    public event EventHandler<MachineDotMatrixChangedEventArgs>? DotMatrixChanged { add { } remove { } }

    public Task StartAsync(EmulationLaunchRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        if (State != EmulationBackendState.Stopped)
            throw new InvalidOperationException($"System6 native backend cannot start while it is {State}.");

        SetState(EmulationBackendState.Starting);
        var comparison = new AmberComparisonSession(_comparisonEnabled(), "direct", _comparisonSink);
        _comparison = comparison.Enabled ? comparison : null;
        _pumpIteration = 0;
        _comparison?.Write("ComparisonStart", $"selected_backend:Direct_Amber|platform:{request.Platform}|machine:{request.MachineName}|process_id:{Environment.ProcessId}|amber_dll:{AmberComparisonSession.SafeFileName(_bridgePath)}");
        _comparison?.Write("BackendCreate");
        try
        {
            if (!Path.IsPathFullyQualified(_bridgePath))
                throw new InvalidOperationException($"Amber Bridge path must be absolute: '{_bridgePath}'.");
            if (!string.Equals(Path.GetFileName(_bridgePath), "AmberBridge.dll", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("System6 native backend must be configured with AmberBridge.dll, not the JPM core DLL.");
            if (!File.Exists(_bridgePath))
                throw new FileNotFoundException("AmberBridge.dll was not found.", _bridgePath);

            var roms = request.System6NativeRoms
                ?? throw new InvalidOperationException("System6 native backend requires native ROM settings.");
            var programs = ValidateRomPaths(roms.ProgramRomPaths, requireTwoProgramRoms: true, "program");
            var sounds = ValidateRomPaths(roms.SoundRomPaths, requireTwoProgramRoms: false, "sound");

            _comparison?.Write("LoadProgramRoms", AmberComparisonSession.RomSummary("program", roms.ProgramRomPaths));
            _comparison?.Write("LoadSoundRoms", AmberComparisonSession.RomSummary("sound", roms.SoundRomPaths));

            _bridge = _bridgeFactory(_bridgePath);
            _comparison?.Write("LibraryLoad", $"filename:{AmberComparisonSession.SafeFileName(_bridgePath)}");
            _amberCapabilities = _bridge.GetCapabilities();
            ValidateRequiredCapabilities(_amberCapabilities);
            Debug.WriteLine($"Amber Bridge: Capabilities read; mask 0x{_amberCapabilities.RawFeatureBits:X16}; maximum switches {_amberCapabilities.MaximumSwitches}.");
            _bridge.Initialise(programs, sounds);
            _comparison?.Write("Initialise");
            _bridge.Reset(); // Preserve the direct backend's post-ROM-load startup reset.
            _comparison?.Write("Reset", "startup:true");
            var audioFormat = _bridge.GetAudioFormat();
            _comparison?.Write("GetAudioFormat", $"sample_rate:{audioFormat.SampleRate}|channels:{audioFormat.Channels}|bits_per_sample:16|encoding:pcm_signed|interleaving:{audioFormat.Interleaving}");
            ValidateAudioFormat(audioFormat);
            _audioSampleRate = checked((int)audioFormat.SampleRate);
            _audioChannelCount = checked((int)audioFormat.Channels);
            _audioFramesPerPump = CalculateFramesPerPump(_audioSampleRate, EmulationPumpHz);
            _audioSamples = new short[checked(_audioFramesPerPump * _audioChannelCount)];
            _audioSink.Start(new((int)audioFormat.SampleRate, (int)audioFormat.Channels, 16));
            _comparison?.Write("AudioSink", "state:started");
            ApplyProjectConfiguration(roms);
            _shutdown = false;
            var details = _bridge.BridgeDetails;
            Debug.WriteLine($"Amber Bridge product metadata: {details.Name} {details.BridgeVersion}; compatibility {AmberApiVersions.Format(details.ApiVersion)}.");
            Debug.WriteLine($"System6 Amber Bridge startup: negotiated {AmberApiVersions.Format(_bridge.NegotiatedApiVersion)}, table size {_bridge.NegotiatedApiTableSize}; core jpm-system6; program ROMs={programs.Count}; sound ROMs={sounds.Count}.");

            _runCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            Debug.WriteLine("System6 Amber Bridge startup: Run loop starting.");
            _runTask = Task.Run(() => RunLoopAsync(_runCancellation.Token), CancellationToken.None);
            SetState(EmulationBackendState.Running);
            return Task.CompletedTask;
        }
        catch (Exception exception)
        {
            _comparison?.Write("Failure", result: "failure", summary: exception.GetType().Name);
            DisposeBridgeAfterFailure();
            SetState(EmulationBackendState.Failed);
            throw;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_disposed || State == EmulationBackendState.Stopped) return;
        SetState(EmulationBackendState.Stopping);
        if (_runCancellation is not null) await _runCancellation.CancelAsync().ConfigureAwait(false);
        _comparison?.Write("Cancellation", "expected:true|operation:pump");
        if (_runTask is not null)
        {
            try { await _runTask.WaitAsync(cancellationToken).ConfigureAwait(false); }
            catch (OperationCanceledException) when (_runCancellation?.IsCancellationRequested == true) { }
            catch { /* Cleanup must still run after a failed run loop. */ }
        }
        try { ShutdownBridgeOnce(); }
        finally
        {
            _comparison?.Write("Shutdown");
            _audioSink.Stop();
            _comparison?.Write("AudioSink", "state:stopped");
            _bridge?.Dispose();
            _comparison?.Write("Destroy");
            _bridge = null;
        }
        _runCancellation?.Dispose();
        _runCancellation = null;
        _runTask = null;
        SetState(EmulationBackendState.Stopped);
        _comparison?.Write("ComparisonEnd");
        _comparison = null;
    }

    public Task PauseAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        if (State == EmulationBackendState.Running) SetState(EmulationBackendState.Paused);
        return Task.CompletedTask;
    }

    public Task ResumeAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        if (State == EmulationBackendState.Paused) SetState(EmulationBackendState.Running);
        return Task.CompletedTask;
    }

    public Task ResetAsync(EmulationResetKind resetKind, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        if (resetKind is not EmulationResetKind.Soft and not EmulationResetKind.Hard)
            throw new ArgumentOutOfRangeException(nameof(resetKind), resetKind, null);
        ReleaseAssertedSwitches();
        RequireActiveBridge().Reset();
        _audioSink.Clear();
        ClearOutputCaches();
        RequireActiveBridge().GetOutputSnapshot(_snapshot);
        return Task.CompletedTask;
    }

    public Task SetInputStateAsync(InputDefinitionModel inputDefinition, bool isPressed, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(inputDefinition);
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        if (!uint.TryParse(inputDefinition.ButtonNumber, out var switchIndex))
            throw new InvalidOperationException($"System 6 input '{inputDefinition.Id}' has no numeric switch mapping.");
        if (_amberCapabilities is null || switchIndex >= _amberCapabilities.MaximumSwitches)
            throw new ArgumentOutOfRangeException(nameof(inputDefinition), $"Switch {switchIndex} exceeds the bridge capability limit.");
        _switchCommands.Enqueue((switchIndex, isPressed));
        _comparison?.WriteBounded("input", AmberComparisonSession.InputLimit, "SubmitInput", $"oasis_id:{inputDefinition.Id}|switch_index:{switchIndex}|active:{isPressed.ToString().ToLowerInvariant()}|duplicate_suppression:false|shutdown_release:false", "queued");
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        await StopAsync(CancellationToken.None).ConfigureAwait(false);
        _audioSink.Dispose();
        _disposed = true;
    }

    internal int RunCycles(long cycles)
    {
        if (cycles < 0 || cycles > uint.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(cycles), cycles, "Amber Bridge cycle requests must be between 0 and UInt32.MaxValue.");
        var result = RequireActiveBridge().Run(checked((uint)cycles));
        LastCyclesRun = result; // Deliberately signed: negative bridge diagnostics must not wrap.
        return result;
    }

    private async Task RunLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            var pumpTicks = Math.Max(1L, Stopwatch.Frequency / EmulationPumpHz);
            var nextDeadline = Stopwatch.GetTimestamp();
            while (!cancellationToken.IsCancellationRequested)
            {
                if (State != EmulationBackendState.Running)
                {
                    await Task.Delay(PumpInterval, cancellationToken).ConfigureAwait(false);
                    nextDeadline = Stopwatch.GetTimestamp();
                    continue;
                }
                var iteration = ++_pumpIteration;
                var advanceStarted = Stopwatch.GetTimestamp();
                var cyclesRun = RunCycles(System6ClockHz / EmulationPumpHz);
                var durationNs = (Stopwatch.GetTimestamp() - advanceStarted) * 1_000_000_000L / Stopwatch.Frequency;
                _comparison?.WriteBounded("advance", AmberComparisonSession.AdvanceLimit, "Advance", $"iteration:{iteration}|elapsed_ns:1000000|time_source:fixed|requested_cycles:8000|native_run_calls:1|native_return:{cyclesRun}|maximum_catch_up:3|clamping:true|accumulated_remainder:false|duration_ns:{durationNs}");
                ProcessSwitchCommands();
                PollOutputsAndAudio();

                nextDeadline += pumpTicks;
                var now = Stopwatch.GetTimestamp();
                // Never run an unbounded sequence of immediate catch-up slices after a stall.
                if (now - nextDeadline > pumpTicks * 3)
                    nextDeadline = now + pumpTicks;

                var remainingTicks = nextDeadline - Stopwatch.GetTimestamp();
                if (remainingTicks > 0)
                {
                    var remaining = TimeSpan.FromSeconds((double)remainingTicks / Stopwatch.Frequency);
                    await Task.Delay(remaining, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    await Task.Yield();
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch
        {
            SetState(EmulationBackendState.Failed);
            throw;
        }
    }

    private void ProcessSwitchCommands()
    {
        var bridge = RequireActiveBridge();
        while (_switchCommands.TryDequeue(out var command))
        {
            bridge.SetSwitchState(command.Index, command.IsOn);
            if (command.IsOn) _assertedSwitches.Add(command.Index); else _assertedSwitches.Remove(command.Index);
        }
    }

    private void PollOutputsAndAudio()
    {
        var bridge = RequireActiveBridge();
        bridge.GetOutputSnapshot(_snapshot);
        if (_comparison?.Enabled == true)
        {
            var fingerprint = string.Join(',', Enumerable.Range(0, (int)_snapshot.MatrixLampCount).Select(i => _snapshot.GetLamp(i))) + ";" +
                string.Join(',', Enumerable.Range(0, (int)_snapshot.ReelCount).Select(i => _snapshot.GetReelPosition(i))) + ";" +
                string.Join(',', Enumerable.Range(0, (int)_snapshot.SevenSegmentDisplayCount).Select(i => _snapshot.GetSevenSegmentDisplay(i).SegmentMask));
            var change = _comparison.TrackSnapshot(fingerprint);
            _comparison.WriteBounded("snapshot", AmberComparisonSession.SnapshotLimit, "GetSnapshot", $"lamp_count:{_snapshot.MatrixLampCount}|reel_count:{_snapshot.ReelCount}|reel_positions:{string.Join(',', Enumerable.Range(0, (int)_snapshot.ReelCount).Select(i => _snapshot.GetReelPosition(i)))}|character_display_count:{_snapshot.AlphaDisplayCount}|segment_display_count:{_snapshot.SevenSegmentDisplayCount}|{change}");
        }
        if (!_firstSnapshot) { _firstSnapshot = true; Debug.WriteLine($"Amber Bridge: First output snapshot received; lamps={_snapshot.MatrixLampCount}, reels={_snapshot.ReelCount}, alpha={_snapshot.AlphaDisplayCount}, sevenSegment={_snapshot.SevenSegmentDisplayCount}."); }
        TranslateSnapshot();
        var writtenFrames = bridge.FillAudioFrames(_audioSamples, checked((uint)_audioFramesPerPump));
        var writtenSamples = checked((int)writtenFrames * _audioChannelCount);
        var bytes = MemoryMarshal.AsBytes(_audioSamples.AsSpan(0, writtenSamples));
        if (!bytes.IsEmpty) _audioSink.PushPcm(bytes);
        if (_comparison is not null)
        {
            var samples = _audioSamples.AsSpan(0, writtenSamples);
            var nonzero = 0; var peak = 0;
            foreach (var sample in samples) { if (sample != 0) nonzero++; peak = Math.Max(peak, Math.Abs((int)sample)); }
            _comparison.WriteBounded("audio", AmberComparisonSession.AudioLimit, "ReadAudio", $"requested_frames:{_audioFramesPerPump}|returned_frames:{writtenFrames}|nonzero_samples:{nonzero}|peak_absolute_sample:{peak}|submitted_frames:{writtenFrames}");
        }
        if (!_firstAudio) { _firstAudio = true; Debug.WriteLine($"Amber Bridge: First audio fill completed; sampleRate={_audioSampleRate} Hz, pumpRate={EmulationPumpHz} Hz, framesRequested={_audioFramesPerPump}, framesWritten={writtenFrames}, samplesWritten={writtenSamples}, bytesSubmitted={bytes.Length}."); }
    }

    private void TranslateSnapshot()
    {
        if (_lastLamps.Length != _snapshot.MatrixLampCount) _lastLamps = Enumerable.Repeat(new AmberLampState(false, double.NaN), (int)_snapshot.MatrixLampCount).ToArray();
        for (var i = 0; i < _lastLamps.Length; i++) { var value = _snapshot.GetLamp(i); if (value == _lastLamps[i]) continue; _lastLamps[i] = value; LampChanged?.Invoke(this, new(i, value.IsOn ? Math.Max(1, (int)Math.Round(value.Brightness * 255)) : 0)); }
        if (_lastReels.Length != _snapshot.ReelCount) _lastReels = Enumerable.Repeat(int.MinValue, (int)_snapshot.ReelCount).ToArray();
        for (var i = 0; i < _lastReels.Length; i++) { var value = _snapshot.GetReelPosition(i); if (value == _lastReels[i]) continue; _lastReels[i] = value; ReelChanged?.Invoke(this, new(i, value)); }
        if (_lastAlpha.Length != _snapshot.AlphaDisplayCount * 16) _lastAlpha = Enumerable.Repeat(ushort.MaxValue, (int)_snapshot.AlphaDisplayCount * 16).ToArray();
        for (var display = 0; display < _snapshot.AlphaDisplayCount; display++) { var brightness = _snapshot.GetAlphaBrightness(display); for (var cell = 0; cell < 16; cell++) { var index = display * 16 + cell; var mask = _snapshot.GetAlphaSegmentMask(display, cell); if (_lastAlpha[index] != mask) { _lastAlpha[index] = mask; SegmentChanged?.Invoke(this, new(index, mask, MameSegmentOutputType.NativeAlpha)); } VfdBrightnessChanged?.Invoke(this, new(index, brightness)); } }
        if (_lastSevenSegments.Length != _snapshot.SevenSegmentDisplayCount) _lastSevenSegments = Enumerable.Repeat(uint.MaxValue, (int)_snapshot.SevenSegmentDisplayCount).ToArray();
        for (var i = 0; i < _lastSevenSegments.Length; i++) { var value = _snapshot.GetSevenSegmentDisplay(i); if (_lastSevenSegments[i] == value.SegmentMask) continue; _lastSevenSegments[i] = value.SegmentMask; SegmentChanged?.Invoke(this, new(i, unchecked((int)value.SegmentMask), MameSegmentOutputType.Digit)); }
    }

    private void ApplyProjectConfiguration(System6NativeRomSettings settings)
    {
        var bridge = RequireActiveBridge(); var caps = _amberCapabilities!;
        _comparison?.Write("ConfigureReels", string.Join(",", settings.ReelOptos.Select(r => $"index:{r.ReelIndex}|enabled:{r.Enabled}|steps:{r.Steps}|opto_start:{r.OptoStart}|opto_end:{r.OptoEnd}|opto_invert:{r.OptoInvert}|apply:true")));
        _comparison?.Write("ConfigureCoins", string.Join(",", settings.Coins.Select(c => $"index:{c.Num}|enabled:{c.Enabled}|value:{c.CoinValue}|lockout_invert:{c.LockoutInvert}|port_index:{c.PortIndex}|coin_code:{c.Coin}|level:{c.Level}|full_level:{c.FullLevel}")));
        _comparison?.Write("ConfigurePercentage", $"raw_value:{settings.PercentSwitchValue}");
        if (settings.ReelOptos.Count != 0) { if (!caps.SupportsReelConfiguration) throw new InvalidOperationException("Project contains reel configuration but the bridge does not support it."); var reels = settings.ReelOptos.Select(r => new AmberReelConfigurationEntry((uint)r.ReelIndex, r.Enabled, (uint)r.Steps, (uint)r.OptoStart, (uint)r.OptoEnd, r.OptoInvert)).ToArray(); bridge.ConfigureReels(new(reels.Aggregate(0u, (m, r) => m | 1u << (int)r.Index), reels)); }
        if (settings.Coins.Any(c => c.Enabled)) { if (!caps.SupportsCoinConfiguration) throw new InvalidOperationException("Project contains coin configuration but the bridge does not support it."); var channels = settings.Coins.Where(c => c.Enabled).Select(c => new AmberCoinChannelConfiguration((uint)c.Num, c.CoinEnable != 0, (uint)c.CoinValue, c.LockoutInvert != 0)).ToArray(); var routes = settings.Coins.Where(c => c.Enabled).Select(c => new AmberCoinRouteConfiguration((uint)c.Num, c.Enabled, (uint)c.CounterIn, (uint)c.CounterOut, (uint)c.PortIndex, (uint)c.Coin, (uint)c.Level, (uint)c.FullLevel)).ToArray(); bridge.ConfigureCoins(new(channels.Aggregate(0u, (m, c) => m | 1u << (int)c.Index), routes.Aggregate(0u, (m, r) => m | 1u << (int)r.Index), channels, routes)); }
        if (caps.SupportsPercentageSwitch) bridge.SetPercentageSwitch(checked((uint)settings.PercentSwitchValue));
    }

    private static void ValidateRequiredCapabilities(AmberBridgeCapabilities c) { var missing = new List<string>(); if (!c.SupportsSwitchInput) missing.Add("switch input"); if (!c.SupportsOutputSnapshots) missing.Add("output snapshots"); if (!c.SupportsAudio) missing.Add("audio"); if (missing.Count != 0) throw new InvalidOperationException($"Amber Bridge API v2 core jpm-system6 is missing required capabilities: {string.Join(", ", missing)}. Raw mask=0x{c.RawFeatureBits:X16}."); }
    private static void ValidateAudioFormat(AmberAudioFormat f) { if (f is not { SampleRate: 48000, Channels: 2, SampleFormat: 1, Interleaving: 1 }) throw new NotSupportedException($"Unsupported Amber audio format: rate={f.SampleRate}, channels={f.Channels}, sampleFormat={f.SampleFormat}, interleaving={f.Interleaving}."); CalculateFramesPerPump(checked((int)f.SampleRate), EmulationPumpHz); Debug.WriteLine("Amber Bridge: Audio format 48000 Hz, 2 channels, signed PCM16, interleaved."); }
    internal static int CalculateFramesPerPump(int sampleRate, int pumpRate)
    {
        if (sampleRate <= 0) throw new ArgumentOutOfRangeException(nameof(sampleRate));
        if (pumpRate <= 0) throw new ArgumentOutOfRangeException(nameof(pumpRate));
        if (sampleRate % pumpRate != 0)
            throw new NotSupportedException($"Audio sample rate {sampleRate} Hz is not evenly divisible by the {pumpRate} Hz emulation pump.");
        return sampleRate / pumpRate;
    }
    private void ReleaseAssertedSwitches() { if (_bridge is not null) foreach (var index in _assertedSwitches) _bridge.SetSwitchState(index, false); _assertedSwitches.Clear(); while (_switchCommands.TryDequeue(out _)) { } }
    private void ClearOutputCaches() { _lastLamps = []; _lastReels = []; _lastAlpha = []; _lastSevenSegments = []; }

    private static IReadOnlyList<string> ValidateRomPaths(IReadOnlyList<string> paths, bool requireTwoProgramRoms, string kind)
    {
        ArgumentNullException.ThrowIfNull(paths);
        if (paths.Count > 4) throw new InvalidOperationException($"Amber Bridge supports at most four {kind} ROM paths; none will be truncated.");
        if (requireTwoProgramRoms && (paths.Count < 2 || string.IsNullOrWhiteSpace(paths[0]) || string.IsNullOrWhiteSpace(paths[1])))
            throw new InvalidOperationException("System6 native backend requires Program ROM 1 and Program ROM 2 before starting.");

        var result = new List<string>(paths.Count);
        var sawEmptySlot = false;
        for (var index = 0; index < paths.Count; index++)
        {
            var path = paths[index];
            if (string.IsNullOrWhiteSpace(path)) { sawEmptySlot = true; continue; }
            if (sawEmptySlot)
                throw new InvalidOperationException($"System6 native {kind} ROM paths must occupy consecutive slots so their ordering is preserved.");
            if (!File.Exists(path)) throw new FileNotFoundException($"System6 native {kind} ROM {index + 1} was not found.", path);
            result.Add(path);
        }
        return result;
    }

    private IAmberBridgeLibrary RequireActiveBridge()
    {
        ThrowIfDisposed();
        if (_shutdown || _bridge is null || State is EmulationBackendState.Stopped or EmulationBackendState.Stopping or EmulationBackendState.Failed)
            throw new InvalidOperationException("System6 Amber Bridge backend is not active.");
        return _bridge;
    }

    private void ShutdownBridgeOnce()
    {
        if (_bridge is null || _shutdown) return;
        var releasedCount = _assertedSwitches.Count;
        ReleaseAssertedSwitches();
        _comparison?.Write("SubmitInput", "shutdown_release:true", "success", $"released_count:{releasedCount}");
        _shutdown = true;
        _bridge.Shutdown();
    }

    private void DisposeBridgeAfterFailure()
    {
        try { _bridge?.Dispose(); } finally { _bridge = null; _runCancellation?.Dispose(); _runCancellation = null; _runTask = null; }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(System6NativeBackend));
    }

    private void SetState(EmulationBackendState state)
    {
        lock (_stateGate) _state = state;
        StateChanged?.Invoke(this, state);
    }

    private sealed class NullAudioSink : IEmulationAudioSink
    { public void Start(EmulationAudioFormat format) { } public void PushPcm(ReadOnlySpan<byte> pcmBytes) { } public void Stop() { } public void Clear() { } public void Dispose() { } }
}
