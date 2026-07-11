using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using MfmeFmlDecoder.Application;
using MfmeFmlDecoder.src.Model;
using MfmeFmlDecoder.src.Model.Component;
using OasisEditor.Features.LayoutImport;

namespace OasisEditor.Features.FmlImport;

internal interface IFmlImportService
{
    LayoutImportResult ImportFromFml(string fmlPath, string projectRootPath, string projectAssetsPath, bool copyAssets = true);
}

internal sealed class FmlImportService : IFmlImportService
{
    private readonly FmlDecoderService _decoderService;
    private readonly FmlToOasisMapper _mapper;
    private readonly LayoutImportAssetCopier _assetCopier;

    public FmlImportService(FmlDecoderService? decoderService = null, FmlToOasisMapper? mapper = null, LayoutImportAssetCopier? assetCopier = null)
    {
        _decoderService = decoderService ?? new FmlDecoderService();
        _mapper = mapper ?? new FmlToOasisMapper();
        _assetCopier = assetCopier ?? new LayoutImportAssetCopier();
    }

    public LayoutImportResult ImportFromFml(string fmlPath, string projectRootPath, string projectAssetsPath, bool copyAssets = true)
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
            return new LayoutImportResult
            {
                ImportedElements = [],
                CopiedAssetRelativePaths = [],
                InputDefinitions = [],
                UnsupportedComponentTypes = [],
                Warnings = decodeResult.Warnings.Select(w => new LayoutImportWarning("fml.decode.warning", w)).ToArray(),
                Errors = decodeResult.Errors.Count > 0 ? decodeResult.Errors : ["FML decode failed."],
                DebugDiagnostics = diagnostics
            };
        }

        var stagingDirectory = CreateStagingDirectory();
        Directory.CreateDirectory(stagingDirectory);
        var imagePaths = FmlDecodedAssetExporter.ExportImages(decodeResult.Layout!, stagingDirectory);
        var mapResult = _mapper.Map(decodeResult.Layout!, imagePaths);

        diagnostics.Add($"Temp staging directory: {stagingDirectory}");
        diagnostics.Add($"Generated image count: {imagePaths.Count}; image root: {stagingDirectory}; image paths: {FormatImagePaths(imagePaths.Values)}");
        diagnostics.Add($"Decoded FML component counts by Type: {FormatCounts(CountDecodedTypes(decodeResult.Layout!.ToJson(indented: false)))}");

        var assetResult = _assetCopier.CopyAssetsFromStaging(
            stagingDirectory,
            Path.GetFileNameWithoutExtension(fullFmlPath),
            projectAssetsPath,
            copyAssets,
            mapResult.Elements);

        return new LayoutImportResult
        {
            ImportedElements = assetResult.Elements,
            CopiedAssetRelativePaths = assetResult.CopiedAssetRelativePaths,
            InputDefinitions = mapResult.InputDefinitions,
            UnsupportedComponentTypes = mapResult.UnsupportedComponentTypes,
            Warnings = [.. decodeResult.Warnings.Select(w => new LayoutImportWarning("fml.decode.warning", w)), .. mapResult.Warnings, .. assetResult.Warnings],
            Errors = assetResult.Errors,
            DebugDiagnostics = diagnostics
        };
    }

    private static string CreateStagingDirectory()
        => Path.Combine(Path.GetTempPath(), "OasisEditor", "FmlImport", Guid.NewGuid().ToString("N"));

    private static LayoutImportResult Failure(string error) => new()
    {
        ImportedElements = [],
        CopiedAssetRelativePaths = [],
        InputDefinitions = [],
        UnsupportedComponentTypes = [],
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
