using OasisEditor.Rendering;
using SkiaSharp;
using Xunit;

namespace OasisEditor.Tests;

public sealed class SkiaColorParserTests
{
    [Fact]
    public void ParseOrDefault_ParsesValidColor()
    {
        var color = SkiaColorParser.ParseOrDefault("#FF00AA", SKColors.Black);

        Assert.Equal((byte)0xFF, color.Red);
        Assert.Equal((byte)0x00, color.Green);
        Assert.Equal((byte)0xAA, color.Blue);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-color")]
    public void ParseOrDefault_UsesFallbackForInvalidValues(string? value)
    {
        var fallback = SKColors.CornflowerBlue;

        var color = SkiaColorParser.ParseOrDefault(value, fallback);

        Assert.Equal(fallback, color);
    }
}
