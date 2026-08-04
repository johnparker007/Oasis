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
        Assert.Equal(2400, AudioPrebufferPolicy.CalculateCapacityFrames(format, 50));
        Assert.Equal(38, AudioPrebufferPolicy.CalculateThresholdMilliseconds(50));
        var policy = new AudioPrebufferPolicy(format, 50);
        Assert.False(policy.ObserveQueuedFrames(1823));
        Assert.True(policy.ObserveQueuedFrames(1824));
    }
}
