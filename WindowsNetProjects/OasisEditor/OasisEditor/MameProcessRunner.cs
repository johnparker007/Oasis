using System.Diagnostics;
using System.Text;

namespace OasisEditor;

public sealed class MameProcessRunner : IMameProcessRunner, IDisposable
{
    private readonly Func<Process> _processFactory;
    private readonly Action<string>? _stdoutLogger;
    private readonly Action<string>? _stderrLogger;
    private Process? _process;
    private Task? _stdoutPumpTask;
    private Task? _stderrPumpTask;

    public MameProcessRunner(Action<string>? stdoutLogger = null, Action<string>? stderrLogger = null)
        : this(() => new Process(), stdoutLogger, stderrLogger)
    {
    }

    internal MameProcessRunner(Func<Process> processFactory, Action<string>? stdoutLogger = null, Action<string>? stderrLogger = null)
    {
        _processFactory = processFactory ?? throw new ArgumentNullException(nameof(processFactory));
        _stdoutLogger = stdoutLogger;
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

        if (!process.Start())
        {
            process.Dispose();
            throw new InvalidOperationException("Failed to start MAME process.");
        }

        _process = process;
        _stdoutPumpTask = PumpStreamAsync(process.StandardOutput, _stdoutLogger, cancellationToken);
        _stderrPumpTask = PumpStreamAsync(process.StandardError, _stderrLogger, cancellationToken);

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
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            }

            await AwaitPumpAsync(_stdoutPumpTask).ConfigureAwait(false);
            await AwaitPumpAsync(_stderrPumpTask).ConfigureAwait(false);
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
}
