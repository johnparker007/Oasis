using System.IO;
using System.Windows.Media.Imaging;

namespace OasisEditor;

/// <summary>Decodes mutable project images without participating in WPF's URI image cache.</summary>
internal static class ReloadableBitmapImageLoader
{
    public static BitmapImage? Load(string? path, int? maximumDecodeDimension = null)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;

        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var decodeWidth = 0;
        var decodeHeight = 0;
        if (maximumDecodeDimension is > 0)
        {
            var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.DelayCreation | BitmapCreateOptions.PreservePixelFormat,
                BitmapCacheOption.None);
            var frame = decoder.Frames[0];
            if (Math.Max(frame.PixelWidth, frame.PixelHeight) > maximumDecodeDimension.Value)
            {
                if (frame.PixelWidth >= frame.PixelHeight) decodeWidth = maximumDecodeDimension.Value;
                else decodeHeight = maximumDecodeDimension.Value;
            }
            stream.Position = 0;
        }
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
        image.DecodePixelWidth = decodeWidth;
        image.DecodePixelHeight = decodeHeight;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }
}
