using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace OasisEditor;

public sealed class System6NativeBackend : IEmulationBackend
{
    private const int DefaultFramesPerSecond = 60;
    private const int System6ClockHz = 8000000;
    private const int LampCount = 32;
    private const int ReelCount = 16;
    private const int AlphaCharacterCount = 16;
    private const int SupportedReelOptoCount = 8;
    private const string DiagnosticStageEnvironmentVariable = "OASIS_SYSTEM6_STARTUP_STAGE";
    private static readonly TimeSpan SlowRunWarningThreshold = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan RunWarningThrottleInterval = TimeSpan.FromSeconds(10);

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
    private string? _lastAlphaString;
    private bool _hasLoggedAlphaUnavailable;

    private ISystem6NativeLibrary? _library;
    private CancellationTokenSource? _runLoopCancellation;
    private Task? _runLoopTask;
    private EmulationBackendState _state = EmulationBackendState.Stopped;
    private DateTime _lastRunWarningUtc = DateTime.MinValue;

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
    public event EventHandler<MachineSegmentChangedEventArgs>? SegmentChanged
    {
        add { }
        remove { }
    }

    public event EventHandler<MachineVfdBrightnessChangedEventArgs>? VfdBrightnessChanged
    {
        add { }
        remove { }
    }

    public event EventHandler<MachineDotMatrixChangedEventArgs>? DotMatrixChanged
    {
        add { }
        remove { }
    }

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
            if (string.IsNullOrWhiteSpace(_libraryPath) || !File.Exists(_libraryPath))
            {
                throw new InvalidOperationException($"System6 native backend DLL path is missing or does not exist: '{_libraryPath}'.");
            }

            var startupStage = ResolveStartupStage();
            LogStartupStage($"startup diagnostic stage = {startupStage}");

            _library = _libraryFactory(_libraryPath);
            LogStartupStage("native DLL loaded and exports bound");
            if (startupStage is System6StartupStage.LoadBindOnly)
            {
                return CompleteStagedStartup();
            }

            try
            {
                var initialiseResult = _library.Initialise();
                LogStartupStage($"Initialise returned {initialiseResult}");
                if (initialiseResult == 0)
                {
                    throw new InvalidOperationException("SYSTEM6Initialise returned failure.");
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"System6 native backend failed to initialise core '{_libraryPath}'.", ex);
            }
            if (startupStage is System6StartupStage.InitialiseOnly)
            {
                return CompleteStagedStartup();
            }

            var nativeRoms = request.System6NativeRoms
                ?? throw new InvalidOperationException("System6 native backend requires native DLL ROM settings.");
            var programRomPaths = ValidateNativeRomPaths(nativeRoms.ProgramRomPaths, true, "program");
            var soundRomPaths = ValidateNativeRomPaths(nativeRoms.SoundRomPaths, false, "sound");
            var reelOptos = ValidateReelOptos(nativeRoms.ReelOptos);

            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var loadRomResult = _library.LoadRom(programRomPaths, nativeRoms.FlashSwitch);
                LogStartupStage($"LoadROM returned {loadRomResult}");
                if (loadRomResult == 0)
                {
                    throw new InvalidOperationException("SYSTEM6LoadROM returned failure.");
                }

                if (soundRomPaths.Any(path => !string.IsNullOrWhiteSpace(path)))
                {
                    var loadSoundRomResult = _library.LoadSoundRom(soundRomPaths);
                    LogStartupStage($"LoadSoundROM returned {loadSoundRomResult}");
                    if (loadSoundRomResult == 0)
                    {
                        throw new InvalidOperationException("SYSTEM6LoadSoundROM returned failure.");
                    }
                }
                else
                {
                    LogStartupStage("LoadSoundROM skipped");
                }

            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"System6 native backend failed to load native ROMs with core '{_libraryPath}'.", ex);
            }
            if (startupStage is System6StartupStage.LoadRomOnly)
            {
                return CompleteStagedStartup();
            }

            try
            {
                LogStartupStage("Reset starting");
                _library.Reset();
                LogStartupStage("Reset completed");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"System6 native backend failed to reset core '{_libraryPath}' during startup.", ex);
            }
            ResetCachedOutputState();

            try
            {
                LogStartupStage("Apply reel optos starting after Reset");
                ApplyReelOptos(_library, reelOptos);
                LogStartupStage("Apply reel optos completed before first Run");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"System6 native backend failed to apply reel optos with core '{_libraryPath}'.", ex);
            }
            if (startupStage is System6StartupStage.ResetOnly)
            {
                LogStartupStage("first Run skipped / pending");
                return CompleteStagedStartup();
            }

            if (startupStage is System6StartupStage.OneRunOnly)
            {
                var runResult = _library.Run(_cyclesPerFrame);
                LogStartupStage($"first Run({_cyclesPerFrame}) returned {runResult}");
                return CompleteStagedStartup();
            }

            LogStartupStage("first Run skipped / pending");
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

    private Task CompleteStagedStartup()
    {
        LogStartupStage("staged startup complete; run loop not started");
        SetState(EmulationBackendState.Running);
        return Task.CompletedTask;
    }

    private static System6StartupStage ResolveStartupStage()
    {
        var raw = Environment.GetEnvironmentVariable(DiagnosticStageEnvironmentVariable);
        return Enum.TryParse<System6StartupStage>(raw, ignoreCase: true, out var stage)
            ? stage
            : System6StartupStage.FullRunLoop;
    }

    private static int ToLampBrightnessValue(float brightness)
    {
        if (float.IsNaN(brightness) || brightness <= 0f)
        {
            return 0;
        }

        if (brightness <= 1f)
        {
            return (int)MathF.Round(brightness * 255f);
        }

        return (int)MathF.Round(Math.Min(brightness, 255f));
    }

    internal static string FormatAlphaDebugString(IReadOnlyList<byte> rawChars)
    {
        ArgumentNullException.ThrowIfNull(rawChars);

        var chars = new char[rawChars.Count];
        for (var i = 0; i < rawChars.Count; i++)
        {
            var value = rawChars[i];
            chars[i] = value switch
            {
                0 => ' ',
                >= 32 and <= 126 => (char)value,
                _ => '?'
            };
        }

        return new string(chars);
    }

    internal static string FormatAlphaRawBytes(IReadOnlyList<byte> rawChars)
    {
        ArgumentNullException.ThrowIfNull(rawChars);
        return string.Join(" ", rawChars.Select(value => value.ToString("X2", CultureInfo.InvariantCulture)));
    }

    private static void LogStartupStage(string message)
    {
        Debug.WriteLine($"System6 native startup: {message}");
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
                        var stopwatch = Stopwatch.StartNew();
                        var runResult = library.Run(_cyclesPerFrame);
                        stopwatch.Stop();
                        if (runResult == 0)
                        {
                            Debug.WriteLine($"System6 native backend Run({_cyclesPerFrame}) returned 0.");
                        }
                        WarnIfRunCallIsSlow(stopwatch.Elapsed);
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

    private static IReadOnlyList<System6ReelOptoSettings> ValidateReelOptos(IReadOnlyList<System6ReelOptoSettings>? reelOptos)
    {
        var settings = reelOptos is { Count: > 0 } ? reelOptos : System6NativeRomSettings.CreateDefaultReelOptos();
        foreach (var reel in settings)
        {
            if (reel.ReelIndex is < 0 or >= SupportedReelOptoCount)
            {
                throw new InvalidOperationException($"System6 reel opto setting has invalid reel index {reel.ReelIndex}; supported range is 0-{SupportedReelOptoCount - 1}.");
            }
            if (!reel.Enabled)
            {
                continue;
            }
            if (reel.Steps is < 1 or > byte.MaxValue)
            {
                throw new InvalidOperationException($"System6 reel {reel.ReelIndex + 1} opto steps must be between 1 and 255.");
            }
            if (reel.OptoStart is < 0 or > byte.MaxValue)
            {
                throw new InvalidOperationException($"System6 reel {reel.ReelIndex + 1} opto start must be between 0 and 255.");
            }
            if (reel.OptoEnd is < 0 or > byte.MaxValue)
            {
                throw new InvalidOperationException($"System6 reel {reel.ReelIndex + 1} opto end must be between 0 and 255.");
            }
            if (reel.OptoStart >= reel.OptoEnd)
            {
                throw new InvalidOperationException($"System6 reel {reel.ReelIndex + 1} opto start must be less than opto end.");
            }
        }

        return settings;
    }

    private static void ApplyReelOptos(ISystem6NativeLibrary library, IReadOnlyList<System6ReelOptoSettings> reelOptos)
    {
        foreach (var reel in reelOptos.Where(reel => reel.Enabled))
        {
            var nativeReelIndex = checked((byte)reel.ReelIndex);
            var displayReelNumber = reel.ReelIndex + 1;
            library.SetSteps(nativeReelIndex, checked((byte)reel.Steps));
            library.SetOptoStart(nativeReelIndex, checked((byte)reel.OptoStart));
            library.SetOptoEnd(nativeReelIndex, checked((byte)reel.OptoEnd));
            library.SetOptoInvert(nativeReelIndex, reel.OptoInvert ? (byte)1 : (byte)0);
            LogStartupStage($"System6 reel {displayReelNumber} -> native index {nativeReelIndex}: steps={reel.Steps} start={reel.OptoStart} end={reel.OptoEnd} inverted={reel.OptoInvert.ToString(CultureInfo.InvariantCulture).ToLowerInvariant()}");
        }
    }

    private static IReadOnlyList<string> ValidateNativeRomPaths(IReadOnlyList<string> romPaths, bool requireFirst, string romKind)
    {
        if (requireFirst && (romPaths.Count < 2 || string.IsNullOrWhiteSpace(romPaths[0]) || string.IsNullOrWhiteSpace(romPaths[1])))
        {
            throw new InvalidOperationException("System6 native backend requires Program ROM 1 and Program ROM 2 before starting.");
        }

        var validated = new[] { string.Empty, string.Empty, string.Empty, string.Empty };
        for (var i = 0; i < Math.Min(validated.Length, romPaths.Count); i++)
        {
            var path = romPaths[i];
            if (string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"System6 native {romKind} ROM {i + 1} was not found.", path);
            }

            validated[i] = path;
        }

        return validated;
    }

    private void WarnIfRunCallIsSlow(TimeSpan elapsed)
    {
        if (elapsed <= SlowRunWarningThreshold || DateTime.UtcNow - _lastRunWarningUtc < RunWarningThrottleInterval)
        {
            return;
        }

        _lastRunWarningUtc = DateTime.UtcNow;
        Debug.WriteLine($"System6 native backend Run({_cyclesPerFrame}) took {elapsed.TotalMilliseconds:0.0} ms; native core may be pegging a CPU core.");
    }

    private void PollOutputs(ISystem6NativeLibrary library)
    {
        for (var lampIndex = 0; lampIndex < LampCount; lampIndex++)
        {
            var nativeLampIndex = checked((ushort)lampIndex);
            var isOn = library.GetLampsOn(nativeLampIndex);
            var value = isOn ? ToLampBrightnessValue(library.GetLampBrightness(nativeLampIndex)) : 0;
            if (_lastLampValues[lampIndex] != value)
            {
                _lastLampValues[lampIndex] = value;
                LampChanged?.Invoke(this, new MachineLampChangedEventArgs(lampIndex, value));
            }
        }

        for (var reelIndex = 0; reelIndex < ReelCount; reelIndex++)
        {
            var position = library.GetPosOut(checked((sbyte)reelIndex));
            if (_lastReelPositions[reelIndex] != position)
            {
                _lastReelPositions[reelIndex] = position;
                ReelChanged?.Invoke(this, new MachineReelChangedEventArgs(reelIndex, position));
            }
        }

        PollAlphaDebugOutput(library);
    }

    private void PollAlphaDebugOutput(ISystem6NativeLibrary library)
    {
        if (!library.IsAlphaCharPollingAvailable)
        {
            if (!_hasLoggedAlphaUnavailable)
            {
                _hasLoggedAlphaUnavailable = true;
                Debug.WriteLine("[System6 Alpha] GetAlphaChar unavailable; alpha debug polling disabled.");
            }

            return;
        }

        var rawChars = new byte[AlphaCharacterCount];
        for (var index = 0; index < rawChars.Length; index++)
        {
            rawChars[index] = library.GetAlphaChar(checked((byte)index));
        }

        // The native TechsClass.cpp reference exposes GetAlphaChar(UINT8 num) as a raw byte.
        // First-pass diagnostics treat printable ASCII values as characters while also logging
        // raw byte values so we can verify whether the native core returns ASCII or display codes.
        var alphaString = FormatAlphaDebugString(rawChars);
        if (string.Equals(_lastAlphaString, alphaString, StringComparison.Ordinal))
        {
            return;
        }

        _lastAlphaString = alphaString;
        Debug.WriteLine($"[System6 Alpha] chars=\"{alphaString}\" raw={FormatAlphaRawBytes(rawChars)}");
    }

    private void ResetCachedOutputState()
    {
        Array.Fill(_lastLampValues, -1);
        Array.Fill(_lastReelPositions, int.MinValue);
        _lastAlphaString = null;
        _hasLoggedAlphaUnavailable = false;
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
            var shutdownResult = library.Shutdown();
            LogStartupStage($"Shutdown returned {shutdownResult}");
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


internal enum System6StartupStage
{
    LoadBindOnly,
    InitialiseOnly,
    LoadRomOnly,
    ResetOnly,
    OneRunOnly,
    FullRunLoop
}
