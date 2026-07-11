using OasisEditor.Rendering;
using SkiaSharp;
using Xunit;

namespace OasisEditor.Tests;

public sealed class MfmeTypefaceResolverTests
{
    [Fact]
    public void Resolve_BundledMfmeFontByActualFamilyName_ReturnsBundledFamily()
    {
        var typeface = MfmeTypefaceResolver.Resolve("MFME", "Regular");

        Assert.Equal("MFME", typeface.FamilyName, ignoreCase: true);
        Assert.False(typeface.FontWeight >= (int)SKFontStyleWeight.SemiBold);
    }

    [Fact]
    public void Resolve_RegularRequest_SelectsRegularBundledFace()
    {
        var typeface = MfmeTypefaceResolver.Resolve("MFME", "Regular");

        Assert.Equal("MFME", typeface.FamilyName, ignoreCase: true);
        Assert.False(typeface.FontWeight >= (int)SKFontStyleWeight.SemiBold);
    }

    [Fact]
    public void Resolve_BoldRequest_SelectsBoldBundledFace()
    {
        var typeface = MfmeTypefaceResolver.Resolve("Lithograph", "Bold");

        Assert.Equal("Lithograph", typeface.FamilyName, ignoreCase: true);
        Assert.True(typeface.FontWeight >= (int)SKFontStyleWeight.SemiBold);
    }

    [Fact]
    public void Resolve_MissingFamily_FallsBackSafely()
    {
        var exception = Record.Exception(() => MfmeTypefaceResolver.Resolve("Definitely Missing MFME Font", "Bold Italic"));

        Assert.Null(exception);
    }
}
