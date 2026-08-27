using SkiaSharp;

namespace OasisEditor;

/// <summary>Stride-aware access to the RGBA/BGRA premultiplied storage used by generated artwork.</summary>
internal static unsafe class BitmapPixelBuffer
{
    public static bool IsSupported(SKBitmap bitmap) => bitmap.ColorType is SKColorType.Rgba8888 or SKColorType.Bgra8888;

    public static SKColor Read(SKBitmap bitmap, int x, int y)
    {
        if (!IsSupported(bitmap)) return bitmap.GetPixel(x, y);
        var p = (byte*)bitmap.GetPixels() + (y * bitmap.RowBytes) + (x * 4);
        var alpha = p[3];
        var first = Unpremultiply(p[0], alpha);
        var second = Unpremultiply(p[1], alpha);
        var third = Unpremultiply(p[2], alpha);
        return bitmap.ColorType == SKColorType.Rgba8888
            ? new SKColor(first, second, third, alpha)
            : new SKColor(third, second, first, alpha);
    }

    public static void Write(SKBitmap bitmap, int x, int y, byte red, byte green, byte blue, byte alpha)
    {
        if (!IsSupported(bitmap)) { bitmap.SetPixel(x, y, new SKColor(red, green, blue, alpha)); return; }
        var p = (byte*)bitmap.GetPixels() + (y * bitmap.RowBytes) + (x * 4);
        if (bitmap.ColorType == SKColorType.Rgba8888)
        {
            p[0] = Premultiply(red, alpha); p[1] = Premultiply(green, alpha); p[2] = Premultiply(blue, alpha);
        }
        else
        {
            p[0] = Premultiply(blue, alpha); p[1] = Premultiply(green, alpha); p[2] = Premultiply(red, alpha);
        }
        p[3] = alpha;
    }

    private static byte Premultiply(byte value, byte alpha) => (byte)((value * alpha + 127) / 255);
    private static byte Unpremultiply(byte value, byte alpha) => alpha == 0 ? (byte)0 : (byte)Math.Min(255, (value * 255 + (alpha / 2)) / alpha);
}
