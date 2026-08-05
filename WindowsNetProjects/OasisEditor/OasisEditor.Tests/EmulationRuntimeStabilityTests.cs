using System.Runtime.InteropServices;
using NAudio.Wave;
using Xunit;

namespace OasisEditor.Tests;

public sealed class EmulationRuntimeStabilityTests
{
    [Fact]
    public void Coalescer_CollapsesRepeatedLampAndSchedulesOneCallback()
    {
        var scheduled = new Queue<Action>();
        MachineOutputBatch? applied = null;
        var coalescer = new CoalescedMachineOutputDispatcher(a => scheduled.Enqueue(a), b => applied = b);
        for (var i = 0; i < 1000; i++) coalescer.EnqueueLamp(7, i);
        Assert.Single(scheduled);
        Assert.True(coalescer.DispatchPending);
        Assert.Equal(1, coalescer.PendingEntryCount);
        scheduled.Dequeue()();
        Assert.NotNull(applied);
        Assert.Equal(new LampValue(7, 999), applied!.Lamps.Single());
    }

    [Fact]
    public void Coalescer_BatchesTypesAndReschedulesOnceForConcurrentArrivals()
    {
        var scheduled = new Queue<Action>();
        var batches = new List<MachineOutputBatch>();
        CoalescedMachineOutputDispatcher? coalescer = null;
        coalescer = new(a => scheduled.Enqueue(a), b => { batches.Add(b); coalescer!.EnqueueLamp(1, 2); });
        coalescer.EnqueueLamp(1, 1);
        coalescer.EnqueueReel(2, 3);
        coalescer.EnqueueSegment(4, 5, SegmentOutputType.Digit);
        coalescer.EnqueueVfdBrightness(6, .7);
        Assert.Single(scheduled);
        scheduled.Dequeue()();
        Assert.Single(scheduled);
        Assert.Equal(4, batches[0].Lamps.Length + batches[0].Reels.Length + batches[0].Segments.Length + batches[0].VfdBrightness.Length);
    }

    [Fact]
    public void RingBuffer_PreservesStereoOrderingAcrossWrapAndPartialOperations()
    {
        var ring = new PcmFrameRingBuffer(2, 3);
        Assert.Equal(2, ring.Write(new short[] { 1, 10, 2, 20 }, 2));
        var read = new short[2];
        Assert.Equal(1, ring.Read(read, 1));
        Assert.Equal(new short[] { 1, 10 }, read);
        Assert.Equal(2, ring.Write(new short[] { 3, 30, 4, 40, 5, 50 }, 3));
        var all = new short[6];
        Assert.Equal(3, ring.Read(all, 3));
        Assert.Equal(new short[] { 2, 20, 3, 30, 4, 40 }, all);
    }

    [Fact]
    public void RingBuffer_DoesNotOverwriteAndClearResetsDepth()
    {
        var ring = new PcmFrameRingBuffer(1, 2);
        Assert.Equal(2, ring.Write(new short[] { 1, 2, 3 }, 3));
        Assert.Equal(0, ring.WritableFrames);
        ring.Clear();
        Assert.Equal(0, ring.ReadableFrames);
        Assert.Equal(2, ring.WritableFrames);
    }

    [Fact]
    public async Task RingBuffer_ConcurrentProducerConsumerStressIsSafe()
    {
        var ring = new PcmFrameRingBuffer(1, 64);
        var produced = 0;
        var consumed = 0;
        var producer = Task.Run(() => { var sample = new short[1]; for (var i = 0; i < 20000; i++) { sample[0] = (short)i; if (ring.Write(sample, 1) == 1) Interlocked.Increment(ref produced); } });
        var consumer = Task.Run(() => { var sample = new short[1]; while (!producer.IsCompleted || ring.ReadableFrames > 0) if (ring.Read(sample, 1) == 1) Interlocked.Increment(ref consumed); });
        await Task.WhenAll(producer, consumer);
        Assert.Equal(produced, consumed);
    }

    [Fact]
    public void WaveProvider_BulkCopiesPcmAndFillsPartialSilence()
    {
        var ring = new PcmFrameRingBuffer(2, 8);
        ring.Write(new short[] { 1, 2, 3, 4 }, 2);
        var provider = new PcmRingWaveProvider(ring, new WaveFormat(48000, 16, 2));
        var bytes = new byte[12];
        Assert.Equal(12, provider.Read(bytes, 0, bytes.Length));
        var samples = MemoryMarshal.Cast<byte, short>(bytes).ToArray();
        Assert.Equal(new short[] { 1, 2, 3, 4, 0, 0 }, samples);
        Assert.Equal(1, provider.UnderrunEpisodes);
    }

    [Fact]
    public void WaveProvider_UnderrunEpisodesAreDistinctAndRecover()
    {
        var ring = new PcmFrameRingBuffer(1, 8);
        var provider = new PcmRingWaveProvider(ring, new WaveFormat(48000, 16, 1));
        var bytes = new byte[4];
        provider.Read(bytes, 0, bytes.Length);
        provider.Read(bytes, 0, bytes.Length);
        ring.Write(new short[] { 9, 10 }, 2);
        provider.Read(bytes, 0, bytes.Length);
        provider.Read(bytes, 0, bytes.Length);
        Assert.Equal(2, provider.UnderrunEpisodes);
    }

    [Fact]
    public void PrebufferPolicy_UsesSeventyFivePercentCapacity()
    {
        var format = new EmulationAudioFormat(48000, 2, 16);
        Assert.Equal(4800, AudioPrebufferPolicy.CalculateCapacityFrames(format, 100));
        Assert.Equal(75, AudioPrebufferPolicy.CalculateThresholdMilliseconds(100));
        Assert.Equal(2400, AudioPrebufferPolicy.CalculateCapacityFrames(format, 50));
        Assert.Equal(38, AudioPrebufferPolicy.CalculateThresholdMilliseconds(50));
        var policy = new AudioPrebufferPolicy(format, 50);
        Assert.False(policy.ObserveQueuedFrames(1823));
        Assert.True(policy.ObserveQueuedFrames(1824));
    }

    [Fact]
    public void WaveProvider_StatisticsSeparatePcmAndSilenceFrames()
    {
        var ring = new PcmFrameRingBuffer(1, 8);
        ring.Write(new short[] { 1, 2 }, 2);
        var provider = new PcmRingWaveProvider(ring, new WaveFormat(48000, 16, 1));
        provider.Read(new byte[8], 0, 8);
        Assert.Equal(2, provider.FramesDelivered);
        Assert.Equal(2, provider.SilenceFrames);
        Assert.Equal(1, provider.UnderrunEpisodes);
    }

    [Fact]
    public void WaveProvider_NoUnderrunReportsZeroSilenceAndZeroEpisodes()
    {
        var ring = new PcmFrameRingBuffer(1, 8);
        ring.Write(new short[] { 1, 2, 3, 4 }, 4);
        var provider = new PcmRingWaveProvider(ring, new WaveFormat(48000, 16, 1));
        provider.Read(new byte[8], 0, 8);
        Assert.Equal(4, provider.FramesDelivered);
        Assert.Equal(0, provider.SilenceFrames);
        Assert.Equal(0, provider.UnderrunEpisodes);
    }

    [Fact]
    public void WaveProvider_MinimumDepthIsMeasuredWhenDeviceReadsAfterPlaybackStarts()
    {
        var ring = new PcmFrameRingBuffer(1, 8);
        ring.Write(new short[] { 1, 2, 3, 4 }, 4);
        var provider = new PcmRingWaveProvider(ring, new WaveFormat(48000, 16, 1));
        provider.Read(new byte[4], 0, 4);
        provider.Read(new byte[4], 0, 4);
        Assert.Equal(2, provider.MinimumRingFrames);
    }



    [Fact]
    public void CapacityAwareSliceLimit_RunsOnlySlicesThatFitAndRetainsDebtByCalculation()
    {
        var available = FabricEmulationBackend.CalculateAvailableSlices(now: 39, nextDeadline: 0, pumpTicks: 1);
        var executable = Math.Min(available, FabricEmulationBackend.CalculateAudioCapacityLimitedSlices(48000, 480));
        Assert.Equal(40, available);
        Assert.Equal(10, executable);
        Assert.Equal(30, available - executable);
    }

    [Fact]
    public void CapacityAwareSliceLimit_ZeroWritableFramesExecutesNoAudioSlices()
    {
        Assert.Equal(0, FabricEmulationBackend.CalculateAudioCapacityLimitedSlices(48000, 0));
    }


    [Theory]
    [InlineData(48000, 48)]
    [InlineData(44100, 45)]
    [InlineData(32000, 32)]
    public void FrameCapacity_UsesCeilingSafeFramesPerOneMillisecondSlice(int sampleRate, int expectedFrames)
    {
        Assert.Equal(expectedFrames, FabricEmulationBackend.CalculateSafeFramesPerSlice(sampleRate));
    }

    [Fact]
    public void WaveProvider_UnderrunCountersExcludeInactivePlaybackReads()
    {
        var active = false;
        var ring = new PcmFrameRingBuffer(1, 8);
        var provider = new PcmRingWaveProvider(ring, new WaveFormat(48000, 16, 1), () => active);
        provider.Read(new byte[8], 0, 8);
        Assert.Equal(0, provider.SilenceFrames);
        Assert.Equal(0, provider.UnderrunEpisodes);
        active = true;
        provider.Read(new byte[8], 0, 8);
        Assert.Equal(4, provider.SilenceFrames);
        Assert.Equal(1, provider.UnderrunEpisodes);
        active = false;
        provider.Read(new byte[8], 0, 8);
        Assert.Equal(4, provider.SilenceFrames);
        Assert.Equal(1, provider.UnderrunEpisodes);
    }

    [Fact]
    public void WaveProvider_TracksActualDeviceRequestSizes()
    {
        var ring = new PcmFrameRingBuffer(1, 16);
        var provider = new PcmRingWaveProvider(ring, new WaveFormat(48000, 16, 1));
        provider.Read(new byte[4], 0, 4);
        provider.Read(new byte[10], 0, 10);
        Assert.Equal(2, provider.MinimumRequestedFrames);
        Assert.Equal(5, provider.MaximumRequestedFrames);
        Assert.Equal(7, provider.TotalRequestedFrames);
    }


    [Fact]
    public void StopSummary_ContainsEveryRequiredField()
    {
        var statistics = new EmulationAudioPlaybackStatistics(10, 2, 8, 3, 1, 0, 4, 2400, 1800, 38, 1200, 1800, 96, 192, 10000, 25, true);
        var summary = FabricEmulationBackend.FormatStopSummary(30012.4, 30005, 0.99975, 30005, 42, 6, 0, 1440240, 0, 48, 48, 2, 2, 30, 1440, 123, "Normal", 100, 4, 1.04, 1.5, 1, 0, 0, 0, 0, 0, 2, 0.1, 0.2, 0.3, 0.4, 0.05, 0.01, statistics);
        foreach (var field in new[]
        {
            "wallMs=", "emulatedMs=", "ratio=", "slices=", "catchUpSlices=", "maxCatchUpBatch=", "discardedMs=",
            "fabricAudioFrames=", "ringWritten=", "ringRejected=", "devicePcmFrames=", "silenceFrames=",
            "underrunEpisodes=", "startupRingFrames=", "minimumRingFrames=", "maximumRingFrames=", "currentRingFrames=", "ringCapacityFrames=",
            "writableFramesAtLargestCatchUpBatch=", "capacityLimitedCatchUpBatches=", "retainedDebtIterations=",
            "maxRetainedSlices=", "runnerThreadId=", "runnerThreadPriority=", "singleSliceBatches=", "multiSliceBatches=",
            "averageBatchSize=", "catchUpSlicePercent=", "maxWakeLatenessMs=", "wakeLateOver2Ms=", "wakeLateOver5Ms=",
            "wakeLateOver10Ms=", "wakeLateOver20Ms=", "wakeLateOver50Ms=", "wakeLateOver100Ms=",
            "longestConsecutiveLateWakes=", "maxAdvanceDurationMs=", "maxReadAudioDurationMs=", "maxSnapshotDurationMs=",
            "maxPublishDurationMs=", "maxInputDurationMs=", "maxSessionGateWaitMs=",
            "maxFramesGeneratedByOneSlice=", "minimumRequestedFrames=", "maximumRequestedFrames=",
            "totalRequestedFrames=", "playbackStarted="
        })
        {
            Assert.Contains(field, summary);
        }
    }

    [Fact]
    public void RingRejection_IsReportedIndependentlyInStatisticsShape()
    {
        var statistics = new EmulationAudioPlaybackStatistics(100, 7, 93, 0, 0, 12, 64, 128, 96, 38, 120, 96, 48, 96, 100, 25, true);
        var summary = FabricEmulationBackend.FormatStopSummary(1000, 1000, 1, 1000, 0, 1, 0, 100, 0, 48, 48, 0, 0, 0, 128, 123, "Normal", 1000, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, statistics);
        Assert.Contains("ringRejected=7", summary);
        Assert.Contains("devicePcmFrames=93", summary);
        Assert.Contains("silenceFrames=0", summary);
    }

}
