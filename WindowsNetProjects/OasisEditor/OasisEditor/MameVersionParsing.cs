using System.Text.RegularExpressions;

namespace OasisEditor;

internal static partial class MameVersionParsing
{
    private static readonly string[] SeedVersions = ["0281", "0267", "0258"];

    public static IReadOnlyList<string> GetSeedVersions() => SeedVersions;

    public static string? TryParseLatestFromMamedevReleasePage(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return null;
        }

        var matches = MamedevReleaseRegexMatches(html);
        return SelectHighestNormalized(matches.Select(m => NormalizeVersion(m.Groups[1].Value)));
    }

    public static string? TryParseLatestFromGitHubReleases(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return null;
        }

        var fromReleaseTitle = GitHubReleaseTitleRegex().Matches(html)
            .Select(m => NormalizeVersion(m.Groups[1].Value));
        var fromTag = GitHubTagRegex().Matches(html)
            .Select(m => NormalizeVersion(m.Groups[1].Value));

        return SelectHighestNormalized(fromReleaseTitle.Concat(fromTag));
    }

    public static string NormalizeVersion(string version)
    {
        var numericOnly = new string(version.Where(char.IsDigit).ToArray());
        if (string.IsNullOrWhiteSpace(numericOnly))
        {
            throw new InvalidOperationException("MAME version must contain digits.");
        }

        var numericVersion = int.Parse(numericOnly);
        return numericVersion.ToString("0000");
    }

    public static IReadOnlyList<string> NormalizeSortAndDedupe(IEnumerable<string> versions)
    {
        var ordered = versions
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(NormalizeVersion)
            .Distinct(StringComparer.Ordinal)
            .OrderByDescending(v => int.Parse(v))
            .ToArray();

        return ordered;
    }

    private static string? SelectHighestNormalized(IEnumerable<string> versions)
    {
        return versions
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct(StringComparer.Ordinal)
            .OrderByDescending(v => int.Parse(v))
            .FirstOrDefault();
    }

    [GeneratedRegex("MAME\\s+0\\.(\\d{3})\\s+Official Binary Packages", RegexOptions.IgnoreCase)]
    private static partial Regex MamedevReleaseRegex();

    [GeneratedRegex("latest official MAME release is version\\s+0\\.(\\d{3})", RegexOptions.IgnoreCase)]
    private static partial Regex MamedevLatestRegex();

    [GeneratedRegex("MAME\\s+0\\.(\\d{3})", RegexOptions.IgnoreCase)]
    private static partial Regex GitHubReleaseTitleRegex();

    [GeneratedRegex("mame(\\d{4})", RegexOptions.IgnoreCase)]
    private static partial Regex GitHubTagRegex();

    private static IEnumerable<Match> MamedevReleaseRegexMatches(string html)
        => MamedevReleaseRegex().Matches(html).Cast<Match>()
            .Concat(MamedevLatestRegex().Matches(html).Cast<Match>());
}

