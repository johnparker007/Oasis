using System.Buffers.Binary;
using OasisEditor.Features.LayoutImport;
using Xunit;

namespace OasisEditor.Avalonia.Tests;

public sealed class PortableBitmapTests
{
    [Fact]
    public void PngRoundTripPreservesStraightColorAndAlphaBytes()
    {
        var path = Path.Combine(Path.GetTempPath(), "OasisPixels-" + Guid.NewGuid().ToString("N") + ".png");
        try
        {
            byte[] pixels = [20, 40, 200, 128];
            MfmeBitmapCodec.SavePng(new(1, 1, 4, pixels), path);
            Assert.Equal(pixels, MfmeBitmapCodec.Decode(path).Pixels);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void IndexedBmpPreservesPaletteAlphaWithoutPremultiplyingColor()
    {
        var path = Path.Combine(Path.GetTempPath(), "OasisPalette-" + Guid.NewGuid().ToString("N") + ".bmp");
        try
        {
            var bmp = new byte[66]; bmp[0] = (byte)'B'; bmp[1] = (byte)'M';
            void I32(int offset, int value) => BinaryPrimitives.WriteInt32LittleEndian(bmp.AsSpan(offset, 4), value);
            I32(2, bmp.Length); I32(10, 62); I32(14, 40); I32(18, 1); I32(22, 1);
            BinaryPrimitives.WriteUInt16LittleEndian(bmp.AsSpan(26), 1);
            BinaryPrimitives.WriteUInt16LittleEndian(bmp.AsSpan(28), 8);
            I32(34, 4); I32(46, 2);
            bmp[58] = 20; bmp[59] = 40; bmp[60] = 200; bmp[61] = 128; bmp[62] = 1;
            File.WriteAllBytes(path, bmp);
            Assert.Equal(new byte[] { 20, 40, 200, 128 }, MfmeBitmapCodec.Decode(path).Pixels);
        }
        finally { File.Delete(path); }
    }
}
