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

        Assert.Equal("https://archive.org/download/CentralArquivistaArcade/mpu4.zip", url);
    }

    [Fact]
    public void BuildRomArchiveFileName_RejectsWhitespace()
    {
        var sut = new MameRomDownloadService();

        Assert.Throws<ArgumentException>(new Action(() => sut.BuildRomArchiveFileName("   ")));
    }
}
