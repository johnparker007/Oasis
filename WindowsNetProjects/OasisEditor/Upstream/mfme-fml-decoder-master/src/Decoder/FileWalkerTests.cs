using System;
using System.Text;
using MfmeFmlDecoder.Model;
using MfmeFmlDecoder.src.Decoder.Component.Core;
using MfmeFmlDecoder.src.Model;
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

        [Fact]
        public void WalkTlv_ParsesLayoutDescriptionTextNotesAndSplash()
        {
            byte[] description = Encoding.ASCII.GetBytes("Test layout description");
            Array.Resize(ref description, LayoutFileHeader.LayoutDescriptionLength);

            byte[] notes = Encoding.ASCII.GetBytes("Handy author notes\0padding");
            byte[] splash = BuildMinimalBmp(width: 2, height: 1);

            byte[] data = Concat(
                Tlv(LayoutFileHeader.LayoutDescriptionTag, description),
                Tlv(LayoutFileHeader.TextNotesTag, notes),
                Tlv(LayoutFileHeader.SplashBitmapTag, splash));

            var fileWalker = new FileWalker(new ComponentWalker(new ComponentParser()));
            fileWalker.WalkTlv(data, offset: 0);

            Assert.Equal("Test layout description", fileWalker.Header.Description);
            Assert.Equal("Handy author notes", fileWalker.Header.TextNotes);
            Assert.True(fileWalker.Header.HasSplash);
            Assert.NotNull(fileWalker.Header.SplashBitmap);
            Assert.Equal(2, fileWalker.Header.SplashBitmap.Width);
            Assert.Equal(1, fileWalker.Header.SplashBitmap.Height);
            Assert.Equal(LayoutFileHeader.SplashBitmapImageKey, fileWalker.Header.SplashBitmap.Purpose);
            Assert.True(fileWalker.Header.Images.ContainsKey(LayoutFileHeader.SplashBitmapImageKey));
        }

        [Fact]
        public void WalkTlv_EmptySplashAndStrings_LeaveDefaults()
        {
            byte[] data = Concat(
                Tlv(LayoutFileHeader.LayoutDescriptionTag, new byte[LayoutFileHeader.LayoutDescriptionLength]),
                Tlv(LayoutFileHeader.TextNotesTag, Array.Empty<byte>()),
                Tlv(LayoutFileHeader.SplashBitmapTag, Array.Empty<byte>()));

            var fileWalker = new FileWalker(new ComponentWalker(new ComponentParser()));
            fileWalker.WalkTlv(data, offset: 0);

            Assert.Equal(string.Empty, fileWalker.Header.Description);
            Assert.Equal(string.Empty, fileWalker.Header.TextNotes);
            Assert.False(fileWalker.Header.HasSplash);
            Assert.Empty(fileWalker.Header.Images);
        }

        [Fact]
        public void Layout_ToJson_IsComponentsOnly_HeaderFieldsStayOffJson()
        {
            var header = new LayoutFileHeader
            {
                Description = "Cabinet artwork",
                TextNotes = "Needs lamp remap",
                SplashBitmap = new BitmapEntry(2, 1, 24, BuildMinimalBmp(2, 1))
                {
                    Purpose = LayoutFileHeader.SplashBitmapImageKey
                }
            };

            var layout = new Layout(Array.Empty<BaseComponentStub>(), header);
            string json = layout.ToJson(indented: false);

            Assert.Equal("Cabinet artwork", layout.Description);
            Assert.Equal("Needs lamp remap", layout.TextNotes);
            Assert.True(layout.HasSplash);
            Assert.DoesNotContain("Description", json);
            Assert.DoesNotContain("TextNotes", json);
            Assert.DoesNotContain("HasSplash", json);
            Assert.DoesNotContain("splash_bitmap", json);
            Assert.Contains("\"Components\":[]", json);
        }

        private static byte[] Tlv(uint tag, byte[] value)
        {
            value ??= Array.Empty<byte>();
            byte[] record = new byte[8 + value.Length];
            BitConverter.TryWriteBytes(record.AsSpan(0, 4), tag);
            BitConverter.TryWriteBytes(record.AsSpan(4, 4), (uint)value.Length);
            value.CopyTo(record, 8);
            return record;
        }

        private static byte[] Concat(params byte[][] parts)
        {
            int total = 0;
            foreach (byte[] part in parts)
            {
                total += part.Length;
            }

            byte[] result = new byte[total];
            int offset = 0;
            foreach (byte[] part in parts)
            {
                part.CopyTo(result, offset);
                offset += part.Length;
            }

            return result;
        }

        private static byte[] BuildMinimalBmp(int width, int height)
        {
            // BITMAPFILEHEADER (14) + BITMAPINFOHEADER (40) = 54 bytes header; pad to >= 30 for ReadInfo.
            byte[] bmp = new byte[54];
            bmp[0] = (byte)'B';
            bmp[1] = (byte)'M';
            BitConverter.TryWriteBytes(bmp.AsSpan(2, 4), (uint)bmp.Length);
            BitConverter.TryWriteBytes(bmp.AsSpan(14, 4), 40u); // biSize
            BitConverter.TryWriteBytes(bmp.AsSpan(18, 4), width);
            BitConverter.TryWriteBytes(bmp.AsSpan(22, 4), height);
            BitConverter.TryWriteBytes(bmp.AsSpan(26, 2), (ushort)1); // planes
            BitConverter.TryWriteBytes(bmp.AsSpan(28, 2), (ushort)24); // bpp
            return bmp;
        }

        // Stub so Layout tests do not need a concrete component type.
        private sealed class BaseComponentStub : MfmeFmlDecoder.src.Model.Component.BaseComponent
        {
        }
    }
}
