using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using MfmeFmlDecoder.Application;
using MfmeFmlDecoder.src.Model;
using MfmeFmlDecoder.src.Model.Component;
using OasisEditor.Features.MfmeImport;

namespace OasisEditor.Features.FmlImport;

internal interface IFmlImportService
{
    MfmeImportResult ImportFromFml(string fmlPath, string projectRootPath, string projectAssetsPath, bool copyAssets = true);
}

internal sealed class FmlImportService : IFmlImportService
{
    private readonly FmlDecoderService _decoderService;
    private readonly MfmeImportService _mfmeImportService;

    public FmlImportService(FmlDecoderService? decoderService = null, MfmeImportService? mfmeImportService = null)
    {
        _decoderService = decoderService ?? new FmlDecoderService();
        _mfmeImportService = mfmeImportService ?? new MfmeImportService();
    }

    public MfmeImportResult ImportFromFml(string fmlPath, string projectRootPath, string projectAssetsPath, bool copyAssets = true)
    {
        if (string.IsNullOrWhiteSpace(fmlPath))
        {
            return Failure("FML path is required.");
        }

        var fullFmlPath = Path.GetFullPath(fmlPath);
        if (!File.Exists(fullFmlPath))
        {
            return Failure($"FML file was not found: '{fullFmlPath}'.");
        }

        var diagnostics = new List<string>
        {
            $"FML source path: {fullFmlPath}"
        };

        var decodeResult = _decoderService.DecodeToLayout(fullFmlPath);
        if (!decodeResult.Succeeded)
        {
            return new MfmeImportResult
            {
                ImportedElements = [],
                CopiedAssetRelativePaths = [],
                InputDefinitions = [],
                SkippedLegacyComponentTypes = [],
                Warnings = decodeResult.Warnings.Select(w => new MfmeImportWarning("fml.decode.warning", w)).ToArray(),
                Errors = decodeResult.Errors.Count > 0 ? decodeResult.Errors : ["FML decode failed."],
                DebugDiagnostics = diagnostics
            };
        }

        var stagingDirectory = CreateStagingDirectory();
        Directory.CreateDirectory(stagingDirectory);
        var decodedLayoutPath = Path.Combine(stagingDirectory, "decoded-layout.json");
        var manifestPath = Path.Combine(stagingDirectory, "layout.json");
        var imagePaths = FmlDecodedAssetExporter.ExportImages(decodeResult.Layout!, stagingDirectory);
        var decodedJson = decodeResult.Layout!.ToJson(indented: true);
        File.WriteAllText(
            decodedLayoutPath,
            decodedJson);
        var adaptedManifestJson = FmlDecodedLayoutAdapter.ToMfmeExtractManifestJson(
            decodedJson,
            Path.GetFileNameWithoutExtension(fullFmlPath),
            imagePaths);
        File.WriteAllText(
            manifestPath,
            adaptedManifestJson);

        diagnostics.Add($"Temp staging directory: {stagingDirectory}");
        diagnostics.Add($"Decoded layout JSON path: {decodedLayoutPath}");
        diagnostics.Add($"Generated layout JSON path: {manifestPath}");
        diagnostics.Add($"Generated image count: {imagePaths.Count}; image root: {stagingDirectory}; image paths: {FormatImagePaths(imagePaths.Values)}");
        diagnostics.Add($"Decoded FML component counts by Type: {FormatCounts(CountDecodedTypes(decodedJson))}");
        diagnostics.Add($"Adapted manifest component counts by legacy $type: {FormatCounts(CountAdaptedTypes(adaptedManifestJson))}");

        var context = new MfmeImportContext
        {
            SourceExtractPath = manifestPath,
            ProjectRootPath = projectRootPath,
            ProjectAssetsPath = projectAssetsPath,
            CopyAssets = copyAssets
        };

        var result = _mfmeImportService.Import(context);
        result.DebugDiagnostics = diagnostics;
        if (decodeResult.Warnings.Count == 0)
        {
            return result;
        }

        return new MfmeImportResult
        {
            ImportedElements = result.ImportedElements,
            CopiedAssetRelativePaths = result.CopiedAssetRelativePaths,
            InputDefinitions = result.InputDefinitions,
            SkippedLegacyComponentTypes = result.SkippedLegacyComponentTypes,
            Warnings = [.. decodeResult.Warnings.Select(w => new MfmeImportWarning("fml.decode.warning", w)), .. result.Warnings],
            Errors = result.Errors,
            DebugDiagnostics = diagnostics
        };
    }

    private static string CreateStagingDirectory()
        => Path.Combine(Path.GetTempPath(), "OasisEditor", "FmlImport", Guid.NewGuid().ToString("N"));

    private static MfmeImportResult Failure(string error) => new()
    {
        ImportedElements = [],
        CopiedAssetRelativePaths = [],
        InputDefinitions = [],
        SkippedLegacyComponentTypes = [],
        Warnings = [],
        Errors = [error]
    };

    private static string FormatImagePaths(IEnumerable<string> imagePaths)
    {
        var paths = imagePaths.OrderBy(path => path, StringComparer.Ordinal).ToArray();
        return paths.Length == 0 ? "(none)" : string.Join(", ", paths);
    }

    private static IReadOnlyDictionary<string, int> CountDecodedTypes(string decodedJson)
        => CountComponentsByProperty(decodedJson, "Type");

    private static IReadOnlyDictionary<string, int> CountAdaptedTypes(string adaptedManifestJson)
        => CountComponentsByProperty(adaptedManifestJson, "$type", static value =>
        {
            var commaIndex = value.IndexOf(',', StringComparison.Ordinal);
            var qualified = commaIndex >= 0 ? value[..commaIndex] : value;
            var lastDot = qualified.LastIndexOf('.');
            return lastDot >= 0 ? qualified[(lastDot + 1)..] : qualified;
        });

    private static IReadOnlyDictionary<string, int> CountComponentsByProperty(
        string json,
        string propertyName,
        Func<string, string>? normalize = null)
    {
        var root = JsonNode.Parse(json)?.AsObject();
        if (root?["Components"] is not JsonArray components)
        {
            return new Dictionary<string, int>();
        }

        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var component in components.OfType<JsonObject>())
        {
            var key = component[propertyName]?.GetValue<string>() ?? "(missing)";
            key = normalize?.Invoke(key) ?? key;
            counts[key] = counts.TryGetValue(key, out var count) ? count + 1 : 1;
        }

        return counts;
    }

    private static string FormatCounts(IReadOnlyDictionary<string, int> counts)
        => counts.Count == 0
            ? "(none)"
            : string.Join(", ", counts.OrderBy(kvp => kvp.Key, StringComparer.Ordinal).Select(kvp => $"{kvp.Key}: {kvp.Value}"));
}

internal static class FmlDecodedLayoutAdapter
{
    public static string ToMfmeExtractManifestJson(
        string decodedJson,
        string layoutName,
        IReadOnlyDictionary<FmlDecodedImageKey, string>? imagePaths = null)
    {
        var root = JsonNode.Parse(decodedJson)?.AsObject() ?? new JsonObject();
        var manifest = new JsonObject
        {
            ["ASName"] = layoutName,
            ["Components"] = new JsonArray()
        };

        var output = (JsonArray)manifest["Components"]!;
        if (root["Components"] is JsonArray components)
        {
            foreach (var component in components.OfType<JsonObject>())
            {
                output.Add(ConvertComponent(component, imagePaths, output.Count));
            }
        }

        return manifest.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    private static JsonObject ConvertComponent(
        JsonObject component,
        IReadOnlyDictionary<FmlDecodedImageKey, string>? imagePaths,
        int componentIndex)
    {
        var type = component["Type"]?.GetValue<string>() ?? "Unknown";
        var legacy = new JsonObject
        {
            ["$type"] = $"Oasis.FmlImport.ExtractComponent{MapType(type)}, Oasis.FmlImport",
            ["Position"] = new JsonObject
            {
                ["X"] = ReadNumber(component, "Geometry", "X"),
                ["Y"] = ReadNumber(component, "Geometry", "Y")
            },
            ["Size"] = new JsonObject
            {
                ["X"] = Math.Max(1, ReadComponentWidth(component, type)),
                ["Y"] = Math.Max(1, ReadComponentHeight(component, type))
            }
        };

        var geometryNumber = ReadNumber(component, "Geometry", "Number");
        if (geometryNumber != 0)
        {
            legacy["Number"] = geometryNumber;
        }

        CopyValue(component, legacy, "Text", "TextBoxText");
        CopyValue(component, legacy, "Caption", "TextBoxText");
        CopyValue(component, legacy, "Number", "Number");
        CopyValue(component, legacy, "Reversed", "Reversed");
        CopyValue(component, legacy, "Reverse", "Reversed");
        CopyValue(component, legacy, "Stops", "Stops");
        CopyReelHeight(component, legacy, type);
        CopyValue(component, legacy, "ButtonNumber", "ButtonNumberAsString");
        CopyValue(component, legacy, "Button", "ButtonNumberAsString");
        CopyColor(component, legacy, "Colour", "TextColor");
        CopyColor(component, legacy, "Color", "TextColor");
        CopyColor(component, legacy, "OnColour", "SegmentOnColor");
        CopyColor(component, legacy, "OnColor", "SegmentOnColor");
        CopyImageReferences(component, legacy, type, imagePaths, componentIndex);

        return legacy;
    }

    private static string MapType(string type) => type switch
    {
        "Lamp" or "PrismLamp" => "Lamp",
        "Button" or "Checkbox" => "Button",
        "Reel" or "BandReel" or "DiscReel" or "FlipReel" => "Reel",
        "SevenSeg" or "SevenSegBlock" => "SevenSegment",
        "Alpha" or "AlphaNew" or "MatrixAlpha" or "DotAlpha" or "BFMAlpha" => "Alpha",
        "Label" => "Label",
        "Background" or "Bitmap" or "Frame" => "Background",
        _ => type
    };

    private static int ReadNumber(JsonObject component, string objectName, string propertyName)
        => component[objectName] is JsonObject obj && obj[propertyName] is JsonValue value && value.TryGetValue<double>(out var number)
            ? (int)Math.Round(number)
            : 0;

    private static int ReadComponentWidth(JsonObject component, string type)
        => ReadComponentSize(component, type, "Width");

    private static int ReadComponentHeight(JsonObject component, string type)
        => ReadComponentSize(component, type, "Height");

    private static int ReadComponentSize(JsonObject component, string type, string propertyName)
    {
        if (string.Equals(MapType(type), "Background", StringComparison.Ordinal))
        {
            var backgroundImageWidth = ReadBackgroundImageSize(component, "Width");
            var backgroundImageHeight = ReadBackgroundImageSize(component, "Height");
            if (backgroundImageWidth > 0 && backgroundImageHeight > 0)
            {
                return string.Equals(propertyName, "Width", StringComparison.Ordinal)
                    ? backgroundImageWidth
                    : backgroundImageHeight;
            }
        }

        return ReadGeometrySize(component, propertyName);
    }

    private static int ReadGeometrySize(JsonObject component, string propertyName)
        => ReadNumber(component, "Geometry", propertyName);

    private static int ReadBackgroundImageSize(JsonObject component, string propertyName)
        => component["Images"] is JsonObject images
            && images["background_image"] is JsonObject backgroundImage
            && backgroundImage[propertyName] is JsonValue value
            && value.TryGetValue<double>(out var number)
            ? (int)Math.Round(number)
            : 0;

    private static void CopyValue(JsonObject source, JsonObject target, string sourceName, string targetName)
    {
        var value = FindValue(source, sourceName);
        if (value is not null && !target.ContainsKey(targetName))
        {
            target[targetName] = JsonNode.Parse(value.ToJsonString());
        }
    }

    private static void CopyReelHeight(JsonObject source, JsonObject target, string type)
    {
        if (!string.Equals(MapType(type), "Reel", StringComparison.Ordinal) || target.ContainsKey("Height"))
        {
            return;
        }

        var reelHeight = FindValue(source, "ReelHeight") ?? FindValue(source, "Height");
        if (reelHeight is not null)
        {
            target["Height"] = JsonNode.Parse(reelHeight.ToJsonString());
        }
    }

    private static void CopyColor(JsonObject source, JsonObject target, string sourceName, string targetName)
    {
        var value = FindValue(source, sourceName);
        if (value is null || target.ContainsKey(targetName)) return;
        var text = value.ToString();
        if (string.IsNullOrWhiteSpace(text)) return;
        target[targetName] = new JsonObject { ["R"] = 1, ["G"] = 1, ["B"] = 1, ["A"] = 1 };
    }

    private static JsonNode? FindValue(JsonObject source, string name)
    {
        if (source.TryGetPropertyValue(name, out var direct) && direct is not null)
        {
            return direct;
        }

        return source["Values"] is JsonObject values && values.TryGetPropertyValue(name, out var nested) ? nested : null;
    }

    private static void CopyImageReferences(
        JsonObject component,
        JsonObject legacy,
        string type,
        IReadOnlyDictionary<FmlDecodedImageKey, string>? imagePaths,
        int componentIndex)
    {
        if (imagePaths is null || component["Images"] is not JsonObject images || images.Count == 0)
        {
            return;
        }

        var keys = images.Select(kvp => kvp.Key).OrderBy(key => key, StringComparer.Ordinal).ToArray();
        if (keys.Length == 0)
        {
            return;
        }

        string? first = FindExportedImage(imagePaths, componentIndex, keys[0]);
        string? reelBand = FindFirstImageByKeyRole(imagePaths, componentIndex, keys, IsReelBandImageKey) ?? first;
        string? overlay = FindFirstImageByKeyRole(imagePaths, componentIndex, keys, IsOverlayImageKey);

        switch (MapType(type))
        {
            case "Background":
                legacy["BmpImageFilename"] = FileNameFromRelativePath(first);
                break;
            case "Lamp":
                legacy["Graphic"] = keys.Any(key => FindExportedImage(imagePaths, componentIndex, key) is not null);
                legacy["LampElements"] = BuildLampElements(component, imagePaths, componentIndex, keys);
                break;
            case "Reel":
                legacy["BandBmpImageFilename"] = FileNameFromRelativePath(reelBand);
                legacy["HasOverlay"] = overlay is not null;
                legacy["OverlayBmpImageFilename"] = FileNameFromRelativePath(overlay);
                break;
            case "Alpha":
            case "SevenSegment":
                legacy["HasOverlay"] = overlay is not null;
                legacy["OverlayBmpImageFilename"] = FileNameFromRelativePath(overlay);
                break;
        }
    }

    private static string? FindFirstImageByKeyRole(
        IReadOnlyDictionary<FmlDecodedImageKey, string> imagePaths,
        int componentIndex,
        IEnumerable<string> imageKeys,
        Func<string, bool> matchesRole)
    {
        foreach (var imageKey in imageKeys)
        {
            if (!matchesRole(imageKey))
            {
                continue;
            }

            var path = FindExportedImage(imagePaths, componentIndex, imageKey);
            if (path is not null)
            {
                return path;
            }
        }

        return null;
    }

    private static bool IsReelBandImageKey(string imageKey)
    {
        var normalizedKey = NormalizeImageKey(imageKey);
        return normalizedKey.Contains("band", StringComparison.Ordinal)
            || normalizedKey.Contains("gradient", StringComparison.Ordinal)
            || normalizedKey.Contains("strip", StringComparison.Ordinal)
            || normalizedKey.Contains("reel_image", StringComparison.Ordinal)
            || normalizedKey.EndsWith("reel", StringComparison.Ordinal);
    }

    private static bool IsOverlayImageKey(string imageKey)
    {
        var normalizedKey = NormalizeImageKey(imageKey);
        return normalizedKey.Contains("overlay", StringComparison.Ordinal)
            || normalizedKey.Contains("over_lay", StringComparison.Ordinal)
            || normalizedKey.Contains("window", StringComparison.Ordinal)
            || normalizedKey.Contains("cutout", StringComparison.Ordinal)
            || normalizedKey.Contains("cut_out", StringComparison.Ordinal)
            || normalizedKey.Contains("mask_overlay", StringComparison.Ordinal);
    }

    private static string? FindExportedImage(IReadOnlyDictionary<FmlDecodedImageKey, string> imagePaths, int componentIndex, string imageName)
        => imagePaths.TryGetValue(new FmlDecodedImageKey(componentIndex, imageName), out var path) ? path : null;

    private static string? FileNameFromRelativePath(string? relativePath)
        => string.IsNullOrWhiteSpace(relativePath) ? null : Path.GetFileName(relativePath);

    private static JsonArray BuildLampElements(
        JsonObject component,
        IReadOnlyDictionary<FmlDecodedImageKey, string> imagePaths,
        int componentIndex,
        IReadOnlyList<string> imageKeys)
    {
        var elements = new JsonArray();
        var sublampNumbers = ReadSublampNumbers(component);

        if (sublampNumbers.Count > 0)
        {
            foreach (var entry in sublampNumbers.OrderBy(kvp => kvp.Key))
            {
                var mainImage = FindLampImage(imagePaths, componentIndex, imageKeys, entry.Key, isMask: false)
                    ?? FindLampImage(imagePaths, componentIndex, imageKeys, entry.Key, isMask: null);
                var maskImage = FindLampImage(imagePaths, componentIndex, imageKeys, entry.Key, isMask: true);
                elements.Add(CreateLampElement(entry.Value, mainImage, maskImage));
            }
        }

        if (elements.Count == 0)
        {
            var lampNumber = FindLampNumber(component);
            var mainImage = FindFirstLampImage(imagePaths, componentIndex, imageKeys, isMask: false);
            var maskImage = FindFirstLampImage(imagePaths, componentIndex, imageKeys, isMask: true);
            elements.Add(CreateLampElement(lampNumber, mainImage, maskImage));
        }

        return elements;
    }

    private static JsonObject CreateLampElement(int? number, string? imagePath, string? maskPath)
    {
        var lampElement = new JsonObject
        {
            ["NumberAsText"] = number?.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["BmpImageFilename"] = FileNameFromRelativePath(imagePath),
            ["BmpMaskImageFilename"] = FileNameFromRelativePath(maskPath),
            ["Graphic"] = imagePath is not null
        };

        return lampElement;
    }

    private static IReadOnlyDictionary<int, int> ReadSublampNumbers(JsonObject component)
    {
        var numbers = new Dictionary<int, int>();
        if (component["SubLampNumberTable"] is not JsonObject table)
        {
            return numbers;
        }

        foreach (var kvp in table)
        {
            const string prefix = "Lamp";
            if (!kvp.Key.StartsWith(prefix, StringComparison.Ordinal)
                || !int.TryParse(kvp.Key[prefix.Length..], out var sublampIndex)
                || kvp.Value is not JsonValue value
                || !value.TryGetValue<int>(out var sublampNumber))
            {
                continue;
            }

            numbers[sublampIndex] = sublampNumber;
        }

        return numbers;
    }

    private static int? FindLampNumber(JsonObject component)
    {
        var number = FindValue(component, "Number");
        if (TryGetInt(number, out var parsedNumber))
        {
            return parsedNumber;
        }

        if (component["Geometry"] is JsonObject geometry && TryGetInt(geometry["Number"], out var geometryNumber) && geometryNumber != 0)
        {
            return geometryNumber;
        }

        return null;
    }

    private static bool TryGetInt(JsonNode? node, out int value)
    {
        value = 0;
        if (node is not JsonValue jsonValue)
        {
            return false;
        }

        if (jsonValue.TryGetValue<int>(out value))
        {
            return true;
        }

        if (jsonValue.TryGetValue<double>(out var doubleValue))
        {
            value = (int)Math.Round(doubleValue);
            return true;
        }

        if (jsonValue.TryGetValue<string>(out var stringValue)
            && int.TryParse(stringValue, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out value))
        {
            return true;
        }

        return false;
    }

    private static string? FindLampImage(
        IReadOnlyDictionary<FmlDecodedImageKey, string> imagePaths,
        int componentIndex,
        IEnumerable<string> imageKeys,
        int sublampIndex,
        bool? isMask)
    {
        foreach (var imageKey in imageKeys)
        {
            if (!IsSublampImageKey(imageKey, sublampIndex) || !MatchesMask(imageKey, isMask))
            {
                continue;
            }

            var path = FindExportedImage(imagePaths, componentIndex, imageKey);
            if (path is not null)
            {
                return path;
            }
        }

        return null;
    }

    private static bool IsSublampImageKey(string imageKey, int sublampIndex)
    {
        var normalizedKey = NormalizeImageKey(imageKey);
        return normalizedKey.StartsWith($"sublamp_{sublampIndex}_", StringComparison.Ordinal)
            || imageKey.StartsWith($"Sublamp {sublampIndex} ", StringComparison.OrdinalIgnoreCase);
    }

    private static string? FindFirstLampImage(
        IReadOnlyDictionary<FmlDecodedImageKey, string> imagePaths,
        int componentIndex,
        IEnumerable<string> imageKeys,
        bool? isMask)
    {
        foreach (var imageKey in imageKeys)
        {
            if (!MatchesMask(imageKey, isMask))
            {
                continue;
            }

            var path = FindExportedImage(imagePaths, componentIndex, imageKey);
            if (path is not null)
            {
                return path;
            }
        }

        return null;
    }

    private static bool MatchesMask(string imageKey, bool? isMask)
    {
        if (isMask is null)
        {
            return true;
        }

        var normalizedKey = NormalizeImageKey(imageKey);
        var keyIsMask = normalizedKey.Contains("mask", StringComparison.Ordinal);
        return keyIsMask == isMask.Value;
    }

    private static string NormalizeImageKey(string imageKey)
    {
        var chars = imageKey
            .Select(ch => char.IsLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : '_')
            .ToArray();

        var normalized = new string(chars);
        while (normalized.Contains("__", StringComparison.Ordinal))
        {
            normalized = normalized.Replace("__", "_", StringComparison.Ordinal);
        }

        return normalized.Trim('_');
    }
}

internal readonly record struct FmlDecodedImageKey(int ComponentIndex, string ImageName);

internal static class FmlDecodedAssetExporter
{
    public static IReadOnlyDictionary<FmlDecodedImageKey, string> ExportImages(Layout layout, string stagingDirectory)
    {
        var imagePaths = new Dictionary<FmlDecodedImageKey, string>();
        for (var componentIndex = 0; componentIndex < layout.Components.Count; componentIndex++)
        {
            var component = layout.Components[componentIndex];
            foreach (var image in component.Images.OrderBy(entry => entry.Key, StringComparer.Ordinal))
            {
                var folder = GetLegacyImageFolder(component);
                var fileName = $"{componentIndex:D4}_{SanitizeFileName(image.Key)}.bmp";
                var relativePath = $"{folder}/{fileName}";
                var outputPath = Path.Combine(stagingDirectory, folder, fileName);
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
                File.WriteAllBytes(outputPath, image.Value.Bytes);
                imagePaths[new FmlDecodedImageKey(componentIndex, image.Key)] = relativePath;
            }
        }

        return imagePaths;
    }

    private static string GetLegacyImageFolder(BaseComponent component)
    {
        var type = component.GetType().Name;
        return type switch
        {
            "Lamp" or "PrismLamp" => "lamps",
            "Reel" or "BandReel" or "DiscReel" or "FlipReel" => "reels",
            "Alpha" or "AlphaNew" or "MatrixAlpha" or "DotAlpha" or "BFMAlpha" or "SevenSeg" or "SevenSegBlock" => "reels",
            _ => "background"
        };
    }

    private static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(value
            .Select(ch => char.IsLetterOrDigit(ch) || ch == '-' || ch == '_' ? char.ToLowerInvariant(ch) : '_')
            .Select(ch => invalid.Contains(ch) ? '_' : ch)
            .ToArray())
            .Trim('_');

        while (sanitized.Contains("__", StringComparison.Ordinal))
        {
            sanitized = sanitized.Replace("__", "_", StringComparison.Ordinal);
        }

        return string.IsNullOrWhiteSpace(sanitized) ? "image" : sanitized;
    }
}
