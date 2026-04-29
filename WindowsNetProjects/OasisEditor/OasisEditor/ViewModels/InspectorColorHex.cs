using System;
using System.Globalization;
using System.Windows.Media;

namespace OasisEditor;

public static class InspectorColorHex
{
    public static bool TryParse(string? value, out Color color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim();
        if (normalized.StartsWith('#'))
        {
            normalized = normalized[1..];
        }

        if (normalized.Length == 6 && byte.TryParse(normalized[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var r)
            && byte.TryParse(normalized.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var g)
            && byte.TryParse(normalized.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var b))
        {
            color = Color.FromArgb(255, r, g, b);
            return true;
        }

        if (normalized.Length == 8 && byte.TryParse(normalized[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var a)
            && byte.TryParse(normalized.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out r)
            && byte.TryParse(normalized.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out g)
            && byte.TryParse(normalized.Substring(6, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out b))
        {
            color = Color.FromArgb(a, r, g, b);
            return true;
        }

        return false;
    }

    public static string Format(Color color)
    {
        if (color.A == 255)
        {
            return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        }

        return $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
    }
}
