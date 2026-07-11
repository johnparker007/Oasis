using System.Diagnostics;
using System.IO;
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
    private readonly FmlImportDiagnosticsWriter _diagnosticsWriter;

    public FmlImportService(FmlDecoderService? decoderService = null, FmlToOasisMapper? mapper = null, LayoutImportAssetCopier? assetCopier = null, FmlImportDiagnosticsWriter? diagnosticsWriter = null)
    {
        _decoderService = decoderService ?? new FmlDecoderService();
        _mapper = mapper ?? new FmlToOasisMapper();
        _assetCopier = assetCopier ?? new LayoutImportAssetCopier();
        _diagnosticsWriter = diagnosticsWriter ?? new FmlImportDiagnosticsWriter();
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

        var importStartedUtc = DateTimeOffset.UtcNow;
        var totalStopwatch = Stopwatch.StartNew();
        var stagingDirectory = CreateStagingDirectory();
        Directory.CreateDirectory(stagingDirectory);

        var diagnostics = new List<string>
        {
            $"FML source path: {fullFmlPath}",
            $"Persistent staging directory: {stagingDirectory}"
        };

        var decodeStopwatch = Stopwatch.StartNew();
        var decodeResult = _decoderService.DecodeToLayout(fullFmlPath);
        decodeStopwatch.Stop();

        if (!decodeResult.Succeeded)
        {
            totalStopwatch.Stop();
            _diagnosticsWriter.WriteReport(new FmlImportDiagnosticsReport
            {
                FmlPath = fullFmlPath,
                ImportTimestampUtc = importStartedUtc,
                StagingDirectory = stagingDirectory,
                DecoderErrors = decodeResult.Errors,
                DecoderWarnings = decodeResult.Warnings,
                DecodeElapsed = decodeStopwatch.Elapsed,
                TotalElapsed = totalStopwatch.Elapsed
            });

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

        var layout = decodeResult.Layout!;
        _diagnosticsWriter.WriteDecodedLayout(layout, stagingDirectory);
        var imagePaths = FmlDecodedAssetExporter.ExportImages(layout, stagingDirectory);

        FmlToOasisMapResult mapResult;
        var mappingStopwatch = Stopwatch.StartNew();
        try
        {
            mapResult = _mapper.Map(layout, imagePaths);
        }
        catch (Exception ex)
        {
            mappingStopwatch.Stop();
            totalStopwatch.Stop();
            _diagnosticsWriter.WriteReport(new FmlImportDiagnosticsReport
            {
                FmlPath = fullFmlPath,
                ImportTimestampUtc = importStartedUtc,
                StagingDirectory = stagingDirectory,
                Layout = layout,
                ImagePaths = imagePaths,
                DecoderWarnings = decodeResult.Warnings,
                MapperWarnings = [new LayoutImportWarning("fml.mapping.exception", ex.Message)],
                DecodeElapsed = decodeStopwatch.Elapsed,
                MappingElapsed = mappingStopwatch.Elapsed,
                TotalElapsed = totalStopwatch.Elapsed
            });

            return new LayoutImportResult
            {
                ImportedElements = [],
                CopiedAssetRelativePaths = [],
                InputDefinitions = [],
                UnsupportedComponentTypes = [],
                Warnings = decodeResult.Warnings.Select(w => new LayoutImportWarning("fml.decode.warning", w)).ToArray(),
                Errors = [$"FML mapping failed: {ex.Message}"],
                DebugDiagnostics = diagnostics
            };
        }
        mappingStopwatch.Stop();
        _diagnosticsWriter.WriteMappedElements(mapResult, layout, imagePaths, stagingDirectory);

        diagnostics.Add($"Generated image count: {imagePaths.Count}; image root: {stagingDirectory}; image paths: {FormatImagePaths(imagePaths.Values)}");
        diagnostics.Add($"Decoded FML component counts by Type: {FormatCounts(CountDecodedTypes(layout))}");

        var assetCopyStopwatch = Stopwatch.StartNew();
        var assetResult = _assetCopier.CopyAssetsFromStaging(
            stagingDirectory,
            Path.GetFileNameWithoutExtension(fullFmlPath),
            projectAssetsPath,
            copyAssets,
            mapResult.Elements);
        assetCopyStopwatch.Stop();
        totalStopwatch.Stop();

        _diagnosticsWriter.WriteReport(new FmlImportDiagnosticsReport
        {
            FmlPath = fullFmlPath,
            ImportTimestampUtc = importStartedUtc,
            StagingDirectory = stagingDirectory,
            Layout = layout,
            MapResult = mapResult,
            ImagePaths = imagePaths,
            DecoderWarnings = decodeResult.Warnings,
            MapperWarnings = mapResult.Warnings,
            AssetCopyWarnings = assetResult.Warnings,
            UnsupportedComponentTypes = mapResult.UnsupportedComponentTypes,
            ImportedAssetCount = assetResult.CopiedAssetRelativePaths.Count,
            DecodeElapsed = decodeStopwatch.Elapsed,
            MappingElapsed = mappingStopwatch.Elapsed,
            AssetCopyElapsed = assetCopyStopwatch.Elapsed,
            TotalElapsed = totalStopwatch.Elapsed
        });

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

    private static IReadOnlyDictionary<string, int> CountDecodedTypes(Layout layout)
        => layout.Components
            .GroupBy(component => component.GetType().Name, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

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
