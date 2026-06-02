using OasisEditor.Features.MameDebugger;
using OasisEditor.Features.MameDebugger.ViewModels;
using Xunit;

namespace OasisEditor.Tests;

public sealed class MameDebuggerPanelViewModelTests
{
    [Fact]
    public async Task ControlRefreshStatusUpdatesDisplayedState()
    {
        var service = new FakeMameDebuggerService
        {
            Snapshot = new MameDebuggerStateSnapshot(true, true, MameDebuggerExecutionState.Stopped, ":maincpu", 0x1234),
            Status = new MameDebuggerStatus(true, MameDebuggerExecutionState.Stopped, ":maincpu", 0x1234)
        };
        var logs = new List<string>();
        var viewModel = new DebuggerControlViewModel(service, (message, _) => logs.Add(message));

        await viewModel.RefreshStatusAsync();

        Assert.Equal("Stopped", viewModel.StateText);
        Assert.Equal(":maincpu", viewModel.CurrentCpuText);
        Assert.Equal("0x1234", viewModel.CurrentPcText);
        Assert.Contains(logs, log => log.Contains("Debugger status", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RegistersRefreshUsesCurrentStatusCpu()
    {
        var service = new FakeMameDebuggerService
        {
            Snapshot = new MameDebuggerStateSnapshot(true, true, MameDebuggerExecutionState.Stopped, ":maincpu", 0x1234),
            Status = new MameDebuggerStatus(true, MameDebuggerExecutionState.Stopped, ":maincpu", 0x1234),
            Registers =
            [
                new MameDebuggerRegister(":maincpu", "PC", 0x1234, "1234", Editable: false),
                new MameDebuggerRegister(":maincpu", "A", 0x56, "56", Editable: true)
            ]
        };
        var viewModel = new DebuggerRegistersViewModel(service, (_, _) => { });

        await viewModel.RefreshAsync();

        Assert.Equal(":maincpu", service.LastRegisterRequest?.Cpu);
        Assert.Collection(
            viewModel.Registers,
            register => Assert.Equal("PC", register.Name),
            register => Assert.Equal("A", register.Name));
    }

    [Fact]
    public async Task DisassemblyRefreshFromInvalidAddressLogsWarningWithoutBackendCall()
    {
        var service = new FakeMameDebuggerService
        {
            Snapshot = new MameDebuggerStateSnapshot(true, true, MameDebuggerExecutionState.Stopped, ":maincpu", 0x1234),
            Status = new MameDebuggerStatus(true, MameDebuggerExecutionState.Stopped, ":maincpu", 0x1234)
        };
        var logs = new List<(string Message, OutputLogStatus Status)>();
        var viewModel = new DebuggerDisassemblyViewModel(service, (message, status) => logs.Add((message, status)))
        {
            StartAddressText = "not-an-address"
        };

        await viewModel.RefreshFromAddressAsync();

        Assert.Equal(0, service.DisassembleCallCount);
        Assert.Contains(logs, log => log.Status == OutputLogStatus.Warning && log.Message.Contains("not a valid", StringComparison.Ordinal));
    }

    private sealed class FakeMameDebuggerService : IMameDebuggerService
    {
        public MameDebuggerStateSnapshot Snapshot { get; set; } = new(true, false, MameDebuggerExecutionState.Unknown, null, null);
        public MameDebuggerStatus Status { get; set; } = new(false, MameDebuggerExecutionState.Unknown, null, null);
        public IReadOnlyList<MameDebuggerRegister> Registers { get; set; } = [];
        public MameDebuggerRegisterRequest? LastRegisterRequest { get; private set; }
        public int DisassembleCallCount { get; private set; }

        public MameDebuggerStateSnapshot State => Snapshot;
        public event EventHandler<MameDebuggerEvent>? DebuggerEventReceived;

        public Task<MameDebuggerPing> PingAsync(CancellationToken cancellationToken) => Task.FromResult(new MameDebuggerPing(true, Status.Available));
        public Task<MameDebuggerStatus> GetStatusAsync(CancellationToken cancellationToken) => Task.FromResult(Status);
        public Task<IReadOnlyList<MameDebuggerCpu>> GetCpuListAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<MameDebuggerCpu>>([]);
        public Task RunAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task BreakAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task StepAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<MameDebuggerBreakpoint> SetBreakpointAsync(MameDebuggerBreakpointRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<MameDebuggerBreakpoint>> GetBreakpointsAsync(string? cpu, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<MameDebuggerBreakpoint>>([]);
        public Task<MameDebuggerBreakpoint> EnableBreakpointAsync(MameDebuggerBreakpointRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<MameDebuggerBreakpoint> DisableBreakpointAsync(MameDebuggerBreakpointRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<MameDebuggerBreakpoint>> ClearBreakpointAsync(MameDebuggerBreakpointRequest request, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<MameDebuggerBreakpoint>>([]);
        public Task<MameDebuggerWatchpoint> SetWatchpointAsync(MameDebuggerWatchpointRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<MameDebuggerWatchpoint>> GetWatchpointsAsync(string? cpu, CancellationToken cancellationToken, string? addressSpace = null) => Task.FromResult<IReadOnlyList<MameDebuggerWatchpoint>>([]);
        public Task<MameDebuggerWatchpoint> EnableWatchpointAsync(MameDebuggerWatchpointRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<MameDebuggerWatchpoint> DisableWatchpointAsync(MameDebuggerWatchpointRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<MameDebuggerWatchpoint>> ClearWatchpointAsync(MameDebuggerWatchpointRequest request, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<MameDebuggerWatchpoint>>([]);
        public Task<IReadOnlyList<MameDebuggerRegister>> GetRegistersAsync(MameDebuggerRegisterRequest request, CancellationToken cancellationToken)
        {
            LastRegisterRequest = request;
            return Task.FromResult(Registers);
        }

        public Task<MameDebuggerRegister> SetRegisterAsync(MameDebuggerRegisterRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<MameDebuggerMemoryBlock> ReadMemoryAsync(MameDebuggerMemoryReadRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<MameDebuggerMemoryBlock> WriteMemoryAsync(MameDebuggerMemoryWriteRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<MameDebuggerDisassemblyBlock> DisassembleAsync(MameDebuggerDisassemblyRequest request, CancellationToken cancellationToken)
        {
            DisassembleCallCount++;
            return Task.FromResult(new MameDebuggerDisassemblyBlock(request.Cpu ?? ":maincpu", request.StartAddress ?? 0, request.LineCount, 0, []));
        }

        public void ProcessStdoutLine(string line) { }
        public void SetDebuggerLaunchActive(bool isActive) => Snapshot = Snapshot with { IsDebuggerLaunchActive = isActive };

        public void RaiseEvent(MameDebuggerEvent debuggerEvent) => DebuggerEventReceived?.Invoke(this, debuggerEvent);
    }
}
