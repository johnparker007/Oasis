using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace OasisEditor.Avalonia.Mcp;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length != 2 || args[0] != "--workspace" || !Directory.Exists(args[1]))
        {
            Console.Error.WriteLine("Usage: OasisEditor.Avalonia.Mcp --workspace <existing-directory>. Headless stdio session; no desktop windows.");
            return 2;
        }
        Console.SetOut(Console.Error);
        try
        {
            global::OasisEditor.Avalonia.Program.BuildHeadless().SetupWithoutStarting();
            var session = new AuthoringSession(args[1]);
            var faces = new FaceAuthoringSession(session);
            using var capture = new WorkspaceCapture(session, faces);
            var builder = Host.CreateApplicationBuilder();
            builder.Logging.ClearProviders();
            builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);
            builder.Logging.SetMinimumLevel(LogLevel.Warning);
            builder.Services.AddSingleton(session);
            builder.Services.AddSingleton(faces);
            builder.Services.AddSingleton(capture);
            builder.Services.AddSingleton<EditorDispatcher>();
            builder.Services.AddMcpServer().WithStdioServerTransport().WithTools<EditorTools>();
            using var host = builder.Build();
            using var stop = new CancellationTokenSource();
            Exception? failure = null;
            var run = Task.Run(async () =>
            {
                try { await host.RunAsync(); }
                catch (Exception e) { failure = e; }
                finally { Dispatcher.UIThread.Post(stop.Cancel); }
            });
            Dispatcher.UIThread.MainLoop(stop.Token);
            run.GetAwaiter().GetResult();
            if (failure is not null) throw failure;
            return 0;
        }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }
}

public sealed class EditorDispatcher
{
    public async Task<T> Invoke<T>(Func<T> action, CancellationToken cancellationToken)
        => await Dispatcher.UIThread.InvokeAsync(action, DispatcherPriority.Normal, cancellationToken);
}
