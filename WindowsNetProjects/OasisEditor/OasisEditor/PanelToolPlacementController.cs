using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace OasisEditor;

internal static class PanelToolPlacementController
{
    public static bool TryHandlePlacement(
        FrameworkElement canvas,
        MouseButtonEventArgs eventArgs,
        bool isRectangleToolActive,
        bool isImageToolActive,
        Action<FrameworkElement, Commands.ICommand> executeCanvasMutation)
    {
        if (canvas is not Canvas panelCanvas)
        {
            return false;
        }

        if (panelCanvas.DataContext is not DocumentTabViewModel tab)
        {
            return false;
        }

        if (!isRectangleToolActive && !isImageToolActive)
        {
            return false;
        }

        var canvasPoint = GetCanvasPoint(panelCanvas, eventArgs);
        if (isRectangleToolActive)
        {
            var rectangle = PanelElementFactory.CreateRectangleElement(canvasPoint);
            executeCanvasMutation(panelCanvas, CanvasMutationCommands.CreateAddRectangleCommand(tab.DocumentId, tab, rectangle));
            return true;
        }

        if (isImageToolActive)
        {
            var image = PanelElementFactory.CreateImageElement(canvasPoint);
            executeCanvasMutation(panelCanvas, CanvasMutationCommands.CreateAddImageCommand(tab.DocumentId, tab, image));
            return true;
        }

        return false;
    }

    private static Point GetCanvasPoint(Canvas panelCanvas, MouseButtonEventArgs eventArgs)
    {
        var clickPosition = eventArgs.GetPosition(panelCanvas.Parent as IInputElement ?? panelCanvas);
        var (scale, translate) = CanvasPanZoomBehavior.EnsureTransformGroup(panelCanvas);
        return new Point(
            (clickPosition.X - translate.X) / scale.ScaleX,
            (clickPosition.Y - translate.Y) / scale.ScaleY);
    }
}
