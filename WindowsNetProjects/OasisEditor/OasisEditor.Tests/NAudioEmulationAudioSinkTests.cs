using Xunit;

namespace OasisEditor.Tests;

public sealed class NAudioEmulationAudioSinkTests
{
    [Theory]
    [InlineData(50, 10)]
    [InlineData(100, 10)]
    [InlineData(10, 5)]
    [InlineData(1, 1)]
    public void PrebufferThreshold_IsBoundedByTenMillisecondsAndHalfTheBuffer(int buffer, int expected)
    {
        Assert.Equal(expected, AudioPrebufferPolicy.CalculateThresholdMilliseconds(buffer));
    }

    [Fact]
    public void Prebuffer_StartsAtThresholdAndResetRequiresPrebufferAgain()
    {
        var policy = new AudioPrebufferPolicy(new(48000, 2, 16), 50);

        Assert.Equal(1920, policy.ThresholdBytes);
        Assert.False(policy.ObserveQueuedBytes(1919));
        Assert.True(policy.ObserveQueuedBytes(1920));
        policy.Reset();
        Assert.False(policy.PlaybackStarted);
        Assert.False(policy.ObserveQueuedBytes(192));
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
    [InlineData(true, 0, 192, true)]
    [InlineData(true, 191, 192, true)]
    [InlineData(true, 192, 192, false)]
    [InlineData(false, 0, 192, false)]
    public void RuntimeStarvation_RebuffersOnlyWhenPlaybackHasStartedAndDepthFallsBelowOneMillisecond(
        bool playbackStarted, int bufferedBytes, int bytesPerMillisecond, bool expected)
    {
        Assert.Equal(expected,
            NAudioEmulationAudioSink.ShouldRebufferAfterRuntimeStarvation(playbackStarted, bufferedBytes, bytesPerMillisecond));
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
