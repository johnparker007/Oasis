using System.IO;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OasisEditor.Automation;

namespace OasisEditor.Mcp;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length != 2 || args[0] != "--workspace")
        {
            Console.Error.WriteLine("Usage: OasisEditor.Mcp --workspace <existing-directory>. Runs an isolated stdio session; no visible windows.");
            return 2;
        }
        // Keep legacy decoder diagnostics away from the MCP protocol stream.
        Console.SetOut(Console.Error);
        try
        {
            var app = new App { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            app.InitializeComponent();
            var themes = new ApplicationThemeService();
            themes.EnsureFluentThemeResources(app);
            themes.ApplyTheme(app, ThemePreference.System);
            var session = new McpEditorSession(args[1]);
            var dispatcher = Dispatcher.CurrentDispatcher;
            var builder = Host.CreateApplicationBuilder();
            builder.Logging.SetMinimumLevel(LogLevel.Warning);
            builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);
            builder.Services.AddSingleton(session);
            builder.Services.AddSingleton(new EditorDispatcher(dispatcher));
            builder.Services.AddMcpServer().WithStdioServerTransport().WithTools<EditorTools>();
            using var host = builder.Build();
            Exception? failure = null;
            var run = Task.Run(async () =>
            {
                try { await host.RunAsync(); }
                catch (Exception exception) { failure = exception; }
                finally { dispatcher.BeginInvokeShutdown(DispatcherPriority.Normal); }
            });
            Dispatcher.Run();
            run.GetAwaiter().GetResult();
            if (failure is not null) throw failure;
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }
}

public sealed class EditorDispatcher(Dispatcher dispatcher)
{
    public Task<T> Invoke<T>(Func<T> action, CancellationToken cancellationToken) =>
        dispatcher.InvokeAsync(action, DispatcherPriority.Normal, cancellationToken).Task;
}
