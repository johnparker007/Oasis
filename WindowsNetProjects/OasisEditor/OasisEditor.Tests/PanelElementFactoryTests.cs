using System;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows.Controls;
using System.Windows.Media;
using Xunit;

namespace OasisEditor.Tests;

public sealed class PanelElementFactoryTests
{
    [Theory]
    [InlineData((int)PanelElementKind.Background)]
    [InlineData((int)PanelElementKind.Lamp)]
    [InlineData((int)PanelElementKind.Reel)]
    [InlineData((int)PanelElementKind.SevenSegment)]
    [InlineData((int)PanelElementKind.Alpha)]
    public void CreateVisualFromElement_ForNativeKinds_PreservesElementKindForRoundTrip(int kindValue)
    {
        RunInSta(() =>
        {
            var kind = (PanelElementKind)kindValue;
            var source = new PanelElementFile
            {
                ObjectId = "obj-1",
                Name = "Element",
                Kind = Panel2DDocumentStorage.SerializeElementKind(kind),
                X = 10,
                Y = 20,
                Width = 100,
                Height = 40
            };

            var visual = PanelElementFactory.CreateVisualFromElement(source);

            Assert.NotNull(visual);
            Assert.Equal(kind, PanelElementFactory.GetElementKind(visual!));

            var roundTrip = PanelElementFactory.CreateElementFromVisual(visual!);
            Assert.NotNull(roundTrip);
            Assert.Equal(kind, roundTrip!.ElementKind);
        });
    }

    [Fact]
    public void CreateVisualFromElement_LampWithoutImage_UsesLampNumberAndDisplayText()
    {
        RunInSta(() =>
        {
            var source = new PanelElementFile
            {
                ObjectId = "lamp-1",
                Name = "Lamp 9",
                Kind = Panel2DDocumentStorage.SerializeElementKind(PanelElementKind.Lamp),
                Width = 90,
                Height = 40,
                DisplayNumber = 9,
                DisplayText = "HOLD",
                OnColorHex = "#FF00CC00"
            };

            var visual = PanelElementFactory.CreateVisualFromElement(source);

            var border = Assert.IsType<Border>(visual);
            var stack = Assert.IsType<StackPanel>(border.Child);
            var title = Assert.IsType<TextBlock>(stack.Children[0]);
            var detail = Assert.IsType<TextBlock>(stack.Children[1]);

            Assert.Equal("Lamp 9", title.Text);
            Assert.Equal("HOLD", detail.Text);
            Assert.Equal(Visibility.Visible, detail.Visibility);
        });
    }

    [Fact]
    public void CreateVisualFromElement_BackgroundWithoutImage_UsesConfiguredColor()
    {
        RunInSta(() =>
        {
            var source = new PanelElementFile
            {
                ObjectId = "bg-1",
                Name = "Background",
                Kind = Panel2DDocumentStorage.SerializeElementKind(PanelElementKind.Background),
                Width = 320,
                Height = 180,
                OffColorHex = "#FF112233"
            };

            var visual = PanelElementFactory.CreateVisualFromElement(source);

            var border = Assert.IsType<Border>(visual);
            var brush = Assert.IsType<SolidColorBrush>(border.Background);
            Assert.Equal(Color.FromArgb(0xFF, 0x11, 0x22, 0x33), brush.Color);
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
