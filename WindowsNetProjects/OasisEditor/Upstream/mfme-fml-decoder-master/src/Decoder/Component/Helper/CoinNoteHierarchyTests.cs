using Xunit;
using MfmeFmlDecoder.src.Decoder.Component.Helper;

namespace MfmeFmlDecoder.Tests.Decoder.Component.Helper
{
    public class CoinNoteHierarchyTests
    {

        [Fact]
        public void ResolveCoinNote_0x39_0x04_ReturnsTokMPU4()
        {
            Assert.Equal("Tok MPU4", CoinNoteHierarchy.ResolveCoinNote(0x39, 0x04));
        }

        [Fact]
        public void ResolveCoinNote_0x47_0x04_ReturnsEmpty()
        {
            Assert.Equal(string.Empty, CoinNoteHierarchy.ResolveCoinNote(0x47, 0x04));
        }

        [Fact]
        public void ResolveEffect_0x2D_0x01_ReturnsCcTalkCoin3()
        {
            Assert.Equal("Electronic", CoinNoteHierarchy.ResolveEffect(0x2D, 0x01));
        }


        [Fact]
        public void ResolveEffect_0x47_0x04_ReturnsEmpty()
        {
            Assert.Equal("S10 Token", CoinNoteHierarchy.ResolveEffect(0x47, 0x04));
        }
    }
}