using System.Globalization;
using System.Text.Json;

namespace OasisEditor.Features.MfmeImport;

internal static class MfmeExtractManifestParser
{
    public static MfmeExtractReadResult Parse(string extractDirectory, string manifestPath)
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                return Error("mfme.extract.manifest.format", $"Manifest root must be an object: '{manifestPath}'.");
            }

            var warnings = new List<MfmeImportWarning>();
            var components = ParseComponents(root, warnings);

            var layoutName = ReadString(root, "ASName") ?? Path.GetFileNameWithoutExtension(manifestPath);
            var extract = new MfmeLegacyExtractData
            {
                ExtractRootPath = extractDirectory,
                ManifestPath = manifestPath,
                LayoutName = layoutName,
                Components = components
            };

            return new MfmeExtractReadResult
            {
                Extract = extract,
                Warnings = warnings,
                Errors = []
            };
        }
        catch (JsonException ex)
        {
            return Error("mfme.extract.manifest.json", $"Manifest JSON is invalid: {ex.Message}");
        }
        catch (IOException ex)
        {
            return Error("mfme.extract.manifest.io", $"Failed to read manifest '{manifestPath}': {ex.Message}");
        }
    }

    private static IReadOnlyList<MfmeLegacyComponentBase> ParseComponents(JsonElement root, List<MfmeImportWarning> warnings)
    {
        if (!root.TryGetProperty("Components", out var componentsElement) || componentsElement.ValueKind != JsonValueKind.Array)
        {
            warnings.Add(new MfmeImportWarning("mfme.extract.components.missing", "Manifest has no Components array."));
            return [];
        }

        var components = new List<MfmeLegacyComponentBase>();
        var index = 0;
        foreach (var component in componentsElement.EnumerateArray())
        {
            if (component.ValueKind != JsonValueKind.Object)
            {
                warnings.Add(new MfmeImportWarning("mfme.extract.component.invalid", $"Component at index {index} is not an object."));
                index++;
                continue;
            }

            var sourceType = ReadSourceType(component);
            if (TryParseSupportedComponent(sourceType, component, out var parsedComponent))
            {
                components.Add(parsedComponent);
            }
            else
            {
                warnings.Add(new MfmeImportWarning(
                    "mfme.extract.component.unsupported",
                    $"Unsupported legacy component type '{sourceType}' at index {index}; skipped."));
            }

            index++;
        }

        return components;
    }

    private static bool TryParseSupportedComponent(string sourceType, JsonElement component, out MfmeLegacyComponentBase parsedComponent)
    {
        var position = ReadPoint(component, "Position");
        var size = ReadPoint(component, "Size");
        var baseType = sourceType.ToLowerInvariant();

        switch (baseType)
        {
            case "extractcomponentbackground":
                parsedComponent = new MfmeLegacyBackgroundComponent(
                    position,
                    size,
                    ReadString(component, "BmpImageFilename"),
                    ReadColor(component, "Color"));
                return true;

            case "extractcomponentlamp":
                parsedComponent = new MfmeLegacyLampComponent(
                    position,
                    size,
                    ReadString(component, "TextBoxText"),
                    ReadString(component, "TextBoxFontName"),
                    ReadString(component, "TextBoxFontStyle"),
                    ReadString(component, "TextBoxFontSize"),
                    ReadFirstLampElement(component),
                    ReadColor(component, "OffImageColor"),
                    ReadColor(component, "TextColor"),
                    ReadBool(component, "NoOutline"));
                return true;

            case "extractcomponentreel":
                parsedComponent = new MfmeLegacyReelComponent(
                    position,
                    size,
                    ReadInt(component, "Number"),
                    ReadInt(component, "Stops"),
                    ReadBool(component, "Reversed"),
                    ReadInt(component, "Height"),
                    ReadString(component, "BandBmpImageFilename"),
                    ReadBool(component, "HasOverlay"),
                    ReadString(component, "OverlayBmpImageFilename"));
                return true;

            case "extractcomponentsevensegment":
                parsedComponent = new MfmeLegacySevenSegmentComponent(
                    position,
                    size,
                    ReadInt(component, "Number"),
                    ReadColor(component, "SegmentOnColor"));
                return true;

            case "extractcomponentalpha":
            case "extractcomponentalphanew":
            case "extractcomponentmatrixalpha":
                parsedComponent = new MfmeLegacyAlphaComponent(
                    sourceType,
                    position,
                    size,
                    ReadBool(component, "Reversed"));
                return true;

            default:
                parsedComponent = null!;
                return false;
        }
    }

    private static MfmeLegacyLampElement? ReadFirstLampElement(JsonElement component)
    {
        if (!component.TryGetProperty("LampElements", out var lampElements) || lampElements.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var lampElement in lampElements.EnumerateArray())
        {
            if (lampElement.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var numberAsText = ReadString(lampElement, "NumberAsText");
            return new MfmeLegacyLampElement(
                numberAsText,
                TryParseNullableInt(numberAsText),
                ReadColor(lampElement, "OnColor"),
                ReadString(lampElement, "BmpImageFilename"));
        }

        return null;
    }

    private static string ReadSourceType(JsonElement component)
    {
        var typeValue = ReadString(component, "$type");
        if (string.IsNullOrWhiteSpace(typeValue))
        {
            return "Unknown";
        }

        var commaIndex = typeValue.IndexOf(',', StringComparison.Ordinal);
        var qualifiedType = commaIndex >= 0 ? typeValue[..commaIndex] : typeValue;
        var lastDot = qualifiedType.LastIndexOf('.', StringComparison.Ordinal);
        return lastDot >= 0 ? qualifiedType[(lastDot + 1)..] : qualifiedType;
    }

    private static string? ReadString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : property.ToString();
    }

    private static int ReadInt(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return 0;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var intValue))
        {
            return intValue;
        }

        if (property.ValueKind == JsonValueKind.String &&
            int.TryParse(property.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        return 0;
    }

    private static bool ReadBool(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return false;
        }

        if (property.ValueKind == JsonValueKind.True)
        {
            return true;
        }

        if (property.ValueKind == JsonValueKind.False)
        {
            return false;
        }

        if (property.ValueKind == JsonValueKind.String &&
            bool.TryParse(property.GetString(), out var parsed))
        {
            return parsed;
        }

        return false;
    }

    private static MfmeLegacyPoint ReadPoint(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var point) || point.ValueKind != JsonValueKind.Object)
        {
            return new MfmeLegacyPoint(0, 0);
        }

        return new MfmeLegacyPoint(
            ReadInt(point, "X"),
            ReadInt(point, "Y"));
    }

    private static MfmeLegacyColor? ReadColor(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var color) || color.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return new MfmeLegacyColor(
            ReadFloat(color, "R"),
            ReadFloat(color, "G"),
            ReadFloat(color, "B"),
            ReadFloat(color, "A"));
    }

    private static float ReadFloat(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return 0f;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetSingle(out var number))
        {
            return number;
        }

        if (property.ValueKind == JsonValueKind.String &&
            float.TryParse(property.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        return 0f;
    }

    private static int? TryParseNullableInt(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)
            ? number
            : null;
    }

    private static MfmeExtractReadResult Error(string code, string error)
    {
        return new MfmeExtractReadResult
        {
            Extract = null,
            Warnings = [],
            Errors = [new MfmeImportWarning(code, error).Message]
        };
    }
}
