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
                Errors = decodeResult.Errors.Count > 0 ? decodeResult.Errors : ["FML decode failed."]
            };
        }

        var stagingDirectory = CreateStagingDirectory();
        Directory.CreateDirectory(stagingDirectory);
        var manifestPath = Path.Combine(stagingDirectory, "layout.json");
        var imagePaths = FmlDecodedAssetExporter.ExportImages(decodeResult.Layout!, stagingDirectory);
        File.WriteAllText(
            manifestPath,
            FmlDecodedLayoutAdapter.ToMfmeExtractManifestJson(
                decodeResult.Layout!.ToJson(indented: true),
                Path.GetFileNameWithoutExtension(fullFmlPath),
                imagePaths));

        var context = new MfmeImportContext
        {
            SourceExtractPath = manifestPath,
            ProjectRootPath = projectRootPath,
            ProjectAssetsPath = projectAssetsPath,
            CopyAssets = copyAssets
        };

        var result = _mfmeImportService.Import(context);
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
            Errors = result.Errors
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
                ["X"] = Math.Max(1, ReadNumber(component, "Geometry", "Width")),
                ["Y"] = Math.Max(1, ReadNumber(component, "Geometry", "Height"))
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
        CopyValue(component, legacy, "Height", "Height");
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

    private static void CopyValue(JsonObject source, JsonObject target, string sourceName, string targetName)
    {
        var value = FindValue(source, sourceName);
        if (value is not null && !target.ContainsKey(targetName))
        {
            target[targetName] = JsonNode.Parse(value.ToJsonString());
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
        string? mask = keys.Select(key => new { Key = key, Path = FindExportedImage(imagePaths, componentIndex, key) })
            .FirstOrDefault(entry => entry.Path is not null && entry.Key.Contains("mask", StringComparison.OrdinalIgnoreCase))
            ?.Path;
        string? overlay = keys.Select(key => new { Key = key, Path = FindExportedImage(imagePaths, componentIndex, key) })
            .FirstOrDefault(entry => entry.Path is not null && !string.Equals(entry.Path, first, StringComparison.OrdinalIgnoreCase))
            ?.Path;

        switch (MapType(type))
        {
            case "Background":
                legacy["BmpImageFilename"] = FileNameFromRelativePath(first);
                break;
            case "Lamp":
                legacy["BmpImageFilename"] = FileNameFromRelativePath(first);
                legacy["BmpMaskImageFilename"] = FileNameFromRelativePath(mask);
                legacy["Graphic"] = first is not null;
                break;
            case "Reel":
                legacy["BandBmpImageFilename"] = FileNameFromRelativePath(first);
                legacy["HasOverlay"] = overlay is not null;
                legacy["OverlayBmpImageFilename"] = FileNameFromRelativePath(overlay);
                break;
            case "Alpha":
            case "SevenSegment":
                legacy["HasOverlay"] = first is not null;
                legacy["OverlayBmpImageFilename"] = FileNameFromRelativePath(first);
                break;
        }
    }

    private static string? FindExportedImage(IReadOnlyDictionary<FmlDecodedImageKey, string> imagePaths, int componentIndex, string imageName)
        => imagePaths.TryGetValue(new FmlDecodedImageKey(componentIndex, imageName), out var path) ? path : null;

    private static string? FileNameFromRelativePath(string? relativePath)
        => string.IsNullOrWhiteSpace(relativePath) ? null : Path.GetFileName(relativePath);
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
        var sanitized = new string(value.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(sanitized) ? "image" : sanitized;
    }
}
