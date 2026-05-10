namespace OasisEditor.Tests;

public sealed class MameVersionParsingTests
{
    [Fact]
    public void MamedevParser_Parses_OfficialBinaryPackagesPattern()
    {
        const string html = "<h1>MAME 0.287 Official Binary Packages</h1>";
        var parsed = MameVersionParsing.TryParseLatestFromMamedevReleasePage(html);
        Assert.Equal("0287", parsed);
    }

    [Fact]
    public void MamedevParser_Parses_LatestReleasePattern()
    {
        const string html = "The latest official MAME release is version 0.286.";
        var parsed = MameVersionParsing.TryParseLatestFromMamedevReleasePage(html);
        Assert.Equal("0286", parsed);
    }

    [Fact]
    public void GithubParser_ChoosesHighestAcrossPatterns()
    {
        const string html = "<span>MAME 0.286</span><a href='/tag/mame0287'>mame0287</a>";
        var parsed = MameVersionParsing.TryParseLatestFromGitHubReleases(html);
        Assert.Equal("0287", parsed);
    }

    [Fact]
    public void NormalizeSortAndDedupe_NormalizesDedupesAndSortsNumerically()
    {
        var ordered = MameVersionParsing.NormalizeSortAndDedupe(["287", "0286", "mame0287", "0258"]);
        Assert.Equal(["0287", "0286", "0258"], ordered);
    }

    [Fact]
    public void MamedevParser_ReturnsNullForMalformedInput()
    {
        const string html = "<html>No release text here</html>";
        var parsed = MameVersionParsing.TryParseLatestFromMamedevReleasePage(html);
        Assert.Null(parsed);
    }
}
