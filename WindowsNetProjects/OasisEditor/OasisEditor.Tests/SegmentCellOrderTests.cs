using MfmeFmlDecoder.src.Model;
using MfmeFmlDecoder.src.Model.Component;
using OasisEditor.Features.FmlImport;
using Xunit;

namespace OasisEditor.Tests;

public sealed class SegmentCellOrderTests
{
    private static readonly int[] Cells = Enumerable.Range(0, 16).ToArray();

    [Fact]
    public void NormalAlpha_UsesSourceCellsFromLeftToRight()
    {
        Assert.Equal(Cells, VisualOrder(reversed: false));
    }

    [Fact]
    public void ReversedAlpha_UsesSourceCellsFromRightToLeft()
    {
        Assert.Equal(Cells.Reverse(), VisualOrder(reversed: true));
    }

    [Fact]
    public void ImpactMfmeAlpha_PreservesReversedFlagAndAppliesItOnce()
    {
        var alpha = new Alpha { X = 0, Y = 0, Width = 160, Height = 20 };
        alpha.Booleans["Reversed"] = true;

        var imported = Assert.Single(new FmlToOasisMapper()
            .Map(new Layout([alpha]), new Dictionary<FmlDecodedImageKey, string>()).Elements);

        Assert.True(imported.IsReversed);
        Assert.Equal(Cells.Reverse(), VisualOrder(imported.IsReversed == true));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EditorAndPlayerOrderingContractsMatch(bool reversed)
    {
        var editor = Enumerable.Range(0, 16)
            .Select(i => SegmentCellOrder.SourceIndexForVisualCell(i, 16, reversed));
        var player = Enumerable.Range(0, 16)
            .Select(sourceIndex => (VisualIndex: reversed ? 15 - sourceIndex : sourceIndex, SourceIndex: sourceIndex))
            .OrderBy(cell => cell.VisualIndex)
            .Select(cell => cell.SourceIndex);

        Assert.Equal(editor, player);
    }

    private static int[] VisualOrder(bool reversed) => Enumerable.Range(0, 16)
        .Select(i => Cells[SegmentCellOrder.SourceIndexForVisualCell(i, 16, reversed)])
        .ToArray();
}
