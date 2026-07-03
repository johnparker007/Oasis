using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace OasisEditor;

public sealed class System6NativeBackend : IEmulationBackend
{
    private const int DefaultOutputPollsPerSecond = 60;
    private const int DefaultEmulationPumpHz = 1000;
    private const int System6ClockHz = 8000000;
    private const int FallbackLampCount = 32;
    private const int MaximumLampId = byte.MaxValue;
    private const int ReelCount = 16;
    private const int DiagnosticChangedLampSampleCount = 8;
    private const int DiagnosticMappingSampleCount = 8;
    private const int AlphaCellCount = 16;
    private const int SevenSegmentMaskBits = 8;
    private const int NativeSevenSegmentCellStride = 16;
    private const int SupportedReelOptoCount = 8;
    private const string DiagnosticStageEnvironmentVariable = "OASIS_SYSTEM6_STARTUP_STAGE";
    private const string TimingDiagnosticsEnvironmentVariable = "OASIS_SYSTEM6_TIMING_DIAGNOSTICS";
    private const string OutputPollingEnabledEnvironmentVariable = "OASIS_SYSTEM6_OUTPUT_POLLING";
    private const string OutputPollingRateEnvironmentVariable = "OASIS_SYSTEM6_OUTPUT_POLL_HZ";
    private const string EmulationPumpRateEnvironmentVariable = "OASIS_SYSTEM6_EMULATION_PUMP_HZ";
    private const int MaxCatchUpSlicesPerLoop = 4;
    private const double SlowRunWarningMilliseconds = 8.0d;
    private const double SlowPollOutputsWarningMilliseconds = 8.0d;
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
    private readonly TimeSpan _emulationInterval;
    private readonly TimeSpan _pollInterval;
    private readonly int _emulationPumpHz;
    private readonly int _outputPollsPerSecond;
    private readonly bool _outputPollingEnabled;
    private readonly long _emulationIntervalTimestampTicks;
    private readonly long _pollIntervalTimestampTicks;
    private readonly bool _timingDiagnosticsEnabled;
    private readonly object _stateGate = new();
    private readonly int[] _lastLampValues = Enumerable.Repeat(-1, MaximumLampId + 1).ToArray();
    private readonly int[] _lastLampRawValues = Enumerable.Repeat(-1, MaximumLampId + 1).ToArray();
    private readonly int[] _lastReelPositions = Enumerable.Repeat(int.MinValue, ReelCount).ToArray();
    private readonly int[] _reelStepCounts = new int[ReelCount];
    private int[]? _lastAlphaSegments;
    private readonly Dictionary<int, int> _lastSevenSegmentMasks = new();
    private bool _hasLoggedSevenSegmentUnavailable;
    private IReadOnlyList<int>? _configuredSevenSegmentDisplayIds;
    private double? _lastAlphaBrightness;
    private bool _hasLoggedAlphaUnavailable;
    private bool _hasLoggedLampPollingConfiguration;
    private bool _hasLoggedReelPollingMapping;
    private int[]? _configuredLampPollingIds;
    private int[] _enabledReelPollingIndices = [];

    private ISystem6NativeLibrary? _library;
    private CancellationTokenSource? _runLoopCancellation;
    private Task? _runLoopTask;
    private Task? _pollLoopTask;
    private EmulationBackendState _state = EmulationBackendState.Stopped;
    private DateTime _lastRunWarningUtc = DateTime.MinValue;

    public System6NativeBackend(string libraryPath)
        : this(libraryPath, static path => new System6NativeLibrary(path))
    {
    }

    public System6NativeBackend(string libraryPath, Func<string, ISystem6NativeLibrary> libraryFactory)
        : this(libraryPath, libraryFactory, DefaultOutputPollsPerSecond)
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
        _timingDiagnosticsEnabled = IsTimingDiagnosticsEnabled();
        _outputPollingEnabled = ResolveOutputPollingEnabled();
        _outputPollsPerSecond = ResolveOutputPollingRate(framesPerSecond);
        _emulationPumpHz = ResolveEmulationPumpRate();
        _emulationInterval = TimeSpan.FromMilliseconds(1000.0 / _emulationPumpHz);
        _pollInterval = TimeSpan.FromMilliseconds(1000.0 / _outputPollsPerSecond);
        _emulationIntervalTimestampTicks = Math.Max(1L, (long)Math.Round((double)Stopwatch.Frequency / _emulationPumpHz));
        _pollIntervalTimestampTicks = Math.Max(1L, (long)Math.Round((double)Stopwatch.Frequency / _outputPollsPerSecond));
    }

    public EmulationBackendKind BackendKind => EmulationBackendKind.NativeSystem6;

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
            LogStartupStage($"GetOutputSnapshot size={_library.GetOutputSnapshotSize()} bytes");
            LogStartupStage($"SetPercent export {(_library.IsSetPercentAvailable ? "available" : "missing")}");
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
                    throw new InvalidOperationException("Initialise returned failure.");
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
            var coins = ValidateCoins(nativeRoms.Coins);
            var percentSwitchValue = ValidatePercentSwitchValue(nativeRoms.PercentSwitchValue);
            ConfigureLampPolling(request.ConfiguredLampIds);
            ConfigureSevenSegmentPolling(request.ConfiguredSevenSegmentDisplayIds);

            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var loadRomResult = _library.LoadRom(programRomPaths, nativeRoms.FlashSwitch);
                LogStartupStage($"LoadROM returned {loadRomResult}");
                if (loadRomResult == 0)
                {
                    throw new InvalidOperationException("LoadROM returned failure.");
                }

                if (soundRomPaths.Any(path => !string.IsNullOrWhiteSpace(path)))
                {
                    var loadSoundRomResult = _library.LoadSoundRom(soundRomPaths);
                    LogStartupStage($"LoadSoundROM returned {loadSoundRomResult}");
                    if (loadSoundRomResult == 0)
                    {
                        throw new InvalidOperationException("LoadSoundROM returned failure.");
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
            ResetConfiguredReelStepCounts();
            ResetEnabledReelPollingIndices();

            try
            {
                LogStartupStage("Apply reel optos starting after Reset");
                ApplyReelOptos(_library, reelOptos);
                LogStartupStage("Apply reel optos completed before first Run");
                LogStartupStage("Apply coins starting before first Run");
                ApplyCoins(_library, coins);
                LogStartupStage("Apply coins completed before first Run");
                ApplyPercent(_library, percentSwitchValue);
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
                var cycles = CalculateCyclesForSlice(0);
                var runResult = _library.Run(cycles);
                LogStartupStage($"first Run({cycles}) returned {runResult}");
                PollOutputs(_library);
                return CompleteStagedStartup();
            }

            LogStartupStage("first Run skipped / pending");
            Debug.WriteLine($"System6 native backend timing config: emulationPumpHz={_emulationPumpHz}; slice={_emulationInterval.TotalMilliseconds:0.###} ms; outputPollingEnabled={_outputPollingEnabled}; outputPollHz={_outputPollsPerSecond}.");
            _runLoopCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _runLoopTask = Task.Run(() => RunLoopAsync(_runLoopCancellation.Token), CancellationToken.None);
            if (_outputPollingEnabled)
            {
                _pollLoopTask = Task.Run(() => PollLoopAsync(_runLoopCancellation.Token), CancellationToken.None);
            }
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

        if (_pollLoopTask is not null)
        {
            try
            {
                await _pollLoopTask.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (runLoopCancellation?.IsCancellationRequested == true)
            {
            }
            catch
            {
            }
        }

        ShutdownAndDisposeLibrary();
        _runLoopCancellation?.Dispose();
        _runLoopCancellation = null;
        _runLoopTask = null;
        _pollLoopTask = null;
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

    public Task SetInputStateAsync(InputDefinitionModel inputDefinition, bool isPressed, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(inputDefinition);
        cancellationToken.ThrowIfCancellationRequested();

        if (inputDefinition.Kind is not InputDefinitionKind.Button || inputDefinition.CoinInput)
        {
            return Task.CompletedTask;
        }

        var library = _library ?? throw new InvalidOperationException("System6 native backend cannot set input state before it has started.");
        if (!int.TryParse(inputDefinition.ButtonNumber, NumberStyles.Integer, CultureInfo.InvariantCulture, out var switchIndex))
        {
            throw new InvalidOperationException($"System6 native backend input '{inputDefinition.Id}' button number '{inputDefinition.ButtonNumber}' is not a numeric switch index.");
        }

        if (switchIndex is < byte.MinValue or > byte.MaxValue)
        {
            throw new InvalidOperationException($"System6 native backend input '{inputDefinition.Id}' switch index {switchIndex} is outside the supported range 0-255.");
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

    private static bool IsTimingDiagnosticsEnabled()
    {
        var raw = Environment.GetEnvironmentVariable(TimingDiagnosticsEnvironmentVariable);
        return bool.TryParse(raw, out var enabled)
            ? enabled
            : string.Equals(raw, "1", StringComparison.Ordinal);
    }


    private static bool ResolveOutputPollingEnabled()
    {
        var raw = Environment.GetEnvironmentVariable(OutputPollingEnabledEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return true;
        }

        return bool.TryParse(raw, out var enabled)
            ? enabled
            : !string.Equals(raw, "0", StringComparison.Ordinal);
    }

    private static int ResolveOutputPollingRate(int constructorDefault)
    {
        var raw = Environment.GetEnvironmentVariable(OutputPollingRateEnvironmentVariable);
        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var rate))
        {
            rate = constructorDefault;
        }

        return rate switch
        {
            10 or 30 or 60 => rate,
            _ => DefaultOutputPollsPerSecond
        };
    }

    private static int ResolveEmulationPumpRate()
    {
        var raw = Environment.GetEnvironmentVariable(EmulationPumpRateEnvironmentVariable);
        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var rate))
        {
            return DefaultEmulationPumpHz;
        }

        return Math.Clamp(rate, 500, 2000);
    }

    private static System6StartupStage ResolveStartupStage()
    {
        var raw = Environment.GetEnvironmentVariable(DiagnosticStageEnvironmentVariable);
        return Enum.TryParse<System6StartupStage>(raw, ignoreCase: true, out var stage)
            ? stage
            : System6StartupStage.FullRunLoop;
    }


    internal static string FormatAlphaSegments(IReadOnlyList<int> segments)
    {
        ArgumentNullException.ThrowIfNull(segments);
        return string.Join(" ", segments.Select((value, index) => $"[{index}]=0x{(value & 0xFFFF):X4}/{value.ToString(CultureInfo.InvariantCulture)}"));
    }

    private static string FormatOptionalExportStatus(bool isAvailable, string? exportName)
    {
        return isAvailable && !string.IsNullOrWhiteSpace(exportName)
            ? $"available ({exportName})"
            : "missing";
    }

    private static void LogStartupStage(string message)
    {
        Debug.WriteLine($"System6 native startup: {message}");
    }

    private async Task RunLoopAsync(CancellationToken cancellationToken)
    {
        var nextSliceTimestamp = Stopwatch.GetTimestamp();
        var sliceIndex = 0L;
        var diagnosticsStartedAt = Stopwatch.GetTimestamp();
        var diagnosticsCycles = 0L;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (State is not EmulationBackendState.Running || _library is not { } library)
                {
                    nextSliceTimestamp = Stopwatch.GetTimestamp() + _emulationIntervalTimestampTicks;
                    await Task.Delay(_emulationInterval, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                var slicesRunThisLoop = 0;
                do
                {
                    var cycles = CalculateCyclesForSlice(sliceIndex++);
                    var runStartTimestamp = Stopwatch.GetTimestamp();
                    var runResult = library.Run(cycles);
                    var runElapsed = TimestampTicksToTimeSpan(Stopwatch.GetTimestamp() - runStartTimestamp);
                    diagnosticsCycles += cycles;
                    if (runResult == 0)
                    {
                        Debug.WriteLine($"System6 native backend Run({cycles}) returned 0.");
                    }

                    WarnIfEmulationTimingIsSlow(cycles, runElapsed, Stopwatch.GetTimestamp() - diagnosticsStartedAt, diagnosticsCycles, 0);
                    nextSliceTimestamp += _emulationIntervalTimestampTicks;
                    slicesRunThisLoop++;
                }
                while (!cancellationToken.IsCancellationRequested
                    && State is EmulationBackendState.Running
                    && slicesRunThisLoop < MaxCatchUpSlicesPerLoop
                    && Stopwatch.GetTimestamp() >= nextSliceTimestamp);

                var now = Stopwatch.GetTimestamp();
                if (now >= nextSliceTimestamp)
                {
                    WarnIfEmulationTimingIsSlow(0, TimeSpan.Zero, now - diagnosticsStartedAt, diagnosticsCycles, slicesRunThisLoop);
                    if (slicesRunThisLoop >= MaxCatchUpSlicesPerLoop)
                    {
                        nextSliceTimestamp = now + _emulationIntervalTimestampTicks;
                    }

                    continue;
                }

                await Task.Delay(TimestampTicksToTimeSpan(nextSliceTimestamp - now), cancellationToken).ConfigureAwait(false);
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

    private async Task PollLoopAsync(CancellationToken cancellationToken)
    {
        var nextPollTimestamp = Stopwatch.GetTimestamp() + _pollIntervalTimestampTicks;
        var skippedPolls = 0L;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (State is not EmulationBackendState.Running || _library is not { } library)
                {
                    nextPollTimestamp = Stopwatch.GetTimestamp() + _pollIntervalTimestampTicks;
                    await Task.Delay(_pollInterval, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                var now = Stopwatch.GetTimestamp();
                if (now < nextPollTimestamp)
                {
                    await Task.Delay(TimestampTicksToTimeSpan(nextPollTimestamp - now), cancellationToken).ConfigureAwait(false);
                    continue;
                }

                var pollStartTimestamp = Stopwatch.GetTimestamp();
                var pollInvokeCount = PollOutputs(library);
                var pollElapsed = TimestampTicksToTimeSpan(Stopwatch.GetTimestamp() - pollStartTimestamp);
                nextPollTimestamp += _pollIntervalTimestampTicks;

                now = Stopwatch.GetTimestamp();
                while (now >= nextPollTimestamp)
                {
                    skippedPolls++;
                    nextPollTimestamp += _pollIntervalTimestampTicks;
                }

                WarnIfPollTimingIsSlow(pollElapsed, skippedPolls, pollInvokeCount);
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

    private int CalculateCyclesForSlice(long sliceIndex)
    {
        var completedCycles = Math.DivRem((sliceIndex + 1) * (long)System6ClockHz, _emulationPumpHz, out _);
        var previousCycles = Math.DivRem(sliceIndex * (long)System6ClockHz, _emulationPumpHz, out _);
        return checked((int)(completedCycles - previousCycles));
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

    private static IReadOnlyList<System6CoinSettings> ValidateCoins(IReadOnlyList<System6CoinSettings>? coins)
    {
        var defaults = System6NativeRomSettings.CreateDefaultCoins();
        if (coins is null || coins.Count == 0)
        {
            return defaults;
        }

        var settings = new List<System6CoinSettings>(System6NativeRomSettings.DefaultCoinSlotCount);
        for (var index = 0; index < System6NativeRomSettings.DefaultCoinSlotCount; index++)
        {
            settings.Add(index < coins.Count ? coins[index] : defaults[index]);
        }

        foreach (var coin in settings.Where(coin => coin.Enabled))
        {
            ValidateCoinByte(coin.Num, nameof(coin.Num));
            ValidateCoinByte(coin.Coin, nameof(coin.Coin));
            ValidateCoinByte(coin.CoinValue, nameof(coin.CoinValue));
            ValidateCoinByte(coin.CoinEnable, nameof(coin.CoinEnable));
            ValidateCoinByte(coin.LockoutValue, nameof(coin.LockoutValue));
            ValidateCoinByte(coin.LockoutInvert, nameof(coin.LockoutInvert));
            ValidateCoinByte(coin.CounterIn, nameof(coin.CounterIn));
            ValidateCoinByte(coin.CounterOut, nameof(coin.CounterOut));
            ValidateCoinByte(coin.PortIndex, nameof(coin.PortIndex));
            ValidateCoinByte(coin.Level, nameof(coin.Level));
            ValidateCoinByte(coin.FullLevel, nameof(coin.FullLevel));
        }

        return settings;
    }

    private static void ValidateCoinByte(int value, string propertyName)
    {
        if (value is < 0 or > byte.MaxValue)
        {
            throw new InvalidOperationException($"System6 coin {propertyName} must be between 0 and 255.");
        }
    }

    private static int ValidatePercentSwitchValue(int percentSwitchValue)
    {
        if (percentSwitchValue is < 0 or > 15)
        {
            throw new InvalidOperationException("System6 percent switch value must be between 0 and 15.");
        }

        return percentSwitchValue;
    }

    private void ApplyReelOptos(ISystem6NativeLibrary library, IReadOnlyList<System6ReelOptoSettings> reelOptos)
    {
        var enabledReelIndices = new List<int>();
        foreach (var reel in reelOptos.Where(reel => reel.Enabled))
        {
            var nativeReelIndex = checked((byte)reel.ReelIndex);
            var displayReelNumber = reel.ReelIndex + 1;
            library.SetSteps(nativeReelIndex, checked((byte)reel.Steps));
            enabledReelIndices.Add(reel.ReelIndex);
            _reelStepCounts[reel.ReelIndex] = reel.Steps;
            library.SetOptoStart(nativeReelIndex, checked((byte)reel.OptoStart));
            library.SetOptoEnd(nativeReelIndex, checked((byte)reel.OptoEnd));
            library.SetOptoInvert(nativeReelIndex, reel.OptoInvert ? (byte)1 : (byte)0);
            LogStartupStage($"System6 reel {displayReelNumber} -> native index {nativeReelIndex}: steps={reel.Steps} start={reel.OptoStart} end={reel.OptoEnd} inverted={reel.OptoInvert.ToString(CultureInfo.InvariantCulture).ToLowerInvariant()}");
        }

        _enabledReelPollingIndices = enabledReelIndices.Distinct().OrderBy(index => index).ToArray();
    }

    private static void ApplyCoins(ISystem6NativeLibrary library, IReadOnlyList<System6CoinSettings> coins)
    {
        foreach (var coin in coins.Where(coin => coin.Enabled))
        {
            var num = checked((byte)coin.Num);
            var channel = checked((byte)coin.Coin);
            library.SetCoinEnable(channel, checked((byte)coin.CoinEnable));
            library.SetCoinValue(channel, checked((byte)coin.CoinValue));
            library.SetLockoutVal(channel, checked((byte)coin.LockoutValue));
            library.SetLockoutInvert(channel, checked((byte)coin.LockoutInvert));
            library.SetEnable(num, 1);
            library.SetCounterIn(num, checked((byte)coin.CounterIn));
            library.SetCounterOut(num, checked((byte)coin.CounterOut));
            library.SetPortIndex(num, checked((byte)coin.PortIndex));
            library.SetCoin(num, channel);
            library.SetLevel(num, checked((byte)coin.Level));
            library.SetFullLevel(num, checked((byte)coin.FullLevel));
            LogStartupStage($"System6 coin '{coin.Name}' -> num={coin.Num} coin={coin.Coin} value={coin.CoinValue} enabled={coin.CoinEnable}");
        }
    }

    private static void ApplyPercent(ISystem6NativeLibrary library, int percentSwitchValue)
    {
        LogStartupStage($"SetPercent export {(library.IsSetPercentAvailable ? "available" : "missing")}");
        LogStartupStage($"configured percent switch value = {percentSwitchValue}");
        if (!library.IsSetPercentAvailable)
        {
            return;
        }

        var nativeValue = checked((byte)percentSwitchValue);
        library.SetPercent(nativeValue);
        LogStartupStage($"SetPercent passed value {nativeValue} (0x{nativeValue:X2}) to DLL");
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

    private static TimeSpan TimestampTicksToTimeSpan(long timestampTicks)
    {
        if (timestampTicks <= 0)
        {
            return TimeSpan.Zero;
        }

        return TimeSpan.FromSeconds((double)timestampTicks / Stopwatch.Frequency);
    }

    private void WarnIfEmulationTimingIsSlow(int cycles, TimeSpan runElapsed, long diagnosticsElapsedTicks, long diagnosticsCycles, int skippedSlices)
    {
        var elapsed = TimestampTicksToTimeSpan(diagnosticsElapsedTicks);
        var achievedCyclesPerSecond = elapsed.TotalSeconds > 0 ? diagnosticsCycles / elapsed.TotalSeconds : 0d;
        var shouldLog = _timingDiagnosticsEnabled
            || runElapsed.TotalMilliseconds > SlowRunWarningMilliseconds
            || skippedSlices > 0;
        if (!shouldLog || DateTime.UtcNow - _lastRunWarningUtc < RunWarningThrottleInterval)
        {
            return;
        }

        _lastRunWarningUtc = DateTime.UtcNow;
        Debug.WriteLine($"System6 native backend emulation timing: slice={_emulationInterval.TotalMilliseconds:0.###} ms; Run({cycles})={runElapsed.TotalMilliseconds:0.00} ms; achievedHz={achievedCyclesPerSecond:0}; targetHz={System6ClockHz}; skippedEmulationSlices={skippedSlices}.");
    }

    private void WarnIfPollTimingIsSlow(TimeSpan pollElapsed, long skippedPolls, int nativeInvokeCount)
    {
        var shouldLog = _timingDiagnosticsEnabled
            || pollElapsed.TotalMilliseconds > SlowPollOutputsWarningMilliseconds
            || skippedPolls > 0;
        if (!shouldLog || DateTime.UtcNow - _lastRunWarningUtc < RunWarningThrottleInterval)
        {
            return;
        }

        _lastRunWarningUtc = DateTime.UtcNow;
        Debug.WriteLine($"System6 native backend output timing: pollHz={_outputPollsPerSecond}; PollOutputs={pollElapsed.TotalMilliseconds:0.00} ms; skippedVisualPolls={skippedPolls}; nativeCalls={nativeInvokeCount}.");
    }

    private unsafe int PollOutputs(ISystem6NativeLibrary library)
    {
        var snapshot = library.GetOutputSnapshot();
        var nativeInvokeCount = 1;
        nativeInvokeCount += PollLampOutputs(snapshot);
        nativeInvokeCount += PollReelOutputs(snapshot);
        nativeInvokeCount += PollAlphaOutputs(snapshot);
        nativeInvokeCount += PollSevenSegmentOutputs(snapshot);
        return nativeInvokeCount;
    }

    private unsafe int PollSevenSegmentOutputs(System6NativeOutputSnapshot snapshot)
    {
        var displayIds = _configuredSevenSegmentDisplayIds;
        if (displayIds is null || displayIds.Count == 0)
        {
            return 0;
        }

        foreach (var displayId in displayIds)
        {
            if (displayId >= snapshot.LedDisplayCount || displayId >= System6NativeOutputSnapshot.LedDisplayCapacity)
            {
                continue;
            }

            var ledDisplay = snapshot.GetLedDisplay(displayId);
            var mask = unchecked((int)ledDisplay.OnOff);
            if (!_lastSevenSegmentMasks.TryGetValue(displayId, out var previous) || previous != mask)
            {
                _lastSevenSegmentMasks[displayId] = mask;
                SegmentChanged?.Invoke(this, new MachineSegmentChangedEventArgs(displayId, mask, MameSegmentOutputType.Digit));
            }
        }

        return 0;
    }

    internal static int GetNativeSevenSegmentBaseIndex(int displayId) => displayId;

    private int PollLampOutputs(System6NativeOutputSnapshot snapshot)
    {
        var changedDiagnostics = new List<string>();
        var lampCount = (int)Math.Min(snapshot.MatrixLampCount, (uint)System6NativeOutputSnapshot.MatrixLampCapacity);
        foreach (var lampIndex in GetLampPollingIds().Where(id => id < lampCount))
        {
            var lamp = snapshot.GetMatrixLamp(lampIndex);
            var rawValue = lamp.OnOff != 0 ? 1 : 0;
            var value = NormalizeLampBrightness(lamp);
            var diagnosticChanged = _lastLampRawValues[lampIndex] != rawValue;
            if (diagnosticChanged)
            {
                _lastLampRawValues[lampIndex] = rawValue;
            }
            if (_lastLampValues[lampIndex] != value)
            {
                _lastLampValues[lampIndex] = value;
                LampChanged?.Invoke(this, new MachineLampChangedEventArgs(lampIndex, value));
            }

            if (diagnosticChanged && changedDiagnostics.Count < DiagnosticChangedLampSampleCount)
            {
                changedDiagnostics.Add(FormatLampChangeDiagnostic((ushort)lampIndex, lampIndex, rawValue, value > 0, value));
            }
        }

        if (_timingDiagnosticsEnabled && changedDiagnostics.Count > 0)
        {
            Debug.WriteLine($"[System6 Lamps] changed: {string.Join("; ", changedDiagnostics)}");
        }

        return 0;
    }

    private static int NormalizeLampBrightness(System6NativeLampState lamp)
    {
        if (lamp.Brightness > 0f)
        {
            return Math.Clamp((int)Math.Round(lamp.Brightness * 255f), 0, 255);
        }

        return lamp.OnOff != 0 ? 255 : 0;
    }

    private static string FormatLampChangeDiagnostic(ushort nativeLampIndex, int lampIndex, int rawValue, bool isOn, int value)
    {
        return $"native {nativeLampIndex} -> lamp:{lampIndex} raw={rawValue.ToString(CultureInfo.InvariantCulture)} on={isOn.ToString(CultureInfo.InvariantCulture).ToLowerInvariant()} value={value}";
    }

    private int PollReelOutputs(System6NativeOutputSnapshot snapshot)
    {
        LogReelPollingMapping();
        var reelCount = (int)Math.Min(snapshot.ReelCount, (uint)System6NativeOutputSnapshot.ReelCapacity);
        foreach (var reelIndex in _enabledReelPollingIndices.Where(index => index < reelCount))
        {
            var reelId = reelIndex + 1;
            var rawPosition = snapshot.GetReelPosition(reelIndex);
            var position = NormalizeConfiguredNativeSystem6ReelPosition(reelIndex, rawPosition);
            if (_lastReelPositions[reelIndex] != position)
            {
                _lastReelPositions[reelIndex] = position;
                ReelChanged?.Invoke(this, new MachineReelChangedEventArgs(reelId, position));
            }
        }

        return 0;
    }

    private unsafe int PollAlphaOutputs(System6NativeOutputSnapshot snapshot)
    {
        if (snapshot.AlphaSegmentedDisplayCount == 0)
        {
            return 0;
        }

        var alpha = snapshot.GetAlphaSegmented(0);
        var normalizedBrightness = Math.Clamp(alpha.Brightness, 0d, 1d);
        if (!_lastAlphaBrightness.HasValue || Math.Abs(_lastAlphaBrightness.Value - normalizedBrightness) >= 0.000001d)
        {
            _lastAlphaBrightness = normalizedBrightness;
            VfdBrightnessChanged?.Invoke(this, new MachineVfdBrightnessChangedEventArgs(0, normalizedBrightness));
        }

        var segments = new int[AlphaCellCount];
        var mappedSegments = new int[AlphaCellCount];
        for (var index = 0; index < segments.Length; index++)
        {
            segments[index] = alpha.Segments[index];
            mappedSegments[index] = System6AlphaSegmentMapper.MapNativeMaskToOasisMask(segments[index]);
        }

        if (_lastAlphaSegments is not null && segments.SequenceEqual(_lastAlphaSegments))
        {
            return 0;
        }

        _lastAlphaSegments = segments;
        for (var index = 0; index < segments.Length; index++)
        {
            SegmentChanged?.Invoke(this, new MachineSegmentChangedEventArgs(index, mappedSegments[index], MameSegmentOutputType.NativeAlpha));
        }

        return 0;
    }

    private void ResetConfiguredReelStepCounts()
    {
        Array.Clear(_reelStepCounts);
    }

    private void ResetEnabledReelPollingIndices()
    {
        _enabledReelPollingIndices = [];
    }

    private void ConfigureLampPolling(IReadOnlyList<int>? configuredLampIds)
    {
        _configuredLampPollingIds = configuredLampIds?
            .Where(lampId => lampId is >= 0 and <= MaximumLampId)
            .Distinct()
            .OrderBy(lampId => lampId)
            .ToArray();
        if (_configuredLampPollingIds is { Length: 0 })
        {
            _configuredLampPollingIds = null;
        }
    }

    private void ConfigureSevenSegmentPolling(IReadOnlyList<int>? configuredSevenSegmentDisplayIds)
    {
        _configuredSevenSegmentDisplayIds = configuredSevenSegmentDisplayIds?
            .Where(displayId => displayId is >= 0 and <= (ushort.MaxValue / NativeSevenSegmentCellStride))
            .Distinct()
            .OrderBy(displayId => displayId)
            .ToArray();
        if (_configuredSevenSegmentDisplayIds is { Count: 0 })
        {
            _configuredSevenSegmentDisplayIds = null;
        }
    }

    private IReadOnlyList<int> GetLampPollingIds()
    {
        return _configuredLampPollingIds ?? Enumerable.Range(0, FallbackLampCount).ToArray();
    }

    private int NormalizeConfiguredNativeSystem6ReelPosition(int reelIndex, int rawPosition)
    {
        var steps = reelIndex >= 0 && reelIndex < _reelStepCounts.Length
            ? _reelStepCounts[reelIndex]
            : 0;

        return NormalizeNativeSystem6ReelPosition(rawPosition, steps);
    }

    internal static int NormalizeNativeSystem6ReelPosition(int rawPosition, int steps)
    {
        if (steps <= 0)
        {
            return rawPosition;
        }

        // Native System 6 internals expose reel motion in the opposite direction
        // to the MAME/Oasis convention. Normalize at the backend boundary so
        // rendering can treat both emulation backends identically.
        var positionWithinReel = rawPosition % steps;
        if (positionWithinReel < 0)
        {
            positionWithinReel += steps;
        }

        return (steps - positionWithinReel) % steps;
    }

    private void LogReelPollingMapping()
    {
        if (_hasLoggedReelPollingMapping)
        {
            return;
        }

        _hasLoggedReelPollingMapping = true;
        var mappings = _enabledReelPollingIndices.Take(DiagnosticMappingSampleCount)
            .Select(index => $"native {index} -> reel:{index + 1} (GetPosOut selector {index})");
        Debug.WriteLine($"[System6 Reels] mapping sample: {string.Join("; ", mappings)}");
    }

    internal static double NormalizeSystem6AlphaBrightness(byte rawBrightness)
    {
        const double maxSystem6AlphaDuty = 31d;
        return Math.Clamp(rawBrightness / maxSystem6AlphaDuty, 0d, 1d);
    }

    private void ResetCachedOutputState()
    {
        Array.Fill(_lastLampValues, -1);
        Array.Fill(_lastLampRawValues, -1);
        Array.Fill(_lastReelPositions, int.MinValue);
        _lastAlphaSegments = null;
        _lastSevenSegmentMasks.Clear();
        _lastAlphaBrightness = null;
        _hasLoggedAlphaUnavailable = false;
        _hasLoggedLampPollingConfiguration = false;
        _hasLoggedReelPollingMapping = false;
        _hasLoggedSevenSegmentUnavailable = false;
    }

    private void CleanupLibraryAfterFailedStart()
    {
        ShutdownAndDisposeLibrary();
        _runLoopCancellation?.Dispose();
        _runLoopCancellation = null;
        _runLoopTask = null;
        _pollLoopTask = null;
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
