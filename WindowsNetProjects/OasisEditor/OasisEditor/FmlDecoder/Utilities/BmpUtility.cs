using System;
using System.Buffers.Binary;

namespace MfmeFmlDecoder.Utilities
{
    internal static class BmpUtility
    {
        internal readonly record struct BmpInfo(int Width, int Height, ushort BitsPerPixel);

        public static BmpInfo ReadInfo(byte[] bmpBytes)
        {
            if (bmpBytes is null) throw new ArgumentNullException(nameof(bmpBytes));
            if (bmpBytes.Length < 30) throw new InvalidOperationException($"BMP payload too small ({bmpBytes.Length} bytes).");

            if (bmpBytes[0] != (byte)'B' || bmpBytes[1] != (byte)'M')
            {
                throw new InvalidOperationException("BMP payload does not start with 'BM'.");
            }

            // BITMAPINFOHEADER fields (most common): width @ 18, height @ 22, bpp @ 28
            int width = BinaryPrimitives.ReadInt32LittleEndian(bmpBytes.AsSpan(18, 4));
            int height = BinaryPrimitives.ReadInt32LittleEndian(bmpBytes.AsSpan(22, 4));
            ushort bpp = BinaryPrimitives.ReadUInt16LittleEndian(bmpBytes.AsSpan(28, 2));

            // Height can be negative (top-down). Preserve magnitude; caller can interpret sign if needed.
            if (height < 0) height = -height;

            return new BmpInfo(width, height, bpp);
        }
    }
}

