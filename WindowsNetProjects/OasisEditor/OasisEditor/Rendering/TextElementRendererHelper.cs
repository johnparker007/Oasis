using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using SkiaSharp;

namespace OasisEditor.Rendering;

internal static class TextElementRendererHelper
{
    private static readonly ConcurrentDictionary<string, SKTypeface> TypefaceCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<TextLayoutCacheKey, IReadOnlyList<PixelTextLine>> TextLayoutCache = new();

    internal static double ParseFontSize(string? value)
    {
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) && parsed > 0d)
        {
            return parsed * 1.33333333d;
        }

        return 10.66666664d;
    }

    internal static SKTypeface ResolveTypeface(string? fontName, string? fontStyle)
    {
        var family = string.IsNullOrWhiteSpace(fontName) ? "Tahoma" : fontName.Trim();
        var styleToken = string.IsNullOrWhiteSpace(fontStyle) ? "Regular" : fontStyle.Trim();
        var cacheKey = $"{family}|{styleToken}";
        if (TypefaceCache.TryGetValue(cacheKey, out var cached)) return cached;

        var weight = styleToken.Contains("Bold", StringComparison.OrdinalIgnoreCase) ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal;
        var slant = styleToken.Contains("Italic", StringComparison.OrdinalIgnoreCase) ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright;
        var style = new SKFontStyle(weight, SKFontStyleWidth.Normal, slant);

        if (TryResolveMfmeTypeface(family, styleToken, out var mfmeTypeface))
        {
            TypefaceCache[cacheKey] = mfmeTypeface;
            return mfmeTypeface;
        }

        var resolved = SKTypeface.FromFamilyName(family, style) ?? SKTypeface.FromFamilyName("Tahoma", style) ?? SKTypeface.Default;
        TypefaceCache[cacheKey] = resolved;
        return resolved;
    }

    internal static SKRect GetInsetTextBounds(in SKRect bounds)
    {
        var horizontalInset = Math.Min(bounds.Width * 0.1f, 8f);
        var verticalInset = Math.Min(bounds.Height * 0.1f, 6f);
        var width = Math.Max(1f, bounds.Width - (horizontalInset * 2f));
        var height = Math.Max(1f, bounds.Height - (verticalInset * 2f));
        return SKRect.Create(bounds.Left + horizontalInset, bounds.Top + verticalInset, width, height);
    }

    internal static int DrawCenteredWrappedText(SKCanvas canvas, string text, SKRect localBounds, SKRect textBounds, SKPaint textPaint)
    {
        var fontSize = textPaint.TextSize;
        var fontMetrics = textPaint.FontMetrics;
        var measuredLineHeight = Math.Abs(fontMetrics.Ascent) + Math.Abs(fontMetrics.Descent) + Math.Abs(fontMetrics.Leading);
        var lineHeight = Math.Max(1d, measuredLineHeight > 0f ? measuredLineHeight : fontSize * 1.2d);
        var wrapWidth = GetEffectiveWrapWidth(text, textBounds.Width, localBounds.Width, textPaint);
        var cacheKey = new TextLayoutCacheKey(text, textPaint.Typeface?.FamilyName ?? string.Empty, textPaint.TextSize, wrapWidth);
        var lines = GetOrCreateTextLayout(cacheKey, text, wrapWidth, textPaint);
        if (lines.Count == 0) return 0;

        var drawn = 0;
        var totalTextHeight = lines.Count * lineHeight;
        var baselineOffset = Math.Abs(fontMetrics.Ascent) > 0f ? Math.Abs(fontMetrics.Ascent) : fontSize;
        var startY = textBounds.Top + ((textBounds.Height - totalTextHeight) / 2d) + baselineOffset;
        for (var lineIndex = 0; lineIndex < lines.Count; lineIndex++)
        {
            var line = lines[lineIndex];
            if (string.IsNullOrEmpty(line.Text)) continue;
            var x = textBounds.Left + ((textBounds.Width - line.Width) / 2d);
            var y = startY + (lineIndex * lineHeight);
            canvas.DrawText(line.Text, (float)x, (float)y, textPaint);
            drawn++;
        }
        return drawn;
    }

    internal static List<PixelTextLine> WrapTextToPixelWidth(string text, double maxWidth, SKPaint paint)
    {
        if (maxWidth <= 0d) return [];
        var paragraphs = text.Replace("\r\n", "\n").Split('\n');
        var lines = new List<PixelTextLine>();
        foreach (var paragraph in paragraphs)
        {
            var wrapped = WrapParagraphToPixelWidth(paragraph, maxWidth, paint);
            if (wrapped.Count == 0) lines.Add(new PixelTextLine(string.Empty, 0f)); else lines.AddRange(wrapped);
        }
        return lines;
    }

    internal static double GetEffectiveWrapWidth(string text, double insetWidth, double elementWidth, SKPaint paint)
    {
        var epsilon = 1.5d;
        var fullWidth = Math.Max(insetWidth, elementWidth);
        return paint.MeasureText(text ?? string.Empty) <= fullWidth + epsilon ? fullWidth + epsilon : insetWidth + epsilon;
    }

    private static IReadOnlyList<PixelTextLine> GetOrCreateTextLayout(TextLayoutCacheKey cacheKey, string text, double wrapWidth, SKPaint paint)
    {
        if (TextLayoutCache.TryGetValue(cacheKey, out var cached)) return cached;
        var computed = WrapTextToPixelWidth(text, wrapWidth, paint);
        TextLayoutCache[cacheKey] = computed;
        return computed;
    }

    private static List<PixelTextLine> WrapParagraphToPixelWidth(string paragraph, double maxWidth, SKPaint paint)
    {
        if (string.IsNullOrEmpty(paragraph)) return [];
        var words = paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0) return [];
        var lines = new List<PixelTextLine>();
        var current = string.Empty;
        var currentWidth = 0f;
        foreach (var word in words)
        {
            var candidate = string.IsNullOrEmpty(current) ? word : $"{current} {word}";
            var candidateWidth = paint.MeasureText(candidate);
            if (candidateWidth <= maxWidth || string.IsNullOrEmpty(current)) { current = candidate; currentWidth = candidateWidth; continue; }
            lines.Add(new PixelTextLine(current, currentWidth));
            current = word;
            currentWidth = paint.MeasureText(word);
        }
        if (!string.IsNullOrEmpty(current)) lines.Add(new PixelTextLine(current, currentWidth));
        return lines;
    }

    private static bool TryResolveMfmeTypeface(string family, string styleToken, out SKTypeface typeface)
    {
        var fontsDirectory = Path.Combine(AppContext.BaseDirectory, "MfmeFonts");
        if (!Directory.Exists(fontsDirectory)) { typeface = null!; return false; }
        var wantsBold = styleToken.Contains("Bold", StringComparison.OrdinalIgnoreCase);
        foreach (var fontPath in Directory.EnumerateFiles(fontsDirectory, "*.ttf"))
        {
            SKTypeface? candidate = null;
            try { candidate = SKTypeface.FromFile(fontPath); } catch { continue; }
            if (candidate is null) continue;
            if (!string.Equals(candidate.FamilyName, family, StringComparison.OrdinalIgnoreCase)) { candidate.Dispose(); continue; }
            var isBoldFace = candidate.FontWeight >= (int)SKFontStyleWeight.SemiBold;
            if (wantsBold != isBoldFace) { candidate.Dispose(); continue; }
            typeface = candidate;
            return true;
        }
        typeface = null!;
        return false;
    }

    internal readonly record struct PixelTextLine(string Text, float Width);
    private readonly record struct TextLayoutCacheKey(string Text, string FamilyName, float TextSize, double WrapWidth);
}
