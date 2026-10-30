using Avalonia;
using Avalonia.Media;
using Xunit;

namespace OasisEditor.Avalonia.Tests;

[Collection("Avalonia headless UI")]
public sealed class LockIconTests
{
    [Fact]
    public void ApprovedPadlockRetainsShackleBodyKeyholeAndRoundedStrokes()
    {
        HeadlessTestHost.Run(() =>
        {
            var group = new DrawingGroup();
            using (var context = group.Open()) EditorIcons.Draw(context, EditorIconKind.Lock, Brushes.White);
            var shapes = group.Children.OfType<GeometryDrawing>().ToArray(); Assert.Equal(3, shapes.Length);
            Assert.Equal(new Rect(5, 2, 6, 5), shapes[0].Geometry!.Bounds);
            var body = shapes[1].Geometry!; Assert.Equal(new Rect(3, 7, 10, 7), body.Bounds);
            Assert.False(body.FillContains(new Point(3.2, 7.5))); Assert.True(body.FillContains(new Point(3.4, 7.5)));
            Assert.Equal(new Rect(8, 10, 0, 1.5), shapes[2].Geometry!.Bounds);
            Assert.All(shapes, shape =>
            {
                Assert.Null(shape.Brush); Assert.Equal(1.25, shape.Pen!.Thickness);
                Assert.Equal(PenLineCap.Round, shape.Pen.LineCap); Assert.Equal(PenLineJoin.Round, shape.Pen.LineJoin);
            });
        });
    }
}
