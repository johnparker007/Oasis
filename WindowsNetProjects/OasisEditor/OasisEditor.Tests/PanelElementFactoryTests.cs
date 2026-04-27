using System;
using System.Runtime.ExceptionServices;
using System.Threading;
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
