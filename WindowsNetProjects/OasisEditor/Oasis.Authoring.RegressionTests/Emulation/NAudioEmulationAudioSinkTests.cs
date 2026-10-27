using Xunit;

namespace OasisEditor.Tests;

public sealed class NAudioEmulationAudioSinkTests
{
    [Theory]
    [InlineData(50, 38)]
    [InlineData(100, 75)]
    [InlineData(10, 8)]
    [InlineData(1, 1)]
    public void PrebufferThreshold_IsSeventyFivePercentOfBuffer(int buffer, int expected)
    {
        Assert.Equal(expected, AudioPrebufferPolicy.CalculateThresholdMilliseconds(buffer));
    }

    [Fact]
    public void Prebuffer_StartsAtThresholdAndResetRequiresPrebufferAgain()
    {
        var policy = new AudioPrebufferPolicy(new(48000, 2, 16), 50);

        Assert.Equal(1824, policy.ThresholdFrames);
        Assert.False(policy.ObserveQueuedFrames(1823));
        Assert.True(policy.ObserveQueuedFrames(1824));
        policy.Reset();
        Assert.False(policy.PlaybackStarted);
        Assert.False(policy.ObserveQueuedFrames(48));
    }

    [Fact]
    public void WasapiLatency_IsIndependentFromRingReserveCapacity()
    {
        using var sink = new NAudioEmulationAudioSink(bufferLengthMilliseconds: 100, wasapiLatencyMilliseconds: 25);

        Assert.Equal(100, sink.BufferLengthMilliseconds);
        Assert.Equal(25, sink.WasapiLatencyMilliseconds);
    }
}
