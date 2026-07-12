using MfmeFmlDecoder.Model;
using MfmeFmlDecoder.src.Decoder.Component.Core;
using Xunit;

namespace MfmeFmlDecoder.Decoder
{
    public class FileWalkerTests
    {
        [Fact]
        public void WalkTlv_AcceptsSupportedMfmeVersion()
        {
            byte[] data =
            {
                0x2F, 0x00, 0x00, 0x00,
                0x04, 0x00, 0x00, 0x00,
                0x32, 0x30, 0x2E, 0x31,
            };

            var fileWalker = new FileWalker(new ComponentWalker(new ComponentParser()));

            fileWalker.WalkTlv(data, offset: 0);
        }

        [Fact]
        public void WalkTlv_RejectsUnsupportedMfmeVersion()
        {
            byte[] data =
            {
                0x2F, 0x00, 0x00, 0x00,
                0x04, 0x00, 0x00, 0x00,
                0x31, 0x39, 0x2E, 0x30,
            };

            var fileWalker = new FileWalker(new ComponentWalker(new ComponentParser()));

            UnsupportedMfmeVersionException ex = Assert.Throws<UnsupportedMfmeVersionException>(() =>
                fileWalker.WalkTlv(data, offset: 0));

            Assert.Equal("19.0", ex.FoundVersion);
            Assert.Contains("Only MFME v20.1 is supported.", ex.Message);
        }
    }
}
