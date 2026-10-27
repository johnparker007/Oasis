using System.Collections.Concurrent;
using System.IO;
using SkiaSharp;

namespace OasisEditor.Rendering;

internal static class MfmeTypefaceResolver
{
    private const string DefaultFamily = "Tahoma";
    private const string DefaultStyle = "Regular";
    private static readonly ConcurrentDictionary<string, SKTypeface> TypefaceCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Lazy<IReadOnlyList<BundledTypeface>> BundledTypefaces = new(LoadBundledTypefaces, isThreadSafe: true);

    public static SKTypeface Resolve(string? fontName, string? fontStyle)
    {
        var family = NormalizeFamily(fontName);
        var styleToken = NormalizeStyle(fontStyle);
        var cacheKey = $"{family}|{styleToken}";

        return TypefaceCache.GetOrAdd(cacheKey, _ => ResolveUncached(family, styleToken));
    }

    private static SKTypeface ResolveUncached(string family, string styleToken)
    {
        var style = CreateFontStyle(styleToken);
        if (TryResolveBundledTypeface(family, styleToken, out var bundledTypeface))
        {
            return bundledTypeface;
        }

        return SKTypeface.FromFamilyName(family, style)
            ?? SKTypeface.FromFamilyName(DefaultFamily, style)
            ?? SKTypeface.Default;
    }

    private static bool TryResolveBundledTypeface(string family, string styleToken, out SKTypeface typeface)
    {
        var wantsBold = styleToken.Contains("Bold", StringComparison.OrdinalIgnoreCase);
        var wantsItalic = styleToken.Contains("Italic", StringComparison.OrdinalIgnoreCase);
        BundledTypeface? compatible = null;

        foreach (var candidate in BundledTypefaces.Value)
        {
            if (!string.Equals(candidate.FamilyName, family, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (candidate.IsBold != wantsBold)
            {
                continue;
            }

            compatible ??= candidate;
            if (candidate.IsItalic == wantsItalic)
            {
                typeface = candidate.Typeface;
                return true;
            }
        }

        if (compatible is not null)
        {
            typeface = compatible.Typeface;
            return true;
        }

        typeface = null!;
        return false;
    }

    private static IReadOnlyList<BundledTypeface> LoadBundledTypefaces()
    {
        var fontsDirectory = Path.Combine(AppContext.BaseDirectory, "MfmeFonts");
        if (!Directory.Exists(fontsDirectory))
        {
            return [];
        }

        var bundledTypefaces = new List<BundledTypeface>();
        IEnumerable<string> fontPaths;
        try
        {
            fontPaths = Directory.EnumerateFiles(fontsDirectory)
                .Where(path => string.Equals(Path.GetExtension(path), ".ttf", StringComparison.OrdinalIgnoreCase))
                .ToArray();
        }
        catch
        {
            return bundledTypefaces;
        }

        foreach (var fontPath in fontPaths)
        {
            SKTypeface? candidate = null;
            try
            {
                candidate = SKTypeface.FromFile(fontPath);
                if (candidate is null || string.IsNullOrWhiteSpace(candidate.FamilyName))
                {
                    candidate?.Dispose();
                    continue;
                }

                bundledTypefaces.Add(new BundledTypeface(
                    candidate.FamilyName,
                    candidate.FontWeight >= (int)SKFontStyleWeight.SemiBold,
                    candidate.FontSlant == SKFontStyleSlant.Italic || candidate.FontSlant == SKFontStyleSlant.Oblique,
                    candidate));
                candidate = null;
            }
            catch
            {
                // Ignore invalid or unreadable bundled fonts; fallback resolution remains safe.
            }
            finally
            {
                candidate?.Dispose();
            }
        }

        return bundledTypefaces;
    }

    private static SKFontStyle CreateFontStyle(string styleToken)
    {
        var weight = styleToken.Contains("Bold", StringComparison.OrdinalIgnoreCase)
            ? SKFontStyleWeight.Bold
            : SKFontStyleWeight.Normal;
        var slant = styleToken.Contains("Italic", StringComparison.OrdinalIgnoreCase)
            ? SKFontStyleSlant.Italic
            : SKFontStyleSlant.Upright;

        return new SKFontStyle(weight, SKFontStyleWidth.Normal, slant);
    }

    private static string NormalizeFamily(string? fontName) =>
        string.IsNullOrWhiteSpace(fontName) ? DefaultFamily : fontName.Trim();

    private static string NormalizeStyle(string? fontStyle) =>
        string.IsNullOrWhiteSpace(fontStyle) ? DefaultStyle : fontStyle.Trim();

    private sealed record BundledTypeface(string FamilyName, bool IsBold, bool IsItalic, SKTypeface Typeface);
}
