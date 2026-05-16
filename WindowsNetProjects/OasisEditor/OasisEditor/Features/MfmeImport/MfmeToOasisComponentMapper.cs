using System.Globalization;

namespace OasisEditor.Features.MfmeImport;

internal sealed class MfmeToOasisComponentMapper
{
    private const string ImportSourceFormat = "LegacyImport";

    public MfmeToOasisMapResult Map(MfmeLegacyExtractData extract)
    {
        ArgumentNullException.ThrowIfNull(extract);

        var elements = new List<PanelElementModel>();
        var warnings = new List<MfmeImportWarning>();
        var skipped = new List<string>();

        foreach (var component in extract.Components)
        {
            switch (component)
            {
                case MfmeLegacyBackgroundComponent background:
                    elements.Add(MapBackground(background));
                    break;
                case MfmeLegacyLampComponent lamp:
                    elements.Add(MapLamp(lamp, warnings));
                    break;
                case MfmeLegacyReelComponent reel:
                    elements.Add(MapReel(reel, warnings));
                    break;
                case MfmeLegacySevenSegmentComponent sevenSegment:
                    elements.Add(MapSevenSegment(sevenSegment));
                    break;
                case MfmeLegacyAlphaComponent alpha:
                    elements.Add(MapAlpha(alpha));
                    break;
                case MfmeLegacyLabelComponent label:
                    elements.Add(MapLabel(label));
                    break;
                default:
                    skipped.Add(component.SourceType);
                    warnings.Add(new MfmeImportWarning(
                        "mfme.import.component.unsupported",
                        $"Unsupported MFME component '{component.SourceType}' was skipped.",
                        component.SourceType));
                    break;
            }
        }

        return new MfmeToOasisMapResult
        {
            Elements = elements,
            Warnings = warnings,
            SkippedLegacyComponentTypes = skipped
        };
    }

    private static PanelElementModel MapBackground(MfmeLegacyBackgroundComponent component)
    {
        return new PanelElementModel
        {
            ObjectId = Guid.NewGuid().ToString("N"),
            Name = "Background",
            Kind = PanelElementKind.Background,
            X = 0,
            Y = 0,
            Width = component.Size.X,
            Height = component.Size.Y,
            AssetPath = BuildExtractRelativePath("background", component.BmpImageFilename),
            OnColorHex = ToHex(component.Color),
            ImportSource = CreateImportSource(component.SourceType)
        };
    }

    private static PanelElementModel MapLamp(MfmeLegacyLampComponent component, ICollection<MfmeImportWarning> warnings)
    {
        var number = component.FirstLampElement?.Number;
        if (number is null && !string.IsNullOrWhiteSpace(component.FirstLampElement?.NumberAsText))
        {
            if (int.TryParse(component.FirstLampElement.NumberAsText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                number = parsed;
            }
            else
            {
                warnings.Add(new MfmeImportWarning(
                    "mfme.import.lamp.number.invalid",
                    "Lamp number could not be parsed; number will be omitted.",
                    component.FirstLampElement.NumberAsText));
            }
        }

        return new PanelElementModel
        {
            ObjectId = Guid.NewGuid().ToString("N"),
            Name = number.HasValue ? $"Lamp {number.Value}" : "Lamp",
            Kind = PanelElementKind.Lamp,
            X = component.Position.X,
            Y = component.Position.Y,
            Width = component.Size.X,
            Height = component.Size.Y,
            DisplayNumber = number,
            AssetPath = component.FirstLampElement?.Graphic == true
                ? BuildExtractRelativePath("lamps", component.FirstLampElement.BmpImageFilename)
                : null,
            SecondaryAssetPath = component.FirstLampElement?.Graphic == true
                ? BuildExtractRelativePath("lamps", component.FirstLampElement.BmpMaskImageFilename)
                : null,
            OnColorHex = ToHex(component.FirstLampElement?.OnColor),
            OffColorHex = ToHex(component.OffImageColor),
            TextColorHex = ToHex(component.TextColor),
            DisplayText = NormalizeOptional(component.TextBoxText),
            TextBoxFontName = NormalizeLampFontName(component.TextBoxFontName),
            TextBoxFontStyle = NormalizeLampFontStyle(component.TextBoxFontStyle),
            TextBoxFontSize = NormalizeLampFontSize(component.TextBoxFontSize),
            ImportSource = CreateImportSource(number.HasValue ? $"{component.SourceType}:{number.Value}" : component.SourceType)
        };
    }

    private static string NormalizeLampFontName(string? value) => string.IsNullOrWhiteSpace(value) ? "Tahoma" : value.Trim();
    private static string NormalizeLampFontStyle(string? value) => string.IsNullOrWhiteSpace(value) ? "Regular" : value.Trim();
    private static string NormalizeLampFontSize(string? value) => string.IsNullOrWhiteSpace(value) ? "8" : value.Trim();

    private static PanelElementModel MapReel(MfmeLegacyReelComponent component, ICollection<MfmeImportWarning> warnings)
    {
        var mappedNumber = component.Number + 1;
        double? visibleScale = null;

        if (component.Stops > 0)
        {
            var visibleSymbols = component.Height / 50d;
            visibleScale = visibleSymbols / component.Stops;
        }
        else
        {
            warnings.Add(new MfmeImportWarning(
                "mfme.import.reel.stops.invalid",
                "Reel stops must be greater than zero to calculate visible scale.",
                component.Number.ToString(CultureInfo.InvariantCulture)));
        }

        return new PanelElementModel
        {
            ObjectId = Guid.NewGuid().ToString("N"),
            Name = $"Reel {mappedNumber}",
            Kind = PanelElementKind.Reel,
            X = component.Position.X,
            Y = component.Position.Y,
            Width = component.Size.X,
            Height = component.Size.Y,
            DisplayNumber = mappedNumber,
            AssetPath = BuildExtractRelativePath("reels", component.BandBmpImageFilename),
            SecondaryAssetPath = component.HasOverlay
                ? BuildExtractRelativePath("reels", component.OverlayBmpImageFilename)
                : null,
            Stops = component.Stops,
            IsReversed = component.Reversed,
            VisibleScale = visibleScale,
            ImportSource = CreateImportSource($"{component.SourceType}:{mappedNumber}")
        };
    }

    private static PanelElementModel MapSevenSegment(MfmeLegacySevenSegmentComponent component)
    {
        return new PanelElementModel
        {
            ObjectId = Guid.NewGuid().ToString("N"),
            Name = $"7 Segment {component.Number}",
            Kind = PanelElementKind.SevenSegment,
            X = component.Position.X,
            Y = component.Position.Y,
            Width = component.Size.X,
            Height = component.Size.Y,
            DisplayNumber = component.Number,
            OnColorHex = ToHex(component.SegmentOnColor),
            ImportSource = CreateImportSource($"{component.SourceType}:{component.Number}")
        };
    }

    private static PanelElementModel MapAlpha(MfmeLegacyAlphaComponent component)
    {
        return new PanelElementModel
        {
            ObjectId = Guid.NewGuid().ToString("N"),
            Name = "Alpha",
            Kind = PanelElementKind.Alpha,
            X = component.Position.X,
            Y = component.Position.Y,
            Width = component.Size.X,
            Height = component.Size.Y,
            IsReversed = component.Reversed,
            ImportSource = CreateImportSource(component.SourceType)
        };
    }

    private static PanelElementModel MapLabel(MfmeLegacyLabelComponent component)
    {
        return new PanelElementModel
        {
            ObjectId = Guid.NewGuid().ToString("N"),
            Name = "Label",
            Kind = PanelElementKind.Label,
            X = component.Position.X,
            Y = component.Position.Y,
            Width = component.Size.X,
            Height = component.Size.Y,
            DisplayText = NormalizeOptional(component.TextBoxText),
            TextBoxFontName = NormalizeOptional(component.TextBoxFontName),
            TextBoxFontStyle = NormalizeOptional(component.TextBoxFontStyle),
            TextBoxFontSize = NormalizeOptional(component.TextBoxFontSize),
            TextColorHex = ToHex(component.TextColor),
            ImportSource = CreateImportSource(component.SourceType)
        };
    }

    private static PanelElementImportSourceModel CreateImportSource(string reference)
    {
        return new PanelElementImportSourceModel
        {
            Format = ImportSourceFormat,
            Reference = reference
        };
    }

    private static string? BuildExtractRelativePath(string folder, string? fileName)
    {
        var normalized = NormalizeOptional(fileName);
        return normalized is null ? null : $"{folder}/{normalized}";
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string? ToHex(MfmeLegacyColor? color)
    {
        if (!color.HasValue)
        {
            return null;
        }

        var value = color.Value;
        var a = ClampByte(value.A);
        var r = ClampByte(value.R);
        var g = ClampByte(value.G);
        var b = ClampByte(value.B);
        return $"#{a:X2}{r:X2}{g:X2}{b:X2}";
    }

    private static int ClampByte(float value)
    {
        var scaled = Math.Round(value * 255f, MidpointRounding.AwayFromZero);
        return (int)Math.Clamp(scaled, 0, 255);
    }
}

internal sealed class MfmeToOasisMapResult
{
    public required IReadOnlyList<PanelElementModel> Elements { get; init; }

    public required IReadOnlyList<MfmeImportWarning> Warnings { get; init; }

    public required IReadOnlyList<string> SkippedLegacyComponentTypes { get; init; }
}
