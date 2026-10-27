using OasisEditor.SegmentDisplays;
using Xunit;

namespace OasisEditor.Tests;

public sealed class SegmentDisplayCanonicalGeometryTests
{
    [Fact]
    public void SevenSegmentGeometry_UsesCanonicalBitsAndNormalizedBounds()
    {
        var definition = SevenSegmentCanonicalGeometry.Definition;

        Assert.Equal("sevenSegment", definition.Topology);
        Assert.Equal(7, definition.Segments.Count);
        Assert.Equal(Enumerable.Range(0, 7), definition.Segments.Select(segment => segment.SegmentIndex));
        Assert.Equal(7, definition.DecimalPoint?.SegmentIndex);
        Assert.Equal(["A", "B", "C", "D", "E", "F", "G"], definition.Segments.Select(segment => segment.SegmentName).ToArray());
        foreach (var shape in definition.Segments.Append(definition.DecimalPoint!))
        {
            Assert.All(shape.Polygon, point =>
            {
                Assert.InRange(point.X, 0d, 1d);
                Assert.InRange(point.Y, 0d, 1d);
            });
        }
    }
}
