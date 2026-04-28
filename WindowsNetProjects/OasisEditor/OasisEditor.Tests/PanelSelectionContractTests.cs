using System;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using Xunit;

namespace OasisEditor.Tests;

public sealed class PanelSelectionContractTests
{
    [Theory]
    [InlineData((int)PanelElementKind.Background)]
    [InlineData((int)PanelElementKind.Lamp)]
    [InlineData((int)PanelElementKind.Reel)]
    [InlineData((int)PanelElementKind.SevenSegment)]
    public void TryCreateFromVisual_ForAttachedKinds_ReturnsSelectable(int kindValue)
    {
        RunInSta(() =>
        {
            var kind = (PanelElementKind)kindValue;
            var element = new PanelElementFile
            {
                ObjectId = "selectable-1",
                Name = "Selectable",
                Kind = Panel2DDocumentStorage.SerializeElementKind(kind),
                X = 20,
                Y = 30,
                Width = 110,
                Height = 45
            };

            var visual = PanelElementFactory.CreateVisualFromElement(element);
            Assert.NotNull(visual);

            var success = PanelSelectionContract.TryCreateFromVisual(visual!, out var selectable);

            Assert.True(success);
            Assert.Equal(kind, selectable.ElementKind);
            Assert.Equal("selectable-1", selectable.ObjectId);
        });
    }

    [Fact]
    public void TryCreateFromVisual_WithoutAttachedKind_UsesFallbackVisualType()
    {
        RunInSta(() =>
        {
            var rectangle = new System.Windows.Shapes.Rectangle
            {
                Uid = "rect-1",
                Width = 10,
                Height = 20
            };

            var success = PanelSelectionContract.TryCreateFromVisual(rectangle, out var selectable);

            Assert.True(success);
            Assert.Equal(PanelElementKind.Rectangle, selectable.ElementKind);
        });
    }

    private static void RunInSta(Action action)
    {
        Exception? captured = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                captured = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (captured is not null)
        {
            ExceptionDispatchInfo.Capture(captured).Throw();
        }
    }
}
