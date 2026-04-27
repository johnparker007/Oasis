using System;
using System.Globalization;
using System.Text.Json;

namespace OasisEditor.Features.MfmeImport;

internal static class MfmeExtractComponentParser
{
    public static bool TryParse(
        JsonElement component,
        string contextPath,
        Action<MfmeImportWarning> addWarning,
        out MfmeExtractComponentData? parsed)
    {
        var sourceType = ReadString(component, "$type")
            ?? ReadString(component, "Type")
            ?? string.Empty;

        var normalizedType = sourceType.ToLowerInvariant();
        var position = TryReadPoint(component, "Position");
        var size = TryReadPoint(component, "Size");

        var common = new CommonData
        {
            SourceType = string.IsNullOrWhiteSpace(sourceType) ? "Unknown" : sourceType,
            DisplayName = ReadString(component, "TextBoxText"),
            X = position.X,
            Y = position.Y,
            Width = size.X,
            Height = size.Y,
            RawJson = component.GetRawText()
        };

        if (normalizedType.Contains("background", StringComparison.Ordinal))
        {
            var image = ReadString(component, "BmpImageFilename");
            if (string.IsNullOrWhiteSpace(image))
            {
                addWarning(new MfmeImportWarning("missing-image", "Background image is missing; placeholder import will be used.", contextPath));
            }

            parsed = new MfmeBackgroundComponentData
            {
                Kind = MfmeComponentKind.Background,
                SourceType = common.SourceType,
                DisplayName = common.DisplayName,
                X = common.X,
                Y = common.Y,
                Width = common.Width,
                Height = common.Height,
                RawJson = common.RawJson,
                ImageFileName = image,
                Color = ReadString(component, "Color")
            };
            return true;
        }

        if (normalizedType.Contains("lamp", StringComparison.Ordinal))
        {
            var lampElement = TryGetFirstArrayItem(component, "LampElements");
            var image = lampElement.HasValue ? ReadString(lampElement.Value, "BmpImageFilename") : null;
            if (string.IsNullOrWhiteSpace(image))
            {
                addWarning(new MfmeImportWarning("missing-image", "Lamp image is missing; placeholder import will be used.", contextPath));
            }

            parsed = new MfmeLampComponentData
            {
                Kind = MfmeComponentKind.Lamp,
                SourceType = common.SourceType,
                DisplayName = common.DisplayName,
                X = common.X,
                Y = common.Y,
                Width = common.Width,
                Height = common.Height,
                RawJson = common.RawJson,
                Number = lampElement.HasValue ? TryReadInt(lampElement.Value, "Number") ?? TryReadInt(lampElement.Value, "NumberAsText") : null,
                ImageFileName = image,
                OnColor = lampElement.HasValue ? ReadString(lampElement.Value, "OnColor") : null,
                OffColor = ReadString(component, "OffImageColor"),
                TextColor = ReadString(component, "TextColor")
            };
            return true;
        }

        if (normalizedType.Contains("reel", StringComparison.Ordinal))
        {
            var bandImage = ReadString(component, "BandBmpImageFilename");
            if (string.IsNullOrWhiteSpace(bandImage))
            {
                addWarning(new MfmeImportWarning("missing-image", "Reel band image is missing; placeholder import will be used.", contextPath));
            }

            parsed = new MfmeReelComponentData
            {
                Kind = MfmeComponentKind.Reel,
                SourceType = common.SourceType,
                DisplayName = common.DisplayName,
                X = common.X,
                Y = common.Y,
                Width = common.Width,
                Height = common.Height,
                RawJson = common.RawJson,
                Number = TryReadInt(component, "Number"),
                Stops = TryReadInt(component, "Stops"),
                ReelHeight = TryReadInt(component, "Height"),
                Reversed = TryReadBool(component, "Reversed"),
                BandImageFileName = bandImage
            };
            return true;
        }

        if (normalizedType.Contains("sevensegment", StringComparison.Ordinal) || normalizedType.Contains("seven_segment", StringComparison.Ordinal))
        {
            parsed = new MfmeSevenSegmentComponentData
            {
                Kind = MfmeComponentKind.SevenSegment,
                SourceType = common.SourceType,
                DisplayName = common.DisplayName,
                X = common.X,
                Y = common.Y,
                Width = common.Width,
                Height = common.Height,
                RawJson = common.RawJson,
                Number = TryReadInt(component, "Number"),
                SegmentOnColor = ReadString(component, "SegmentOnColor")
            };
            return true;
        }

        if (normalizedType.Contains("alpha", StringComparison.Ordinal))
        {
            var image = ReadString(component, "BmpImageFilename");
            if (normalizedType.Contains("alpha") && string.IsNullOrWhiteSpace(image))
            {
                addWarning(new MfmeImportWarning("missing-image", "Alpha image is missing; placeholder import will be used.", contextPath));
            }

            parsed = new MfmeAlphaComponentData
            {
                Kind = MfmeComponentKind.Alpha,
                SourceType = common.SourceType,
                DisplayName = common.DisplayName,
                X = common.X,
                Y = common.Y,
                Width = common.Width,
                Height = common.Height,
                RawJson = common.RawJson,
                Number = TryReadInt(component, "Number"),
                Reversed = TryReadBool(component, "Reversed"),
                Color = ReadString(component, "Color") ?? ReadString(component, "OnColor"),
                ImageFileName = image,
                AlphaVariant = normalizedType.Contains("matrix", StringComparison.Ordinal)
                    ? "MatrixAlpha"
                    : normalizedType.Contains("alphanew", StringComparison.Ordinal)
                        ? "AlphaNew"
                        : "Alpha"
            };
            return true;
        }

        parsed = null;
        return false;
    }

    private static (double X, double Y) TryReadPoint(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.Object)
        {
            return (0d, 0d);
        }

        return (TryReadDouble(value, "X"), TryReadDouble(value, "Y"));
    }

    private static JsonElement? TryGetFirstArrayItem(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var item in value.EnumerateArray())
        {
            return item;
        }

        return null;
    }

    private static string? ReadString(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return value.GetString();
    }

    private static int? TryReadInt(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var numericValue))
        {
            return numericValue;
        }

        if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedValue))
        {
            return parsedValue;
        }

        return null;
    }

    private static bool TryReadBool(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var value))
        {
            return false;
        }

        if (value.ValueKind == JsonValueKind.True)
        {
            return true;
        }

        if (value.ValueKind == JsonValueKind.False)
        {
            return false;
        }

        if (value.ValueKind == JsonValueKind.String && bool.TryParse(value.GetString(), out var parsed))
        {
            return parsed;
        }

        return false;
    }

    private static double TryReadDouble(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var value))
        {
            return 0d;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var numericValue))
        {
            return numericValue;
        }

        if (value.ValueKind == JsonValueKind.String &&
            double.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedValue))
        {
            return parsedValue;
        }

        return 0d;
    }

    private sealed record CommonData
    {
        public required string SourceType { get; init; }

        public string? DisplayName { get; init; }

        public double X { get; init; }

        public double Y { get; init; }

        public double Width { get; init; }

        public double Height { get; init; }

        public required string RawJson { get; init; }
    }
}
