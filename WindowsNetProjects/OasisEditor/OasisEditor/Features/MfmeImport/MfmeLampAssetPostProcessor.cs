using System;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace OasisEditor.Features.MfmeImport;

internal static class MfmeLampAssetPostProcessor
{
    public static bool TryProcessLamp(
        string sourceLampPath,
        string? sourceMaskPath,
        string destinationLampPath,
        bool applyMaskTint,
        out string? error)
    {
        error = null;

        try
        {
            var lamp = LoadBgra32(sourceLampPath, preservePaletteAlpha: true);
            var preserveExistingTransparency = HasAnyFullyTransparentPixels(lamp);

            if (!string.IsNullOrWhiteSpace(sourceMaskPath) && File.Exists(sourceMaskPath))
            {
                var mask = LoadBgra32(sourceMaskPath!, preservePaletteAlpha: false);
                ApplyMask(lamp, mask, applyMaskTint, preserveExistingTransparency);
            }

            var directory = Path.GetDirectoryName(destinationLampPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            SavePng(lamp, destinationLampPath);
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static PixelBuffer LoadBgra32(string path, bool preservePaletteAlpha)
    {
        var sourceBytes = File.ReadAllBytes(path);
        using var stream = new MemoryStream(sourceBytes);
        var decoder = new BmpBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        var frame = decoder.Frames[0];

        var converted = new FormatConvertedBitmap(frame, PixelFormats.Bgra32, frame.Palette, 0);
        var stride = (converted.PixelWidth * converted.Format.BitsPerPixel + 7) / 8;
        var pixels = new byte[stride * converted.PixelHeight];
        converted.CopyPixels(pixels, stride, 0);

        if (preservePaletteAlpha && IsPalettized(frame.Format) && frame.Palette is not null)
        {
            ReapplyIndexedAlpha(frame, pixels, stride);
        }
        else if (preservePaletteAlpha)
        {
            ReapplyBgra32BmpAlpha(sourceBytes, frame.PixelWidth, frame.PixelHeight, pixels, stride);
        }

        return new PixelBuffer(converted.PixelWidth, converted.PixelHeight, stride, pixels);
    }

    private static bool IsPalettized(PixelFormat format)
    {
        return format == PixelFormats.Indexed1
            || format == PixelFormats.Indexed2
            || format == PixelFormats.Indexed4
            || format == PixelFormats.Indexed8;
    }

    private static void ReapplyIndexedAlpha(BitmapSource frame, byte[] pixels, int stride)
    {
        var palette = frame.Palette.Colors;
        if (palette.Count == 0)
        {
            return;
        }

        var bitsPerPixel = frame.Format.BitsPerPixel;
        if (bitsPerPixel is not (1 or 2 or 4 or 8))
        {
            return;
        }

        var indexedStride = (frame.PixelWidth * bitsPerPixel + 7) / 8;
        var indices = new byte[indexedStride * frame.PixelHeight];
        frame.CopyPixels(indices, indexedStride, 0);

        for (var y = 0; y < frame.PixelHeight; y++)
        {
            for (var x = 0; x < frame.PixelWidth; x++)
            {
                var paletteIndex = ReadPaletteIndex(indices, indexedStride, bitsPerPixel, x, y);
                if (paletteIndex < 0 || paletteIndex >= palette.Count)
                {
                    continue;
                }

                var color = palette[paletteIndex];
                pixels[(y * stride) + (x * 4) + 3] = color.A;
            }
        }
    }

    private static int ReadPaletteIndex(byte[] indices, int stride, int bitsPerPixel, int x, int y)
    {
        if (bitsPerPixel == 8)
        {
            return indices[(y * stride) + x];
        }

        var bitIndex = x * bitsPerPixel;
        var byteIndex = (y * stride) + (bitIndex / 8);
        var bitOffset = bitIndex % 8;
        var shift = 8 - bitsPerPixel - bitOffset;
        var mask = (1 << bitsPerPixel) - 1;
        return (indices[byteIndex] >> shift) & mask;
    }

    private static void ReapplyBgra32BmpAlpha(byte[] sourceBytes, int width, int height, byte[] pixels, int stride)
    {
        if (sourceBytes.Length < 54 || sourceBytes[0] != 'B' || sourceBytes[1] != 'M')
        {
            return;
        }

        var pixelDataOffset = ReadInt32(sourceBytes, 10);
        var dibHeaderSize = ReadInt32(sourceBytes, 14);
        if (dibHeaderSize < 40 || sourceBytes.Length < 14 + dibHeaderSize)
        {
            return;
        }

        var bmpWidth = ReadInt32(sourceBytes, 18);
        var bmpHeight = ReadInt32(sourceBytes, 22);
        var bitsPerPixel = ReadUInt16(sourceBytes, 28);
        if (bmpWidth != width || Math.Abs(bmpHeight) != height || bitsPerPixel != 32)
        {
            return;
        }

        var sourceStride = ((bmpWidth * bitsPerPixel + 31) / 32) * 4;
        if (pixelDataOffset < 0 || pixelDataOffset + (sourceStride * height) > sourceBytes.Length)
        {
            return;
        }

        var hasTransparentAlpha = false;
        var hasVisibleAlpha = false;
        for (var y = 0; y < height; y++)
        {
            var sourceY = bmpHeight < 0 ? y : height - 1 - y;
            var sourceRow = pixelDataOffset + (sourceY * sourceStride);
            for (var x = 0; x < width; x++)
            {
                var alpha = sourceBytes[sourceRow + (x * 4) + 3];
                hasTransparentAlpha |= alpha < 255;
                hasVisibleAlpha |= alpha > 0;
            }
        }

        if (!hasTransparentAlpha || !hasVisibleAlpha)
        {
            return;
        }

        for (var y = 0; y < height; y++)
        {
            var sourceY = bmpHeight < 0 ? y : height - 1 - y;
            var sourceRow = pixelDataOffset + (sourceY * sourceStride);
            var destinationRow = y * stride;
            for (var x = 0; x < width; x++)
            {
                pixels[destinationRow + (x * 4) + 3] = sourceBytes[sourceRow + (x * 4) + 3];
            }
        }
    }

    private static int ReadInt32(byte[] bytes, int offset)
    {
        if (offset < 0 || offset + 4 > bytes.Length)
        {
            return 0;
        }

        return bytes[offset]
            | (bytes[offset + 1] << 8)
            | (bytes[offset + 2] << 16)
            | (bytes[offset + 3] << 24);
    }

    private static int ReadUInt16(byte[] bytes, int offset)
    {
        if (offset < 0 || offset + 2 > bytes.Length)
        {
            return 0;
        }

        return bytes[offset] | (bytes[offset + 1] << 8);
    }

    private static bool HasAnyFullyTransparentPixels(PixelBuffer image)
    {
        for (var i = 3; i < image.Pixels.Length; i += 4)
        {
            if (image.Pixels[i] == 0)
            {
                return true;
            }
        }

        return false;
    }

    private static void ApplyMask(PixelBuffer image, PixelBuffer mask, bool applyMaskTint, bool preserveExistingTransparency)
    {
        for (var y = 0; y < image.Height; y++)
        {
            for (var x = 0; x < image.Width; x++)
            {
                var offset = (y * image.Stride) + (x * 4);
                if (image.Pixels[offset + 3] == 0)
                {
                    continue;
                }

                var maskX = ScaleCoordinate(x, image.Width, mask.Width);
                var maskY = ScaleCoordinate(y, image.Height, mask.Height);
                var maskOffset = (maskY * mask.Stride) + (maskX * 4);

                var mb = mask.Pixels[maskOffset];
                var mg = mask.Pixels[maskOffset + 1];
                var mr = mask.Pixels[maskOffset + 2];
                var maskAlpha = Math.Max(mr, Math.Max(mg, mb));
                if (!preserveExistingTransparency)
                {
                    image.Pixels[offset + 3] = maskAlpha;
                }

                if (applyMaskTint)
                {
                    image.Pixels[offset] = Multiply255(image.Pixels[offset], mb);
                    image.Pixels[offset + 1] = Multiply255(image.Pixels[offset + 1], mg);
                    image.Pixels[offset + 2] = Multiply255(image.Pixels[offset + 2], mr);
                }
            }
        }
    }

    private static int ScaleCoordinate(int value, int sourceSize, int destinationSize)
    {
        if (sourceSize <= 1 || destinationSize <= 1)
        {
            return 0;
        }

        return (int)Math.Round((double)value * (destinationSize - 1) / (sourceSize - 1));
    }

    private static byte Multiply255(byte left, byte right) => (byte)((left * right) / 255);

    private static void SavePng(PixelBuffer image, string destinationPath)
    {
        var bitmap = BitmapSource.Create(image.Width, image.Height, 96, 96, PixelFormats.Bgra32, null, image.Pixels, image.Stride);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));

        using var stream = File.Create(destinationPath);
        encoder.Save(stream);
    }

    private sealed class PixelBuffer(int width, int height, int stride, byte[] pixels)
    {
        public int Width { get; } = width;
        public int Height { get; } = height;
        public int Stride { get; } = stride;
        public byte[] Pixels { get; } = pixels;
    }
}
