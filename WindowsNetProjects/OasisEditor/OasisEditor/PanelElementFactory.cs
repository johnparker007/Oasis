using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Globalization;

namespace OasisEditor;

internal static class PanelElementFactory
{
    private static string? _projectDirectoryPath;
    private static readonly Dictionary<string, FontFamily> MfmeFontFamilies = new(StringComparer.OrdinalIgnoreCase);
    private static readonly PanelRuntimeState DefaultRuntimeState = new();

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
    public const double NewVfdDotMatrixWidth = 480;
    public const double NewVfdDotMatrixHeight = 80;

    public static string? ProjectDirectoryPath
    {
        get => _projectDirectoryPath;
        set => _projectDirectoryPath = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

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

    public static PanelElementFile CreateVfdDotMatrixElement(Point canvasPoint)
    {
        var x = Math.Max(0, canvasPoint.X - (NewVfdDotMatrixWidth / 2));
        var y = Math.Max(0, canvasPoint.Y - (NewVfdDotMatrixHeight / 2));
        var objectId = Guid.NewGuid().ToString("N");
        return new PanelElementFile
        {
            ObjectId = objectId,
            Name = Panel2DDocumentStorage.CreateDefaultElementName(PanelElementKind.VfdDotMatrix, objectId),
            Kind = Panel2DDocumentStorage.SerializeElementKind(PanelElementKind.VfdDotMatrix),
            X = x,
            Y = y,
            Width = NewVfdDotMatrixWidth,
            Height = NewVfdDotMatrixHeight,
            OnColorHex = "#FF4040"
        };
    }


    public static FrameworkElement? CreateVisualFromElement(PanelElementFile element)
    {
        return CreateVisualFromElement(element, DefaultRuntimeState);
    }

    public static FrameworkElement? CreateVisualFromElement(PanelElementFile element, PanelRuntimeState runtimeState)
    {
        FrameworkElement? visual = element.ElementKind switch
        {
            PanelElementKind.Rectangle => CreateRectangleVisual(element),
            PanelElementKind.Image => CreateImageVisual(element),
            PanelElementKind.Background => CreateBackgroundVisual(element),
            PanelElementKind.Lamp => CreateLampVisual(element, runtimeState),
            PanelElementKind.Reel => CreateReelVisual(element),
            PanelElementKind.SevenSegment => CreateSevenSegmentVisual(element),
            PanelElementKind.Alpha => CreateAlphaVisual(element),
            PanelElementKind.VfdDotMatrix => CreateVfdDotMatrixVisual(element),
            PanelElementKind.Label => CreateLabelVisual(element),
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

    private static Rectangle CreateRectangleVisual(PanelElementFile element)
    {
        return new Rectangle
        {
            Uid = element.ObjectId,
            Width = element.Width <= 0 ? NewRectangleWidth : element.Width,
            Height = element.Height <= 0 ? NewRectangleHeight : element.Height
        };
    }

    private static Image CreateImageVisual(PanelElementFile element)
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

    private static Border CreateBackgroundVisual(PanelElementFile element)
    {
        var hasImage = TryCreateImageSource(element.AssetPath, out var source);
        var surface = CreatePlaceholderComponentVisual(
            element,
            "Background",
            hasImage ? null : element.OnColorHex ?? element.OffColorHex);
        if (hasImage)
        {
            surface.Child = new Image
            {
                Stretch = Stretch.Fill,
                Source = source
            };
        }

        return surface;
    }

    private static Border CreateLampVisual(PanelElementFile element, PanelRuntimeState runtimeState)
    {
        var hasGraphic = TryCreateImageSource(element.AssetPath, out var source);
        var isLampOn = runtimeState.IsLampTestActive
            && !string.IsNullOrWhiteSpace(runtimeState.LampTestObjectId)
            && string.Equals(element.ObjectId, runtimeState.LampTestObjectId, StringComparison.Ordinal);

        if (hasGraphic)
        {
            var width = element.Width <= 0 ? NewRectangleWidth : element.Width;
            var height = element.Height <= 0 ? NewRectangleHeight : element.Height;
            return new Border
            {
                Uid = element.ObjectId,
                Width = width,
                Height = height,
                BorderThickness = new Thickness(1),
                BorderBrush = Brushes.SlateGray,
                Background = Brushes.Transparent,
                Child = new Image
                {
                    Stretch = Stretch.Fill,
                    Source = source,
                    Opacity = isLampOn ? 1d : 0d
                }
            };
        }

        return CreatePlaceholderComponentVisual(
            element,
            element.DisplayText ?? string.Empty,
            isLampOn ? element.OnColorHex : element.OffColorHex ?? element.OnColorHex,
            null,
            element.TextColorHex,
            CreateFontSettings(element.TextBoxFontName, element.TextBoxFontStyle, element.TextBoxFontSize));
    }

    private static Border CreateReelVisual(PanelElementFile element)
    {
        var label = element.DisplayNumber.HasValue ? $"Reel {element.DisplayNumber.Value}" : "Reel";
        var surface = CreatePlaceholderComponentVisual(element, label, "#1E293B");
        if (TryCreateImageSource(element.AssetPath, out var source))
        {
            var width = element.Width <= 0 ? NewRectangleWidth : element.Width;
            var height = element.Height <= 0 ? NewRectangleHeight : element.Height;
            var visibleScale = ResolveVisibleScale(element.VisibleScale);

            var reelImage = new Image
            {
                Stretch = Stretch.Fill,
                Source = source,
                Width = width,
                Height = height / visibleScale,
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            var wrappedReelImage = new Image
            {
                Stretch = Stretch.Fill,
                Source = source,
                Width = reelImage.Width,
                Height = reelImage.Height,
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            var reelCanvas = new Canvas
            {
                Width = width,
                Height = reelImage.Height
            };
            Canvas.SetTop(reelImage, 0d);
            Canvas.SetTop(wrappedReelImage, reelImage.Height);
            reelCanvas.Children.Add(reelImage);
            reelCanvas.Children.Add(wrappedReelImage);

            surface.Child = new Grid
            {
                ClipToBounds = true,
                Children =
                {
                    reelCanvas
                }
            };
        }

        return surface;
    }


    private static double ResolveVisibleScale(double? visibleScale)
    {
        if (!visibleScale.HasValue || double.IsNaN(visibleScale.Value) || double.IsInfinity(visibleScale.Value))
        {
            return 1d;
        }

        return Math.Clamp(visibleScale.Value, 0.01d, 1d);
    }

    private static Border CreateSevenSegmentVisual(PanelElementFile element)
    {
        var width = element.Width <= 0 ? NewRectangleWidth : element.Width;
        var height = element.Height <= 0 ? NewRectangleHeight : element.Height;

        return new Border
        {
            Uid = element.ObjectId,
            Width = width,
            Height = height,
            BorderThickness = new Thickness(1),
            BorderBrush = Brushes.SlateGray,
            Background = TryCreateBrush("#111827", Brushes.Black),
            Padding = new Thickness(4),
            Child = new SevenSegmentDisplayVisual
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                CellCount = 1,
                DisplayText = string.IsNullOrWhiteSpace(element.DisplayText) ? null : element.DisplayText,
                LitBrush = TryCreateBrush(element.OnColorHex, Brushes.Red),
                UnlitBrush = TryCreateBrush(element.OffColorHex, new SolidColorBrush(Color.FromArgb(32, 255, 0, 0))),
                ShowDecimalPoint = element.ShowDecimalPoint ?? true,
                ShowCommaTail = element.ShowCommaTail ?? false
            }
        };
    }

    private static Border CreateAlphaVisual(PanelElementFile element)
    {
        var width = element.Width <= 0 ? NewRectangleWidth : element.Width;
        var height = element.Height <= 0 ? NewRectangleHeight : element.Height;

        return new Border
        {
            Uid = element.ObjectId,
            Width = width,
            Height = height,
            BorderThickness = new Thickness(1),
            BorderBrush = Brushes.SlateGray,
            Background = TryCreateBrush("#111827", Brushes.Black),
            Padding = new Thickness(4),
            Child = new AlphaSixteenSegmentDisplayVisual(element.SegmentDisplayType)
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                CellCount = 16,
                DisplayText = element.DisplayText,
                LitBrush = TryCreateBrush(element.OnColorHex, Brushes.Cyan),
                UnlitBrush = TryCreateBrush(element.OffColorHex, new SolidColorBrush(Color.FromArgb(32, 0, 255, 255))),
                ShowDecimalPoint = element.ShowDecimalPoint ?? true,
                ShowCommaTail = element.ShowCommaTail ?? false
            }
        };
    }

    private static Border CreateVfdDotMatrixVisual(PanelElementFile element)
    {
        return CreatePlaceholderComponentVisual(
            element,
            "VFD Dot Matrix 96 x 8",
            element.OnColorHex ?? "#220909");
    }

    private static Border CreateLabelVisual(PanelElementFile element)
    {
        var text = string.IsNullOrWhiteSpace(element.DisplayText) ? "Label" : element.DisplayText;
        return CreatePlaceholderComponentVisual(
            element,
            text,
            null,
            null,
            element.TextColorHex,
            CreateFontSettings(element.TextBoxFontName, element.TextBoxFontStyle, element.TextBoxFontSize));
    }

    private static Border CreatePlaceholderComponentVisual(PanelElementFile element, string label)
    {
        return CreatePlaceholderComponentVisual(element, label, null, null);
    }

    private static Border CreatePlaceholderComponentVisual(
        PanelElementFile element,
        string label,
        string? backgroundColorHex,
        string? detailText = null,
        string? labelColorHex = null,
        LampFontSettings? labelFontSettings = null)
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
            Background = TryCreateBrush(backgroundColorHex, Brushes.Transparent),
            Child = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Children =
                {
                    new TextBlock
                    {
                        Text = label,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        TextAlignment = TextAlignment.Center,
                        TextWrapping = TextWrapping.WrapWithOverflow,
                        Foreground = TryCreateBrush(labelColorHex, Brushes.LightSteelBlue),
                        FontFamily = labelFontSettings?.FontFamily ?? new FontFamily("Segoe UI"),
                        FontStyle = labelFontSettings?.FontStyle ?? FontStyles.Normal,
                        FontWeight = labelFontSettings?.FontWeight ?? FontWeights.Normal,
                        FontSize = labelFontSettings?.FontSize ?? 12
                    },
                    new TextBlock
                    {
                        Text = detailText ?? string.Empty,
                        Margin = new Thickness(0, 4, 0, 0),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        TextAlignment = TextAlignment.Center,
                        TextWrapping = TextWrapping.WrapWithOverflow,
                        Foreground = Brushes.Gainsboro,
                        FontSize = 10,
                        Visibility = string.IsNullOrWhiteSpace(detailText) ? Visibility.Collapsed : Visibility.Visible
                    }
                }
            }
        };

        return border;
    }


    public static Brush TryCreateBrushForRuntime(string? colorHex, Brush fallback)
    {
        return TryCreateBrush(colorHex, fallback);
    }

    public static ImageSource? TryCreateImageSourceForRuntime(string? assetPath)
    {
        return TryCreateImageSource(assetPath, out var source) ? source : null;
    }

    private static Brush TryCreateBrush(string? colorHex, Brush fallback)
    {
        if (string.IsNullOrWhiteSpace(colorHex))
        {
            return fallback;
        }

        try
        {
            var color = (Color)ColorConverter.ConvertFromString(colorHex);
            return new SolidColorBrush(color);
        }
        catch
        {
            return fallback;
        }
    }

    private const double MfmeLampTextScaleFactor = 1.33333333d;

    private static LampFontSettings CreateFontSettings(string? fontName, string? fontStyle, string? fontSize)
    {
        var styleToken = string.IsNullOrWhiteSpace(fontStyle) ? "Regular" : fontStyle.Trim();
        var family = ResolveFontFamily(fontName, styleToken);
        var style = styleToken.Contains("Italic", StringComparison.OrdinalIgnoreCase) ? FontStyles.Italic : FontStyles.Normal;
        var weight = styleToken.Contains("Bold", StringComparison.OrdinalIgnoreCase) ? FontWeights.Bold : FontWeights.Normal;
        var size = double.TryParse(fontSize, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) && parsed > 0
            ? parsed
            : 8d;
        size *= MfmeLampTextScaleFactor;
        return new LampFontSettings(family, style, weight, size);
    }

    private static FontFamily ResolveFontFamily(string? fontName, string styleToken)
    {
        if (string.IsNullOrWhiteSpace(fontName))
        {
            return new FontFamily("Tahoma");
        }

        var trimmedName = fontName.Trim();
        if (TryResolveMfmeFontFamily(trimmedName, styleToken, out var mfmeFontFamily))
        {
            return mfmeFontFamily;
        }

        return new FontFamily(trimmedName);
    }

    private static bool TryResolveMfmeFontFamily(string fontName, string styleToken, out FontFamily fontFamily)
    {
        var cacheKey = $"{fontName}|{styleToken}";
        if (MfmeFontFamilies.TryGetValue(cacheKey, out fontFamily!))
        {
            return true;
        }

        var fontsDirectory = System.IO.Path.Combine(AppContext.BaseDirectory, "MfmeFonts");
        if (!System.IO.Directory.Exists(fontsDirectory))
        {
            return false;
        }

        foreach (var fontPath in System.IO.Directory.EnumerateFiles(fontsDirectory, "*.ttf"))
        {
            var fontUri = new Uri(fontPath, UriKind.Absolute);
            GlyphTypeface glyphTypeface;
            try
            {
                glyphTypeface = new GlyphTypeface(fontUri);
            }
            catch
            {
                continue;
            }

            var familyNameMatches = glyphTypeface.Win32FamilyNames.Values.Any(v => string.Equals(v, fontName, StringComparison.OrdinalIgnoreCase));
            if (!familyNameMatches)
            {
                continue;
            }

            var wantsBold = styleToken.Contains("Bold", StringComparison.OrdinalIgnoreCase);
            var faceName = glyphTypeface.FaceNames.Values.FirstOrDefault() ?? string.Empty;
            var isBoldFace = faceName.Contains("Bold", StringComparison.OrdinalIgnoreCase);
            if (wantsBold != isBoldFace)
            {
                continue;
            }

            var fontFolderUri = new Uri(System.IO.Path.GetDirectoryName(fontPath)! + System.IO.Path.DirectorySeparatorChar, UriKind.Absolute);
            fontFamily = new FontFamily(fontFolderUri, $"./#{fontName}");
            MfmeFontFamilies[cacheKey] = fontFamily;
            return true;
        }

        return false;
    }

    private sealed record LampFontSettings(FontFamily FontFamily, FontStyle FontStyle, FontWeight FontWeight, double FontSize);

    private static bool TryCreateImageSource(string? assetPath, out ImageSource? source)
    {
        source = null;
        if (string.IsNullOrWhiteSpace(assetPath))
        {
            return false;
        }

        if (!TryResolveAssetPath(assetPath, out var resolvedPath))
        {
            return false;
        }

        if (!System.IO.File.Exists(resolvedPath))
        {
            return false;
        }

        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.UriSource = new Uri(resolvedPath, UriKind.Absolute);
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.EndInit();
        bitmap.Freeze();
        source = bitmap;
        return true;
    }

    private static bool TryResolveAssetPath(string assetPath, out string resolvedPath)
    {
        resolvedPath = string.Empty;
        var candidate = assetPath.Trim();
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        if (System.IO.Path.IsPathRooted(candidate))
        {
            resolvedPath = candidate;
            return true;
        }

        if (string.IsNullOrWhiteSpace(ProjectDirectoryPath))
        {
            return false;
        }

        var relativePath = candidate
            .Replace('/', System.IO.Path.DirectorySeparatorChar)
            .Replace('\\', System.IO.Path.DirectorySeparatorChar);
        resolvedPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(ProjectDirectoryPath, relativePath));
        return true;
    }

    private static BitmapSource CreatePlaceholderImageSource()
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
