using Xunit;

namespace OasisEditor.Tests;

public sealed class NAudioEmulationAudioSinkTests
{
    [Fact]
    public void ReservePolicy_ForFiftyMillisecondsTargetsUsefulStartupDepth()
    {
        var policy = EmulationAudioReservePolicy.Create(new(48000, 2, 16), 50);
        Assert.Equal(2400, policy.CapacityFrames);
        Assert.Equal(1800, policy.TargetFrames);
        Assert.Equal(900, policy.LowWaterFrames);
        Assert.Equal(2160, policy.HighWaterFrames);
        Assert.Equal(240, policy.FeedBlockFrames);
    }

    [Fact]
    public void ReservePolicy_TargetNeverExceedsSafeCapacity()
    {
        var policy = EmulationAudioReservePolicy.Create(new(48000, 2, 16), 25, 100);
        Assert.True(policy.TargetFrames < policy.CapacityFrames);
        Assert.True(policy.LowWaterFrames < policy.TargetFrames);
        Assert.True(policy.HighWaterFrames >= policy.TargetFrames);
    }

    [Fact]
    public void Fifo_PreservesStereoOrderingAcrossWrapAndPartialReads()
    {
        var fifo = new EmulationPcmFrameFifo(4, 2);
        short[] first = [1, 10, 2, 20, 3, 30];
        Assert.Equal(3, fifo.Write(first).AcceptedFrames);
        short[] read = new short[4];
        Assert.Equal(2, fifo.Read(read, 2));
        Assert.Equal(new short[] { 1, 10, 2, 20 }, read);
        short[] second = [4, 40, 5, 50, 6, 60];
        var write = fifo.Write(second);
        Assert.Equal(3, write.OfferedFrames);
        Assert.Equal(3, write.AcceptedFrames);
        short[] rest = new short[8];
        Assert.Equal(4, fifo.Read(rest, 4));
        Assert.Equal(new short[] { 3, 30, 4, 40, 5, 50, 6, 60 }, rest);
    }

    [Fact]
    public void Fifo_RejectsOverflowInCompleteFramesAndClearRemovesStaleAudio()
    {
        var fifo = new EmulationPcmFrameFifo(2, 2);
        var result = fifo.Write(new short[] { 1, 10, 2, 20, 3, 30 });
        Assert.Equal(3, result.OfferedFrames);
        Assert.Equal(2, result.AcceptedFrames);
        Assert.Equal(1, result.RejectedFrames);
        fifo.Clear();
        short[] read = new short[4];
        Assert.Equal(0, fifo.Read(read, 2));
    }
}
