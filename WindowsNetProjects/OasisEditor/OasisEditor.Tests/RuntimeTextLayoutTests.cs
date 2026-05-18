using OasisEditor.Rendering;
using Xunit;

namespace OasisEditor.Tests;

public sealed class RuntimeTextLayoutTests
{
    [Fact]
    public void Layout_WrapsTextByMaxCharacters()
    {
        var layout = RuntimeTextLayout.Layout(
            "ONE TWO THREE",
            maxWidth: 7,
            charWidth: 1,
            lineHeight: 10,
            RuntimeTextHorizontalAlignment.Left);

        Assert.Collection(layout.Lines,
            line => Assert.Equal("ONE TWO", line.Text),
            line => Assert.Equal("THREE", line.Text));
    }

    [Fact]
    public void Layout_BreaksLongWords()
    {
        var layout = RuntimeTextLayout.Layout(
            "ABCDEFGHIJK",
            maxWidth: 4,
            charWidth: 1,
            lineHeight: 10,
            RuntimeTextHorizontalAlignment.Left);

        Assert.Collection(layout.Lines,
            line => Assert.Equal("ABCD", line.Text),
            line => Assert.Equal("EFGH", line.Text),
            line => Assert.Equal("IJK", line.Text));
    }

    [Fact]
    public void Layout_AppliesRightAlignment()
    {
        var layout = RuntimeTextLayout.Layout(
            "AB",
            maxWidth: 10,
            charWidth: 2,
            lineHeight: 10,
            RuntimeTextHorizontalAlignment.Right);

        var line = Assert.Single(layout.Lines);
        Assert.Equal(6d, line.X, 6);
    }

    [Fact]
    public void Layout_AssignsLineYUsingLineHeight()
    {
        var layout = RuntimeTextLayout.Layout(
            "A\nB",
            maxWidth: 10,
            charWidth: 1,
            lineHeight: 12,
            RuntimeTextHorizontalAlignment.Left);

        Assert.Equal(0d, layout.Lines[0].Y, 6);
        Assert.Equal(12d, layout.Lines[1].Y, 6);
    }
}
