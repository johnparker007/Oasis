using Avalonia.Threading;

namespace OasisEditor.Avalonia.Tests;

internal static class HeadlessTestHost
{
    private static readonly Lazy<Task<Dispatcher>> Host = new(Start);
    private static Task<Dispatcher> Start()
    {
        var ready = new TaskCompletionSource<Dispatcher>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                Program.BuildHeadless().SetupWithoutStarting();
                ready.SetResult(Dispatcher.UIThread);
                Dispatcher.UIThread.MainLoop(CancellationToken.None);
            }
            catch (Exception e) { ready.TrySetException(e); }
        }) { IsBackground = true, Name = "Oasis headless test UI" };
        if (OperatingSystem.IsWindows()) thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return ready.Task;
    }
    public static void Run(Action test) => Host.Value.GetAwaiter().GetResult().InvokeAsync(test).GetAwaiter().GetResult();
}
