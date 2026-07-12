using System;
using Xunit;

namespace MfmeFmlDecoder.Decoder
{
    public class MfmeVersionReaderTests
    {
        [Fact]
        public void Read_ReturnsVersionFromTag0x2F()
        {
            byte[] data =
            {
                0x2F, 0x00, 0x00, 0x00,
                0x04, 0x00, 0x00, 0x00,
                0x32, 0x30, 0x2E, 0x31,
            };

            string version = MfmeVersionReader.Read(data, offset: 0);

            Assert.Equal("20.1", version);
        }

        [Fact]
        public void Read_SkipsEarlierTagsBeforeVersion()
        {
            byte[] data =
            {
                0x01, 0x00, 0x00, 0x00,
                0x02, 0x00, 0x00, 0x00,
                0xAA, 0xBB,
                0x2F, 0x00, 0x00, 0x00,
                0x04, 0x00, 0x00, 0x00,
                0x31, 0x39, 0x2E, 0x30,
            };

            string version = MfmeVersionReader.Read(data, offset: 0);

            Assert.Equal("19.0", version);
        }

        [Fact]
        public void Read_ThrowsWhenVersionTagMissing()
        {
            byte[] data =
            {
                0x01, 0x00, 0x00, 0x00,
                0x02, 0x00, 0x00, 0x00,
                0xAA, 0xBB,
            };

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
                MfmeVersionReader.Read(data, offset: 0));

            Assert.Contains("MFME version tag 0x2F was not found", ex.Message);
        }
    }
}
