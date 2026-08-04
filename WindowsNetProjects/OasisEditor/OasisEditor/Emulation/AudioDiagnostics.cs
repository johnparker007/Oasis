using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;

namespace OasisEditor;

internal readonly record struct AudioDiagnosticSessionInfo(string BackendKind, string MachineIdentifier, string RuntimePath, string ProviderPath, EmulationAudioFormat Format, int NAudioBufferMilliseconds)
{
    internal static AudioDiagnosticSessionInfo Unknown { get; } = new(string.Empty, string.Empty, string.Empty, string.Empty, default, 0);
}

internal readonly record struct AudioSinkTimelineEntry(int IncomingFrames, int BufferedBytesBefore, int BufferedBytesAfter, int BufferCapacityBytes, bool PlaybackStarted, bool DroppedBlock, long AccumulatedDroppedBytes, long AdvanceLatenessTicks, bool ZeroFrameRead);

internal enum EmulationAudioCaptureBoundary { FabricManagedRead, FabricBackendSubmit, NAudioAccepted }

internal readonly record struct EmulationAudioCaptureBlock(EmulationAudioCaptureBoundary Boundary, EmulationAudioFormat Format, long Sequence, long StartFrame, short[] Samples, int Frames);

internal sealed class EmulationAudioDiagnostics : IDisposable
{
    private const int DefaultQueueCapacity = 256;
    private readonly ConcurrentQueue<EmulationAudioCaptureBlock> _queue = new();
    private readonly SemaphoreSlim _signal = new(0);
    private readonly CancellationTokenSource _cancellation = new();
    private readonly Task _writer;
    private readonly string _summaryPath;
    private readonly string _timelinePath;
    private readonly string _dropPath;
    private readonly AudioDiagnosticSessionInfo _sessionInfo;
    private readonly DateTimeOffset _enabledAt = DateTimeOffset.Now;
    private readonly int _queueCapacity;
    private readonly int _captureDurationSeconds;
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private readonly object _timelineGate = new();
    private DateTimeOffset? _stoppedAt;
    private long _lastTimelineTicks;
    private bool _shutdownCompleted;
    private long _queued;
    private long _captureDrops;
    private bool _disposed;

    private EmulationAudioDiagnostics(string directory, int queueCapacity, int captureDurationSeconds, AudioDiagnosticSessionInfo sessionInfo)
    {
        SessionDirectory = directory;
        _queueCapacity = queueCapacity;
        _captureDurationSeconds = captureDurationSeconds;
        _sessionInfo = sessionInfo;
        _summaryPath = Path.Combine(directory, "session-summary.txt");
        _timelinePath = Path.Combine(directory, "buffer-timeline.csv");
        _dropPath = Path.Combine(directory, "sink-drops.csv");
        Directory.CreateDirectory(directory);
        var probe = Path.Combine(directory, "diagnostic-write-probe.tmp");
        File.WriteAllText(probe, "probe");
        File.Delete(probe);
        File.WriteAllText(_dropPath, "sequence,startFrame,frames,bytes,reason\n");
        File.WriteAllText(_timelinePath, "elapsedMilliseconds,incomingFrames,bufferedBytesBefore,bufferedBytesAfter,bufferCapacityBytes,playbackStarted,droppedBlock,accumulatedDroppedBytes,advanceLatenessTicks,zeroFrameRead\n");
        _writer = Task.Run(WriteLoopAsync);
        WriteSummaryFile(false);
    }

    internal AudioBoundaryAccounting FabricManagedRead { get; } = new("Fabric managed read");
    internal AudioBoundaryAccounting FabricBackendSubmit { get; } = new("Fabric backend submit");
    internal AudioBoundaryAccounting NAudioAccepted { get; } = new("NAudio accepted");
    internal long CaptureQueueDrops => Interlocked.Read(ref _captureDrops);
    internal string SessionDirectory { get; }
    internal int QueueCapacity => _queueCapacity;

    internal static EmulationAudioDiagnostics? CreateFromEnvironment()
    {
        var directory = Environment.GetEnvironmentVariable("OASIS_AUDIO_DIAGNOSTIC_CAPTURE_DIR");
        if (string.IsNullOrWhiteSpace(directory)) return null;
        var capacityText = Environment.GetEnvironmentVariable("OASIS_AUDIO_DIAGNOSTIC_QUEUE_BLOCKS");
        var capacity = int.TryParse(capacityText, out var parsed) && parsed > 0 ? parsed : DefaultQueueCapacity;
        return Start(new(true, directory, capacity, NativeEmulationPreferences.DefaultAudioDiagnosticCaptureDurationSeconds), AudioDiagnosticSessionInfo.Unknown);
    }

    internal static AmberFabricAudioDiagnosticSettings ApplyEnvironmentOverrides(AmberFabricAudioDiagnosticSettings settings)
    {
        var directory = Environment.GetEnvironmentVariable("OASIS_AUDIO_DIAGNOSTIC_CAPTURE_DIR");
        if (string.IsNullOrWhiteSpace(directory)) return settings;
        var capacityText = Environment.GetEnvironmentVariable("OASIS_AUDIO_DIAGNOSTIC_QUEUE_BLOCKS");
        var capacity = int.TryParse(capacityText, out var parsed) && parsed > 0 ? parsed : settings.QueueBlockCapacity;
        return settings with { Enabled = true, CaptureDirectory = directory, QueueBlockCapacity = capacity };
    }

    internal static EmulationAudioDiagnostics Start(AmberFabricAudioDiagnosticSettings settings, AudioDiagnosticSessionInfo sessionInfo)
    {
        if (!settings.Enabled) throw new InvalidOperationException("Audio diagnostics are disabled.");
        if (string.IsNullOrWhiteSpace(settings.CaptureDirectory)) throw new InvalidOperationException("Choose an Amber/Fabric audio diagnostics capture directory in Preferences.");
        var session = DateTime.Now.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture) + "_" + Guid.NewGuid().ToString("N")[..4];
        return new EmulationAudioDiagnostics(Path.Combine(settings.CaptureDirectory, session), Math.Clamp(settings.QueueBlockCapacity, 16, 8192), Math.Clamp(settings.CaptureDurationSeconds, 1, 600), sessionInfo);
    }

    internal void Capture(EmulationAudioCaptureBoundary boundary, EmulationAudioFormat format, long sequence, long startFrame, ReadOnlySpan<short> samples, int frames)
    {
        if (_disposed || frames <= 0 || _stopwatch.Elapsed.TotalSeconds > _captureDurationSeconds) return;
        if (Interlocked.Read(ref _queued) >= _queueCapacity)
        {
            Interlocked.Increment(ref _captureDrops);
            return;
        }
        Interlocked.Increment(ref _queued);
        _queue.Enqueue(new(boundary, format, sequence, startFrame, samples.ToArray(), frames));
        _signal.Release();
    }

    internal void ObserveSinkPush(EmulationAudioPushResult result, int offeredFrames, FabricAudioFormat format)
    {
        var bytesPerFrame = checked((int)format.ChannelCount * sizeof(short));
        NAudioAccepted.ObserveSink(offeredFrames, result.AcceptedBytes / bytesPerFrame, result.DroppedBytes / bytesPerFrame);
    }

    internal void RecordSinkDrop(EmulationAudioPushContext context, int droppedBytes, string reason)
    {
        lock (_timelineGate)
            File.AppendAllText(_dropPath, $"{context.Sequence},{context.StartFrame},{context.Frames},{droppedBytes},{reason.Replace(",", ";")}\n");
    }

    internal void RecordTimeline(AudioSinkTimelineEntry entry)
    {
        if (_stopwatch.Elapsed.TotalSeconds > _captureDurationSeconds) return;
        var nowTicks = _stopwatch.ElapsedTicks;
        var previous = Interlocked.Read(ref _lastTimelineTicks);
        var minimumTicks = Stopwatch.Frequency / 100;
        if (previous != 0 && nowTicks - previous < minimumTicks && !entry.DroppedBlock && !entry.ZeroFrameRead) return;
        Interlocked.Exchange(ref _lastTimelineTicks, nowTicks);
        lock (_timelineGate)
        {
            File.AppendAllText(_timelinePath, string.Format(CultureInfo.InvariantCulture, "{0:F3},{1},{2},{3},{4},{5},{6},{7},{8},{9}\n", _stopwatch.Elapsed.TotalMilliseconds, entry.IncomingFrames, entry.BufferedBytesBefore, entry.BufferedBytesAfter, entry.BufferCapacityBytes, entry.PlaybackStarted, entry.DroppedBlock, entry.AccumulatedDroppedBytes, entry.AdvanceLatenessTicks, entry.ZeroFrameRead));
        }
    }

    internal void Complete(bool shutdownCompleted)
    {
        _shutdownCompleted = shutdownCompleted;
        _stoppedAt = DateTimeOffset.Now;
        WriteSummaryFile(true);
    }

    internal void WriteSummary(Action<string> logger)
    {
        logger(FabricManagedRead.CreateSummary());
        logger(FabricBackendSubmit.CreateSummary());
        logger(NAudioAccepted.CreateSummary());
        logger($"Audio diagnostics capture: queueDrops={CaptureQueueDrops}, queuedBlocks={Interlocked.Read(ref _queued)}, directory='{SessionDirectory}', summary='{_summaryPath}'.");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _cancellation.Cancel();
        _signal.Release();
        try { _writer.Wait(TimeSpan.FromSeconds(5)); } catch { }
        WriteSummaryFile(true);
        _signal.Dispose();
        _cancellation.Dispose();
    }

    private async Task WriteLoopAsync()
    {
        var writers = new Dictionary<EmulationAudioCaptureBoundary, WavBoundaryWriter>();
        try
        {
            while (!_cancellation.IsCancellationRequested || !_queue.IsEmpty)
            {
                await _signal.WaitAsync(_cancellation.Token).ConfigureAwait(false);
                Drain(writers);
            }
        }
        catch (OperationCanceledException) when (_cancellation.IsCancellationRequested) { Drain(writers); }
        finally { foreach (var writer in writers.Values) writer.Dispose(); }
    }

    private void Drain(Dictionary<EmulationAudioCaptureBoundary, WavBoundaryWriter> writers)
    {
        while (_queue.TryDequeue(out var block))
        {
            Interlocked.Decrement(ref _queued);
            if (!writers.TryGetValue(block.Boundary, out var writer))
            {
                writer = new WavBoundaryWriter(SessionDirectory, block.Boundary, block.Format);
                writers.Add(block.Boundary, writer);
            }
            writer.Write(block);
        }
    }

    private void WriteSummaryFile(bool final)
    {
        File.WriteAllLines(_summaryPath, [
            $"captureEnabledTime={_enabledAt:O}",
            $"stopTime={(_stoppedAt?.ToString("O") ?? string.Empty)}",
            $"backend={_sessionInfo.BackendKind}",
            $"machine={_sessionInfo.MachineIdentifier}",
            $"runtimeDllPath={_sessionInfo.RuntimePath}",
            $"providerDllPath={_sessionInfo.ProviderPath}",
            $"audioFormat={_sessionInfo.Format.SampleRate} Hz, {_sessionInfo.Format.Channels} channels, {_sessionInfo.Format.BitsPerSample} bits",
            $"nAudioBufferMilliseconds={_sessionInfo.NAudioBufferMilliseconds}",
            "capturedBoundaries=FabricManagedRead,FabricBackendSubmit,NAudioAccepted",
            FabricManagedRead.CreateSummary(),
            FabricBackendSubmit.CreateSummary(),
            NAudioAccepted.CreateSummary(),
            $"captureQueueDrops={CaptureQueueDrops}",
            $"captureDirectory={SessionDirectory}",
            $"timelinePath={_timelinePath}",
            $"sinkDropsPath={_dropPath}",
            $"shutdownCompletedNormally={_shutdownCompleted}",
            $"summaryFinal={final}"
        ]);
    }
}

internal sealed class AudioBoundaryAccounting
{
    private readonly string _name;
    private long _generated, _offered, _accepted, _read, _submitted, _dropped, _zeroReads, _partialReads, _maxQueued, _overflows, _underflows, _invalidReturnedCounts;
    private long _firstSequence = -1, _lastSequence = -1;
    private int _sampleRate, _channels;
    internal AudioBoundaryAccounting(string name) => _name = name;
    internal void SetFormat(int sampleRate, int channels) { Volatile.Write(ref _sampleRate, sampleRate); Volatile.Write(ref _channels, channels); }
    internal void ObserveRead(int requestedFrames, int returnedFrames, long sequence)
    {
        AddSequence(sequence);
        if (returnedFrames < 0 || returnedFrames > requestedFrames) Interlocked.Increment(ref _invalidReturnedCounts);
        if (returnedFrames == 0) Interlocked.Increment(ref _zeroReads);
        if (returnedFrames > 0 && returnedFrames < requestedFrames) Interlocked.Increment(ref _partialReads);
        Interlocked.Add(ref _offered, requestedFrames);
        Interlocked.Add(ref _read, Math.Max(0, returnedFrames));
        Interlocked.Add(ref _accepted, Math.Max(0, returnedFrames));
    }
    internal void ObserveSubmit(int frames, long sequence) { AddSequence(sequence); Interlocked.Add(ref _offered, frames); }
    internal void ObserveSink(long offeredFrames, long acceptedFrames, long droppedFrames) { Interlocked.Add(ref _offered, offeredFrames); Interlocked.Add(ref _accepted, acceptedFrames); Interlocked.Add(ref _submitted, acceptedFrames); Interlocked.Add(ref _dropped, droppedFrames); }
    internal string CreateSummary() => $"Audio diagnostics {_name}: generated={_generated}, offered={_offered}, accepted={_accepted}, read={_read}, submitted={_submitted}, dropped={_dropped}, zeroReads={_zeroReads}, partialReads={_partialReads}, maxQueued={_maxQueued}, overflows={_overflows}, underflows={_underflows}, invalidReturnedCounts={_invalidReturnedCounts}, sampleRate={_sampleRate}, channels={_channels}, firstSequence={_firstSequence}, lastSequence={_lastSequence}.";
    private void AddSequence(long sequence) { if (sequence < 0) return; _ = Interlocked.CompareExchange(ref _firstSequence, sequence, -1); Interlocked.Exchange(ref _lastSequence, sequence); }
}

internal sealed class WavBoundaryWriter : IDisposable
{
    private readonly FileStream _wav;
    private readonly StreamWriter _metadata;
    private readonly EmulationAudioFormat _format;
    private long _dataBytes, _frames;
    internal WavBoundaryWriter(string directory, EmulationAudioCaptureBoundary boundary, EmulationAudioFormat format)
    {
        _format = format;
        var prefix = boundary.ToString();
        _wav = new FileStream(Path.Combine(directory, prefix + ".wav"), FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        _metadata = new StreamWriter(Path.Combine(directory, prefix + ".metadata.txt"), false, Encoding.UTF8);
        _metadata.WriteLine("format=signed 16-bit little-endian interleaved PCM WAV");
        _metadata.WriteLine($"sampleRate={format.SampleRate}");
        _metadata.WriteLine($"channels={format.Channels}");
        _metadata.WriteLine($"bitsPerSample={format.BitsPerSample}");
        _metadata.WriteLine("columns=sequence,startFrame,frames,byteOffset");
        WriteWavHeader(0);
    }
    internal void Write(EmulationAudioCaptureBlock block)
    {
        var bytes = new byte[block.Samples.Length * sizeof(short)];
        Buffer.BlockCopy(block.Samples, 0, bytes, 0, bytes.Length);
        _metadata.WriteLine($"{block.Sequence},{block.StartFrame},{block.Frames},{_dataBytes}");
        _wav.Write(bytes, 0, bytes.Length);
        _dataBytes += bytes.Length;
        _frames += block.Frames;
    }
    public void Dispose()
    {
        _metadata.WriteLine($"totalFrames={_frames}");
        _metadata.Dispose();
        _wav.Position = 0;
        WriteWavHeader(_dataBytes);
        _wav.Dispose();
    }
    private void WriteWavHeader(long dataBytes)
    {
        Span<byte> header = stackalloc byte[44];
        WriteAscii(header[..4], "RIFF");
        BitConverter.GetBytes((uint)Math.Min(uint.MaxValue, 36 + dataBytes)).CopyTo(header[4..]);
        WriteAscii(header[8..12], "WAVE");
        WriteAscii(header[12..16], "fmt ");
        BitConverter.GetBytes(16u).CopyTo(header[16..]);
        BitConverter.GetBytes((ushort)1).CopyTo(header[20..]);
        BitConverter.GetBytes((ushort)_format.Channels).CopyTo(header[22..]);
        BitConverter.GetBytes((uint)_format.SampleRate).CopyTo(header[24..]);
        var blockAlign = (ushort)(_format.Channels * (_format.BitsPerSample / 8));
        BitConverter.GetBytes((uint)(_format.SampleRate * blockAlign)).CopyTo(header[28..]);
        BitConverter.GetBytes(blockAlign).CopyTo(header[32..]);
        BitConverter.GetBytes((ushort)_format.BitsPerSample).CopyTo(header[34..]);
        WriteAscii(header[36..40], "data");
        BitConverter.GetBytes((uint)Math.Min(uint.MaxValue, dataBytes)).CopyTo(header[40..]);
        _wav.Write(header);
    }
    private static void WriteAscii(Span<byte> destination, string text) { for (var i = 0; i < text.Length; i++) destination[i] = (byte)text[i]; }
}
