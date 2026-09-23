using Xunit;

namespace OasisEditor.Tests;

public sealed class ReelDocumentTests
{
    [Fact]
    public void Version1_RoundTripsReusablePhysicalDefinition()
    {
        var source = ReelDocument.Create("JPM Standard Reel") with { DiameterMm = 290, WidthMm = 70 };
        var json = ReelDocumentStorage.Serialize(source);
        Assert.True(ReelDocumentStorage.TryRead(json, out var result, out var error), error);
        Assert.Equal(source, result);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void ReaderRejectsNonCurrentVersion(int version)
    {
        var json = ReelDocumentStorage.Serialize(ReelDocument.Create("Reel")).Replace("\"version\": 1", $"\"version\": {version}");
        Assert.False(ReelDocumentStorage.TryRead(json, out _, out _));
    }

    [Theory]
    [InlineData(0, 70)]
    [InlineData(290, 0)]
    [InlineData(-1, 70)]
    public void ReaderRejectsInvalidDimensions(double diameter, double width)
    {
        var reel = ReelDocument.Create("Reel") with { DiameterMm = diameter, WidthMm = width };
        Assert.Throws<InvalidOperationException>(() => ReelDocumentStorage.Serialize(reel));
    }
}
