using MfmeFmlDecoder.src.Model;
using MfmeFmlDecoder.src.Model.Component;
using OasisEditor.Features.LayoutImport;

namespace OasisEditor.Features.FmlImport;

internal enum FmlBackgroundMode
{
    ImageBackedBackground,
    SolidColourBackground,
    NoBackground
}

internal sealed class FmlBackgroundClassification
{
    public required FmlBackgroundMode Mode { get; init; }
    public int? MainBackgroundComponentIndex { get; init; }
    public string? MainBackgroundImagePath { get; init; }
    public string? DecodedBackgroundColour { get; init; }
    public string? MappedBackgroundColour { get; init; }
    public IReadOnlyList<LayoutImportWarning> Warnings { get; init; } = [];
}

internal static class FmlBackgroundClassifier
{
    public static FmlBackgroundClassification Classify(Layout layout, IReadOnlyDictionary<FmlDecodedImageKey, string> imagePaths)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(imagePaths);

        var warnings = new List<LayoutImportWarning>();
        var backgroundCandidates = layout.Components
            .Select((Component, Index) => new { Component, Index })
            .Where(entry => entry.Component is Background)
            .ToArray();

        if (backgroundCandidates.Length == 0)
        {
            return new FmlBackgroundClassification { Mode = FmlBackgroundMode.NoBackground };
        }

        var imageCandidates = backgroundCandidates
            .Select(entry => new
            {
                entry.Index,
                ImagePath = FindUsableMainBackgroundImage(imagePaths, entry.Index)
            })
            .Where(entry => !string.IsNullOrWhiteSpace(entry.ImagePath))
            .ToArray();

        if (imageCandidates.Length > 1)
        {
            warnings.Add(new LayoutImportWarning(
                "fml.import.background.multipleMainImages",
                "Multiple Background components have usable main background images; the earliest source-order Background image was selected.",
                string.Join(", ", imageCandidates.Select(candidate => candidate.Index.ToString(System.Globalization.CultureInfo.InvariantCulture)))));
        }

        var imageBacked = imageCandidates.FirstOrDefault();
        if (imageBacked is not null)
        {
            var component = (Background)layout.Components[imageBacked.Index];
            var decodedColour = FindDecodedBackgroundColour(component);
            return new FmlBackgroundClassification
            {
                Mode = FmlBackgroundMode.ImageBackedBackground,
                MainBackgroundComponentIndex = imageBacked.Index,
                MainBackgroundImagePath = imageBacked.ImagePath,
                DecodedBackgroundColour = decodedColour,
                MappedBackgroundColour = FmlToOasisMapper.ConvertDecoderRgbaToOasisArgb(decodedColour),
                Warnings = warnings
            };
        }

        var solid = backgroundCandidates
            .Select(entry => new
            {
                entry.Index,
                DecodedColour = FindDecodedBackgroundColour(entry.Component),
                MappedColour = FmlToOasisMapper.ConvertDecoderRgbaToOasisArgb(FindDecodedBackgroundColour(entry.Component))
            })
            .FirstOrDefault(entry => !string.IsNullOrWhiteSpace(entry.MappedColour));

        if (solid is not null)
        {
            return new FmlBackgroundClassification
            {
                Mode = FmlBackgroundMode.SolidColourBackground,
                MainBackgroundComponentIndex = solid.Index,
                DecodedBackgroundColour = solid.DecodedColour,
                MappedBackgroundColour = solid.MappedColour,
                Warnings = warnings
            };
        }

        warnings.Add(new LayoutImportWarning(
            "fml.import.background.noUsableImageOrColour",
            "Background component has neither a usable main image nor a valid decoded fill colour.",
            backgroundCandidates[0].Index.ToString(System.Globalization.CultureInfo.InvariantCulture)));

        return new FmlBackgroundClassification
        {
            Mode = FmlBackgroundMode.NoBackground,
            MainBackgroundComponentIndex = backgroundCandidates[0].Index,
            Warnings = warnings
        };
    }

    private static string? FindUsableMainBackgroundImage(IReadOnlyDictionary<FmlDecodedImageKey, string> imagePaths, int componentIndex)
        => imagePaths
            .Where(kvp => kvp.Key.ComponentIndex == componentIndex && IsMainBackgroundImageName(kvp.Key.ImageName) && !string.IsNullOrWhiteSpace(kvp.Value))
            .OrderBy(kvp => kvp.Key.ImageName, StringComparer.Ordinal)
            .Select(kvp => kvp.Value)
            .FirstOrDefault();

    private static bool IsMainBackgroundImageName(string imageName)
    {
        var normalized = new string(imageName.Select(ch => char.IsLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : '_').ToArray()).Trim('_');
        return normalized.Contains("background", StringComparison.Ordinal)
            && !normalized.Contains("mask", StringComparison.Ordinal)
            && !normalized.Contains("overlay", StringComparison.Ordinal)
            && !normalized.Contains("over_lay", StringComparison.Ordinal)
            && !normalized.Contains("window", StringComparison.Ordinal)
            && !normalized.Contains("cutout", StringComparison.Ordinal)
            && !normalized.Contains("cut_out", StringComparison.Ordinal);
    }

    private static string? FindDecodedBackgroundColour(BaseComponent component)
        => TryGetColour(component, "Colour") ?? TryGetColour(component, "Color") ?? TryGetColour(component, "BackgroundColour") ?? TryGetColour(component, "BackgroundColor");

    private static string? TryGetColour(BaseComponent component, string key)
        => component.Colours.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;
}
