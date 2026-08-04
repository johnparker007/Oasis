using Xunit;

namespace OasisEditor.Tests;

public sealed class NAudioEmulationAudioSinkTests
{
    [Fact]
    public void PrebufferThreshold_UsesSeventyFivePercentAndStaysBelowCapacity()
    {
        var capacity = AudioPrebufferPolicy.CalculateCapacityFrames(48000, 50);
        Assert.Equal(2400, capacity);
        Assert.Equal(1800, AudioPrebufferPolicy.CalculateThresholdFrames(48000, capacity));
    }

    [Fact]
    public void Prebuffer_StartsAtThresholdOnceAndResetRequiresPrebufferAgain()
    {
        var policy = new AudioPrebufferPolicy(new(48000, 2, 16), 2400);
        Assert.False(policy.ObserveQueuedFrames(1799));
        Assert.True(policy.ObserveQueuedFrames(1800));
        Assert.True(policy.ObserveQueuedFrames(1));
        policy.Reset();
        Assert.False(policy.PlaybackStarted);
        Assert.False(policy.ObserveQueuedFrames(1799));
    }

    [Fact]
    public void SmallPrebufferThreshold_RemainsSafeAndBelowCapacity()
    {
        var capacity = AudioPrebufferPolicy.CalculateCapacityFrames(48000, 10);
        var threshold = AudioPrebufferPolicy.CalculateThresholdFrames(48000, capacity);
        Assert.InRange(threshold, 1, capacity - 1);
    }

    [Theory]
    [InlineData(48000, 2, 192)]
    [InlineData(48000, 1, 96)]
    [InlineData(44100, 2, 176)]
    public void BufferDepthByteRate_AccountsForChannelsExactlyOnce(int rate, int channels, int expected)
    {
        Assert.Equal(expected, new EmulationAudioFormat(rate, channels, 16).BytesPerMillisecond());
    }
    [Theory]
    [InlineData(800, 1000, 192, false)]
    [InlineData(900, 1000, 192, true)]
    [InlineData(1000, 1000, 1, true)]
    public void IncomingBlockIsDroppedRatherThanRequiringBufferedAudioToBeCleared(
        int bufferedBytes, int capacityBytes, int incomingBytes, bool expectedDrop)
    {
        Assert.Equal(expectedDrop,
            NAudioEmulationAudioSink.ShouldDropIncomingBlock(bufferedBytes, capacityBytes, incomingBytes));
    }
}
