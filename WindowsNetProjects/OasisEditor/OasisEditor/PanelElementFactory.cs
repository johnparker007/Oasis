using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace OasisEditor;

internal static class PanelElementFactory
{
    public static readonly DependencyProperty ElementNameProperty =
        DependencyProperty.RegisterAttached(
            "ElementName",
            typeof(string),
            typeof(PanelElementFactory),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty ElementKindProperty =
        DependencyProperty.RegisterAttached(
            "ElementKind",
            typeof(PanelElementKind),
            typeof(PanelElementFactory),
            new PropertyMetadata(PanelElementKind.Unknown));

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
        FrameworkElement? visual = element.ElementKind switch
        {
            PanelElementKind.Rectangle => CreateRectangleVisual(element),
            PanelElementKind.Image => CreateImageVisual(element),
            PanelElementKind.Background => CreatePlaceholderComponentVisual(element, "Background"),
            PanelElementKind.Lamp => CreatePlaceholderComponentVisual(element, "Lamp"),
            PanelElementKind.Reel => CreatePlaceholderComponentVisual(element, "Reel"),
            PanelElementKind.SevenSegment => CreatePlaceholderComponentVisual(element, "7 Segment"),
            PanelElementKind.Alpha => CreatePlaceholderComponentVisual(element, "Alpha"),
            _ => null
        };

        if (visual is null)
        {
            return null;
        }

        SetElementName(visual, element.Name);
        SetElementKind(visual, element.ElementKind);
        return visual;
    }

    public static PanelElementFile? CreateElementFromVisual(FrameworkElement child)
    {
        var attachedKind = GetElementKind(child);
        var kind = attachedKind != PanelElementKind.Unknown
            ? attachedKind
            : child switch
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
        var elementName = GetElementName(child);
        return new PanelElementFile
        {
            ObjectId = objectId,
            Name = string.IsNullOrWhiteSpace(elementName)
                ? Panel2DDocumentStorage.CreateDefaultElementName(kind, objectId)
                : elementName.Trim(),
            Kind = Panel2DDocumentStorage.SerializeElementKind(kind),
            X = Canvas.GetLeft(child),
            Y = Canvas.GetTop(child),
            Width = child.Width,
            Height = child.Height
        };
    }

    public static string GetElementName(DependencyObject dependencyObject)
    {
        return (string)dependencyObject.GetValue(ElementNameProperty);
    }

    public static void SetElementName(DependencyObject dependencyObject, string value)
    {
        dependencyObject.SetValue(ElementNameProperty, value);
    }

    public static PanelElementKind GetElementKind(DependencyObject dependencyObject)
    {
        return (PanelElementKind)dependencyObject.GetValue(ElementKindProperty);
    }

    public static void SetElementKind(DependencyObject dependencyObject, PanelElementKind value)
    {
        dependencyObject.SetValue(ElementKindProperty, value);
    }

    private static FrameworkElement CreateRectangleVisual(PanelElementFile element)
    {
        return new Rectangle
        {
            Uid = element.ObjectId,
            Width = element.Width <= 0 ? NewRectangleWidth : element.Width,
            Height = element.Height <= 0 ? NewRectangleHeight : element.Height
        };
    }

    private static FrameworkElement CreateImageVisual(PanelElementFile element)
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

    private static FrameworkElement CreatePlaceholderComponentVisual(PanelElementFile element, string label)
    {
        var width = element.Width <= 0 ? NewRectangleWidth : element.Width;
        var height = element.Height <= 0 ? NewRectangleHeight : element.Height;
        var border = new Border
        {
            Uid = element.ObjectId,
            Width = width,
            Height = height,
            BorderThickness = new Thickness(1),
            BorderBrush = Brushes.SlateGray,
            Background = Brushes.Transparent,
            Child = new TextBlock
            {
                Text = label,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = Brushes.LightSteelBlue
            }
        };

        return border;
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
