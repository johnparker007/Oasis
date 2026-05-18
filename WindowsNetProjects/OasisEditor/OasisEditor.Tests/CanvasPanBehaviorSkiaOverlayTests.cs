using System;
using System.Threading;
using System.Windows.Controls;
using Xunit;

namespace OasisEditor.Tests;

public sealed class CanvasPanBehaviorSkiaOverlayTests
{
    [Fact]
    public void EnablingSkiaRuntimeRendering_DoesNotClearDocumentBeforeLayoutBindingArrives()
    {
        RunInSta(() =>
        {
            var layoutJson = Panel2DDocumentStorage.SerializeLayout(
            [
                new PanelElementFile
                {
                    ObjectId = "lamp-1",
                    Name = "Lamp 1",
                    Kind = Panel2DDocumentStorage.SerializeElementKind(PanelElementKind.Lamp),
                    X = 10,
                    Y = 20,
                    Width = 30,
                    Height = 40,
                    DisplayText = "Hold"
                }
            ]);
            var document = new DocumentTabViewModel(EditorDocument.CreatePanel2DStub("Panel"), layoutJson);
            var canvas = new Canvas { DataContext = document };

            CanvasPanBehavior.SetIsSkiaRuntimeRenderingEnabled(canvas, true);

            Assert.Equal(layoutJson, document.PanelLayoutJson);
            Assert.Empty(canvas.Children);
        });
    }

    [Fact]
    public void EnablingSkiaRuntimeRendering_AfterLayoutBinding_RemovesWpfRuntimeVisuals()
    {
        RunInSta(() =>
        {
            var layoutJson = Panel2DDocumentStorage.SerializeLayout(
            [
                new PanelElementFile
                {
                    ObjectId = "lamp-1",
                    Name = "Lamp 1",
                    Kind = Panel2DDocumentStorage.SerializeElementKind(PanelElementKind.Lamp),
                    X = 10,
                    Y = 20,
                    Width = 30,
                    Height = 40,
                    DisplayText = "Hold"
                }
            ]);
            var document = new DocumentTabViewModel(EditorDocument.CreatePanel2DStub("Panel"), layoutJson);
            var canvas = new Canvas { DataContext = document };
            CanvasPanBehavior.SetPanelLayoutJson(canvas, layoutJson);
            var wpfRuntimeVisual = Assert.IsType<Border>(Assert.Single(canvas.Children));
            Assert.NotNull(wpfRuntimeVisual.Child);

            CanvasPanBehavior.SetIsSkiaRuntimeRenderingEnabled(canvas, true);

            Assert.Empty(canvas.Children);
            Assert.Equal("lamp-1", Assert.Single(document.GetPanelElements()).ObjectId);
        });
    }

    [Fact]
    public void PanelLayoutJsonChanged_WithSkiaRuntimeRenderingEnabled_CreatesNoWpfRuntimeVisuals()
    {
        RunInSta(() =>
        {
            var layoutJson = Panel2DDocumentStorage.SerializeLayout(
            [
                new PanelElementFile
                {
                    ObjectId = "lamp-1",
                    Name = "Lamp 1",
                    Kind = Panel2DDocumentStorage.SerializeElementKind(PanelElementKind.Lamp),
                    X = 10,
                    Y = 20,
                    Width = 30,
                    Height = 40,
                    DisplayText = "Hold"
                }
            ]);
            var document = new DocumentTabViewModel(EditorDocument.CreatePanel2DStub("Panel"), layoutJson);
            var canvas = new Canvas { DataContext = document };
            CanvasPanBehavior.SetIsSkiaRuntimeRenderingEnabled(canvas, true);

            CanvasPanBehavior.SetPanelLayoutJson(canvas, layoutJson);

            Assert.Empty(canvas.Children);
            Assert.Equal("lamp-1", Assert.Single(document.GetPanelElements()).ObjectId);
        });
    }

    private static void RunInSta(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception is not null)
        {
            throw exception;
        }
    }
}
