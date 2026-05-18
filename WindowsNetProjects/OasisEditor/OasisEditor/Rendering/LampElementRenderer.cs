using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Linq;
using SkiaSharp;

namespace OasisEditor.Rendering;

internal sealed class LampElementRenderer : IPanelElementRenderer
{
    private static readonly ConcurrentDictionary<string, SKTypeface> TypefaceCache = new(StringComparer.OrdinalIgnoreCase);

    public PanelElementKind Kind => PanelElementKind.Lamp;

    public void Render(in PanelElementRenderContext context, PanelElementModel element)
    {
        var bounds = SKRect.Create((float)element.X, (float)element.Y, (float)element.Width, (float)element.Height);
        if (bounds.Width <= 0f || bounds.Height <= 0f)
        {
            return;
        }

        var intensity = context.RuntimeState.GetLampIntensity(element.ObjectId);
        var onColor = SkiaColorParser.ParseOrDefault(element.OnColorHex, SKColors.Red);
        var offColor = SkiaColorParser.ParseOrDefault(element.OffColorHex, new SKColor(40, 0, 0));
        var fill = Lerp(offColor, onColor, intensity);

        using var paint = new SKPaint
        {
            Color = fill,
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };

        context.Canvas.DrawRoundRect(bounds, 4f, 4f, paint);

        var displayText = element.DisplayText;
        if (string.IsNullOrWhiteSpace(displayText))
        {
            return;
        }

        var fontSize = ParseFontSize(element.TextBoxFontSize);
        using var textPaint = new SKPaint
        {
            Color = SkiaColorParser.ParseOrDefault(element.TextColorHex, SKColors.White),
            IsAntialias = true,
            TextSize = (float)fontSize,
            Typeface = ResolveTypeface(element.TextBoxFontName, element.TextBoxFontStyle)
        };

        var charWidth = Math.Max(1d, fontSize * 0.55d);
        var lineHeight = Math.Max(1d, fontSize * 1.2d);
        var layout = RuntimeTextLayout.Layout(
            displayText,
            bounds.Width,
            charWidth,
            lineHeight,
            RuntimeTextHorizontalAlignment.Center);

        if (layout.Lines.Count == 0)
        {
            return;
        }

        var totalTextHeight = layout.Lines.Count * lineHeight;
        var startY = bounds.Top + ((bounds.Height - totalTextHeight) / 2d) + fontSize;
        foreach (var line in layout.Lines)
        {
            if (string.IsNullOrEmpty(line.Text))
            {
                continue;
            }

            var x = bounds.Left + line.X;
            var y = startY + line.Y;
            context.Canvas.DrawText(line.Text, (float)x, (float)y, textPaint);
        }
    }

    private static double ParseFontSize(string? value)
    {
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) && parsed > 0d)
        {
            return parsed * 1.33333333d;
        }

        return 10.66666664d;
    }

    private static SKTypeface ResolveTypeface(string? fontName, string? fontStyle)
    {
        var family = string.IsNullOrWhiteSpace(fontName) ? "Tahoma" : fontName.Trim();
        var styleToken = string.IsNullOrWhiteSpace(fontStyle) ? "Regular" : fontStyle.Trim();
        var cacheKey = $"{family}|{styleToken}";
        if (TypefaceCache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        var weight = styleToken.Contains("Bold", StringComparison.OrdinalIgnoreCase)
            ? SKFontStyleWeight.Bold
            : SKFontStyleWeight.Normal;
        var slant = styleToken.Contains("Italic", StringComparison.OrdinalIgnoreCase)
            ? SKFontStyleSlant.Italic
            : SKFontStyleSlant.Upright;
        var style = new SKFontStyle(weight, SKFontStyleWidth.Normal, slant);

        if (TryResolveMfmeTypeface(family, styleToken, out var mfmeTypeface))
        {
            TypefaceCache[cacheKey] = mfmeTypeface;
            return mfmeTypeface;
        }

        var resolved = SKTypeface.FromFamilyName(family, style)
            ?? SKTypeface.FromFamilyName("Tahoma", style)
            ?? SKTypeface.Default;
        TypefaceCache[cacheKey] = resolved;
        return resolved;
    }

    private static bool TryResolveMfmeTypeface(string family, string styleToken, out SKTypeface typeface)
    {
        var fontsDirectory = Path.Combine(AppContext.BaseDirectory, "MfmeFonts");
        if (!Directory.Exists(fontsDirectory))
        {
            typeface = null!;
            return false;
        }

        var wantsBold = styleToken.Contains("Bold", StringComparison.OrdinalIgnoreCase);
        foreach (var fontPath in Directory.EnumerateFiles(fontsDirectory, "*.ttf"))
        {
            SKTypeface? candidate = null;
            try
            {
                candidate = SKTypeface.FromFile(fontPath);
            }
            catch
            {
                continue;
            }

            if (candidate is null)
            {
                continue;
            }

            var familyMatches = string.Equals(candidate.FamilyName, family, StringComparison.OrdinalIgnoreCase);
            if (!familyMatches)
            {
                candidate.Dispose();
                continue;
            }

            var isBoldFace = candidate.FontWeight >= (int)SKFontStyleWeight.SemiBold;
            if (wantsBold != isBoldFace)
            {
                candidate.Dispose();
                continue;
            }

            typeface = candidate;
            return true;
        }

        typeface = null!;
        return false;
    }

    private static SKColor Lerp(SKColor from, SKColor to, double t)
    {
        var clamped = Math.Clamp(t, 0d, 1d);

        byte Blend(byte a, byte b)
        {
            return (byte)Math.Clamp(Math.Round(a + ((b - a) * clamped)), 0d, 255d);
        }

        return new SKColor(
            Blend(from.Red, to.Red),
            Blend(from.Green, to.Green),
            Blend(from.Blue, to.Blue),
            Blend(from.Alpha, to.Alpha));
    }
}
