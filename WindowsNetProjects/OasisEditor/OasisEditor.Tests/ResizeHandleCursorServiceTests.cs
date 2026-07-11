using Xunit;

namespace OasisEditor.Tests;

public sealed class ResizeHandleCursorServiceTests
{
    [Theory]
    [InlineData(ResizeHandleKind.Left, ResizeHandleCursorKind.SizeWE)]
    [InlineData(ResizeHandleKind.Right, ResizeHandleCursorKind.SizeWE)]
    [InlineData(ResizeHandleKind.Top, ResizeHandleCursorKind.SizeNS)]
    [InlineData(ResizeHandleKind.Bottom, ResizeHandleCursorKind.SizeNS)]
    [InlineData(ResizeHandleKind.TopLeft, ResizeHandleCursorKind.SizeNWSE)]
    [InlineData(ResizeHandleKind.BottomRight, ResizeHandleCursorKind.SizeNWSE)]
    [InlineData(ResizeHandleKind.TopRight, ResizeHandleCursorKind.SizeNESW)]
    [InlineData(ResizeHandleKind.BottomLeft, ResizeHandleCursorKind.SizeNESW)]
    [InlineData(ResizeHandleKind.None, ResizeHandleCursorKind.Arrow)]
    public void GetCursorKind_MapsResizeHandlesToConventionalDirections(ResizeHandleKind handleKind, ResizeHandleCursorKind expected)
    {
        Assert.Equal(expected, ResizeHandleCursorService.GetCursorKind(handleKind));
    }
}
