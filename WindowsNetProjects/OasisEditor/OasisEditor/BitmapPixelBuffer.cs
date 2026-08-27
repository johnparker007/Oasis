using SkiaSharp;

namespace OasisEditor;

/// <summary>A cached, stride-aware view over a bitmap's pixels for tight raster loops.</summary>
internal readonly unsafe struct BitmapPixelBuffer
{
    private readonly SKBitmap _bitmap;
    private readonly byte* _pixels;
    private readonly int _rowBytes;
    private readonly byte _redOffset;
    private readonly byte _blueOffset;

    public BitmapPixelBuffer(SKBitmap bitmap)
    {
        _bitmap = bitmap;
        _rowBytes = bitmap.RowBytes;
        _pixels = (byte*)bitmap.GetPixels();
        IsDirect = _pixels != null && bitmap.ColorType is SKColorType.Rgba8888 or SKColorType.Bgra8888;
        _redOffset = bitmap.ColorType == SKColorType.Bgra8888 ? (byte)2 : (byte)0;
        _blueOffset = bitmap.ColorType == SKColorType.Bgra8888 ? (byte)0 : (byte)2;
    }

    public bool IsDirect { get; }

    /// <summary>Reads the bytes exactly as stored: RGB is premultiplied by alpha.</summary>
    public void ReadPremultiplied(int x, int y, out byte red, out byte green, out byte blue, out byte alpha)
    {
        if (!IsDirect)
        {
            var color = _bitmap.GetPixel(x, y);
            alpha = color.Alpha;
            red = Premultiply(color.Red, alpha); green = Premultiply(color.Green, alpha); blue = Premultiply(color.Blue, alpha);
            return;
        }
        var pixel = _pixels + y * _rowBytes + x * 4;
        red = pixel[_redOffset]; green = pixel[1]; blue = pixel[_blueOffset]; alpha = pixel[3];
    }

    public void ReadPremultipliedDirect(int x, int y, out byte red, out byte green, out byte blue, out byte alpha)
    {
        var pixel = _pixels + y * _rowBytes + x * 4;
        red = pixel[_redOffset]; green = pixel[1]; blue = pixel[_blueOffset]; alpha = pixel[3];
    }

    public void ReadStraight(int x, int y, out byte red, out byte green, out byte blue, out byte alpha)
    {
        ReadPremultiplied(x, y, out red, out green, out blue, out alpha);
        red = Unpremultiply(red, alpha); green = Unpremultiply(green, alpha); blue = Unpremultiply(blue, alpha);
    }

    public void WriteStraight(int x, int y, byte red, byte green, byte blue, byte alpha)
    {
        if (!IsDirect) { _bitmap.SetPixel(x, y, new SKColor(red, green, blue, alpha)); return; }
        var pixel = _pixels + y * _rowBytes + x * 4;
        pixel[_redOffset] = Premultiply(red, alpha); pixel[1] = Premultiply(green, alpha);
        pixel[_blueOffset] = Premultiply(blue, alpha); pixel[3] = alpha;
    }

    /// <summary>Writes already-premultiplied channel bytes without a lossy straight-colour round trip.</summary>
    public void WritePremultiplied(int x, int y, byte red, byte green, byte blue, byte alpha)
    {
        if (!IsDirect)
        {
            _bitmap.SetPixel(x, y, new SKColor(Unpremultiply(red, alpha), Unpremultiply(green, alpha),
                Unpremultiply(blue, alpha), alpha));
            return;
        }
        var pixel = _pixels + y * _rowBytes + x * 4;
        pixel[_redOffset] = red; pixel[1] = green; pixel[_blueOffset] = blue; pixel[3] = alpha;
    }

    private static byte Premultiply(byte value, byte alpha) => (byte)((value * alpha + 127) / 255);
    private static byte Unpremultiply(byte value, byte alpha) => alpha == 0 ? (byte)0 : (byte)Math.Min(255, (value * 255 + alpha / 2) / alpha);
}
