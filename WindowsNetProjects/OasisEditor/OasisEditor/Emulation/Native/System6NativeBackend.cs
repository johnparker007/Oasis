using System.Globalization;

namespace OasisEditor;

public sealed class System6NativeBackend : IEmulationBackend
{
    private const int DefaultFramesPerSecond = 60;
    private const int System6ClockHz = 8000000;
    private const int LampCount = 32;
    private const int ReelCount = 16;

    private static readonly EmulationBackendCapabilities System6Capabilities = new(
        SupportsPause: true,
        SupportsResume: true,
        SupportsSoftReset: true,
        SupportsHardReset: true,
        SupportsSaveState: false,
        SupportsLoadState: false,
        SupportsThrottle: false,
        SupportsDebugger: false);

    private readonly string _libraryPath;
    private readonly Func<string, ISystem6NativeLibrary> _libraryFactory;
    private readonly TimeSpan _pollInterval;
    private readonly int _cyclesPerFrame;
    private readonly object _stateGate = new();
    private readonly int[] _lastLampValues = Enumerable.Repeat(-1, LampCount).ToArray();
    private readonly int[] _lastReelPositions = Enumerable.Repeat(int.MinValue, ReelCount).ToArray();

    private ISystem6NativeLibrary? _library;
    private CancellationTokenSource? _runLoopCancellation;
    private Task? _runLoopTask;
    private EmulationBackendState _state = EmulationBackendState.Stopped;

    public System6NativeBackend(string libraryPath)
        : this(libraryPath, static path => new System6NativeLibrary(path))
    {
    }

    public System6NativeBackend(string libraryPath, Func<string, ISystem6NativeLibrary> libraryFactory)
        : this(libraryPath, libraryFactory, DefaultFramesPerSecond)
    {
    }

    public System6NativeBackend(string libraryPath, Func<string, ISystem6NativeLibrary> libraryFactory, int framesPerSecond)
    {
        if (string.IsNullOrWhiteSpace(libraryPath))
        {
            throw new ArgumentException("System6 native core library path must not be empty.", nameof(libraryPath));
        }

        if (framesPerSecond <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(framesPerSecond), framesPerSecond, "System6 polling rate must be greater than zero.");
        }

        _libraryPath = libraryPath;
        _libraryFactory = libraryFactory ?? throw new ArgumentNullException(nameof(libraryFactory));
        _pollInterval = TimeSpan.FromMilliseconds(1000.0 / framesPerSecond);
        _cyclesPerFrame = System6ClockHz / framesPerSecond;
    }

    public EmulationBackendState State
    {
        get
        {
            lock (_stateGate)
            {
                return _state;
            }
        }
    }

    public EmulationBackendCapabilities Capabilities => System6Capabilities;

    public event EventHandler<EmulationBackendState>? StateChanged;

    public event EventHandler<MachineLampChangedEventArgs>? LampChanged;
    public event EventHandler<MachineReelChangedEventArgs>? ReelChanged;
    public event EventHandler<MachineSegmentChangedEventArgs>? SegmentChanged;
    public event EventHandler<MachineVfdBrightnessChangedEventArgs>? VfdBrightnessChanged;
    public event EventHandler<MachineDotMatrixChangedEventArgs>? DotMatrixChanged;

    public Task StartAsync(EmulationLaunchRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (State is not EmulationBackendState.Stopped)
        {
            throw new InvalidOperationException($"System6 native backend cannot start while it is {State}.");
        }

        SetState(EmulationBackendState.Starting);

        try
        {
            if (request.RomPaths.Count == 0)
            {
                throw new InvalidOperationException($"System6 native backend requires at least one ROM path before starting core '{_libraryPath}'.");
            }

            _library = _libraryFactory(_libraryPath);
            try
            {
                _library.Initialise();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"System6 native backend failed to initialise core '{_libraryPath}'.", ex);
            }

            foreach (var romPath in request.RomPaths)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    _library.LoadRom(romPath);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"System6 native backend failed to load ROM '{romPath}' with core '{_libraryPath}'.", ex);
                }
            }

            try
            {
                _library.Reset();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"System6 native backend failed to reset core '{_libraryPath}' during startup.", ex);
            }
            ResetCachedOutputState();

            _runLoopCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _runLoopTask = Task.Run(() => RunLoopAsync(_runLoopCancellation.Token), CancellationToken.None);
            SetState(EmulationBackendState.Running);
            return Task.CompletedTask;
        }
        catch
        {
            CleanupLibraryAfterFailedStart();
            SetState(EmulationBackendState.Failed);
            throw;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (State is EmulationBackendState.Stopped)
        {
            return;
        }

        SetState(EmulationBackendState.Stopping);

        var runLoopCancellation = _runLoopCancellation;
        if (runLoopCancellation is not null)
        {
            await runLoopCancellation.CancelAsync().ConfigureAwait(false);
        }

        if (_runLoopTask is not null)
        {
            try
            {
                await _runLoopTask.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (runLoopCancellation?.IsCancellationRequested == true)
            {
            }
            catch
            {
                // The run loop moves the backend to Failed before faulting. Stop should still
                // release the native core and return the backend to a clean stopped state.
            }
        }

        ShutdownAndDisposeLibrary();
        _runLoopCancellation?.Dispose();
        _runLoopCancellation = null;
        _runLoopTask = null;
        SetState(EmulationBackendState.Stopped);
    }

    public Task PauseAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (State is EmulationBackendState.Running)
        {
            SetState(EmulationBackendState.Paused);
        }

        return Task.CompletedTask;
    }

    public Task ResumeAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (State is EmulationBackendState.Paused)
        {
            SetState(EmulationBackendState.Running);
        }

        return Task.CompletedTask;
    }

    public Task ResetAsync(EmulationResetKind resetKind, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (resetKind is not EmulationResetKind.Soft and not EmulationResetKind.Hard)
        {
            throw new ArgumentOutOfRangeException(nameof(resetKind), resetKind, null);
        }

        var library = _library ?? throw new InvalidOperationException("System6 native backend cannot reset before it has started.");
        library.Reset();
        ResetCachedOutputState();
        return Task.CompletedTask;
    }

    public Task SetInputStateAsync(MachineInputReference input, bool isPressed, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var library = _library ?? throw new InvalidOperationException("System6 native backend cannot set input state before it has started.");
        if (!int.TryParse(input.Id, NumberStyles.Integer, CultureInfo.InvariantCulture, out var switchIndex))
        {
            throw new InvalidOperationException($"System6 native backend input '{input.Id}' is not a numeric switch index.");
        }

        if (isPressed)
        {
            library.TurnSwitchOn(switchIndex);
        }
        else
        {
            library.TurnSwitchOff(switchIndex);
        }

        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync(CancellationToken.None).ConfigureAwait(false);
    }

    private async Task RunLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (State is EmulationBackendState.Running)
                {
                    var library = _library;
                    if (library is not null)
                    {
                        library.Run(_cyclesPerFrame);
                        PollOutputs(library);
                    }
                }

                await Task.Delay(_pollInterval, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch
        {
            SetState(EmulationBackendState.Failed);
            throw;
        }
    }

    private void PollOutputs(ISystem6NativeLibrary library)
    {
        var lampsOn = library.GetLampsOn();
        for (var lampIndex = 0; lampIndex < LampCount; lampIndex++)
        {
            var isOn = (lampsOn & (1 << lampIndex)) != 0;
            var value = isOn ? library.GetLampBrightness(lampIndex) : 0;
            if (_lastLampValues[lampIndex] != value)
            {
                _lastLampValues[lampIndex] = value;
                LampChanged?.Invoke(this, new MachineLampChangedEventArgs(lampIndex, value));
            }
        }

        for (var reelIndex = 0; reelIndex < ReelCount; reelIndex++)
        {
            var position = library.GetPosOut(reelIndex);
            if (_lastReelPositions[reelIndex] != position)
            {
                _lastReelPositions[reelIndex] = position;
                ReelChanged?.Invoke(this, new MachineReelChangedEventArgs(reelIndex, position));
            }
        }
    }

    private void ResetCachedOutputState()
    {
        Array.Fill(_lastLampValues, -1);
        Array.Fill(_lastReelPositions, int.MinValue);
    }

    private void CleanupLibraryAfterFailedStart()
    {
        ShutdownAndDisposeLibrary();
        _runLoopCancellation?.Dispose();
        _runLoopCancellation = null;
        _runLoopTask = null;
    }

    private void ShutdownAndDisposeLibrary()
    {
        var library = _library;
        _library = null;
        if (library is null)
        {
            return;
        }

        try
        {
            library.Shutdown();
        }
        finally
        {
            library.Dispose();
        }
    }

    private void SetState(EmulationBackendState state)
    {
        var changed = false;
        lock (_stateGate)
        {
            if (_state != state)
            {
                _state = state;
                changed = true;
            }
        }

        if (changed)
        {
            StateChanged?.Invoke(this, state);
        }
    }
}
