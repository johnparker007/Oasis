using System.Collections.Concurrent;
using SkiaSharp;

namespace OasisEditor.Rendering;

internal sealed class LabelElementRenderer : IPanelElementRenderer
{
    private static readonly ConcurrentDictionary<string, SKTypeface> TypefaceCache = new(StringComparer.OrdinalIgnoreCase);

    public PanelElementKind Kind => PanelElementKind.Label;

    public void Render(in PanelElementRenderContext context, PanelElementModel element)
    {
        if (!element.IsVisible || string.IsNullOrWhiteSpace(element.DisplayText))
        {
            return;
        }

        if (element.LampNumber is int lampNumber
            && context.RuntimeState.GetLampIntensity(MachineObjectReference.Lamp(lampNumber)) <= 0d)
        {
            return;
        }

        var bounds = SKRect.Create((float)element.X, (float)element.Y, (float)element.Width, (float)element.Height);
        if (bounds.Width <= 0f || bounds.Height <= 0f)
        {
            return;
        }

        using var textPaint = new SKPaint
        {
            Color = SkiaColorParser.ParseOrDefault(element.TextColorHex, SKColors.White),
            IsAntialias = true,
            TextSize = (float)LampElementRenderer.ParseFontSize(element.TextBoxFontSize),
            Typeface = ResolveTypeface(element.TextBoxFontName, element.TextBoxFontStyle)
        };

        var textBounds = LampElementRenderer.GetTextBounds(bounds);
        var fontMetrics = textPaint.FontMetrics;
        var measuredLineHeight = Math.Abs(fontMetrics.Ascent) + Math.Abs(fontMetrics.Descent) + Math.Abs(fontMetrics.Leading);
        var lineHeight = Math.Max(1d, measuredLineHeight > 0f ? measuredLineHeight : textPaint.TextSize * 1.2d);
        var wrapWidth = LampElementRenderer.GetEffectiveWrapWidth(element.DisplayText, textBounds.Width, bounds.Width, textPaint);
        var lines = LampElementRenderer.WrapTextToPixelWidth(element.DisplayText, wrapWidth, textPaint);
        if (lines.Count == 0)
        {
            return;
        }

        var totalTextHeight = lines.Count * lineHeight;
        var baselineOffset = Math.Abs(fontMetrics.Ascent) > 0f ? Math.Abs(fontMetrics.Ascent) : textPaint.TextSize;
        var startY = textBounds.Top + ((textBounds.Height - totalTextHeight) / 2d) + baselineOffset;
        for (var lineIndex = 0; lineIndex < lines.Count; lineIndex++)
        {
            var line = lines[lineIndex];
            if (string.IsNullOrEmpty(line.Text))
            {
                continue;
            }

            var x = textBounds.Left + ((textBounds.Width - line.Width) / 2d);
            var y = startY + (lineIndex * lineHeight);
            context.Canvas.DrawText(line.Text, (float)x, (float)y, textPaint);
        }
    }

    private static SKTypeface ResolveTypeface(string? fontName, string? fontStyle)
    {
        var family = string.IsNullOrWhiteSpace(fontName) ? "Tahoma" : fontName.Trim();
        var styleToken = string.IsNullOrWhiteSpace(fontStyle) ? "Regular" : fontStyle.Trim();
        var cacheKey = $"{family}|{styleToken}";
        return TypefaceCache.GetOrAdd(cacheKey, _ =>
        {
            var weight = styleToken.Contains("Bold", StringComparison.OrdinalIgnoreCase)
                ? SKFontStyleWeight.Bold
                : SKFontStyleWeight.Normal;
            var slant = styleToken.Contains("Italic", StringComparison.OrdinalIgnoreCase)
                ? SKFontStyleSlant.Italic
                : SKFontStyleSlant.Upright;
            var style = new SKFontStyle(weight, SKFontStyleWidth.Normal, slant);
            return SKTypeface.FromFamilyName(family, style)
                ?? SKTypeface.FromFamilyName("Tahoma", style)
                ?? SKTypeface.Default;
        });
    }
}
