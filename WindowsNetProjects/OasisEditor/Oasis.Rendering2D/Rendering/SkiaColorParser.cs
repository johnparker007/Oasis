using SkiaSharp;

namespace OasisEditor.Rendering;

internal static class SkiaColorParser
{
    public static SKColor ParseOrDefault(string? hex, SKColor fallback)
    {
        if (string.IsNullOrWhiteSpace(hex))
        {
            return fallback;
        }

        return SKColor.TryParse(hex.Trim(), out var parsed)
            ? parsed
            : fallback;
    }
}
