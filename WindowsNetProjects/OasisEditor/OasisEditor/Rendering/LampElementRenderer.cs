using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using SkiaSharp;

namespace OasisEditor.Rendering;

internal sealed class LampElementRenderer : IPanelElementRenderer
{
    private static readonly ConcurrentDictionary<string, SKTypeface> TypefaceCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, SKImage?> CachedLampImages = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<TextLayoutCacheKey, IReadOnlyList<PixelTextLine>> TextLayoutCache = new();
    private static readonly ConcurrentDictionary<TextVisualCacheKey, SKImage> TextVisualCache = new();
    private const int MaxTextVisualCacheEntries = 2048;

    public PanelElementKind Kind => PanelElementKind.Lamp;
    [ThreadStatic]
    private static int _diagnosticsTextLayoutCount;
    [ThreadStatic]
    private static int _diagnosticsTextDrawCount;
    [ThreadStatic]
    private static long _diagnosticsTextTicks;
    [ThreadStatic]
    private static int _diagnosticsTextLayoutCacheHits;
    [ThreadStatic]
    private static int _diagnosticsTextLayoutCacheMisses;
    [ThreadStatic]
    private static int _diagnosticsTextVisualCacheHits;
    [ThreadStatic]
    private static int _diagnosticsTextVisualCacheMisses;
    internal static int DiagnosticsTextLayoutCount => _diagnosticsTextLayoutCount;
    internal static int DiagnosticsTextDrawCount => _diagnosticsTextDrawCount;
    internal static TimeSpan DiagnosticsTextElapsed => TimeSpan.FromTicks(_diagnosticsTextTicks);
    internal static int DiagnosticsTextLayoutCacheHits => _diagnosticsTextLayoutCacheHits;
    internal static int DiagnosticsTextLayoutCacheMisses => _diagnosticsTextLayoutCacheMisses;
    internal static int DiagnosticsTextVisualCacheHits => _diagnosticsTextVisualCacheHits;
    internal static int DiagnosticsTextVisualCacheMisses => _diagnosticsTextVisualCacheMisses;
    internal static void ResetDiagnosticsCounters()
    {
        _diagnosticsTextLayoutCount = 0;
        _diagnosticsTextDrawCount = 0;
        _diagnosticsTextTicks = 0;
        _diagnosticsTextLayoutCacheHits = 0;
        _diagnosticsTextLayoutCacheMisses = 0;
        _diagnosticsTextVisualCacheHits = 0;
        _diagnosticsTextVisualCacheMisses = 0;
    }

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
        var fillColor = Lerp(offColor, onColor, intensity);

        var displayText = element.DisplayText;
        if (string.IsNullOrWhiteSpace(displayText))
        {
            if (TryGetLampImage(element.AssetPath, out var lampImage))
            {
                context.Canvas.DrawImage(lampImage, bounds);
                if (intensity < 1d)
                {
                    using var dimmerPaint = new SKPaint
                    {
                        Color = new SKColor(0, 0, 0, (byte)Math.Clamp(Math.Round((1d - intensity) * 255d), 0d, 255d)),
                        Style = SKPaintStyle.Fill,
                        IsAntialias = true
                    };
                    context.Canvas.DrawRect(bounds, dimmerPaint);
                }

                return;
            }

            using var paint = new SKPaint { Color = fillColor, Style = SKPaintStyle.Fill, IsAntialias = true };
            context.Canvas.DrawRect(bounds, paint);
            return;
        }

        var textStopwatch = Stopwatch.StartNew();
        try
        {
            var fontSize = ParseFontSize(element.TextBoxFontSize);
            var textColor = SkiaColorParser.ParseOrDefault(element.TextColorHex, SKColors.White);
            var visualKey = new TextVisualCacheKey(
                element.ObjectId ?? string.Empty,
                Math.Max(1, (int)Math.Round(bounds.Width)),
                Math.Max(1, (int)Math.Round(bounds.Height)),
                displayText,
                element.TextBoxFontName ?? string.Empty,
                element.TextBoxFontStyle ?? string.Empty,
                (float)fontSize,
                fillColor,
                textColor,
                intensity > 0d);

            if (!TextVisualCache.TryGetValue(visualKey, out var visual))
            {
                _diagnosticsTextVisualCacheMisses++;
                visual = BuildTextLampVisual(visualKey, displayText, fontSize, fillColor, textColor, element.TextBoxFontName, element.TextBoxFontStyle);
                if (TextVisualCache.Count > MaxTextVisualCacheEntries)
                {
                    TextVisualCache.Clear();
                }

                TextVisualCache[visualKey] = visual;
            }
            else
            {
                _diagnosticsTextVisualCacheHits++;
            }

            context.Canvas.DrawImage(visual, bounds.Left, bounds.Top);
        }
        finally
        {
            textStopwatch.Stop();
            _diagnosticsTextTicks += textStopwatch.ElapsedTicks;
        }
    }

    private static SKImage BuildTextLampVisual(TextVisualCacheKey visualKey, string displayText, double fontSize, SKColor fillColor, SKColor textColor, string? fontName, string? fontStyle)
    {
        using var surface = SKSurface.Create(new SKImageInfo(visualKey.Width, visualKey.Height));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        var localBounds = SKRect.Create(0f, 0f, visualKey.Width, visualKey.Height);

        using var paint = new SKPaint { Color = fillColor, Style = SKPaintStyle.Fill, IsAntialias = true };
        canvas.DrawRect(localBounds, paint);

        using var textPaint = new SKPaint
        {
            Color = textColor,
            IsAntialias = true,
            TextSize = (float)fontSize,
            Typeface = ResolveTypeface(fontName, fontStyle)
        };

        var textBounds = GetTextBounds(localBounds);
        var fontMetrics = textPaint.FontMetrics;
        var measuredLineHeight = Math.Abs(fontMetrics.Ascent) + Math.Abs(fontMetrics.Descent) + Math.Abs(fontMetrics.Leading);
        var lineHeight = Math.Max(1d, measuredLineHeight > 0f ? measuredLineHeight : fontSize * 1.2d);
        var wrapWidth = GetEffectiveWrapWidth(displayText, textBounds.Width, localBounds.Width, textPaint);
        var cacheKey = new TextLayoutCacheKey(displayText, textPaint.Typeface?.FamilyName ?? string.Empty, textPaint.TextSize, wrapWidth);
        var lines = GetOrCreateTextLayout(cacheKey, displayText, wrapWidth, textPaint);
        if (lines.Count == 0)
        {
            return surface.Snapshot();
        }

        var totalTextHeight = lines.Count * lineHeight;
        var baselineOffset = Math.Abs(fontMetrics.Ascent) > 0f ? Math.Abs(fontMetrics.Ascent) : fontSize;
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
            canvas.DrawText(line.Text, (float)x, (float)y, textPaint);
            _diagnosticsTextDrawCount++;
        }

        return surface.Snapshot();
    }

    internal static List<PixelTextLine> WrapTextToPixelWidth(string text, double maxWidth, SKPaint paint)
    {
        if (maxWidth <= 0d)
        {
            return [];
        }

        var normalized = text.Replace("\r\n", "\n");
        var paragraphs = normalized.Split('\n');
        var lines = new List<PixelTextLine>();
        foreach (var paragraph in paragraphs)
        {
            var wrapped = WrapParagraphToPixelWidth(paragraph, maxWidth, paint);
            if (wrapped.Count == 0)
            {
                lines.Add(new PixelTextLine(string.Empty, 0f));
                continue;
            }

            lines.AddRange(wrapped);
        }

        return lines;
    }

    internal static double GetEffectiveWrapWidth(string text, double insetWidth, double lampWidth, SKPaint paint)
    {
        var epsilon = 1.5d;
        var fullWidth = Math.Max(insetWidth, lampWidth);
        var singleLineWidth = paint.MeasureText(text ?? string.Empty);

        if (singleLineWidth <= fullWidth + epsilon)
        {
            return fullWidth + epsilon;
        }

        return insetWidth + epsilon;
    }

    private static List<PixelTextLine> WrapParagraphToPixelWidth(string paragraph, double maxWidth, SKPaint paint)
    {
        if (string.IsNullOrEmpty(paragraph))
        {
            return [];
        }

        var words = paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
        {
            return [];
        }

        var lines = new List<PixelTextLine>();
        var current = string.Empty;
        var currentWidth = 0f;

        foreach (var word in words)
        {
            var candidate = string.IsNullOrEmpty(current) ? word : $"{current} {word}";
            var candidateWidth = paint.MeasureText(candidate);
            if (candidateWidth <= maxWidth || string.IsNullOrEmpty(current))
            {
                current = candidate;
                currentWidth = candidateWidth;
                continue;
            }

            lines.Add(new PixelTextLine(current, currentWidth));
            current = word;
            currentWidth = paint.MeasureText(word);
        }

        if (!string.IsNullOrEmpty(current))
        {
            lines.Add(new PixelTextLine(current, currentWidth));
        }

        return lines;
    }

    internal static SKRect GetTextBounds(in SKRect lampBounds)
    {
        var horizontalInset = Math.Min(lampBounds.Width * 0.1f, 8f);
        var verticalInset = Math.Min(lampBounds.Height * 0.1f, 6f);
        var width = Math.Max(1f, lampBounds.Width - (horizontalInset * 2f));
        var height = Math.Max(1f, lampBounds.Height - (verticalInset * 2f));
        return SKRect.Create(lampBounds.Left + horizontalInset, lampBounds.Top + verticalInset, width, height);
    }

    internal static double ParseFontSize(string? value)
    {
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) && parsed > 0d)
        {
            return parsed * 1.33333333d;
        }

        return 10.66666664d;
    }

    internal readonly record struct PixelTextLine(string Text, float Width);
    private readonly record struct TextLayoutCacheKey(string Text, string FamilyName, float TextSize, double WrapWidth);
    private readonly record struct TextVisualCacheKey(string ElementId, int Width, int Height, string Text, string FontName, string FontStyle, float TextSize, SKColor FillColor, SKColor TextColor, bool IsOnBucket);

    private static IReadOnlyList<PixelTextLine> GetOrCreateTextLayout(TextLayoutCacheKey cacheKey, string text, double wrapWidth, SKPaint paint)
    {
        if (TextLayoutCache.TryGetValue(cacheKey, out var cached))
        {
            _diagnosticsTextLayoutCacheHits++;
            return cached;
        }

        _diagnosticsTextLayoutCacheMisses++;
        _diagnosticsTextLayoutCount++;
        var computed = WrapTextToPixelWidth(text, wrapWidth, paint);
        TextLayoutCache[cacheKey] = computed;
        return computed;
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


    private static bool TryGetLampImage(string? assetPath, out SKImage image)
    {
        image = default!;
        if (!TryResolveAssetPath(assetPath, out var resolvedPath))
        {
            return false;
        }

        var cached = CachedLampImages.GetOrAdd(resolvedPath, LoadImage);
        if (cached is null)
        {
            return false;
        }

        image = cached;
        return true;
    }

    private static SKImage? LoadImage(string resolvedPath)
    {
        if (!File.Exists(resolvedPath))
        {
            return null;
        }

        using var codec = SKCodec.Create(resolvedPath);
        if (codec is null)
        {
            return null;
        }

        using var bitmap = SKBitmap.Decode(codec);
        return bitmap is null ? null : SKImage.FromBitmap(bitmap);
    }

    private static bool TryResolveAssetPath(string? assetPath, out string resolvedPath)
    {
        resolvedPath = string.Empty;
        if (string.IsNullOrWhiteSpace(assetPath))
        {
            return false;
        }

        var candidate = assetPath.Trim();
        if (Path.IsPathRooted(candidate))
        {
            resolvedPath = candidate;
            return true;
        }

        if (string.IsNullOrWhiteSpace(PanelElementFactory.ProjectDirectoryPath))
        {
            return false;
        }

        var relativePath = candidate
            .Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar);
        resolvedPath = Path.GetFullPath(Path.Combine(PanelElementFactory.ProjectDirectoryPath, relativePath));
        return true;
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
