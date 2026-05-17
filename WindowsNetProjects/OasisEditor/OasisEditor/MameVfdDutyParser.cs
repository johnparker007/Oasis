using System.Collections.ObjectModel;
using System.Text.RegularExpressions;

namespace OasisEditor;

public sealed class MameVfdDutyParser
{
    private static readonly Regex VfdDutyRegex = new(@"^vfdduty(?<cellId>\d+)\s*=\s*(?<duty>\d+)$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly IReadOnlyDictionary<FruitMachinePlatformType, int> MaxVfdDutyByPlatform =
        new ReadOnlyDictionary<FruitMachinePlatformType, int>(new Dictionary<FruitMachinePlatformType, int>
        {
            [FruitMachinePlatformType.MPU4] = 31,
            [FruitMachinePlatformType.Scorpion4] = 7
        });

    public bool TryParseNormalized(string line, FruitMachinePlatformType platform, out int cellId, out double normalizedBrightness)
    {
        cellId = 0;
        normalizedBrightness = 0d;

        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        var match = VfdDutyRegex.Match(line);
        if (!match.Success
            || !int.TryParse(match.Groups["cellId"].Value, out cellId)
            || !int.TryParse(match.Groups["duty"].Value, out var duty))
        {
            return false;
        }

        var maxDuty = ResolveMaxDuty(platform);
        normalizedBrightness = maxDuty <= 0
            ? 0d
            : Math.Clamp((double)duty / maxDuty, 0d, 1d);
        return true;
    }

    public static int ResolveMaxDuty(FruitMachinePlatformType platform)
    {
        return MaxVfdDutyByPlatform.TryGetValue(platform, out var maxDuty)
            ? maxDuty
            : 31;
    }
}
