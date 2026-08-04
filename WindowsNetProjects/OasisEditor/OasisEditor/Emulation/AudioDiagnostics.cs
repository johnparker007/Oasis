using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace OasisEditor;

internal enum EmulationAudioCaptureBoundary
{
    FabricManagedRead,
    FabricBackendSubmit
}

internal readonly record struct EmulationAudioCaptureBlock(
    EmulationAudioCaptureBoundary Boundary,
    EmulationAudioFormat Format,
    long Sequence,
    long StartFrame,
    short[] Samples,
    int Frames);

internal sealed class EmulationAudioDiagnostics : IDisposable
{
    private const int DefaultQueueCapacity = 256;
    private readonly ConcurrentQueue<EmulationAudioCaptureBlock> _queue = new();
    private readonly SemaphoreSlim _signal = new(0);
    private readonly CancellationTokenSource _cancellation = new();
    private readonly Task? _writer;
    private readonly string _directory;
    private readonly int _queueCapacity;
    private long _queued;
    private long _captureDrops;
    private bool _disposed;

    private EmulationAudioDiagnostics(string directory, int queueCapacity)
    {
        _directory = directory;
        _queueCapacity = queueCapacity;
        Directory.CreateDirectory(directory);
        _writer = Task.Run(WriteLoopAsync);
    }

    internal static EmulationAudioDiagnostics? CreateFromEnvironment()
    {
        var directory = Environment.GetEnvironmentVariable("OASIS_AUDIO_DIAGNOSTIC_CAPTURE_DIR");
        if (string.IsNullOrWhiteSpace(directory))
            return null;

        var capacityText = Environment.GetEnvironmentVariable("OASIS_AUDIO_DIAGNOSTIC_QUEUE_BLOCKS");
        var capacity = int.TryParse(capacityText, out var parsed) && parsed > 0 ? parsed : DefaultQueueCapacity;
        return new EmulationAudioDiagnostics(directory, capacity);
    }

    internal AudioBoundaryAccounting FabricManagedRead { get; } = new("Fabric managed read");
    internal AudioBoundaryAccounting FabricBackendSubmit { get; } = new("Fabric backend submit");

    internal long CaptureQueueDrops => Interlocked.Read(ref _captureDrops);

    internal void Capture(EmulationAudioCaptureBoundary boundary, EmulationAudioFormat format, long sequence, long startFrame, ReadOnlySpan<short> samples, int frames)
    {
        if (_disposed || frames <= 0)
            return;

        if (Interlocked.Read(ref _queued) >= _queueCapacity)
        {
            Interlocked.Increment(ref _captureDrops);
            return;
        }

        var copy = samples.ToArray();
        Interlocked.Increment(ref _queued);
        _queue.Enqueue(new(boundary, format, sequence, startFrame, copy, frames));
        _signal.Release();
    }

    internal void WriteSummary(Action<string> logger)
    {
        logger(FabricManagedRead.CreateSummary());
        logger(FabricBackendSubmit.CreateSummary());
        logger($"Audio diagnostics capture: queueDrops={CaptureQueueDrops}, queuedBlocks={Interlocked.Read(ref _queued)}, directory='{_directory}'.");
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _cancellation.Cancel();
        _signal.Release();
        try { _writer?.Wait(TimeSpan.FromSeconds(5)); } catch { }
        _signal.Dispose();
        _cancellation.Dispose();
    }

    private async Task WriteLoopAsync()
    {
        var writers = new Dictionary<EmulationAudioCaptureBoundary, RawPcmBoundaryWriter>();
        try
        {
            while (!_cancellation.IsCancellationRequested || !_queue.IsEmpty)
            {
                await _signal.WaitAsync(_cancellation.Token).ConfigureAwait(false);
                while (_queue.TryDequeue(out var block))
                {
                    Interlocked.Decrement(ref _queued);
                    if (!writers.TryGetValue(block.Boundary, out var writer))
                    {
                        writer = new RawPcmBoundaryWriter(_directory, block.Boundary, block.Format);
                        writers.Add(block.Boundary, writer);
                    }
                    writer.Write(block);
                }
            }
        }
        catch (OperationCanceledException) when (_cancellation.IsCancellationRequested)
        {
            while (_queue.TryDequeue(out var block))
            {
                Interlocked.Decrement(ref _queued);
                if (!writers.TryGetValue(block.Boundary, out var writer))
                {
                    writer = new RawPcmBoundaryWriter(_directory, block.Boundary, block.Format);
                    writers.Add(block.Boundary, writer);
                }
                writer.Write(block);
            }
        }
        finally
        {
            foreach (var writer in writers.Values)
                writer.Dispose();
        }
    }
}

internal sealed class AudioBoundaryAccounting
{
    private readonly string _name;
    private long _generated;
    private long _offered;
    private long _accepted;
    private long _read;
    private long _submitted;
    private long _dropped;
    private long _zeroReads;
    private long _partialReads;
    private long _maxQueued;
    private long _overflows;
    private long _underflows;
    private long _invalidReturnedCounts;
    private long _firstSequence = -1;
    private long _lastSequence = -1;
    private int _sampleRate;
    private int _channels;

    internal AudioBoundaryAccounting(string name) => _name = name;

    internal void SetFormat(int sampleRate, int channels)
    {
        Volatile.Write(ref _sampleRate, sampleRate);
        Volatile.Write(ref _channels, channels);
    }

    internal void ObserveRead(int requestedFrames, int returnedFrames, long sequence)
    {
        AddSequence(sequence);
        if (returnedFrames < 0 || returnedFrames > requestedFrames)
            Interlocked.Increment(ref _invalidReturnedCounts);
        if (returnedFrames == 0)
            Interlocked.Increment(ref _zeroReads);
        if (returnedFrames > 0 && returnedFrames < requestedFrames)
            Interlocked.Increment(ref _partialReads);
        Interlocked.Add(ref _offered, requestedFrames);
        Interlocked.Add(ref _read, Math.Max(0, returnedFrames));
        Interlocked.Add(ref _accepted, Math.Max(0, returnedFrames));
    }

    internal void ObserveSubmit(int frames, long sequence)
    {
        AddSequence(sequence);
        Interlocked.Add(ref _offered, frames);
        Interlocked.Add(ref _accepted, frames);
        Interlocked.Add(ref _submitted, frames);
    }

    internal void AddDropped(long frames) => Interlocked.Add(ref _dropped, frames);
    internal void AddGenerated(long frames) => Interlocked.Add(ref _generated, frames);
    internal void ObserveQueued(long frames)
    {
        long current;
        do
        {
            current = Interlocked.Read(ref _maxQueued);
            if (frames <= current) return;
        } while (Interlocked.CompareExchange(ref _maxQueued, frames, current) != current);
    }

    internal string CreateSummary() =>
        $"Audio diagnostics {_name}: generated={_generated}, offered={_offered}, accepted={_accepted}, read={_read}, submitted={_submitted}, dropped={_dropped}, zeroReads={_zeroReads}, partialReads={_partialReads}, maxQueued={_maxQueued}, overflows={_overflows}, underflows={_underflows}, invalidReturnedCounts={_invalidReturnedCounts}, sampleRate={_sampleRate}, channels={_channels}, firstSequence={_firstSequence}, lastSequence={_lastSequence}.";

    private void AddSequence(long sequence)
    {
        if (sequence < 0) return;
        if (Interlocked.CompareExchange(ref _firstSequence, sequence, -1) == -1) { }
        Interlocked.Exchange(ref _lastSequence, sequence);
    }
}

internal sealed class RawPcmBoundaryWriter : IDisposable
{
    private readonly FileStream _pcm;
    private readonly StreamWriter _metadata;

    internal RawPcmBoundaryWriter(string directory, EmulationAudioCaptureBoundary boundary, EmulationAudioFormat format)
    {
        var prefix = boundary.ToString();
        _pcm = new FileStream(Path.Combine(directory, prefix + ".s16le.pcm"), FileMode.Create, FileAccess.Write, FileShare.Read);
        _metadata = new StreamWriter(Path.Combine(directory, prefix + ".metadata.txt"), false, Encoding.UTF8);
        _metadata.WriteLine("format=signed 16-bit little-endian interleaved PCM");
        _metadata.WriteLine($"sampleRate={format.SampleRate}");
        _metadata.WriteLine($"channels={format.Channels}");
        _metadata.WriteLine($"bitsPerSample={format.BitsPerSample}");
        _metadata.WriteLine("columns=sequence,startFrame,frames,sampleOffset");
    }

    internal void Write(EmulationAudioCaptureBlock block)
    {
        var bytes = new byte[block.Samples.Length * sizeof(short)];
        Buffer.BlockCopy(block.Samples, 0, bytes, 0, bytes.Length);
        _pcm.Write(bytes, 0, bytes.Length);
        _metadata.WriteLine($"{block.Sequence},{block.StartFrame},{block.Frames},{_pcm.Position - bytes.Length}");
    }

    public void Dispose()
    {
        _metadata.Dispose();
        _pcm.Dispose();
    }
}
