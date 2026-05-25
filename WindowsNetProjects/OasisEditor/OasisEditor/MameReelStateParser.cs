using System.Text.RegularExpressions;

namespace OasisEditor;

public interface IMameReelStateParser
{
    bool TryParse(string line, out int reelId, out int reelValue);
}

public sealed partial class MameReelStateParser : IMameReelStateParser
{
    private const int SreelCycle = 65536;
    private const int LegacyReelPositionsPerRevolution = 96;

    [GeneratedRegex(@"^sreel\s*(\d+)\s*=\s*(-?\d+)$", RegexOptions.IgnoreCase)]
    private static partial Regex SreelEqualsPattern();

    public bool TryParse(string line, out int reelId, out int reelValue)
    {
        reelId = 0;
        reelValue = 0;

        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        var trimmed = line.Trim();
        var sreelRegexMatch = SreelEqualsPattern().Match(trimmed);
        if (sreelRegexMatch.Success
            && int.TryParse(sreelRegexMatch.Groups[1].Value, out reelId)
            && int.TryParse(sreelRegexMatch.Groups[2].Value, out var sreelValue))
        {
            reelValue = ConvertSreelToLegacyReelPosition(sreelValue);
            return true;
        }

        return false;
    }

    private static int ConvertSreelToLegacyReelPosition(int sreelValue)
    {
        var wrapped = ((sreelValue % SreelCycle) + SreelCycle) % SreelCycle;
        return (int)Math.Round((wrapped / (double)SreelCycle) * LegacyReelPositionsPerRevolution, MidpointRounding.AwayFromZero)
            % LegacyReelPositionsPerRevolution;
    }
}
