using System.Windows;
using System.Windows.Threading;

namespace OasisEditor;

public partial class App : Application
{
    private static DateTime _lastSuppressedAvalonDockLogUtc = DateTime.MinValue;
    private readonly IApplicationThemeService _applicationThemeService = new ApplicationThemeService();
    private readonly EditorPreferencesStore _preferencesStore = new();

    public App()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        _applicationThemeService.EnsureFluentThemeResources(this);

        var preferences = _preferencesStore.Load();
        _applicationThemeService.ApplyTheme(this, preferences.ThemePreference);

        base.OnStartup(e);

        var launcherWindow = new LauncherWindow(_applicationThemeService, _preferencesStore);
        launcherWindow.Show();
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        if (IsAvalonDockDragException(e.Exception))
        {
            TryLogSuppressedAvalonDockException(e.Exception);
            e.Handled = true;
            return;
        }

        CrashDiagnostics.Log("DispatcherUnhandledException", e.Exception, isTerminating: false);

        var message = $"A fatal UI exception occurred.\n\nCrash details were written to:\n{CrashDiagnostics.LogPath}";
        MessageBox.Show(message, "Oasis Editor - Crash", MessageBoxButton.OK, MessageBoxImage.Error);

        e.Handled = false;
    }

    private static void OnUnhandledException(object? sender, UnhandledExceptionEventArgs e)
    {
        var exception = e.ExceptionObject as Exception
                        ?? new InvalidOperationException($"Unhandled non-exception object: {e.ExceptionObject}");

        CrashDiagnostics.Log("AppDomain.CurrentDomain.UnhandledException", exception, e.IsTerminating);
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        CrashDiagnostics.Log("TaskScheduler.UnobservedTaskException", e.Exception, isTerminating: false);
        e.SetObserved();
    }

    private static void TryLogSuppressedAvalonDockException(Exception exception)
    {
        var now = DateTime.UtcNow;
        if ((now - _lastSuppressedAvalonDockLogUtc).TotalMilliseconds < 500)
        {
            return;
        }

        _lastSuppressedAvalonDockLogUtc = now;
        CrashDiagnostics.Log("SuppressedAvalonDockDragException", exception, isTerminating: false);
    }

    private static bool IsAvalonDockDragException(Exception exception)
    {
        if (exception is not NullReferenceException && exception is not ArgumentNullException)
        {
            return false;
        }

        var stackTrace = exception.StackTrace;
        if (string.IsNullOrWhiteSpace(stackTrace))
        {
            return false;
        }

        return stackTrace.Contains("AvalonDock.Controls.OverlayWindow.OnApplyTemplate", StringComparison.Ordinal)
               || stackTrace.Contains("AvalonDock.Controls.DragService", StringComparison.Ordinal)
               || stackTrace.Contains("AvalonDock.Controls.OverlayWindow.AvalonDock.Controls.IOverlayWindow.DragEnter", StringComparison.Ordinal);
    }
}
