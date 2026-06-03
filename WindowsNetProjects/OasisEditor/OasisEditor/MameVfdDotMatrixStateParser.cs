namespace OasisEditor;
using System.Text.RegularExpressions;

public interface IMameVfdDotMatrixStateParser
{
    bool TryParse(string line, out int dotIndex, out bool isOn);
}

public sealed class MameVfdDotMatrixStateParser : IMameVfdDotMatrixStateParser
{
    public const int Columns = 96;
    public const int Rows = 8;
    public const int DotCount = Columns * Rows;

    private static readonly Regex DotLineRegex = new(
        @"^vfddotmatrix\s*(\d+)\s*=\s*(-?\d+)\s*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public bool TryParse(string line, out int dotIndex, out bool isOn)
    {
        dotIndex = 0;
        isOn = false;
        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        var match = DotLineRegex.Match(line.Trim());
        if (!match.Success
            || !int.TryParse(match.Groups[1].Value, out dotIndex)
            || dotIndex < 0
            || dotIndex >= DotCount
            || !int.TryParse(match.Groups[2].Value, out var rawValue))
        {
            return false;
        }

        isOn = rawValue == 1;
        return true;
    }
}
