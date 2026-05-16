using System.IO;
using System.Diagnostics;
using System.Text;

namespace OasisEditor;

public sealed class MameProcessRunner : IMameProcessRunner, IDisposable
{
    private static readonly TimeSpan GracefulExitTimeout = TimeSpan.FromSeconds(5);
    private readonly Func<Process> _processFactory;
    private readonly Action<string>? _stdoutLogger;
    private readonly Action<string>? _stdinLogger;
    private readonly Action<string>? _stderrLogger;
    private Process? _process;
    private Task? _stdoutPumpTask;
    private Task? _stderrPumpTask;

    public MameProcessRunner(Action<string>? stdoutLogger = null, Action<string>? stdinLogger = null, Action<string>? stderrLogger = null)
        : this(() => new Process(), stdoutLogger, stdinLogger, stderrLogger)
    {
    }

    internal MameProcessRunner(Func<Process> processFactory, Action<string>? stdoutLogger = null, Action<string>? stdinLogger = null, Action<string>? stderrLogger = null)
    {
        _processFactory = processFactory ?? throw new ArgumentNullException(nameof(processFactory));
        _stdoutLogger = stdoutLogger;
        _stdinLogger = stdinLogger;
        _stderrLogger = stderrLogger;
    }

    public async Task StartAsync(ProcessStartInfo startInfo, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(startInfo);

        if (_process is { HasExited: false })
        {
            throw new InvalidOperationException("MAME process is already running.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        CleanupResidualMameProcesses(startInfo.FileName);

        var process = _processFactory();
        process.StartInfo = startInfo;
        process.EnableRaisingEvents = true;

        if (!process.Start())
        {
            process.Dispose();
            throw new InvalidOperationException("Failed to start MAME process.");
        }

        _process = process;
        _stdoutPumpTask = PumpStreamAsync(process.StandardOutput, _stdoutLogger, cancellationToken);
        _stderrPumpTask = PumpStreamAsync(process.StandardError, _stderrLogger, cancellationToken);

        await Task.Delay(100, cancellationToken).ConfigureAwait(false);
        if (process.HasExited)
        {
            var exitCode = process.ExitCode;
            CleanupResidualMameProcesses(startInfo.FileName);
            _stdoutPumpTask = null;
            _stderrPumpTask = null;
            _process = null;
            process.Dispose();
            throw new InvalidOperationException($"MAME process exited immediately after start (exit code {exitCode}).");
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        var process = _process;
        if (process is null)
        {
            return;
        }

        try
        {
            var executablePath = process.StartInfo.FileName;
            if (!process.HasExited)
            {
                await RequestGracefulExitAsync(process, cancellationToken).ConfigureAwait(false);

                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
                }
            }

            await AwaitPumpAsync(_stdoutPumpTask).ConfigureAwait(false);
            await AwaitPumpAsync(_stderrPumpTask).ConfigureAwait(false);
            CleanupResidualMameProcesses(executablePath);
        }
        finally
        {
            _stdoutPumpTask = null;
            _stderrPumpTask = null;
            _process = null;
            process.Dispose();
        }
    }

    public async Task WriteStandardInputAsync(string command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            throw new ArgumentException("Input command must be non-empty.", nameof(command));
        }

        var process = _process;
        if (process is null || process.HasExited)
        {
            throw new InvalidOperationException("Cannot write input because MAME process is not running.");
        }

        _stdinLogger?.Invoke(command);
        await process.StandardInput.WriteLineAsync(new StringBuilder(command), cancellationToken).ConfigureAwait(false);
        await process.StandardInput.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    public void Dispose()
    {
        _process?.Dispose();
        _process = null;
    }

    private static async Task PumpStreamAsync(StreamReader reader, Action<string>? logger, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (line is null)
            {
                break;
            }

            logger?.Invoke(line);
        }
    }

    private static async Task AwaitPumpAsync(Task? pump)
    {
        if (pump is null)
        {
            return;
        }

        try
        {
            await pump.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // ignored
        }
    }

    private static async Task RequestGracefulExitAsync(Process process, CancellationToken cancellationToken)
    {
        if (!process.StartInfo.RedirectStandardInput)
        {
            return;
        }

        using var gracefulExitCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        gracefulExitCts.CancelAfter(GracefulExitTimeout);

        try
        {
            await process.StandardInput.WriteLineAsync(new StringBuilder("exit"), cancellationToken).ConfigureAwait(false);
            await process.StandardInput.FlushAsync(cancellationToken).ConfigureAwait(false);
            await process.WaitForExitAsync(gracefulExitCts.Token).ConfigureAwait(false);
        }
        catch (ObjectDisposedException)
        {
            // Process was disposed while attempting graceful exit.
        }
        catch (InvalidOperationException)
        {
            // Process input stream no longer available.
        }
        catch (OperationCanceledException)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            // Timed out while waiting for graceful exit.
        }
    }

    private static void CleanupResidualMameProcesses(string executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return;
        }

        var fullPath = Path.GetFullPath(executablePath);
        var processName = Path.GetFileNameWithoutExtension(fullPath);
        var currentProcessId = Environment.ProcessId;

        foreach (var candidate in Process.GetProcessesByName(processName))
        {
            try
            {
                if (candidate.Id == currentProcessId || candidate.HasExited)
                {
                    continue;
                }

                var candidatePath = candidate.MainModule?.FileName;
                if (!string.Equals(candidatePath, fullPath, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                candidate.Kill(entireProcessTree: true);
                candidate.WaitForExit(2000);
            }
            catch
            {
                // Best-effort cleanup only.
            }
            finally
            {
                candidate.Dispose();
            }
        }
    }
}
