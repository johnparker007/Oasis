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
    private const int SreelCycle = 65536;
    private const int LegacyReelPositionsPerRevolution = 96;

    [GeneratedRegex(@"^reel\s*(\d+)(?:\s*=\s*|\s+)(-?\d+)\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex ReelPattern();

    [GeneratedRegex(@"^sreel\s*(\d+)\s*=\s*(-?\d+)\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex SreelPattern();

    public bool TryParse(string line, out int reelId, out int reelValue)
    {
        reelId = 0;
        reelValue = 0;

        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        var trimmed = line.Trim();
        var sreelMatch = SreelPattern().Match(trimmed);
        if (sreelMatch.Success
            && int.TryParse(sreelMatch.Groups[1].Value, out reelId)
            && int.TryParse(sreelMatch.Groups[2].Value, out var sreelValue))
        {
            reelValue = ConvertSreelToLegacyReelPosition(sreelValue);
            return true;
        }

        var match = ReelPattern().Match(trimmed);
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

    private static int ConvertSreelToLegacyReelPosition(int sreelValue)
    {
        var wrapped = ((sreelValue % SreelCycle) + SreelCycle) % SreelCycle;
        return (int)Math.Round((wrapped / (double)SreelCycle) * LegacyReelPositionsPerRevolution, MidpointRounding.AwayFromZero)
            % LegacyReelPositionsPerRevolution;
    }
}
