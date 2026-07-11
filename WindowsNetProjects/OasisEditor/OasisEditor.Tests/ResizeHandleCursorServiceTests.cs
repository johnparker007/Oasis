using Xunit;

namespace OasisEditor.Tests;

public sealed class ResizeHandleCursorServiceTests
{
    [Theory]
    [InlineData((int)ResizeHandleKind.Left, (int)ResizeHandleCursorKind.SizeWE)]
    [InlineData((int)ResizeHandleKind.Right, (int)ResizeHandleCursorKind.SizeWE)]
    [InlineData((int)ResizeHandleKind.Top, (int)ResizeHandleCursorKind.SizeNS)]
    [InlineData((int)ResizeHandleKind.Bottom, (int)ResizeHandleCursorKind.SizeNS)]
    [InlineData((int)ResizeHandleKind.TopLeft, (int)ResizeHandleCursorKind.SizeNWSE)]
    [InlineData((int)ResizeHandleKind.BottomRight, (int)ResizeHandleCursorKind.SizeNWSE)]
    [InlineData((int)ResizeHandleKind.TopRight, (int)ResizeHandleCursorKind.SizeNESW)]
    [InlineData((int)ResizeHandleKind.BottomLeft, (int)ResizeHandleCursorKind.SizeNESW)]
    [InlineData((int)ResizeHandleKind.None, (int)ResizeHandleCursorKind.Arrow)]
    public void GetCursorKind_MapsResizeHandlesToConventionalDirections(int handleKindValue, int expectedValue)
    {
        var handleKind = (ResizeHandleKind)handleKindValue;
        var expected = (ResizeHandleCursorKind)expectedValue;

        Assert.Equal(expected, ResizeHandleCursorService.GetCursorKind(handleKind));
    }
}
