using System;

namespace MfmeFmlDecoder.Model
{
    public sealed record BitmapEntry(
        int Width,
        int Height,
        ushort BitsPerPixel,
        byte[] Bytes
    )
    {
        public string Purpose { get; init; }
    };
}

