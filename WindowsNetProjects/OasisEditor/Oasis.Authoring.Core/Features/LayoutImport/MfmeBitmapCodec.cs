using System.Buffers.Binary;
using SkiaSharp;

namespace OasisEditor.Features.LayoutImport;

/// <summary>Cross-platform BMP decoding that retains MFME palette and 32-bit alpha bytes.</summary>
internal static class MfmeBitmapCodec
{
    internal sealed record PixelData(int Width, int Height, int Stride, byte[] Pixels);

    public static PixelData Decode(string path)
    {
        var bytes = File.ReadAllBytes(path);
        using var stream = new SKMemoryStream(bytes);
        using var codec = SKCodec.Create(stream) ?? throw new InvalidDataException("The MFME image could not be decoded.");
        var info = new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Bgra8888, SKAlphaType.Unpremul);
        using var bitmap = new SKBitmap(info);
        if (codec.GetPixels(info, bitmap.GetPixels()) != SKCodecResult.Success)
            throw new InvalidDataException("The MFME image could not be converted to BGRA32.");
        var stride = checked(bitmap.Width * 4);
        var pixels = bitmap.Bytes.AsSpan(0, checked(stride * bitmap.Height)).ToArray();
        PreserveBmpAlpha(bytes, bitmap.Width, bitmap.Height, pixels, stride);
        return new(bitmap.Width, bitmap.Height, stride, pixels);
    }

    public static bool TryDecodeUncompressedBgra32Bmp(string path, out PixelData image)
    {
        image = default!;
        if (!string.Equals(Path.GetExtension(path), ".bmp", StringComparison.OrdinalIgnoreCase)) return false;
        var bytes = File.ReadAllBytes(path);
        if (!TryBmpHeader(bytes, out var header) || header.BitsPerPixel != 32 || header.Compression != 0) return false;
        var stride = checked(header.Width * 4);
        if ((long)header.PixelOffset + (long)stride * header.Height > bytes.Length) return false;
        var pixels = new byte[checked(stride * header.Height)];
        for (var y = 0; y < header.Height; y++)
        {
            var sourceY = header.TopDown ? y : header.Height - 1 - y;
            Buffer.BlockCopy(bytes, header.PixelOffset + sourceY * stride, pixels, y * stride, stride);
        }
        image = new(header.Width, header.Height, stride, pixels);
        return true;
    }

    public static void SavePng(PixelData image, string path)
    {
        var info = new SKImageInfo(image.Width, image.Height, SKColorType.Bgra8888, SKAlphaType.Unpremul);
        using var bitmap = new SKBitmap(info);
        System.Runtime.InteropServices.Marshal.Copy(image.Pixels, 0, bitmap.GetPixels(), image.Pixels.Length);
        using var encoded = bitmap.Encode(SKEncodedImageFormat.Png, 100)
            ?? throw new InvalidDataException("The MFME image could not be encoded as PNG.");
        using var output = File.Create(path);
        encoded.SaveTo(output);
    }

    private static void PreserveBmpAlpha(byte[] bytes, int width, int height, byte[] pixels, int stride)
    {
        if (!TryBmpHeader(bytes, out var header) || header.Width != width || header.Height != height) return;
        if (header.BitsPerPixel is 1 or 2 or 4 or 8)
        {
            PreservePaletteAlpha(bytes, header, pixels, stride);
            return;
        }
        if (header.BitsPerPixel != 32) return;

        var sourceStride = checked(width * 4);
        if ((long)header.PixelOffset + (long)sourceStride * height > bytes.Length) return;
        var hasTransparentAlpha = false;
        var hasVisibleAlpha = false;
        for (var y = 0; y < height; y++)
        {
            var sourceY = header.TopDown ? y : height - 1 - y;
            var row = header.PixelOffset + sourceY * sourceStride;
            for (var x = 0; x < width; x++)
            {
                var alpha = bytes[row + x * 4 + 3];
                hasTransparentAlpha |= alpha < 255;
                hasVisibleAlpha |= alpha > 0;
            }
        }
        // MFME commonly leaves the fourth byte zero on opaque 32-bit BMPs.
        if (!hasTransparentAlpha || !hasVisibleAlpha) return;
        for (var y = 0; y < height; y++)
        {
            var sourceY = header.TopDown ? y : height - 1 - y;
            var sourceRow = header.PixelOffset + sourceY * sourceStride;
            var destinationRow = y * stride;
            for (var x = 0; x < width; x++) pixels[destinationRow + x * 4 + 3] = bytes[sourceRow + x * 4 + 3];
        }
    }

    private static void PreservePaletteAlpha(byte[] bytes, BmpHeader header, byte[] pixels, int stride)
    {
        var paletteStart = 14 + header.DibHeaderSize;
        if (header.DibHeaderSize == 40 && header.Compression is 3 or 6) paletteStart += header.BitsPerPixel == 32 ? 16 : 12;
        var entryCount = header.ColorsUsed == 0 ? 1 << header.BitsPerPixel : header.ColorsUsed;
        var paletteBytes = (long)entryCount * 4;
        if (entryCount <= 0 || paletteStart < 0 || paletteStart + paletteBytes > header.PixelOffset || paletteStart + paletteBytes > bytes.Length) return;
        var sourceStride = ((header.Width * header.BitsPerPixel + 31) / 32) * 4;
        if ((long)header.PixelOffset + (long)sourceStride * header.Height > bytes.Length) return;
        for (var y = 0; y < header.Height; y++)
        {
            var sourceY = header.TopDown ? y : header.Height - 1 - y;
            var sourceRow = header.PixelOffset + sourceY * sourceStride;
            for (var x = 0; x < header.Width; x++)
            {
                var bitIndex = x * header.BitsPerPixel;
                var byteIndex = sourceRow + bitIndex / 8;
                var shift = 8 - header.BitsPerPixel - bitIndex % 8;
                var index = (bytes[byteIndex] >> shift) & ((1 << header.BitsPerPixel) - 1);
                if (index >= entryCount) continue;
                pixels[y * stride + x * 4 + 3] = bytes[paletteStart + index * 4 + 3];
            }
        }
    }

    private static bool TryBmpHeader(byte[] bytes, out BmpHeader header)
    {
        header = default;
        if (bytes.Length < 54 || bytes[0] != 'B' || bytes[1] != 'M') return false;
        var pixelOffset = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(10, 4));
        var dibSize = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(14, 4));
        if (dibSize < 40 || bytes.Length < 14L + dibSize) return false;
        var width = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(18, 4));
        var signedHeight = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(22, 4));
        var planes = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(26, 2));
        var bpp = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(28, 2));
        var compression = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(30, 4));
        var colorsUsed = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(46, 4));
        if (width <= 0 || signedHeight == 0 || signedHeight == int.MinValue || planes != 1 || pixelOffset < 0) return false;
        header = new(width, Math.Abs(signedHeight), signedHeight < 0, pixelOffset, dibSize, bpp, compression, colorsUsed);
        return true;
    }

    private readonly record struct BmpHeader(int Width, int Height, bool TopDown, int PixelOffset, int DibHeaderSize,
        int BitsPerPixel, int Compression, int ColorsUsed);
}
