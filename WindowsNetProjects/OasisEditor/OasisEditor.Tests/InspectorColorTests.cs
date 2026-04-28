using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using Xunit;

namespace OasisEditor.Tests;

public sealed class InspectorColorTests
{
    [Theory]
    [InlineData("#112233", 255, 0x11, 0x22, 0x33)]
    [InlineData("112233", 255, 0x11, 0x22, 0x33)]
    [InlineData("#AA112233", 0xAA, 0x11, 0x22, 0x33)]
    [InlineData("AA112233", 0xAA, 0x11, 0x22, 0x33)]
    public void InspectorColorHex_TryParse_AcceptsSupportedFormats(string value, byte a, byte r, byte g, byte b)
    {
        Assert.True(InspectorColorHex.TryParse(value, out var color));
        Assert.Equal(a, color.A);
        Assert.Equal(r, color.R);
        Assert.Equal(g, color.G);
        Assert.Equal(b, color.B);
    }

    [Fact]
    public void InspectorColorHex_TryParse_RejectsInvalid()
    {
        Assert.False(InspectorColorHex.TryParse("#XYZXYZ", out _));
    }

    [Fact]
    public void InspectorColorHex_Format_PreservesOpaqueAsRgb()
    {
        var color = Color.FromArgb(255, 0x11, 0x22, 0x33);
        Assert.Equal("#112233", InspectorColorHex.Format(color));
    }

    [Fact]
    public void InspectorColorHex_Format_UsesArgbWhenTransparent()
    {
        var color = Color.FromArgb(0xAA, 0x11, 0x22, 0x33);
        Assert.Equal("#AA112233", InspectorColorHex.Format(color));
    }

    [Fact]
    public void InspectorColorProperty_Commit_UsesCanonicalHex()
    {
        string? committed = null;
        var row = new InspectorColorPropertyViewModel("On Color", "Type-specific", "#FFFFFF", commit: value =>
        {
            committed = value;
            return null;
        });

        row.HexValue = "aa112233";
        row.Commit();

        Assert.Equal("#AA112233", committed);
        Assert.Equal("#AA112233", row.HexValue);
    }

    [Fact]
    public void InspectorColorProperty_Commit_InvalidHexRestoresValue()
    {
        var commits = new List<string?>();
        var row = new InspectorColorPropertyViewModel("On Color", "Type-specific", "#112233", commit: value =>
        {
            commits.Add(value);
            return null;
        });

        row.HexValue = "invalid";
        row.Commit();

        Assert.Equal("Enter a valid hex color (RRGGBB or AARRGGBB).", row.ErrorText);
        Assert.Equal("#112233", row.HexValue);
        Assert.Empty(commits);
    }

    [Fact]
    public void InspectorColorProperty_Commit_BlankAllowedCommitsNull()
    {
        string? committed = "seed";
        var row = new InspectorColorPropertyViewModel("On Color", "Type-specific", "#112233", allowEmpty: true, commit: value =>
        {
            committed = value;
            return null;
        });

        row.HexValue = "  ";
        row.Commit();

        Assert.Null(committed);
        Assert.Equal(string.Empty, row.HexValue);
    }

    [Fact]
    public void InspectorColorProperty_SelectingSameColor_DoesNotCommit()
    {
        var commits = new List<string?>();
        var row = new InspectorColorPropertyViewModel("On Color", "Type-specific", "#112233", commit: value =>
        {
            commits.Add(value);
            return null;
        });

        row.SelectedColor = Color.FromArgb(255, 0x11, 0x22, 0x33);

        Assert.Empty(commits);
    }
}
