using System;
using System.Collections.Generic;

namespace OasisEditor.Features.MfmeImport;

internal sealed record MfmeComponentMappingResult
{
    public IReadOnlyList<PanelElementModel> Elements { get; init; } = [];

    public IReadOnlyList<MfmeExtractComponentData> SkippedComponents { get; init; } = [];

    public IReadOnlyList<MfmeImportWarning> Warnings { get; init; } = [];
}

internal sealed class MfmeComponentMapper
{
    private const double DefaultWidth = 100d;
    private const double DefaultHeight = 100d;
    private const int MfmeHeightPerVisibleSymbol = 50;

    public MfmeComponentMappingResult Map(IReadOnlyList<MfmeExtractComponentData> components)
    {
        ArgumentNullException.ThrowIfNull(components);

        var mappedElements = new List<PanelElementModel>(components.Count);
        var skipped = new List<MfmeExtractComponentData>();
        var warnings = new List<MfmeImportWarning>();

        foreach (var component in components)
        {
            switch (component)
            {
                case MfmeBackgroundComponentData background:
                    mappedElements.Add(MapBackground(background));
                    break;
                case MfmeLampComponentData lamp:
                    mappedElements.Add(MapLamp(lamp));
                    break;
                case MfmeReelComponentData reel:
                    mappedElements.Add(MapReel(reel));
                    break;
                case MfmeSevenSegmentComponentData sevenSegment:
                    mappedElements.Add(MapSevenSegment(sevenSegment));
                    break;
                case MfmeAlphaComponentData alpha:
                    mappedElements.Add(MapAlpha(alpha));
                    break;
                default:
                    skipped.Add(component);
                    warnings.Add(new MfmeImportWarning(
                        Code: "unsupported-component",
                        Message: $"Skipped unsupported MFME component '{component.SourceType}'.",
                        ContextPath: component.SourceType));
                    break;
            }
        }

        return new MfmeComponentMappingResult
        {
            Elements = mappedElements,
            SkippedComponents = skipped,
            Warnings = warnings
        };
    }

    private static PanelElementModel MapBackground(MfmeBackgroundComponentData component)
    {
        return CreateElement(
            kind: PanelElementKind.Background,
            name: "Background",
            x: 0,
            y: 0,
            width: component.Width,
            height: component.Height,
            assetPath: BuildAssetPath("background", component.ImageFileName),
            sourceType: component.SourceType,
            sourceId: null,
            displayNumber: null,
            primaryColor: component.Color,
            secondaryColor: null,
            textColor: null,
            text: null,
            reversed: false,
            stops: null,
            visibleScale: null);
    }

    private static PanelElementModel MapLamp(MfmeLampComponentData component)
    {
        var name = component.Number.HasValue ? $"Lamp {component.Number.Value}" : "Lamp";
        return CreateElement(
            kind: PanelElementKind.Lamp,
            name: name,
            x: component.X,
            y: component.Y,
            width: component.Width,
            height: component.Height,
            assetPath: BuildAssetPath("lamps", component.ImageFileName),
            sourceType: component.SourceType,
            sourceId: component.Number?.ToString(),
            displayNumber: component.Number,
            primaryColor: component.OnColor,
            secondaryColor: component.OffColor,
            textColor: component.TextColor,
            text: component.DisplayName,
            reversed: false,
            stops: null,
            visibleScale: null);
    }

    private static PanelElementModel MapReel(MfmeReelComponentData component)
    {
        var reelNumber = component.Number.HasValue ? component.Number.Value + 1 : (int?)null;
        var name = reelNumber.HasValue ? $"Reel {reelNumber.Value}" : "Reel";
        var visibleScale = TryCalculateVisibleScale(component.Stops, component.ReelHeight);

        return CreateElement(
            kind: PanelElementKind.Reel,
            name: name,
            x: component.X,
            y: component.Y,
            width: component.Width,
            height: component.Height,
            assetPath: BuildAssetPath("reels", component.BandImageFileName),
            sourceType: component.SourceType,
            sourceId: component.Number?.ToString(),
            displayNumber: reelNumber,
            primaryColor: null,
            secondaryColor: null,
            textColor: null,
            text: null,
            reversed: component.Reversed,
            stops: component.Stops is > 0 ? component.Stops : null,
            visibleScale: visibleScale);
    }

    private static PanelElementModel MapSevenSegment(MfmeSevenSegmentComponentData component)
    {
        var name = component.Number.HasValue ? $"7 Segment {component.Number.Value}" : "7 Segment";
        return CreateElement(
            kind: PanelElementKind.SevenSegment,
            name: name,
            x: component.X,
            y: component.Y,
            width: component.Width,
            height: component.Height,
            assetPath: null,
            sourceType: component.SourceType,
            sourceId: component.Number?.ToString(),
            displayNumber: component.Number,
            primaryColor: component.SegmentOnColor,
            secondaryColor: null,
            textColor: null,
            text: null,
            reversed: false,
            stops: null,
            visibleScale: null);
    }

    private static PanelElementModel MapAlpha(MfmeAlphaComponentData component)
    {
        return CreateElement(
            kind: PanelElementKind.Alpha,
            name: "Alpha",
            x: component.X,
            y: component.Y,
            width: component.Width,
            height: component.Height,
            assetPath: null,
            sourceType: component.SourceType,
            sourceId: component.Number?.ToString(),
            displayNumber: component.Number,
            primaryColor: component.Color,
            secondaryColor: null,
            textColor: null,
            text: null,
            reversed: component.Reversed,
            stops: null,
            visibleScale: null);
    }

    private static PanelElementModel CreateElement(
        PanelElementKind kind,
        string name,
        double x,
        double y,
        double width,
        double height,
        string? assetPath,
        string sourceType,
        string? sourceId,
        int? displayNumber,
        string? primaryColor,
        string? secondaryColor,
        string? textColor,
        string? text,
        bool reversed,
        int? stops,
        double? visibleScale)
    {
        return new PanelElementModel
        {
            ObjectId = Guid.NewGuid().ToString("N"),
            Name = name,
            Kind = kind,
            X = x,
            Y = y,
            Width = NormalizeDimension(width, DefaultWidth),
            Height = NormalizeDimension(height, DefaultHeight),
            AssetPath = assetPath,
            MfmeSourceType = sourceType,
            MfmeSourceId = sourceId,
            DisplayNumber = displayNumber,
            PrimaryColor = primaryColor,
            SecondaryColor = secondaryColor,
            TextColor = textColor,
            Text = text,
            Reversed = reversed,
            Stops = stops,
            VisibleScale = visibleScale
        };
    }

    private static string? BuildAssetPath(string categoryFolder, string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }

        return $"{categoryFolder}/{fileName.Trim()}";
    }

    private static double NormalizeDimension(double value, double fallback)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0)
        {
            return fallback;
        }

        return value;
    }

    private static double? TryCalculateVisibleScale(int? stops, int? reelHeight)
    {
        if (!stops.HasValue || !reelHeight.HasValue || stops.Value <= 0 || reelHeight.Value <= 0)
        {
            return null;
        }

        var visibleSymbols = (double)reelHeight.Value / MfmeHeightPerVisibleSymbol;
        var scale = visibleSymbols / stops.Value;
        return scale > 0 ? scale : null;
    }
}
