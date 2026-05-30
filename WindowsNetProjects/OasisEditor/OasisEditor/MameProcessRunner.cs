using System.Diagnostics;
using System.IO;
using System.Text;

namespace OasisEditor;

public sealed class MameProcessRunner : IMameProcessRunner, IDisposable
{
    private static readonly TimeSpan GracefulExitTimeout = TimeSpan.FromSeconds(3);

    private readonly Func<Process> _processFactory;
    private readonly Action<string>? _stdoutLogger;
    private readonly Action<string>? _stdinLogger;
    private readonly Action<string>? _stderrLogger;
    private Process? _process;
    private bool _outputReadStarted;
    private bool _errorReadStarted;

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

    public Task StartAsync(ProcessStartInfo startInfo, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(startInfo);

        if (_process is { HasExited: false })
        {
            throw new InvalidOperationException("MAME process is already running.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        var process = _processFactory();
        process.StartInfo = startInfo;
        process.EnableRaisingEvents = true;

        process.OutputDataReceived += OnOutputDataReceived;
        process.ErrorDataReceived += OnErrorDataReceived;

        try
        {
            if (!process.Start())
            {
                throw new InvalidOperationException("Failed to start MAME process.");
            }

            process.BeginOutputReadLine();
            _outputReadStarted = true;

            process.BeginErrorReadLine();
            _errorReadStarted = true;

            _process = process;
        }
        catch
        {
            process.OutputDataReceived -= OnOutputDataReceived;
            process.ErrorDataReceived -= OnErrorDataReceived;
            process.Dispose();
            _outputReadStarted = false;
            _errorReadStarted = false;
            throw;
        }

        return Task.CompletedTask;
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
            if (!process.HasExited)
            {
                var exitedCleanly = await TryStopCleanlyAsync(process, cancellationToken).ConfigureAwait(false);
                if (!exitedCleanly && !process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
                }
            }
        }
        finally
        {
            DetachOutputHandlers(process);
            _process = null;
            process.Dispose();
        }
    }

    private async Task<bool> TryStopCleanlyAsync(Process process, CancellationToken cancellationToken)
    {
        try
        {
            await WriteStandardInputAsync(process, "exit", cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or ObjectDisposedException)
        {
            return false;
        }

        var waitForExitTask = process.WaitForExitAsync(cancellationToken);
        var timeoutTask = Task.Delay(GracefulExitTimeout, cancellationToken);
        var completedTask = await Task.WhenAny(waitForExitTask, timeoutTask).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        if (completedTask != waitForExitTask)
        {
            return false;
        }

        await waitForExitTask.ConfigureAwait(false);
        return true;
    }

    private async Task WriteStandardInputAsync(Process process, string command, CancellationToken cancellationToken)
    {
        _stdinLogger?.Invoke(command);
        await process.StandardInput.WriteLineAsync(new StringBuilder(command), cancellationToken).ConfigureAwait(false);
        await process.StandardInput.FlushAsync(cancellationToken).ConfigureAwait(false);
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

        await WriteStandardInputAsync(process, command, cancellationToken).ConfigureAwait(false);
    }

    public void Dispose()
    {
        var process = _process;
        if (process is not null)
        {
            DetachOutputHandlers(process);
            process.Dispose();
        }

        _process = null;
    }

    private void OnOutputDataReceived(object sender, DataReceivedEventArgs e)
    {
        if (!string.IsNullOrEmpty(e.Data))
        {
            _stdoutLogger?.Invoke(e.Data);
        }
    }

    private void OnErrorDataReceived(object sender, DataReceivedEventArgs e)
    {
        if (!string.IsNullOrEmpty(e.Data))
        {
            _stderrLogger?.Invoke(e.Data);
        }
    }

    private void DetachOutputHandlers(Process process)
    {
        if (_outputReadStarted)
        {
            try
            {
                process.CancelOutputRead();
            }
            catch (InvalidOperationException)
            {
                // The process may have exited before the async output read was cancelled.
            }
        }

        if (_errorReadStarted)
        {
            try
            {
                process.CancelErrorRead();
            }
            catch (InvalidOperationException)
            {
                // The process may have exited before the async error read was cancelled.
            }
        }

        process.OutputDataReceived -= OnOutputDataReceived;
        process.ErrorDataReceived -= OnErrorDataReceived;
        _outputReadStarted = false;
        _errorReadStarted = false;
    }
}
