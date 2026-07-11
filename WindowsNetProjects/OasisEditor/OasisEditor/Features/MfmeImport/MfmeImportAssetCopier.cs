using System.IO;
using System.Text;

namespace OasisEditor.Features.MfmeImport;

internal sealed class MfmeImportAssetCopier
{

    public MfmeAssetCopyResult CopyAssetsFromStaging(
        string stagingRootPath,
        string layoutName,
        string projectAssetsPath,
        bool copyAssets,
        IReadOnlyList<PanelElementModel> elements)
    {
        ArgumentNullException.ThrowIfNull(elements);

        if (!copyAssets)
        {
            return new MfmeAssetCopyResult
            {
                Elements = SendReelsAndAlphaDisplaysToBack(elements.ToArray()),
                CopiedAssetRelativePaths = [],
                Warnings = [],
                Errors = []
            };
        }

        var warnings = new List<MfmeImportWarning>();
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
            mappedElements = BakeDisplayOverlaysIntoBackgrounds(mappedElements, projectAssetsRoot, copied, errors);
        }

        if (errors.Count > 0)
        {
            return CreateError(elements, warnings, errors);
        }

        mappedElements = SendReelsAndAlphaDisplaysToBack(mappedElements);
        return new MfmeAssetCopyResult
        {
            Elements = mappedElements,
            CopiedAssetRelativePaths = copied,
            Warnings = warnings,
            Errors = []
        };
    }

    private static readonly IReadOnlyDictionary<string, string> ExtractFolderToProjectFolder = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["background"] = "Background",
        ["lamps"] = "Lamps",
        ["reels"] = "Reels"
    };

    public MfmeAssetCopyResult CopyAssets(
        MfmeImportContext context,
        MfmeLegacyExtractData extract,
        IReadOnlyList<PanelElementModel> elements)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(extract);
        ArgumentNullException.ThrowIfNull(elements);

        if (!context.CopyAssets)
        {
            return new MfmeAssetCopyResult
            {
                Elements = SendReelsAndAlphaDisplaysToBack(elements.ToArray()),
                CopiedAssetRelativePaths = [],
                Warnings = [],
                Errors = []
            };
        }

        var warnings = new List<MfmeImportWarning>();
        var errors = new List<string>();
        var copied = new List<string>();

        var projectAssetsRoot = EnsureDirectoryAndPath(context.ProjectAssetsPath, errors, "mfme.import.assetsRoot.invalid");
        if (projectAssetsRoot is null)
        {
            return CreateError(elements, warnings, errors);
        }

        var layoutSegment = SanitizePathSegment(extract.LayoutName, "extract");
        var destinationRoot = Path.Combine(projectAssetsRoot, "MfmeImport", layoutSegment);

        var pathMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var mappedElements = elements
            .Select(element => MapElementAssets(element, extract.ExtractRootPath, destinationRoot, projectAssetsRoot, pathMap, copied, warnings, errors))
            .ToArray();

        if (errors.Count == 0)
        {
            mappedElements = BakeDisplayOverlaysIntoBackgrounds(mappedElements, projectAssetsRoot, copied, errors);
        }

        if (errors.Count > 0)
        {
            return CreateError(elements, warnings, errors);
        }

        mappedElements = SendReelsAndAlphaDisplaysToBack(mappedElements);

        return new MfmeAssetCopyResult
        {
            Elements = mappedElements,
            CopiedAssetRelativePaths = copied,
            Warnings = warnings,
            Errors = []
        };
    }

    private static PanelElementModel MapElementAssets(
        PanelElementModel element,
        string extractRootPath,
        string destinationRoot,
        string projectAssetsRoot,
        IDictionary<string, string> pathMap,
        ICollection<string> copied,
        ICollection<MfmeImportWarning> warnings,
        ICollection<string> errors)
    {
        var primary = CopyOneAsset(
            element.Kind,
            element.AssetPath,
            extractRootPath,
            destinationRoot,
            projectAssetsRoot,
            pathMap,
            copied,
            warnings,
            errors);

        var secondary = CopyOneAsset(
            element.Kind,
            element.SecondaryAssetPath,
            extractRootPath,
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
            var sourceLampPath = TryResolveSourceAssetPath(element.AssetPath, extractRootPath);
            var sourceMaskPath = TryResolveSourceAssetPath(element.SecondaryAssetPath, extractRootPath);
            var outputLampPath = Path.Combine(projectAssetsRoot, primary["Assets/".Length..].Replace('/', Path.DirectorySeparatorChar));

            if (sourceLampPath is not null && File.Exists(sourceLampPath))
            {
                if (!MfmeLampAssetPostProcessor.TryProcessLamp(sourceLampPath, sourceMaskPath, outputLampPath, applyMaskTint: true, out var processingError))
                {
                    errors.Add($"Failed to process lamp asset '{element.AssetPath}': {processingError}");
                }
            }
        }

        return new PanelElementModel
        {
            ObjectId = element.ObjectId,
            Name = element.Name,
            Kind = element.Kind,
            X = element.X,
            Y = element.Y,
            Width = element.Width,
            Height = element.Height,
            AssetPath = primary,
            SecondaryAssetPath = secondary,
            DisplayNumber = element.DisplayNumber,
            SegmentDisplayType = element.SegmentDisplayType,
            ShowDecimalPoint = element.ShowDecimalPoint,
            ShowCommaTail = element.ShowCommaTail,
            OnColorHex = element.OnColorHex,
            OffColorHex = element.OffColorHex,
            TextColorHex = element.TextColorHex,
            DisplayText = element.DisplayText,
            TextBoxFontName = element.TextBoxFontName,
            TextBoxFontStyle = element.TextBoxFontStyle,
            TextBoxFontSize = element.TextBoxFontSize,
            IsReversed = element.IsReversed,
            Stops = element.Stops,
            VisibleScale = element.VisibleScale,
            BandOffset = element.BandOffset,
            IsLocked = element.IsLocked,
            IsVisible = element.IsVisible,
            SourceComponentIndex = element.SourceComponentIndex,
            SourceElementIndex = element.SourceElementIndex,
            SharedSourceSetId = element.SharedSourceSetId,
            SharedSourceSetCount = element.SharedSourceSetCount,
            SourceBlend = element.SourceBlend,
            ImportSource = element.ImportSource
        };
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
        return new PanelElementModel
        {
            ObjectId = element.ObjectId,
            Name = element.Name,
            Kind = element.Kind,
            X = element.X,
            Y = element.Y,
            Width = element.Width,
            Height = element.Height,
            AssetPath = assetPath,
            SecondaryAssetPath = element.SecondaryAssetPath,
            DisplayNumber = element.DisplayNumber,
            SegmentDisplayType = element.SegmentDisplayType,
            ShowDecimalPoint = element.ShowDecimalPoint,
            ShowCommaTail = element.ShowCommaTail,
            OnColorHex = element.OnColorHex,
            OffColorHex = element.OffColorHex,
            TextColorHex = element.TextColorHex,
            DisplayText = element.DisplayText,
            TextBoxFontName = element.TextBoxFontName,
            TextBoxFontStyle = element.TextBoxFontStyle,
            TextBoxFontSize = element.TextBoxFontSize,
            IsReversed = element.IsReversed,
            Stops = element.Stops,
            VisibleScale = element.VisibleScale,
            BandOffset = element.BandOffset,
            IsLocked = element.IsLocked,
            IsVisible = element.IsVisible,
            SourceComponentIndex = element.SourceComponentIndex,
            SourceElementIndex = element.SourceElementIndex,
            SharedSourceSetId = element.SharedSourceSetId,
            SharedSourceSetCount = element.SharedSourceSetCount,
            SourceBlend = element.SourceBlend,
            ImportSource = element.ImportSource
        };
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
        string? extractRelativePath,
        string extractRootPath,
        string destinationRoot,
        string projectAssetsRoot,
        IDictionary<string, string> pathMap,
        ICollection<string> copied,
        ICollection<MfmeImportWarning> warnings,
        ICollection<string> errors)
    {
        if (string.IsNullOrWhiteSpace(extractRelativePath))
        {
            return null;
        }

        var normalizedRelativePath = extractRelativePath.Replace('\\', '/').Trim();
        if (!TrySplitExtractRelativePath(normalizedRelativePath, out var folder, out var filename))
        {
            warnings.Add(new MfmeImportWarning(
                "mfme.import.asset.path.invalid",
                $"Asset path '{extractRelativePath}' is invalid and was skipped.",
                extractRelativePath));
            return null;
        }

        var safeFolder = ExtractFolderToProjectFolder.TryGetValue(folder, out var mappedFolder)
            ? mappedFolder
            : SanitizePathSegment(folder, "Misc");
        var safeFileName = SanitizeFileName(filename);

        var sourceFullPath = Path.GetFullPath(Path.Combine(extractRootPath, folder, filename));
        var extractRootFullPath = Path.GetFullPath(extractRootPath);

        if (!IsPathUnderRoot(sourceFullPath, extractRootFullPath))
        {
            warnings.Add(new MfmeImportWarning(
                "mfme.import.asset.path.escape",
                $"Asset path '{extractRelativePath}' escapes extract root and was skipped.",
                extractRelativePath));
            return null;
        }

        if (!File.Exists(sourceFullPath))
        {
            warnings.Add(new MfmeImportWarning(
                "mfme.import.asset.missing",
                $"Asset file '{extractRelativePath}' was not found in extract and was skipped.",
                extractRelativePath));
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


    private static string? TryResolveSourceAssetPath(string? extractRelativePath, string extractRootPath)
    {
        if (string.IsNullOrWhiteSpace(extractRelativePath))
        {
            return null;
        }

        var normalized = extractRelativePath.Replace('\\', '/').Trim();
        if (!TrySplitExtractRelativePath(normalized, out var folder, out var filename))
        {
            return null;
        }

        var sourceFullPath = Path.GetFullPath(Path.Combine(extractRootPath, folder, filename));
        return IsPathUnderRoot(sourceFullPath, Path.GetFullPath(extractRootPath)) ? sourceFullPath : null;
    }

    private static string? EnsureDirectoryAndPath(string path, ICollection<string> errors, string code)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            errors.Add(new MfmeImportWarning(code, "Project assets path is required.").Message);
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
            errors.Add(new MfmeImportWarning(code, $"Unable to prepare project assets path '{path}': {ex.Message}").Message);
            return null;
        }
    }

    private static bool TrySplitExtractRelativePath(string extractRelativePath, out string folder, out string filename)
    {
        folder = string.Empty;
        filename = string.Empty;

        var slashIndex = extractRelativePath.IndexOf('/');
        if (slashIndex <= 0 || slashIndex == extractRelativePath.Length - 1)
        {
            return false;
        }

        folder = extractRelativePath[..slashIndex].Trim();
        filename = extractRelativePath[(slashIndex + 1)..].Trim();
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

    private static MfmeAssetCopyResult CreateError(
        IReadOnlyList<PanelElementModel> elements,
        IReadOnlyList<MfmeImportWarning> warnings,
        IReadOnlyList<string> errors)
    {
        return new MfmeAssetCopyResult
        {
            Elements = elements,
            CopiedAssetRelativePaths = [],
            Warnings = warnings,
            Errors = errors
        };
    }
}

internal sealed class MfmeAssetCopyResult
{
    public required IReadOnlyList<PanelElementModel> Elements { get; init; }

    public required IReadOnlyList<string> CopiedAssetRelativePaths { get; init; }

    public required IReadOnlyList<MfmeImportWarning> Warnings { get; init; }

    public required IReadOnlyList<string> Errors { get; init; }

    public bool Succeeded => Errors.Count == 0;
}
