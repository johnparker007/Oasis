namespace OasisEditor;
using System.Text.RegularExpressions;

public interface IMameVfdDotMatrixStateParser
{
    bool TryParse(string line, out int dotIndex, out int dotValue);
}

public sealed class MameVfdDotMatrixStateParser : IMameVfdDotMatrixStateParser
{
    public const int DotCount = 96 * 8;

    private static readonly Regex DotLineRegex = new(
        @"^vfddotmatrix\s*(\d+)\s*=\s*(-?\d+)\s*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public bool TryParse(string line, out int dotIndex, out int dotValue)
    {
        dotIndex = 0;
        dotValue = 0;
        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        var match = DotLineRegex.Match(line.Trim());
        if (!match.Success
            || !int.TryParse(match.Groups[1].Value, out dotIndex)
            || !int.TryParse(match.Groups[2].Value, out var rawValue)
            || dotIndex is < 0 or >= DotCount)
        {
            dotIndex = 0;
            dotValue = 0;
            return false;
        }

        dotValue = rawValue == 1 ? 1 : 0;
        return true;
    }
}
