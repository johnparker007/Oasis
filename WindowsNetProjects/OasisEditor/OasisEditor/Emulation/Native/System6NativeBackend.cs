using System.Diagnostics;
using System.IO;

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
    private readonly object _stateGate = new();
    private IAmberBridgeLibrary? _bridge;
    private CancellationTokenSource? _runCancellation;
    private Task? _runTask;
    private EmulationBackendState _state = EmulationBackendState.Stopped;
    private bool _shutdown;
    private bool _disposed;

    public System6NativeBackend(string bridgePath)
        : this(bridgePath, static path => new AmberBridgeLibrary(path)) { }

    internal System6NativeBackend(string bridgePath, Func<string, IAmberBridgeLibrary> bridgeFactory)
    {
        if (string.IsNullOrWhiteSpace(bridgePath))
            throw new ArgumentException("Amber Bridge DLL path must not be empty.", nameof(bridgePath));
        _bridgePath = bridgePath;
        _bridgeFactory = bridgeFactory ?? throw new ArgumentNullException(nameof(bridgeFactory));
    }

    public EmulationBackendKind BackendKind => EmulationBackendKind.NativeSystem6;
    public EmulationBackendCapabilities Capabilities => BackendCapabilities;
    public EmulationBackendState State { get { lock (_stateGate) return _state; } }
    internal int LastCyclesRun { get; private set; }

    public event EventHandler<EmulationBackendState>? StateChanged;
    // Output polling is intentionally disabled until Amber Bridge exposes snapshots.
    public event EventHandler<MachineLampChangedEventArgs>? LampChanged { add { } remove { } }
    public event EventHandler<MachineReelChangedEventArgs>? ReelChanged { add { } remove { } }
    public event EventHandler<MachineSegmentChangedEventArgs>? SegmentChanged { add { } remove { } }
    public event EventHandler<MachineVfdBrightnessChangedEventArgs>? VfdBrightnessChanged { add { } remove { } }
    public event EventHandler<MachineDotMatrixChangedEventArgs>? DotMatrixChanged { add { } remove { } }

    public Task StartAsync(EmulationLaunchRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        if (State != EmulationBackendState.Stopped)
            throw new InvalidOperationException($"System6 native backend cannot start while it is {State}.");

        SetState(EmulationBackendState.Starting);
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

            _bridge = _bridgeFactory(_bridgePath);
            _bridge.Initialise(programs, sounds);
            _bridge.Reset(); // Preserve the direct backend's post-ROM-load startup reset.
            _shutdown = false;
            var details = _bridge.BridgeDetails;
            Debug.WriteLine($"System6 Amber Bridge startup: {details.Name} {details.BridgeVersion}; {AmberApiVersions.Format(details.ApiVersion)}; core jpm-system6; program ROMs={programs.Count}; sound ROMs={sounds.Count}.");

            _runCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            Debug.WriteLine("System6 Amber Bridge startup: Run loop starting.");
            _runTask = Task.Run(() => RunLoopAsync(_runCancellation.Token), CancellationToken.None);
            SetState(EmulationBackendState.Running);
            return Task.CompletedTask;
        }
        catch
        {
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
        if (_runTask is not null)
        {
            try { await _runTask.WaitAsync(cancellationToken).ConfigureAwait(false); }
            catch (OperationCanceledException) when (_runCancellation?.IsCancellationRequested == true) { }
            catch { /* Cleanup must still run after a failed run loop. */ }
        }
        try { ShutdownBridgeOnce(); }
        finally
        {
            _bridge?.Dispose();
            _bridge = null;
        }
        _runCancellation?.Dispose();
        _runCancellation = null;
        _runTask = null;
        SetState(EmulationBackendState.Stopped);
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
        RequireActiveBridge().Reset();
        return Task.CompletedTask;
    }

    public Task SetInputStateAsync(InputDefinitionModel inputDefinition, bool isPressed, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(inputDefinition);
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        throw new NotSupportedException("Switch input is not supported by Amber Bridge v0.1.1; it is deferred to a future bridge API.");
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        await StopAsync(CancellationToken.None).ConfigureAwait(false);
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
            while (!cancellationToken.IsCancellationRequested)
            {
                if (State != EmulationBackendState.Running)
                {
                    await Task.Delay(PumpInterval, cancellationToken).ConfigureAwait(false);
                    continue;
                }
                RunCycles(System6ClockHz / EmulationPumpHz);
                // A zero result still yields to the existing cadence, avoiding a busy loop.
                await Task.Delay(PumpInterval, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch
        {
            SetState(EmulationBackendState.Failed);
            throw;
        }
    }

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
}
