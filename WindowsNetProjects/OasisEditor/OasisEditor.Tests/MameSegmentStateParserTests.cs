using OasisEditor;
using Xunit;

namespace OasisEditor.Tests;

public sealed class MameSegmentStateParserTests
{
    [Fact]
    public void TryParse_VfdLine_ParsesCellAndMask()
    {
        var parser = new MameSegmentStateParser();
        var ok = parser.TryParse("vfd12 = 769", out var cellId, out var mask);
        Assert.True(ok);
        Assert.Equal(12, cellId);
        Assert.Equal(769, mask);
    }
}
