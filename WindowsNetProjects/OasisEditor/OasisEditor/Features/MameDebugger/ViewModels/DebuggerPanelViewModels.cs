using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using OasisEditor;

namespace OasisEditor.Features.MameDebugger.ViewModels;

public sealed class MameDebuggerShellViewModel : INotifyPropertyChanged
{
    private readonly IMameDebuggerService _debuggerService;
    private readonly Action<string, OutputLogStatus> _log;

    public MameDebuggerShellViewModel(IMameDebuggerService debuggerService, Action<string, OutputLogStatus> log)
    {
        _debuggerService = debuggerService ?? throw new ArgumentNullException(nameof(debuggerService));
        _log = log ?? throw new ArgumentNullException(nameof(log));
        Control = new DebuggerControlViewModel(debuggerService, Log);
        Disassembly = new DebuggerDisassemblyViewModel(debuggerService, Log);
        Registers = new DebuggerRegistersViewModel(debuggerService, Log);
        Memory = new DebuggerMemoryViewModel(debuggerService, Log);
        Breakpoints = new DebuggerBreakpointsViewModel(debuggerService, Log);
        Watchpoints = new DebuggerWatchpointsViewModel(debuggerService, Log);
        _debuggerService.DebuggerEventReceived += OnDebuggerEventReceived;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public DebuggerControlViewModel Control { get; }
    public DebuggerDisassemblyViewModel Disassembly { get; }
    public DebuggerRegistersViewModel Registers { get; }
    public DebuggerMemoryViewModel Memory { get; }
    public DebuggerBreakpointsViewModel Breakpoints { get; }
    public DebuggerWatchpointsViewModel Watchpoints { get; }

    public void NotifyCommandStateChanged()
    {
        Control.NotifyCommandStateChanged();
        Disassembly.NotifyCommandStateChanged();
        Registers.NotifyCommandStateChanged();
        Memory.NotifyCommandStateChanged();
        Breakpoints.NotifyCommandStateChanged();
        Watchpoints.NotifyCommandStateChanged();
    }

    private void OnDebuggerEventReceived(object? sender, MameDebuggerEvent debuggerEvent)
    {
        DispatchToUiThread(() =>
        {
            Control.ApplySnapshot(_debuggerService.State);
            NotifyCommandStateChanged();
            if (debuggerEvent.Event.Equals("stopped", StringComparison.OrdinalIgnoreCase)
                || debuggerEvent.Event.Equals("step", StringComparison.OrdinalIgnoreCase))
            {
                _ = Control.RefreshStatusAsync(logRequest: false);
                _ = Disassembly.RefreshAroundPcAsync(logRequest: false);
                _ = Registers.RefreshAsync(logRequest: false);
            }
        });
    }

    private static void DispatchToUiThread(Action work)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished || dispatcher.CheckAccess())
        {
            work();
            return;
        }

        _ = dispatcher.BeginInvoke(work);
    }

    private void Log(string message, OutputLogStatus status) => _log(message, status);

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public abstract class DebuggerPanelViewModelBase : INotifyPropertyChanged
{
    protected DebuggerPanelViewModelBase(IMameDebuggerService debuggerService, Action<string, OutputLogStatus> log)
    {
        DebuggerService = debuggerService ?? throw new ArgumentNullException(nameof(debuggerService));
        Log = log ?? throw new ArgumentNullException(nameof(log));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected IMameDebuggerService DebuggerService { get; }
    protected Action<string, OutputLogStatus> Log { get; }

    public virtual void NotifyCommandStateChanged()
    {
    }

    protected bool IsDebuggerUsable()
    {
        var state = DebuggerService.State;
        return state.IsDebuggerLaunchActive;
    }

    protected async Task RunDebuggerActionAsync(string requestedMessage, string failurePrefix, Func<CancellationToken, Task> action, bool logRequest = true)
    {
        if (!IsDebuggerUsable())
        {
            Log("MAME debugger is unavailable. Start emulation in debugger mode before using debugger panels.", OutputLogStatus.Warning);
            return;
        }

        if (logRequest)
        {
            Log(requestedMessage, OutputLogStatus.Info);
        }

        try
        {
            await action(CancellationToken.None).ConfigureAwait(true);
            NotifyCommandStateChanged();
        }
        catch (Exception ex)
        {
            Log($"{failurePrefix}: {ex.Message}", OutputLogStatus.Error);
            NotifyCommandStateChanged();
        }
    }

    protected void RaiseCommands(params ICommand[] commands)
    {
        foreach (var command in commands)
        {
            if (command is RelayCommand relayCommand)
            {
                relayCommand.RaiseCanExecuteChanged();
            }
        }
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    protected static bool TryParseInteger(string? text, out long value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var trimmed = text.Trim();
        if (trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return long.TryParse(trimmed[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
        }

        return long.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    protected static string ToHex(long value) => $"0x{value:X}";
}

public sealed class DebuggerControlViewModel : DebuggerPanelViewModelBase
{
    private string? _selectedCpu;
    private string _stateText = "Unknown";
    private string _currentCpuText = "unknown";
    private string _currentPcText = "unknown";
    private bool _isAvailable;

    public DebuggerControlViewModel(IMameDebuggerService debuggerService, Action<string, OutputLogStatus> log)
        : base(debuggerService, log)
    {
        RefreshStatusCommand = new RelayCommand(async () => await RefreshStatusAsync(), IsDebuggerUsable);
        RefreshCpusCommand = new RelayCommand(async () => await RefreshCpusAsync(), IsDebuggerUsable);
        RunCommand = new RelayCommand(async () => await RunAsync(), IsDebuggerUsable);
        BreakCommand = new RelayCommand(async () => await BreakAsync(), IsDebuggerUsable);
        StepCommand = new RelayCommand(async () => await StepAsync(), IsDebuggerUsable);
        ApplySnapshot(debuggerService.State);
    }

    public ObservableCollection<MameDebuggerCpu> Cpus { get; } = [];
    public ICommand RefreshStatusCommand { get; }
    public ICommand RefreshCpusCommand { get; }
    public ICommand RunCommand { get; }
    public ICommand BreakCommand { get; }
    public ICommand StepCommand { get; }

    public string? SelectedCpu
    {
        get => _selectedCpu;
        set { if (_selectedCpu != value) { _selectedCpu = value; OnPropertyChanged(); } }
    }

    public string StateText
    {
        get => _stateText;
        private set { if (_stateText != value) { _stateText = value; OnPropertyChanged(); } }
    }

    public string CurrentCpuText
    {
        get => _currentCpuText;
        private set { if (_currentCpuText != value) { _currentCpuText = value; OnPropertyChanged(); } }
    }

    public string CurrentPcText
    {
        get => _currentPcText;
        private set { if (_currentPcText != value) { _currentPcText = value; OnPropertyChanged(); } }
    }

    public bool IsAvailable
    {
        get => _isAvailable;
        private set { if (_isAvailable != value) { _isAvailable = value; OnPropertyChanged(); } }
    }

    public override void NotifyCommandStateChanged() => RaiseCommands(RefreshStatusCommand, RefreshCpusCommand, RunCommand, BreakCommand, StepCommand);

    public void ApplySnapshot(MameDebuggerStateSnapshot snapshot)
    {
        IsAvailable = snapshot.IsDebuggerAvailable;
        StateText = snapshot.ExecutionState.ToString();
        CurrentCpuText = snapshot.CurrentCpu ?? "unknown";
        CurrentPcText = snapshot.CurrentPc.HasValue ? ToHex(snapshot.CurrentPc.Value) : "unknown";
        if (string.IsNullOrWhiteSpace(SelectedCpu))
        {
            SelectedCpu = snapshot.CurrentCpu;
        }
    }

    public Task RefreshStatusAsync(bool logRequest = true)
        => RunDebuggerActionAsync("Debugger panel status refresh requested.", "Debugger panel status refresh failed", async cancellationToken =>
        {
            var status = await DebuggerService.GetStatusAsync(cancellationToken).ConfigureAwait(true);
            ApplySnapshot(DebuggerService.State);
            Log($"Debugger status: available={status.Available}, state={status.State}, cpu={status.Cpu ?? "unknown"}, pc={(status.Pc.HasValue ? ToHex(status.Pc.Value) : "unknown")}.", status.Available ? OutputLogStatus.Info : OutputLogStatus.Warning);
        }, logRequest);

    public Task RefreshCpusAsync(bool logRequest = true)
        => RunDebuggerActionAsync("Debugger panel CPU refresh requested.", "Debugger panel CPU refresh failed", async cancellationToken =>
        {
            var cpus = await DebuggerService.GetCpuListAsync(cancellationToken).ConfigureAwait(true);
            Cpus.Clear();
            foreach (var cpu in cpus)
            {
                Cpus.Add(cpu);
            }

            SelectedCpu = cpus.FirstOrDefault(cpu => cpu.IsCurrent)?.Tag ?? cpus.FirstOrDefault()?.Tag ?? SelectedCpu;
            if (cpus.Count == 0)
            {
                Log("Debugger CPU list returned no CPU devices.", OutputLogStatus.Warning);
            }
        }, logRequest);

    private Task RunAsync() => RunDebuggerActionAsync("Debugger run requested from Control panel.", "Debugger run failed", async cancellationToken =>
    {
        await DebuggerService.RunAsync(cancellationToken).ConfigureAwait(true);
        ApplySnapshot(DebuggerService.State);
    });

    private Task BreakAsync() => RunDebuggerActionAsync("Debugger break requested from Control panel.", "Debugger break failed", async cancellationToken =>
    {
        await DebuggerService.BreakAsync(cancellationToken).ConfigureAwait(true);
        ApplySnapshot(DebuggerService.State);
    });

    private Task StepAsync() => RunDebuggerActionAsync("Debugger step requested from Control panel.", "Debugger step failed", async cancellationToken =>
    {
        await DebuggerService.StepAsync(cancellationToken).ConfigureAwait(true);
        ApplySnapshot(DebuggerService.State);
    });
}

public sealed class DebuggerDisassemblyViewModel : DebuggerPanelViewModelBase
{
    private string _startAddressText = "0x0";
    private string _lineCountText = "32";
    private MameDebuggerDisassemblyLine? _selectedLine;

    public DebuggerDisassemblyViewModel(IMameDebuggerService debuggerService, Action<string, OutputLogStatus> log)
        : base(debuggerService, log)
    {
        RefreshAroundPcCommand = new RelayCommand(async () => await RefreshAroundPcAsync(), IsDebuggerUsable);
        RefreshFromAddressCommand = new RelayCommand(async () => await RefreshFromAddressAsync(), IsDebuggerUsable);
    }

    public ObservableCollection<MameDebuggerDisassemblyLine> Lines { get; } = [];
    public ICommand RefreshAroundPcCommand { get; }
    public ICommand RefreshFromAddressCommand { get; }

    public string StartAddressText
    {
        get => _startAddressText;
        set { if (_startAddressText != value) { _startAddressText = value; OnPropertyChanged(); } }
    }

    public string LineCountText
    {
        get => _lineCountText;
        set { if (_lineCountText != value) { _lineCountText = value; OnPropertyChanged(); } }
    }

    public MameDebuggerDisassemblyLine? SelectedLine
    {
        get => _selectedLine;
        set { if (_selectedLine != value) { _selectedLine = value; OnPropertyChanged(); } }
    }

    public override void NotifyCommandStateChanged() => RaiseCommands(RefreshAroundPcCommand, RefreshFromAddressCommand);

    public Task RefreshAroundPcAsync(bool logRequest = true)
        => RunDebuggerActionAsync("Disassembly refresh around current PC requested.", "Disassembly refresh failed", async cancellationToken =>
        {
            var status = await DebuggerService.GetStatusAsync(cancellationToken).ConfigureAwait(true);
            var block = await DebuggerService.DisassembleAsync(new MameDebuggerDisassemblyRequest(status.Cpu, null, ParseLineCount(), CenterAroundPc: true), cancellationToken).ConfigureAwait(true);
            ApplyBlock(block);
        }, logRequest);

    public Task RefreshFromAddressAsync()
        => RunDebuggerActionAsync("Disassembly refresh from address requested.", "Disassembly refresh from address failed", async cancellationToken =>
        {
            if (!TryParseInteger(StartAddressText, out var address))
            {
                Log($"Disassembly start address '{StartAddressText}' is not a valid decimal or 0x-prefixed value.", OutputLogStatus.Warning);
                return;
            }

            var status = await DebuggerService.GetStatusAsync(cancellationToken).ConfigureAwait(true);
            var block = await DebuggerService.DisassembleAsync(new MameDebuggerDisassemblyRequest(status.Cpu, address, ParseLineCount(), CenterAroundPc: false), cancellationToken).ConfigureAwait(true);
            ApplyBlock(block);
        });

    private void ApplyBlock(MameDebuggerDisassemblyBlock block)
    {
        Lines.Clear();
        foreach (var line in block.Lines)
        {
            Lines.Add(line);
        }

        SelectedLine = block.Lines.FirstOrDefault(line => line.IsCurrentPc);
        StartAddressText = ToHex(block.StartAddress);
        Log($"Disassembly refreshed: cpu={block.Cpu}, start={ToHex(block.StartAddress)}, lines={block.Lines.Count}/{block.LineCount}.", OutputLogStatus.Info);
    }

    private int ParseLineCount()
    {
        return int.TryParse(LineCountText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var count)
            ? Math.Clamp(count, 1, 256)
            : 32;
    }
}

public sealed class DebuggerRegistersViewModel : DebuggerPanelViewModelBase
{
    public DebuggerRegistersViewModel(IMameDebuggerService debuggerService, Action<string, OutputLogStatus> log)
        : base(debuggerService, log)
    {
        RefreshCommand = new RelayCommand(async () => await RefreshAsync(), IsDebuggerUsable);
    }

    public ObservableCollection<MameDebuggerRegister> Registers { get; } = [];
    public ICommand RefreshCommand { get; }

    public override void NotifyCommandStateChanged() => RaiseCommands(RefreshCommand);

    public Task RefreshAsync(bool logRequest = true)
        => RunDebuggerActionAsync("Register refresh requested.", "Register refresh failed", async cancellationToken =>
        {
            var status = await DebuggerService.GetStatusAsync(cancellationToken).ConfigureAwait(true);
            var registers = await DebuggerService.GetRegistersAsync(new MameDebuggerRegisterRequest(status.Cpu), cancellationToken).ConfigureAwait(true);
            Registers.Clear();
            foreach (var register in registers)
            {
                Registers.Add(register);
            }

            Log($"Registers refreshed: cpu={status.Cpu ?? "current"}, count={registers.Count}.", OutputLogStatus.Info);
        }, logRequest);
}

public sealed class DebuggerMemoryViewModel : DebuggerPanelViewModelBase
{
    private string? _cpuText;
    private string _addressSpaceText = "program";
    private string _startAddressText = "0x0";
    private string _lengthText = "64";
    private string _hexText = string.Empty;

    public DebuggerMemoryViewModel(IMameDebuggerService debuggerService, Action<string, OutputLogStatus> log)
        : base(debuggerService, log)
    {
        ReadCommand = new RelayCommand(async () => await ReadAsync(), IsDebuggerUsable);
    }

    public ICommand ReadCommand { get; }

    public string? CpuText
    {
        get => _cpuText;
        set { if (_cpuText != value) { _cpuText = value; OnPropertyChanged(); } }
    }

    public string AddressSpaceText
    {
        get => _addressSpaceText;
        set { if (_addressSpaceText != value) { _addressSpaceText = value; OnPropertyChanged(); } }
    }

    public string StartAddressText
    {
        get => _startAddressText;
        set { if (_startAddressText != value) { _startAddressText = value; OnPropertyChanged(); } }
    }

    public string LengthText
    {
        get => _lengthText;
        set { if (_lengthText != value) { _lengthText = value; OnPropertyChanged(); } }
    }

    public string HexText
    {
        get => _hexText;
        private set { if (_hexText != value) { _hexText = value; OnPropertyChanged(); } }
    }

    public override void NotifyCommandStateChanged() => RaiseCommands(ReadCommand);

    private Task ReadAsync()
        => RunDebuggerActionAsync("Memory read requested.", "Memory read failed", async cancellationToken =>
        {
            if (!TryParseInteger(StartAddressText, out var startAddress))
            {
                Log($"Memory start address '{StartAddressText}' is not a valid decimal or 0x-prefixed value.", OutputLogStatus.Warning);
                return;
            }

            if (!int.TryParse(LengthText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var length) || length <= 0)
            {
                Log($"Memory length '{LengthText}' is not a valid positive byte count.", OutputLogStatus.Warning);
                return;
            }

            length = Math.Clamp(length, 1, 4096);
            var cpu = string.IsNullOrWhiteSpace(CpuText) ? DebuggerService.State.CurrentCpu : CpuText.Trim();
            var addressSpace = string.IsNullOrWhiteSpace(AddressSpaceText) ? null : AddressSpaceText.Trim();
            var block = await DebuggerService.ReadMemoryAsync(new MameDebuggerMemoryReadRequest(cpu, startAddress, length, addressSpace), cancellationToken).ConfigureAwait(true);
            HexText = FormatHexDump(block);
            CpuText = block.Cpu;
            AddressSpaceText = block.AddressSpace;
            StartAddressText = ToHex(block.StartAddress);
            LengthText = block.Length.ToString(CultureInfo.InvariantCulture);
            Log($"Memory read: cpu={block.Cpu}, space={block.AddressSpace}, start={ToHex(block.StartAddress)}, length={block.Length}.", OutputLogStatus.Info);
        });

    private static string FormatHexDump(MameDebuggerMemoryBlock block)
    {
        var lines = new List<string>();
        for (var offset = 0; offset < block.Bytes.Count; offset += 16)
        {
            var bytes = block.Bytes.Skip(offset).Take(16).ToArray();
            lines.Add($"{block.StartAddress + offset:X8}: {string.Join(" ", bytes.Select(b => b.ToString("X2", CultureInfo.InvariantCulture)))}");
        }

        return string.Join(Environment.NewLine, lines);
    }
}

public sealed class DebuggerBreakpointsViewModel : DebuggerPanelViewModelBase
{
    private string _addressText = "0x0";
    private MameDebuggerBreakpoint? _selectedBreakpoint;

    public DebuggerBreakpointsViewModel(IMameDebuggerService debuggerService, Action<string, OutputLogStatus> log)
        : base(debuggerService, log)
    {
        RefreshCommand = new RelayCommand(async () => await RefreshAsync(), IsDebuggerUsable);
        AddCommand = new RelayCommand(async () => await AddAsync(), IsDebuggerUsable);
        EnableCommand = new RelayCommand(async () => await SetEnabledAsync(true), () => IsDebuggerUsable() && SelectedBreakpoint is not null);
        DisableCommand = new RelayCommand(async () => await SetEnabledAsync(false), () => IsDebuggerUsable() && SelectedBreakpoint is not null);
        ClearCommand = new RelayCommand(async () => await ClearAsync(), IsDebuggerUsable);
    }

    public ObservableCollection<MameDebuggerBreakpoint> Breakpoints { get; } = [];
    public ICommand RefreshCommand { get; }
    public ICommand AddCommand { get; }
    public ICommand EnableCommand { get; }
    public ICommand DisableCommand { get; }
    public ICommand ClearCommand { get; }

    public string AddressText
    {
        get => _addressText;
        set { if (_addressText != value) { _addressText = value; OnPropertyChanged(); } }
    }

    public MameDebuggerBreakpoint? SelectedBreakpoint
    {
        get => _selectedBreakpoint;
        set { if (_selectedBreakpoint != value) { _selectedBreakpoint = value; OnPropertyChanged(); NotifyCommandStateChanged(); } }
    }

    public override void NotifyCommandStateChanged() => RaiseCommands(RefreshCommand, AddCommand, EnableCommand, DisableCommand, ClearCommand);

    private Task RefreshAsync()
        => RunDebuggerActionAsync("Breakpoint refresh requested.", "Breakpoint refresh failed", async cancellationToken =>
        {
            var breakpoints = await DebuggerService.GetBreakpointsAsync(null, cancellationToken).ConfigureAwait(true);
            Breakpoints.Clear();
            foreach (var breakpoint in breakpoints)
            {
                Breakpoints.Add(breakpoint);
            }

            Log($"Breakpoints refreshed: count={breakpoints.Count}.", OutputLogStatus.Info);
        });

    private Task AddAsync()
        => RunDebuggerActionAsync("Breakpoint add requested.", "Breakpoint add failed", async cancellationToken =>
        {
            if (!TryParseInteger(AddressText, out var address))
            {
                Log($"Breakpoint address '{AddressText}' is not a valid decimal or 0x-prefixed value.", OutputLogStatus.Warning);
                return;
            }

            var breakpoint = await DebuggerService.SetBreakpointAsync(new MameDebuggerBreakpointRequest(DebuggerService.State.CurrentCpu, address), cancellationToken).ConfigureAwait(true);
            Breakpoints.Add(breakpoint);
            SelectedBreakpoint = breakpoint;
            Log($"Breakpoint #{breakpoint.MameId} added on {breakpoint.Cpu} at {ToHex(breakpoint.Address)}.", OutputLogStatus.Info);
        });

    private Task SetEnabledAsync(bool enabled)
        => RunDebuggerActionAsync(enabled ? "Breakpoint enable requested." : "Breakpoint disable requested.", enabled ? "Breakpoint enable failed" : "Breakpoint disable failed", async cancellationToken =>
        {
            if (SelectedBreakpoint is null)
            {
                return;
            }

            var request = new MameDebuggerBreakpointRequest(SelectedBreakpoint.Cpu, SelectedBreakpoint.Address, MameId: SelectedBreakpoint.MameId, DebuggerId: SelectedBreakpoint.DebuggerId);
            _ = enabled
                ? await DebuggerService.EnableBreakpointAsync(request, cancellationToken).ConfigureAwait(true)
                : await DebuggerService.DisableBreakpointAsync(request, cancellationToken).ConfigureAwait(true);
            await RefreshAsync().ConfigureAwait(true);
        });

    private Task ClearAsync()
        => RunDebuggerActionAsync("Breakpoint clear requested.", "Breakpoint clear failed", async cancellationToken =>
        {
            var targets = SelectedBreakpoint is null ? Breakpoints.ToArray() : [SelectedBreakpoint];
            IReadOnlyList<MameDebuggerBreakpoint> remaining = Breakpoints.ToArray();
            foreach (var target in targets)
            {
                var request = new MameDebuggerBreakpointRequest(target.Cpu, target.Address, MameId: target.MameId, DebuggerId: target.DebuggerId);
                remaining = await DebuggerService.ClearBreakpointAsync(request, cancellationToken).ConfigureAwait(true);
            }

            Breakpoints.Clear();
            foreach (var breakpoint in remaining)
            {
                Breakpoints.Add(breakpoint);
            }

            SelectedBreakpoint = null;
            Log($"Breakpoint clear completed: cleared={targets.Length}, remaining={remaining.Count}.", OutputLogStatus.Info);
        });
}

public sealed class DebuggerWatchpointsViewModel : DebuggerPanelViewModelBase
{
    private string _addressText = "0x0";
    private string _lengthText = "1";
    private MameDebuggerWatchpointType _selectedType = MameDebuggerWatchpointType.ReadWrite;
    private string _addressSpaceText = "program";
    private MameDebuggerWatchpoint? _selectedWatchpoint;

    public DebuggerWatchpointsViewModel(IMameDebuggerService debuggerService, Action<string, OutputLogStatus> log)
        : base(debuggerService, log)
    {
        RefreshCommand = new RelayCommand(async () => await RefreshAsync(), IsDebuggerUsable);
        AddCommand = new RelayCommand(async () => await AddAsync(), IsDebuggerUsable);
        EnableCommand = new RelayCommand(async () => await SetEnabledAsync(true), () => IsDebuggerUsable() && SelectedWatchpoint is not null);
        DisableCommand = new RelayCommand(async () => await SetEnabledAsync(false), () => IsDebuggerUsable() && SelectedWatchpoint is not null);
        ClearCommand = new RelayCommand(async () => await ClearAsync(), IsDebuggerUsable);
    }

    public ObservableCollection<MameDebuggerWatchpoint> Watchpoints { get; } = [];
    public IReadOnlyList<MameDebuggerWatchpointType> WatchpointTypes { get; } = Enum.GetValues<MameDebuggerWatchpointType>();
    public ICommand RefreshCommand { get; }
    public ICommand AddCommand { get; }
    public ICommand EnableCommand { get; }
    public ICommand DisableCommand { get; }
    public ICommand ClearCommand { get; }

    public string AddressText
    {
        get => _addressText;
        set { if (_addressText != value) { _addressText = value; OnPropertyChanged(); } }
    }

    public string LengthText
    {
        get => _lengthText;
        set { if (_lengthText != value) { _lengthText = value; OnPropertyChanged(); } }
    }

    public MameDebuggerWatchpointType SelectedType
    {
        get => _selectedType;
        set { if (_selectedType != value) { _selectedType = value; OnPropertyChanged(); } }
    }

    public string AddressSpaceText
    {
        get => _addressSpaceText;
        set { if (_addressSpaceText != value) { _addressSpaceText = value; OnPropertyChanged(); } }
    }

    public MameDebuggerWatchpoint? SelectedWatchpoint
    {
        get => _selectedWatchpoint;
        set { if (_selectedWatchpoint != value) { _selectedWatchpoint = value; OnPropertyChanged(); NotifyCommandStateChanged(); } }
    }

    public override void NotifyCommandStateChanged() => RaiseCommands(RefreshCommand, AddCommand, EnableCommand, DisableCommand, ClearCommand);

    private Task RefreshAsync()
        => RunDebuggerActionAsync("Watchpoint refresh requested.", "Watchpoint refresh failed", async cancellationToken =>
        {
            var watchpoints = await DebuggerService.GetWatchpointsAsync(null, cancellationToken, NullIfWhiteSpace(AddressSpaceText)).ConfigureAwait(true);
            Watchpoints.Clear();
            foreach (var watchpoint in watchpoints)
            {
                Watchpoints.Add(watchpoint);
            }

            Log($"Watchpoints refreshed: count={watchpoints.Count}.", OutputLogStatus.Info);
        });

    private Task AddAsync()
        => RunDebuggerActionAsync("Watchpoint add requested.", "Watchpoint add failed", async cancellationToken =>
        {
            if (!TryParseInteger(AddressText, out var address))
            {
                Log($"Watchpoint address '{AddressText}' is not a valid decimal or 0x-prefixed value.", OutputLogStatus.Warning);
                return;
            }

            if (!TryParseInteger(LengthText, out var length) || length <= 0)
            {
                Log($"Watchpoint length '{LengthText}' is not a valid positive value.", OutputLogStatus.Warning);
                return;
            }

            var watchpoint = await DebuggerService.SetWatchpointAsync(new MameDebuggerWatchpointRequest(DebuggerService.State.CurrentCpu, address, length, SelectedType, AddressSpace: NullIfWhiteSpace(AddressSpaceText)), cancellationToken).ConfigureAwait(true);
            Watchpoints.Add(watchpoint);
            SelectedWatchpoint = watchpoint;
            Log($"Watchpoint #{watchpoint.MameId} added on {watchpoint.Cpu} at {ToHex(watchpoint.Address)} length={watchpoint.Length} type={watchpoint.Type}.", OutputLogStatus.Info);
        });

    private Task SetEnabledAsync(bool enabled)
        => RunDebuggerActionAsync(enabled ? "Watchpoint enable requested." : "Watchpoint disable requested.", enabled ? "Watchpoint enable failed" : "Watchpoint disable failed", async cancellationToken =>
        {
            if (SelectedWatchpoint is null)
            {
                return;
            }

            var request = new MameDebuggerWatchpointRequest(SelectedWatchpoint.Cpu, SelectedWatchpoint.Address, SelectedWatchpoint.Length, SelectedWatchpoint.Type, AddressSpace: SelectedWatchpoint.AddressSpace, MameId: SelectedWatchpoint.MameId, DebuggerId: SelectedWatchpoint.DebuggerId);
            _ = enabled
                ? await DebuggerService.EnableWatchpointAsync(request, cancellationToken).ConfigureAwait(true)
                : await DebuggerService.DisableWatchpointAsync(request, cancellationToken).ConfigureAwait(true);
            await RefreshAsync().ConfigureAwait(true);
        });

    private Task ClearAsync()
        => RunDebuggerActionAsync("Watchpoint clear requested.", "Watchpoint clear failed", async cancellationToken =>
        {
            var targets = SelectedWatchpoint is null ? Watchpoints.ToArray() : [SelectedWatchpoint];
            IReadOnlyList<MameDebuggerWatchpoint> remaining = Watchpoints.ToArray();
            foreach (var target in targets)
            {
                var request = new MameDebuggerWatchpointRequest(target.Cpu, target.Address, target.Length, target.Type, AddressSpace: target.AddressSpace, MameId: target.MameId, DebuggerId: target.DebuggerId);
                remaining = await DebuggerService.ClearWatchpointAsync(request, cancellationToken).ConfigureAwait(true);
            }

            Watchpoints.Clear();
            foreach (var watchpoint in remaining)
            {
                Watchpoints.Add(watchpoint);
            }

            SelectedWatchpoint = null;
            Log($"Watchpoint clear completed: cleared={targets.Length}, remaining={remaining.Count}.", OutputLogStatus.Info);
        });

    private static string? NullIfWhiteSpace(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
