using System.Text.RegularExpressions;

namespace OasisEditor;

public interface IMameReelStateParser
{
    bool TryParse(string line, out int reelId, out int reelValue);
}

public sealed partial class MameReelStateParser : IMameReelStateParser
{
    private const int MinReelPosition = 0;
    private const int MaxReelPosition = 95;

    [GeneratedRegex(@"^reel\s*(\d+)(?:\s*=\s*|\s+)(-?\d+)\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex ReelPattern();

    public bool TryParse(string line, out int reelId, out int reelValue)
    {
        reelId = 0;
        reelValue = 0;

        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        var match = ReelPattern().Match(line.Trim());
        if (!match.Success
            || !int.TryParse(match.Groups[1].Value, out reelId)
            || !int.TryParse(match.Groups[2].Value, out reelValue))
        {
            reelId = 0;
            reelValue = 0;
            return false;
        }

        if (reelValue is < MinReelPosition or > MaxReelPosition)
        {
            reelId = 0;
            reelValue = 0;
            return false;
        }

        return true;
    }
}
