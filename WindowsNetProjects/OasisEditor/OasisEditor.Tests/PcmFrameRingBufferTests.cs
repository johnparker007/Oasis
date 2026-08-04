using Xunit;

namespace OasisEditor.Tests;

public sealed class PcmFrameRingBufferTests
{
    [Fact] public void MonoOrdering() { var b = new PcmFrameRingBuffer(1, 4); Assert.Equal(3, b.Write([1,2,3], 3)); short[] r = [0,0,0]; Assert.Equal(3, b.Read(r, 3)); Assert.Equal([1,2,3], r); }
    [Fact] public void StereoOrdering() { var b = new PcmFrameRingBuffer(2, 3); Assert.Equal(2, b.Write([10,11,20,21], 2)); short[] r = [0,0,0,0]; Assert.Equal(2, b.Read(r, 2)); Assert.Equal([10,11,20,21], r); }
    [Fact] public void WraparoundPreservesOrder() { var b = new PcmFrameRingBuffer(1, 3); b.Write([1,2,3], 3); short[] a=[0,0]; b.Read(a,2); Assert.Equal(2, b.Write([4,5], 2)); short[] r=[0,0,0]; Assert.Equal(3,b.Read(r,3)); Assert.Equal([3,4,5], r); }
    [Fact] public void PartialWritesAndFullRejectionDoNotOverwrite() { var b = new PcmFrameRingBuffer(1, 2); Assert.Equal(2,b.Write([1,2,3],3)); Assert.Equal(0,b.Write([9],1)); short[] r=[0,0]; b.Read(r,2); Assert.Equal([1,2], r); }
    [Fact] public void PartialReadsLeaveRemainder() { var b = new PcmFrameRingBuffer(1, 3); b.Write([1,2,3],3); short[] r=[0,0]; Assert.Equal(2,b.Read(r,2)); Assert.Equal([1,2],r); short[] r2=[0]; b.Read(r2,1); Assert.Equal([3],r2); }
    [Fact] public void ClearRemovesStaleData() { var b = new PcmFrameRingBuffer(2, 2); b.Write([1,2,3,4],2); b.Clear(); Assert.Equal(0,b.ReadableFrames); short[] r=[7,7]; Assert.Equal(0,b.Read(r,1)); Assert.Equal([7,7], r); }
    [Fact] public void CompleteFrameAlignmentIsRequired() { var b = new PcmFrameRingBuffer(2, 2); Assert.Throws<ArgumentException>(() => b.Write([1],1)); Assert.Throws<ArgumentException>(() => b.Read(new short[1],1)); }
    [Fact] public void StressPreservesAcceptedFrames() { var b = new PcmFrameRingBuffer(1, 8); var expected = new List<short>(); for(short i=0;i<100;i++){ if(b.Write([i],1)==1) expected.Add(i); if(i%3==0){ short[] t=[0]; if(b.Read(t,1)==1) Assert.Equal(expected[0], t[0]); if(expected.Count>0) expected.RemoveAt(0); }} foreach(var e in expected){ short[] t=[0]; Assert.Equal(1,b.Read(t,1)); Assert.Equal(e,t[0]); }}
}
