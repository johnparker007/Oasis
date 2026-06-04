using System.IO;
using Xunit;
using OasisEditor;

namespace OasisEditor.Tests;

public sealed class MameRomDownloadServiceTests
{
    [Fact]
    public void BuildDownloadUrl_UsesDefaultDownloadRootUrl()
    {
        var sut = new MameRomDownloadService();

        var url = sut.BuildDownloadUrl("mpu4");

        var defaultRootUrl = MameRomDownloadService.DefaultDownloadRootUrl;
        var expectedRootUrl = defaultRootUrl.EndsWith("/", StringComparison.Ordinal)
            ? defaultRootUrl
            : defaultRootUrl + "/";

        Assert.Equal($"{expectedRootUrl}mpu4.zip", url);
    }

    [Fact]
    public void BuildRomArchiveFileName_RejectsWhitespace()
    {
        var sut = new MameRomDownloadService();

        Assert.Throws<ArgumentException>(new Action(() => sut.BuildRomArchiveFileName("   ")));
    }

    [Fact]
    public void BuildDownloadUrl_UsesConfiguredBaseUrlAnd7zExtension()
    {
        var sut = new MameRomDownloadService
        {
            DownloadRootUrl = "https://example.com/roms",
            ArchiveExtension = ".zip"
        };

        var url = sut.BuildDownloadUrl("mpu4");

        Assert.Equal("https://example.com/roms/mpu4.zip", url);
    }

    [Fact]
    public void BuildRomArchiveFileName_RejectsUnsupportedExtension()
    {
        var sut = new MameRomDownloadService
        {
            ArchiveExtension = ".rar"
        };

        Assert.Throws<ArgumentException>(() => sut.BuildRomArchiveFileName("mpu4"));
    }

    [Fact]
    public void GetLocalRomArchivePath_UsesConfiguredDirectoryAndExtension()
    {
        var sut = new MameRomDownloadService
        {
            LocalRomSourceDirectory = "C:/MAME_ROMS_0261/",
            LocalRomArchiveExtension = ".7z"
        };

        var path = sut.GetLocalRomArchivePath("mpu4");

        Assert.Equal(Path.Combine("C:/MAME_ROMS_0261/", "mpu4.7z"), path);
    }
}
