namespace OasisEditor;
using System.Text.RegularExpressions;

public interface IMameLampStateParser
{
    bool TryParse(string line, out int lampId, out int lampValue);
}

public sealed class MameLampStateParser : IMameLampStateParser
{
    private static readonly Regex LampLineRegex = new(
        @"lamp\s*(\d+)\s*=\s*(-?\d+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public bool TryParse(string line, out int lampId, out int lampValue)
    {
        lampId = 0;
        lampValue = 0;

        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        var trimmed = line.Trim();
        var regexMatch = LampLineRegex.Match(trimmed);
        if (regexMatch.Success
            && int.TryParse(regexMatch.Groups[1].Value, out lampId)
            && int.TryParse(regexMatch.Groups[2].Value, out lampValue))
        {
            return true;
        }

        var tokens = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length < 2)
        {
            return false;
        }

        var lampTokenWithPrefix = tokens[0];
        if (!lampTokenWithPrefix.StartsWith("lamp", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var lampToken = lampTokenWithPrefix["lamp".Length..];
        if (lampToken.Length == 0)
        {
            return false;
        }

        var valueToken = tokens[^1];

        if (!int.TryParse(lampToken, out lampId))
        {
            return false;
        }

        if (!int.TryParse(valueToken, out lampValue))
        {
            return false;
        }

        return true;
    }
}
