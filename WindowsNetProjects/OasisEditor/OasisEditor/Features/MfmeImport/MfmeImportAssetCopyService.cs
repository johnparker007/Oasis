using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OasisEditor.Features.MfmeImport;

internal sealed record MfmeImportAssetCopyResult
{
    public IReadOnlyList<PanelElementModel> Elements { get; init; } = [];

    public IReadOnlyList<string> CopiedAssets { get; init; } = [];

    public IReadOnlyList<MfmeImportWarning> Warnings { get; init; } = [];
}

internal sealed class MfmeImportAssetCopyService
{
    private const string ImportRootFolder = "MfmeImport";

    public MfmeImportAssetCopyResult CopyAssets(
        MfmeImportContext context,
        string layoutName,
        IReadOnlyList<PanelElementModel> elements)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(layoutName);
        ArgumentNullException.ThrowIfNull(elements);

        var copied = new List<string>();
        var warnings = new List<MfmeImportWarning>();
        var updated = new List<PanelElementModel>(elements.Count);

        var safeLayoutName = SanitizeSegment(layoutName);
        var assetsRootFullPath = Path.GetFullPath(context.AssetsRootPath);
        var projectRootFullPath = Path.GetFullPath(context.ProjectRootPath);
        var sourceExtractFullPath = Path.GetFullPath(context.SourceExtractPath);

        var copiedDestinationBySource = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var element in elements)
        {
            if (string.IsNullOrWhiteSpace(element.AssetPath))
            {
                updated.Add(element);
                continue;
            }

            if (!TryResolveSourceAsset(sourceExtractFullPath, element.AssetPath, out var sourceAssetPath, out var categoryFolder, out var fileName))
            {
                warnings.Add(new MfmeImportWarning(
                    Code: "invalid-asset-path",
                    Message: $"Skipped invalid asset path '{element.AssetPath}'.",
                    ContextPath: element.ObjectId));
                updated.Add(CloneWithAssetPath(element, null));
                continue;
            }

            if (!File.Exists(sourceAssetPath))
            {
                warnings.Add(new MfmeImportWarning(
                    Code: "missing-asset",
                    Message: $"Missing MFME source image '{sourceAssetPath}'.",
                    ContextPath: element.ObjectId));
                updated.Add(CloneWithAssetPath(element, null));
                continue;
            }

            if (copiedDestinationBySource.TryGetValue(sourceAssetPath, out var existingDestination))
            {
                updated.Add(CloneWithAssetPath(element, ToProjectRelativePath(projectRootFullPath, existingDestination)));
                continue;
            }

            var destinationDirectory = Path.Combine(assetsRootFullPath, ImportRootFolder, safeLayoutName, categoryFolder);
            Directory.CreateDirectory(destinationDirectory);

            var safeFileName = SanitizeFileName(fileName);
            var destinationPath = BuildUniqueDestinationPath(destinationDirectory, safeFileName);

            File.Copy(sourceAssetPath, destinationPath, overwrite: false);
            copied.Add(destinationPath);
            copiedDestinationBySource[sourceAssetPath] = destinationPath;

            updated.Add(CloneWithAssetPath(element, ToProjectRelativePath(projectRootFullPath, destinationPath)));
        }

        return new MfmeImportAssetCopyResult
        {
            Elements = updated,
            CopiedAssets = copied,
            Warnings = warnings
        };
    }

    private static bool TryResolveSourceAsset(
        string sourceExtractRoot,
        string assetPath,
        out string sourceAssetPath,
        out string categoryFolder,
        out string fileName)
    {
        sourceAssetPath = string.Empty;
        categoryFolder = string.Empty;
        fileName = string.Empty;

        var normalized = assetPath.Replace('\\', '/').Trim();
        var parts = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            return false;
        }

        categoryFolder = NormalizeCategory(parts[0]);
        fileName = parts[^1];

        var candidate = Path.GetFullPath(Path.Combine(sourceExtractRoot, parts[0], fileName));
        if (!candidate.StartsWith(sourceExtractRoot, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        sourceAssetPath = candidate;
        return true;
    }

    private static string BuildUniqueDestinationPath(string directory, string fileName)
    {
        var name = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);
        var candidate = Path.Combine(directory, fileName);

        if (!File.Exists(candidate))
        {
            return candidate;
        }

        var suffix = 2;
        while (true)
        {
            candidate = Path.Combine(directory, $"{name}-{suffix}{extension}");
            if (!File.Exists(candidate))
            {
                return candidate;
            }

            suffix++;
        }
    }

    private static string NormalizeCategory(string sourceCategory)
    {
        return sourceCategory.ToLowerInvariant() switch
        {
            "background" => "Background",
            "lamps" => "Lamps",
            "reels" => "Reels",
            _ => SanitizeSegment(sourceCategory)
        };
    }

    private static string SanitizeSegment(string value)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var chars = value
            .Trim()
            .Select(ch => invalidChars.Contains(ch) ? '_' : ch)
            .ToArray();

        var sanitized = new string(chars);
        return string.IsNullOrWhiteSpace(sanitized) ? "Unnamed" : sanitized;
    }

    private static string SanitizeFileName(string value)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var chars = value
            .Trim()
            .Select(ch => invalidChars.Contains(ch) ? '_' : ch)
            .ToArray();

        var sanitized = new string(chars);
        return string.IsNullOrWhiteSpace(sanitized) ? "asset.bin" : sanitized;
    }

    private static string ToProjectRelativePath(string projectRootPath, string absolutePath)
    {
        var relative = Path.GetRelativePath(projectRootPath, absolutePath);
        return relative.Replace('\\', '/');
    }

    private static PanelElementModel CloneWithAssetPath(PanelElementModel source, string? assetPath)
    {
        return new PanelElementModel
        {
            ObjectId = source.ObjectId,
            Name = source.Name,
            Kind = source.Kind,
            X = source.X,
            Y = source.Y,
            Width = source.Width,
            Height = source.Height,
            AssetPath = assetPath,
            MfmeSourceType = source.MfmeSourceType,
            MfmeSourceId = source.MfmeSourceId,
            DisplayNumber = source.DisplayNumber,
            PrimaryColor = source.PrimaryColor,
            SecondaryColor = source.SecondaryColor,
            TextColor = source.TextColor,
            Text = source.Text,
            Reversed = source.Reversed,
            Stops = source.Stops,
            VisibleScale = source.VisibleScale
        };
    }
}
