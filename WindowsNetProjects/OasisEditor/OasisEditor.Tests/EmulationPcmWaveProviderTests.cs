using Xunit;

namespace OasisEditor.Tests;

public sealed class EmulationPcmWaveProviderTests
{
    [Fact] public void DeliversExactStereoBytesAndFullCount() { var ring = new PcmFrameRingBuffer(2, 4); ring.Write([0x0102, unchecked((short)0xF304)],1); var p=new EmulationPcmWaveProvider(ring,new(48000,2,16),4); var bytes=new byte[4]; Assert.Equal(4,p.Read(bytes,0,4)); Assert.Equal([0x02,0x01,0x04,0xF3], bytes); Assert.Equal(1,p.FramesDelivered); }
    [Fact] public void PartialUnderrunFillsSilenceAndCountsEpisode() { var ring = new PcmFrameRingBuffer(1, 4); ring.Write([123],1); var p=new EmulationPcmWaveProvider(ring,new(48000,1,16),4); var bytes=Enumerable.Repeat((byte)0xFF,6).ToArray(); p.Read(bytes,0,6); Assert.Equal([123,0,0,0,0,0], bytes); Assert.Equal(2,p.SilenceFrames); Assert.Equal(1,p.UnderrunEpisodes); }
    [Fact] public void CompleteUnderrunAndRecoveryDoesNotRepeatStaleSamples() { var ring = new PcmFrameRingBuffer(1, 4); var p=new EmulationPcmWaveProvider(ring,new(48000,1,16),4); var bytes=Enumerable.Repeat((byte)0x7F,4).ToArray(); p.Read(bytes,0,4); Assert.Equal([0,0,0,0], bytes); ring.Write([44,55],2); p.Read(bytes,0,4); Assert.Equal([44,0,55,0], bytes); Assert.Equal(1,p.UnderrunEpisodes); }
    [Fact] public void NonFrameAlignedRequestClearsTrailingBytes() { var ring = new PcmFrameRingBuffer(2, 4); ring.Write([1,2],1); var p=new EmulationPcmWaveProvider(ring,new(48000,2,16),4); var bytes=Enumerable.Repeat((byte)0xFF,5).ToArray(); Assert.Equal(5,p.Read(bytes,0,5)); Assert.Equal([1,0,2,0,0], bytes); }
}
