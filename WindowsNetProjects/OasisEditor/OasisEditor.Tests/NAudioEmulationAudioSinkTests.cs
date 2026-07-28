using Xunit;

namespace OasisEditor.Tests;

public sealed class NAudioEmulationAudioSinkTests
{
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
