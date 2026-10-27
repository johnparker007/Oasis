using System.IO;

namespace OasisEditor.Features.LayoutImport;

internal static class MfmeLampAssetPostProcessor
{
    public static bool TryProcessLamp(string sourceLampPath, string destinationLampPath, out string? error)
    {
        error = null;
        try
        {
            var lamp = MfmeBitmapCodec.Decode(sourceLampPath);
            var directory = Path.GetDirectoryName(destinationLampPath);
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
            MfmeBitmapCodec.SavePng(lamp, destinationLampPath);
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }
}
