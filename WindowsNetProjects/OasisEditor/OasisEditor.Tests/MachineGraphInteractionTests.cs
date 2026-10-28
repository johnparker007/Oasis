using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace OasisEditor.Avalonia.Tests;

[Collection("Avalonia headless UI")]
public sealed class MachineGraphInteractionTests
{
    [Theory]
    [InlineData("Dark")]
    [InlineData("Light")]
    public void BackgroundPansAndZoomsEvenWhenCanvasHasMovedOutsideViewport(string theme)
    {
        HeadlessTestHost.Run(() =>
        {
            var graph = new MachineCompositionGraphView();
            graph.SetGraph(new MachineOverview([new("machine", "Machine", "Test machine", "", null, false, 40, 40)], [], []));
            var window = new Window { Content = graph, Width = 800, Height = 600,
                RequestedThemeVariant = theme == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light };
            window.Show();
            try
            {
                window.UpdateLayout();
                Dispatcher.UIThread.RunJobs();
                var canvas = graph.GetVisualDescendants().OfType<Canvas>().Single();
                var scroller = graph.GetVisualDescendants().OfType<ScrollViewer>().Single(s => ReferenceEquals(s.Content, canvas));
                var activated = 0;
                graph.NodeActivated += _ => activated++;
                var card = canvas.Children.OfType<Border>().Single();
                var cardPoint = card.TranslatePoint(new Point(15, 15), window)!.Value;
                window.MouseDown(cardPoint, MouseButton.Left);
                window.MouseUp(cardPoint, MouseButton.Left);
                Assert.Equal(1, activated);
                var background = scroller.TranslatePoint(new Point(25, 25), window)!.Value;
                Assert.DoesNotContain(window.InputHitTest(background) as Visual,
                    canvas.Children.SelectMany(child => child.GetVisualDescendants().Prepend(child)));
                var before = Assert.IsType<MatrixTransform>(canvas.RenderTransform).Matrix;
                var scrollOffset = scroller.Offset;

                window.MouseWheel(background, new Vector(0, -1));
                Dispatcher.UIThread.RunJobs();
                var zoomed = Assert.IsType<MatrixTransform>(canvas.RenderTransform).Matrix;
                Assert.Equal(before.M11 / 1.1, zoomed.M11, 6);
                Assert.Equal(scrollOffset, scroller.Offset);

                // Move the entire transformed canvas away from the pointer. Input
                // must still belong to the stationary viewport background.
                var end = background + new Vector(1600, 1200);
                window.MouseDown(background, MouseButton.Middle);
                window.MouseMove(end);
                window.MouseUp(end, MouseButton.Middle);
                var panned = Assert.IsType<MatrixTransform>(canvas.RenderTransform).Matrix;
                Assert.Equal(zoomed.M31 + 1600, panned.M31, 6);
                Assert.Equal(zoomed.M32 + 1200, panned.M32, 6);
                window.MouseWheel(background, new Vector(0, 1));
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(zoomed.M11 * 1.1, Assert.IsType<MatrixTransform>(canvas.RenderTransform).Matrix.M11, 6);
                Assert.Equal(scrollOffset, scroller.Offset);
            }
            finally { window.Close(); }
        });
    }
}
