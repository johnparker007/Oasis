using System.IO;
using System.Text;

namespace OasisEditor.Features.MfmeImport;

internal sealed class MfmeImportAssetCopier
{
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
                Elements = elements,
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

        if (errors.Count > 0)
        {
            return CreateError(elements, warnings, errors);
        }

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
            element.AssetPath,
            extractRootPath,
            destinationRoot,
            projectAssetsRoot,
            pathMap,
            copied,
            warnings,
            errors);

        var secondary = CopyOneAsset(
            element.SecondaryAssetPath,
            extractRootPath,
            destinationRoot,
            projectAssetsRoot,
            pathMap,
            copied,
            warnings,
            errors);

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
            OnColorHex = element.OnColorHex,
            OffColorHex = element.OffColorHex,
            TextColorHex = element.TextColorHex,
            DisplayText = element.DisplayText,
            IsReversed = element.IsReversed,
            Stops = element.Stops,
            VisibleScale = element.VisibleScale,
            ImportSource = element.ImportSource
        };
    }

    private static string? CopyOneAsset(
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

        var destinationFullPath = GetUniqueDestinationPath(destinationFolderPath, safeFileName);
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
