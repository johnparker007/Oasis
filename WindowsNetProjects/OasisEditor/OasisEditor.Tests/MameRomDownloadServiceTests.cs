using Xunit;
using OasisEditor;

namespace OasisEditor.Tests;

public sealed class MameRomDownloadServiceTests
{
    [Fact]
    public void BuildDownloadUrl_UsesArchiveOrgPattern()
    {
        var sut = new MameRomDownloadService();

        var url = sut.BuildDownloadUrl("mpu4");

        Assert.Equal("https://archive.org/download/MAME215RomsOnlyMerged/mpu4.zip", url);
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
}
