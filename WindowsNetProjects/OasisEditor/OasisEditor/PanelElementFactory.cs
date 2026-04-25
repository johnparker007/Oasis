using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace OasisEditor;

internal static class PanelElementFactory
{
    public const double NewRectangleWidth = 180;
    public const double NewRectangleHeight = 120;
    public const double NewImageWidth = 220;
    public const double NewImageHeight = 140;

    public static PanelElementFile CreateRectangleElement(Point canvasPoint)
    {
        var x = Math.Max(0, canvasPoint.X - (NewRectangleWidth / 2));
        var y = Math.Max(0, canvasPoint.Y - (NewRectangleHeight / 2));
        var objectId = Guid.NewGuid().ToString("N");
        return new PanelElementFile
        {
            ObjectId = objectId,
            Name = Panel2DDocumentStorage.CreateDefaultElementName(PanelElementKind.Rectangle, objectId),
            Kind = Panel2DDocumentStorage.SerializeElementKind(PanelElementKind.Rectangle),
            X = x,
            Y = y,
            Width = NewRectangleWidth,
            Height = NewRectangleHeight
        };
    }

    public static PanelElementFile CreateImageElement(Point canvasPoint)
    {
        var x = Math.Max(0, canvasPoint.X - (NewImageWidth / 2));
        var y = Math.Max(0, canvasPoint.Y - (NewImageHeight / 2));
        var objectId = Guid.NewGuid().ToString("N");
        return new PanelElementFile
        {
            ObjectId = objectId,
            Name = Panel2DDocumentStorage.CreateDefaultElementName(PanelElementKind.Image, objectId),
            Kind = Panel2DDocumentStorage.SerializeElementKind(PanelElementKind.Image),
            X = x,
            Y = y,
            Width = NewImageWidth,
            Height = NewImageHeight
        };
    }

    public static FrameworkElement? CreateVisualFromElement(PanelElementFile element)
    {
        if (element.ElementKind == PanelElementKind.Rectangle)
        {
            return new Rectangle
            {
                Uid = element.ObjectId,
                Width = element.Width <= 0 ? NewRectangleWidth : element.Width,
                Height = element.Height <= 0 ? NewRectangleHeight : element.Height
            };
        }

        if (element.ElementKind == PanelElementKind.Image)
        {
            return new Image
            {
                Uid = element.ObjectId,
                Width = element.Width <= 0 ? NewImageWidth : element.Width,
                Height = element.Height <= 0 ? NewImageHeight : element.Height,
                Stretch = Stretch.Fill,
                Source = CreatePlaceholderImageSource()
            };
        }

        return null;
    }

    public static PanelElementFile? CreateElementFromVisual(FrameworkElement child)
    {
        var kind = child switch
        {
            Rectangle => PanelElementKind.Rectangle,
            Image => PanelElementKind.Image,
            _ => PanelElementKind.Unknown
        };

        if (kind == PanelElementKind.Unknown)
        {
            return null;
        }

        var objectId = string.IsNullOrWhiteSpace(child.Uid) ? Guid.NewGuid().ToString("N") : child.Uid.Trim();
        return new PanelElementFile
        {
            ObjectId = objectId,
            Name = Panel2DDocumentStorage.CreateDefaultElementName(kind, objectId),
            Kind = Panel2DDocumentStorage.SerializeElementKind(kind),
            X = Canvas.GetLeft(child),
            Y = Canvas.GetTop(child),
            Width = child.Width,
            Height = child.Height
        };
    }

    private static ImageSource CreatePlaceholderImageSource()
    {
        const int pixelWidth = 220;
        const int pixelHeight = 140;
        const int bytesPerPixel = 4;
        var pixels = new byte[pixelWidth * pixelHeight * bytesPerPixel];

        for (var y = 0; y < pixelHeight; y++)
        {
            for (var x = 0; x < pixelWidth; x++)
            {
                var index = ((y * pixelWidth) + x) * bytesPerPixel;
                var isGridLine = x % 22 == 0 || y % 20 == 0;
                byte red = 58;
                byte green = 80;
                byte blue = 118;

                if (isGridLine)
                {
                    red = 92;
                    green = 118;
                    blue = 163;
                }

                pixels[index] = blue;
                pixels[index + 1] = green;
                pixels[index + 2] = red;
                pixels[index + 3] = 255;
            }
        }

        var bitmap = BitmapSource.Create(
            pixelWidth,
            pixelHeight,
            96,
            96,
            PixelFormats.Bgra32,
            null,
            pixels,
            pixelWidth * bytesPerPixel);
        bitmap.Freeze();
        return bitmap;
    }
}
