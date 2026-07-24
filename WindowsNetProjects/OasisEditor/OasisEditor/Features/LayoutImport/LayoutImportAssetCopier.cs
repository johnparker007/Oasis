using System.IO;
using System.Text;
using SkiaSharp;
using OasisEditor.Features.FmlImport;

namespace OasisEditor.Features.LayoutImport;

internal sealed class LayoutImportAssetCopier
{

    public LayoutAssetCopyResult CopyAssetsFromStaging(
        string stagingRootPath,
        string layoutName,
        string projectAssetsPath,
        bool copyAssets,
        IReadOnlyList<PanelElementModel> elements,
        FmlBackgroundMode backgroundMode = FmlBackgroundMode.ImageBackedBackground)
    {
        ArgumentNullException.ThrowIfNull(elements);

        if (!copyAssets)
        {
            return new LayoutAssetCopyResult
            {
                Elements = FmlPanelElementOrdering.ArrangeForBackgroundMode(elements.ToArray(), backgroundMode),
                CopiedAssetRelativePaths = [],
                Warnings = [],
                Errors = []
            };
        }

        var warnings = new List<LayoutImportWarning>();
        var errors = new List<string>();
        var copied = new List<string>();
        var projectAssetsRoot = EnsureDirectoryAndPath(projectAssetsPath, errors, "layout.import.assetsRoot.invalid");
        if (projectAssetsRoot is null)
        {
            return CreateError(elements, warnings, errors);
        }

        var layoutSegment = SanitizePathSegment(layoutName, "layout");
        var destinationRoot = Path.Combine(projectAssetsRoot, "FmlImport", layoutSegment);
        var pathMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var mappedElements = elements
            .Select(element => MapElementAssets(element, stagingRootPath, destinationRoot, projectAssetsRoot, pathMap, copied, warnings, errors))
            .ToArray();

        if (errors.Count == 0)
        {
            mappedElements = backgroundMode == FmlBackgroundMode.ImageBackedBackground
                ? BakeDisplayOverlaysIntoBackgrounds(mappedElements, projectAssetsRoot, copied, errors)
                : mappedElements;
        }

        if (errors.Count > 0)
        {
            return CreateError(elements, warnings, errors);
        }

        mappedElements = FmlPanelElementOrdering.ArrangeForBackgroundMode(mappedElements, backgroundMode).ToArray();
        return new LayoutAssetCopyResult
        {
            Elements = mappedElements,
            CopiedAssetRelativePaths = copied,
            Warnings = warnings,
            Errors = []
        };
    }

    private static readonly IReadOnlyDictionary<string, string> StagingFolderToProjectFolder = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["background"] = "Background",
        ["lamps"] = "Lamps",
        ["reels"] = "Reels"
    };

    private static PanelElementModel MapElementAssets(
        PanelElementModel element,
        string stagingRootPath,
        string destinationRoot,
        string projectAssetsRoot,
        IDictionary<string, string> pathMap,
        ICollection<string> copied,
        ICollection<LayoutImportWarning> warnings,
        ICollection<string> errors)
    {
        var primary = CopyOneAsset(
            element.Kind,
            element.AssetPath,
            stagingRootPath,
            destinationRoot,
            projectAssetsRoot,
            pathMap,
            copied,
            warnings,
            errors);

        var secondary = CopyOneAsset(
            element.Kind,
            element.SecondaryAssetPath,
            stagingRootPath,
            destinationRoot,
            projectAssetsRoot,
            pathMap,
            copied,
            warnings,
            errors);


        if (element.Kind == PanelElementKind.Lamp &&
            !string.IsNullOrWhiteSpace(primary) &&
            string.Equals(Path.GetExtension(element.AssetPath), ".bmp", StringComparison.OrdinalIgnoreCase))
        {
            var sourceLampPath = TryResolveSourceAssetPath(element.AssetPath, stagingRootPath);
            var sourceMaskPath = TryResolveSourceAssetPath(element.SecondaryAssetPath, stagingRootPath);
            var outputLampPath = Path.Combine(projectAssetsRoot, primary["Assets/".Length..].Replace('/', Path.DirectorySeparatorChar));

            if (sourceLampPath is not null && File.Exists(sourceLampPath))
            {
                if (!MfmeLampAssetPostProcessor.TryProcessLamp(sourceLampPath, sourceMaskPath, outputLampPath, applyMaskTint: true, out var processingError))
                {
                    errors.Add($"Failed to process lamp asset '{element.AssetPath}': {processingError}");
                }
            }
        }

        var transmissionMask = element.ReelLampTransmissionMaskAssetPath;
        if (element.Kind == PanelElementKind.Reel && element.IsOpaqueReel && !string.IsNullOrWhiteSpace(primary))
        {
            var outputReelPath = Path.Combine(projectAssetsRoot, primary["Assets/".Length..].Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(outputReelPath))
            {
                var maskPath = Path.Combine(Path.GetDirectoryName(outputReelPath)!, Path.GetFileNameWithoutExtension(outputReelPath) + "_transmission.png");
                if (MfmeReelLampTransmissionMaskGenerator.TryGenerate(outputReelPath, maskPath, out var processingError))
                {
                    var relative = Path.GetRelativePath(projectAssetsRoot, maskPath).Replace('\\', '/');
                    transmissionMask = $"Assets/{relative}";
                    copied.Add(transmissionMask);
                }
                else
                {
                    errors.Add($"Failed to generate reel transmission mask '{element.AssetPath}': {processingError}");
                }
            }
        }
        else if (element.Kind == PanelElementKind.Reel && !element.IsOpaqueReel)
        {
            transmissionMask = null;
        }

        var mapped = PanelElementModelCloner.Clone(element);
        mapped.AssetPath = primary;
        mapped.SecondaryAssetPath = secondary;
        mapped.ReelLampTransmissionMaskAssetPath = transmissionMask;
        return mapped;
    }

    private static PanelElementModel[] BakeDisplayOverlaysIntoBackgrounds(
        PanelElementModel[] elements,
        string projectAssetsRoot,
        ICollection<string> copied,
        ICollection<string> errors)
    {
        if (!elements.Any(IsBackgroundCutoutDisplay))
        {
            return elements;
        }

        var updatedElements = elements;
        for (var index = 0; index < updatedElements.Length; index++)
        {
            var background = updatedElements[index];
            if (background.Kind != PanelElementKind.Background || string.IsNullOrWhiteSpace(background.AssetPath))
            {
                continue;
            }

            var backgroundPath = TryResolveProjectAssetPath(background.AssetPath, projectAssetsRoot);
            if (backgroundPath is null || !File.Exists(backgroundPath))
            {
                continue;
            }

            if (!MfmeBackgroundOverlayPostProcessor.TryBakeDisplayOverlays(backgroundPath, background, updatedElements, projectAssetsRoot, copied, out var updatedBackgroundPath, out var processingError))
            {
                errors.Add($"Failed to bake MFME display overlay/cutout into background asset '{background.AssetPath}': {processingError}");
                continue;
            }

            if (!string.IsNullOrWhiteSpace(updatedBackgroundPath))
            {
                updatedElements = (PanelElementModel[])updatedElements.Clone();
                updatedElements[index] = CloneWithAssetPath(background, updatedBackgroundPath);
            }
        }

        return updatedElements;
    }

    private static PanelElementModel[] SendReelsAndAlphaDisplaysToBack(PanelElementModel[] elements)
    {
        if (!elements.Any(IsBackgroundCutoutDisplay))
        {
            return elements;
        }

        return elements
            .Where(IsBackgroundCutoutDisplay)
            .Concat(elements.Where(element => !IsBackgroundCutoutDisplay(element)))
            .ToArray();
    }

    private static bool IsBackgroundCutoutDisplay(PanelElementModel element)
    {
        return element.Kind is PanelElementKind.Reel or PanelElementKind.Alpha or PanelElementKind.SevenSegment or PanelElementKind.VfdDotMatrix;
    }

    private static PanelElementModel CloneWithAssetPath(PanelElementModel element, string? assetPath)
    {
        var clone = PanelElementModelCloner.Clone(element);
        clone.AssetPath = assetPath;
        return clone;
    }

    private static string? TryResolveProjectAssetPath(string? projectRelativePath, string projectAssetsRoot)
    {
        if (string.IsNullOrWhiteSpace(projectRelativePath) || !projectRelativePath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var relativePath = projectRelativePath["Assets/".Length..].Replace('/', Path.DirectorySeparatorChar);
        var fullPath = Path.GetFullPath(Path.Combine(projectAssetsRoot, relativePath));
        return IsPathUnderRoot(fullPath, projectAssetsRoot) ? fullPath : null;
    }

    private static string? CopyOneAsset(
        PanelElementKind elementKind,
        string? stagingRelativePath,
        string stagingRootPath,
        string destinationRoot,
        string projectAssetsRoot,
        IDictionary<string, string> pathMap,
        ICollection<string> copied,
        ICollection<LayoutImportWarning> warnings,
        ICollection<string> errors)
    {
        if (string.IsNullOrWhiteSpace(stagingRelativePath))
        {
            return null;
        }

        var normalizedRelativePath = stagingRelativePath.Replace('\\', '/').Trim();
        if (!TrySplitStagingRelativePath(normalizedRelativePath, out var folder, out var filename))
        {
            warnings.Add(new LayoutImportWarning(
                "layout.import.asset.path.invalid",
                $"Asset path '{stagingRelativePath}' is invalid and was skipped.",
                stagingRelativePath));
            return null;
        }

        var safeFolder = StagingFolderToProjectFolder.TryGetValue(folder, out var mappedFolder)
            ? mappedFolder
            : SanitizePathSegment(folder, "Misc");
        var safeFileName = SanitizeFileName(filename);

        var sourceFullPath = Path.GetFullPath(Path.Combine(stagingRootPath, folder, filename));
        var stagingRootFullPath = Path.GetFullPath(stagingRootPath);

        if (!IsPathUnderRoot(sourceFullPath, stagingRootFullPath))
        {
            warnings.Add(new LayoutImportWarning(
                "layout.import.asset.path.escape",
                $"Asset path '{stagingRelativePath}' escapes staging root and was skipped.",
                stagingRelativePath));
            return null;
        }

        if (!File.Exists(sourceFullPath))
        {
            warnings.Add(new LayoutImportWarning(
                "layout.import.asset.missing",
                $"Asset file '{stagingRelativePath}' was not found in staging and was skipped.",
                stagingRelativePath));
            return null;
        }

        if (pathMap.TryGetValue(sourceFullPath, out var existingRelativePath))
        {
            return existingRelativePath;
        }

        var destinationFolderPath = Path.Combine(destinationRoot, safeFolder);
        Directory.CreateDirectory(destinationFolderPath);

        var outputFileName = elementKind == PanelElementKind.Lamp && string.Equals(folder, "lamps", StringComparison.OrdinalIgnoreCase)
            ? Path.ChangeExtension(safeFileName, ".png")
            : safeFileName;
        var destinationFullPath = GetUniqueDestinationPath(destinationFolderPath, outputFileName);
        var destinationFullPathNormalized = Path.GetFullPath(destinationFullPath);

        if (!IsPathUnderRoot(destinationFullPathNormalized, projectAssetsRoot))
        {
            errors.Add($"Destination asset path escaped project assets root: '{destinationFullPathNormalized}'.");
            return null;
        }

        File.Copy(sourceFullPath, destinationFullPathNormalized, overwrite: false);

        var relativeFromAssetsRoot = Path.GetRelativePath(projectAssetsRoot, destinationFullPathNormalized).Replace('\\', '/');
        var projectRelativePath = $"Assets/{relativeFromAssetsRoot}";
        pathMap[sourceFullPath] = projectRelativePath;
        copied.Add(projectRelativePath);
        return projectRelativePath;
    }


    private static string? TryResolveSourceAssetPath(string? stagingRelativePath, string stagingRootPath)
    {
        if (string.IsNullOrWhiteSpace(stagingRelativePath))
        {
            return null;
        }

        var normalized = stagingRelativePath.Replace('\\', '/').Trim();
        if (!TrySplitStagingRelativePath(normalized, out var folder, out var filename))
        {
            return null;
        }

        var sourceFullPath = Path.GetFullPath(Path.Combine(stagingRootPath, folder, filename));
        return IsPathUnderRoot(sourceFullPath, Path.GetFullPath(stagingRootPath)) ? sourceFullPath : null;
    }

    private static string? EnsureDirectoryAndPath(string path, ICollection<string> errors, string code)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            errors.Add(new LayoutImportWarning(code, "Project assets path is required.").Message);
            return null;
        }

        try
        {
            var fullPath = Path.GetFullPath(path.Trim());
            Directory.CreateDirectory(fullPath);
            return fullPath;
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            errors.Add(new LayoutImportWarning(code, $"Unable to prepare project assets path '{path}': {ex.Message}").Message);
            return null;
        }
    }

    private static bool TrySplitStagingRelativePath(string stagingRelativePath, out string folder, out string filename)
    {
        folder = string.Empty;
        filename = string.Empty;

        var slashIndex = stagingRelativePath.IndexOf('/');
        if (slashIndex <= 0 || slashIndex == stagingRelativePath.Length - 1)
        {
            return false;
        }

        folder = stagingRelativePath[..slashIndex].Trim();
        filename = stagingRelativePath[(slashIndex + 1)..].Trim();
        if (string.IsNullOrWhiteSpace(folder) || string.IsNullOrWhiteSpace(filename))
        {
            return false;
        }

        if (filename.Contains('/') || filename.Contains("..", StringComparison.Ordinal))
        {
            return false;
        }

        return true;
    }

    private static string SanitizePathSegment(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var source = value.Trim();
        var invalidChars = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(source.Length);

        foreach (var c in source)
        {
            if (c is '/' or '\\' || invalidChars.Contains(c) || char.IsControl(c))
            {
                builder.Append('_');
            }
            else
            {
                builder.Append(c);
            }
        }

        var sanitized = builder.ToString().Trim(' ', '.');
        return string.IsNullOrWhiteSpace(sanitized) ? fallback : sanitized;
    }

    private static string SanitizeFileName(string fileName)
    {
        var extension = Path.GetExtension(fileName);
        var baseName = Path.GetFileNameWithoutExtension(fileName);
        var safeBaseName = SanitizePathSegment(baseName, "asset");
        var safeExtension = string.IsNullOrWhiteSpace(extension) ? string.Empty : SanitizePathSegment(extension, string.Empty);
        if (!string.IsNullOrEmpty(safeExtension) && !safeExtension.StartsWith(".", StringComparison.Ordinal))
        {
            safeExtension = $".{safeExtension}";
        }

        return $"{safeBaseName}{safeExtension}";
    }

    private static string GetUniqueDestinationPath(string destinationFolderPath, string safeFileName)
    {
        var extension = Path.GetExtension(safeFileName);
        var baseName = Path.GetFileNameWithoutExtension(safeFileName);
        var candidate = Path.Combine(destinationFolderPath, safeFileName);
        var suffix = 2;

        while (File.Exists(candidate))
        {
            candidate = Path.Combine(destinationFolderPath, $"{baseName}_{suffix}{extension}");
            suffix++;
        }

        return candidate;
    }

    private static bool IsPathUnderRoot(string path, string root)
    {
        return path.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || string.Equals(path, root, StringComparison.OrdinalIgnoreCase);
    }

    private static LayoutAssetCopyResult CreateError(
        IReadOnlyList<PanelElementModel> elements,
        IReadOnlyList<LayoutImportWarning> warnings,
        IReadOnlyList<string> errors)
    {
        return new LayoutAssetCopyResult
        {
            Elements = elements,
            CopiedAssetRelativePaths = [],
            Warnings = warnings,
            Errors = errors
        };
    }
}

internal sealed class LayoutAssetCopyResult
{
    public required IReadOnlyList<PanelElementModel> Elements { get; init; }

    public required IReadOnlyList<string> CopiedAssetRelativePaths { get; init; }

    public required IReadOnlyList<LayoutImportWarning> Warnings { get; init; }

    public required IReadOnlyList<string> Errors { get; init; }

    public bool Succeeded => Errors.Count == 0;
}


internal static class MfmeReelLampTransmissionMaskGenerator
{
    public static bool TryGenerate(string sourcePath, string outputPath, out string? error)
    {
        error = null;
        try
        {
            using var bitmap = SKBitmap.Decode(sourcePath);
            if (bitmap is null)
            {
                error = "Image could not be decoded.";
                return false;
            }

            var background = EstimateBackground(bitmap);
            using var mask = new SKBitmap(bitmap.Width, bitmap.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
            for (var y = 0; y < bitmap.Height; y++)
            {
                for (var x = 0; x < bitmap.Width; x++)
                {
                    var pixel = bitmap.GetPixel(x, y);
                    var distance = ColourDistance(pixel, background);
                    var alpha = pixel.Alpha == 0 ? (byte)0 : (byte)Math.Clamp((distance - 18) * 4, 0, 255);
                    mask.SetPixel(x, y, new SKColor(alpha, alpha, alpha, alpha));
                }
            }

            using var image = SKImage.FromBitmap(mask);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            if (data is null)
            {
                error = "Mask image could not be encoded.";
                return false;
            }

            using var stream = File.Open(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);
            data.SaveTo(stream);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            error = ex.Message;
            return false;
        }
    }

    private static SKColor EstimateBackground(SKBitmap bitmap)
    {
        var samples = new[] { bitmap.GetPixel(0, 0), bitmap.GetPixel(bitmap.Width - 1, 0), bitmap.GetPixel(0, bitmap.Height - 1), bitmap.GetPixel(bitmap.Width - 1, bitmap.Height - 1) };
        return new SKColor((byte)samples.Average(c => c.Red), (byte)samples.Average(c => c.Green), (byte)samples.Average(c => c.Blue), 255);
    }

    private static double ColourDistance(SKColor a, SKColor b)
    {
        var r = a.Red - b.Red;
        var g = a.Green - b.Green;
        var bl = a.Blue - b.Blue;
        return Math.Sqrt(r * r + g * g + bl * bl);
    }
}
