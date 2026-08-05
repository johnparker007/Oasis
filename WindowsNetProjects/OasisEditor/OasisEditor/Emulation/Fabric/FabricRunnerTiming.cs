using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace OasisEditor;

internal sealed class FabricRunnerTimer : IDisposable
{
    private const uint CreateWaitableTimerHighResolution = 0x00000002;
    private const uint TimerAllAccess = 0x001F0003;
    private readonly SafeWaitHandle? _timerHandle;
    private readonly WaitHandle? _timerWaitHandle;
    private readonly ManualResetEventSlim _wake;

    public FabricRunnerTimer(ManualResetEventSlim wake)
    {
        _wake = wake;
        if (!OperatingSystem.IsWindows())
        {
            TimingMode = "ManagedWait";
            return;
        }

        _timerHandle = CreateWaitableTimerEx(IntPtr.Zero, null, CreateWaitableTimerHighResolution, TimerAllAccess);
        if (_timerHandle.IsInvalid)
            _timerHandle = CreateWaitableTimerEx(IntPtr.Zero, null, 0, TimerAllAccess);
        if (_timerHandle.IsInvalid)
        {
            TimingMode = "ManagedWait";
            _timerHandle.Dispose();
            _timerHandle = null;
            return;
        }

        HighResolutionTimerActive = true;
        TimingMode = "WaitableTimer";
        _timerWaitHandle = new EventWaitHandle(false, EventResetMode.AutoReset) { SafeWaitHandle = _timerHandle };
    }

    public string TimingMode { get; }
    public bool HighResolutionTimerActive { get; }

    public bool Wait(TimeSpan delay, CancellationToken cancellationToken)
    {
        if (delay <= TimeSpan.Zero)
            return false;
        if (_timerWaitHandle is null)
        {
            _wake.Wait(delay, cancellationToken);
            _wake.Reset();
            return true;
        }

        var dueTime = -Math.Max(1, delay.Ticks);
        if (!SetWaitableTimer(_timerHandle!, ref dueTime, 0, IntPtr.Zero, IntPtr.Zero, false))
            throw new Win32Exception(Marshal.GetLastWin32Error());
        var index = WaitHandle.WaitAny([_timerWaitHandle, _wake.WaitHandle, cancellationToken.WaitHandle]);
        _wake.Reset();
        cancellationToken.ThrowIfCancellationRequested();
        return index == 0;
    }

    public void Dispose()
    {
        _timerWaitHandle?.Dispose();
        _timerHandle?.Dispose();
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern SafeWaitHandle CreateWaitableTimerEx(IntPtr lpTimerAttributes, string? lpTimerName, uint dwFlags, uint dwDesiredAccess);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetWaitableTimer(SafeWaitHandle hTimer, ref long lpDueTime, int lPeriod, IntPtr pfnCompletionRoutine, IntPtr lpArgToCompletionRoutine, bool fResume);
}

internal sealed class FabricRunnerMmcssRegistration : IDisposable
{
    private IntPtr _handle;

    private FabricRunnerMmcssRegistration(IntPtr handle, bool registered)
    {
        _handle = handle;
        Registered = registered;
    }

    public bool Registered { get; }

    public static FabricRunnerMmcssRegistration Register(string taskName)
    {
        if (!OperatingSystem.IsWindows())
            return new(IntPtr.Zero, false);
        var taskIndex = 0u;
        var handle = AvSetMmThreadCharacteristics(taskName, ref taskIndex);
        return new(handle, handle != IntPtr.Zero);
    }

    public void Dispose()
    {
        if (_handle == IntPtr.Zero)
            return;
        AvRevertMmThreadCharacteristics(_handle);
        _handle = IntPtr.Zero;
    }

    [DllImport("avrt.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr AvSetMmThreadCharacteristics(string taskName, ref uint taskIndex);

    [DllImport("avrt.dll", SetLastError = true)]
    private static extern bool AvRevertMmThreadCharacteristics(IntPtr avrtHandle);
}
