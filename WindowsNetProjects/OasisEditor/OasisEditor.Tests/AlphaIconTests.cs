using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;
using Xunit;

namespace OasisEditor.Avalonia.Tests;

[Collection("Avalonia headless UI")]
public sealed class AlphaIconTests
{
    [Fact]
    public void ApprovedAlphaArtworkRetainsFrameStrokeCapsAndDotPattern()
    {
        HeadlessTestHost.Run(() =>
        {
            GeometryDrawing[] Draw(EditorIconKind kind)
            {
                var group = new DrawingGroup();
                using (var context = group.Open()) EditorIcons.Draw(context, kind, Brushes.White);
                return group.Children.OfType<GeometryDrawing>().ToArray();
            }
            var segmented = Draw(EditorIconKind.SegmentedAlpha);
            var matrix = Draw(EditorIconKind.DotMatrixAlpha);
            Assert.Equal(2, segmented.Length);
            foreach (var drawing in new[] { segmented[0], matrix[0] })
            {
                var frame = drawing.Geometry!;
                Assert.Equal(new Rect(1, 3, 14, 10), frame.Bounds);
                Assert.False(frame.FillContains(new Point(1.2, 3.5)));
                Assert.True(frame.FillContains(new Point(1.4, 3.5)));
                Assert.Equal(1.25, drawing.Pen!.Thickness); Assert.Equal(PenLineJoin.Round, drawing.Pen.LineJoin);
                Assert.Null(drawing.Brush);
            }
            Assert.Equal(1, segmented[1].Pen!.Thickness);
            Assert.Equal(PenLineCap.Flat, segmented[1].Pen!.LineCap);
            Assert.Equal(PenLineJoin.Round, segmented[1].Pen!.LineJoin);
            Assert.Null(segmented[1].Brush);
            // Rows from the approved A1 artwork, left-to-right for each character.
            string[] rows = ["010 010", "101 110", "111 010", "101 010", "101 111"];
            var expected = rows.Sum(row => row.Count(c => c == '1'));
            Assert.Equal(expected, matrix.Length - 1);
            for (var y = 0; y < rows.Length; y++)
                for (var letter = 0; letter < 2; letter++)
                    for (var x = 0; x < 3; x++)
                    {
                        var cx = 3.8 + letter * 5.6 + x * 1.4; var cy = 5.2 + y * 1.4;
                        var dot = matrix.Skip(1).SingleOrDefault(d => Math.Abs(d.Geometry!.Bounds.Center.X - cx) < .001 && Math.Abs(d.Geometry.Bounds.Center.Y - cy) < .001);
                        if (rows[y].Split(' ')[letter][x] == '0') { Assert.Null(dot); continue; }
                        Assert.NotNull(dot); Assert.False(dot.Geometry!.FillContains(dot.Geometry.Bounds.TopLeft));
                        Assert.True(dot.Geometry.FillContains(dot.Geometry.Bounds.Center));
                        Assert.Equal(.96, dot.Geometry!.Bounds.Width, 8); Assert.Equal(.96, dot.Geometry.Bounds.Height, 8);
                        Assert.Null(dot.Pen); Assert.Same(Brushes.White, dot.Brush);
                    }
            Assert.All(Draw(EditorIconKind.SevenSegment), d => { Assert.Null(d.Pen); Assert.NotNull(d.Brush); });
        });
    }

    [Fact]
    public async Task PanelEntriesUseApprovedAlphaTypesAlongsideExistingNumericDisplay()
    {
        await HeadlessTestHost.RunAsync(async () =>
        {
            var root = Path.Combine(Path.GetTempPath(), "OasisAlphaIcons-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
            try
            {
                var session = new AuthoringSession(root); session.CreateProject("Alpha"); session.CreatePanel("Displays");
                var alpha = session.AddSpecialistElement("alpha", 20, 20);
                var matrix = session.AddSpecialistElement("vfdDotMatrix", 20, 100);
                var numeric = session.AddSpecialistElement("sevenSegment", 20, 200); session.Save();
                using var capture = new WorkspaceCapture(session); await capture.View.SynchronizeAssetDocumentsAsync(); capture.Capture();
                foreach (var (id, kind) in new[] { (alpha, EditorIconKind.SegmentedAlpha), (matrix, EditorIconKind.DotMatrixAlpha), (numeric, EditorIconKind.SevenSegment) })
                {
                    var row = capture.View.GetVisualDescendants().OfType<StackPanel>().Single(p => Equals(p.Tag, id));
                    Assert.Equal(kind, Assert.Single(row.GetVisualDescendants().OfType<EditorIcon>().Where(i => i.Kind is not EditorIconKind.Lock and not EditorIconKind.Hidden)).Kind);
                }
                Assert.Equal(EditorIconKind.SegmentedAlpha, EditorIcons.ForComponent("alphaDisplay"));
                Assert.Equal(EditorIconKind.SevenSegment, EditorIcons.ForComponent("sevenSegmentDisplay"));
            }
            finally { Directory.Delete(root, true); }
        });
    }
}
