using System.IO;

namespace OasisEditor.Features.MfmeImport;

internal sealed class FileSystemMfmeExtractReader : IMfmeExtractReader
{
    public MfmeExtractReadResult Read(MfmeImportContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (string.IsNullOrWhiteSpace(context.SourceExtractPath))
        {
            return Error("mfme.extract.path.empty", "Source extract path is required.");
        }

        var sourcePath = context.SourceExtractPath.Trim();

        if (ContainsInvalidPathChars(sourcePath))
        {
            return Error("mfme.extract.path.invalid", $"Source extract path is invalid: '{sourcePath}'.");
        }

        if (Directory.Exists(sourcePath))
        {
            return ReadFromExtractDirectory(sourcePath);
        }

        if (File.Exists(sourcePath))
        {
            return ReadFromManifestFile(sourcePath);
        }

        return Error("mfme.extract.path.notFound", $"Source extract path was not found: '{sourcePath}'.");
    }

    private static MfmeExtractReadResult ReadFromExtractDirectory(string extractDirectory)
    {
        var manifests = Directory
            .EnumerateFiles(extractDirectory, "*.json", SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (manifests.Length == 0)
        {
            return Error(
                "mfme.extract.manifest.missing",
                $"No manifest JSON file was found in extract directory '{extractDirectory}'.");
        }

        var warnings = new List<MfmeImportWarning>();
        if (manifests.Length > 1)
        {
            warnings.Add(new MfmeImportWarning(
                "mfme.extract.manifest.multiple",
                $"Multiple manifest JSON files were found in '{extractDirectory}'. The first file in sorted order will be used.",
                extractDirectory));
        }

        return Success(CreateExtractData(extractDirectory, manifests[0]), warnings);
    }

    private static MfmeExtractReadResult ReadFromManifestFile(string manifestPath)
    {
        if (!string.Equals(Path.GetExtension(manifestPath), ".json", StringComparison.OrdinalIgnoreCase))
        {
            return Error(
                "mfme.extract.manifest.extension",
                $"Expected a .json manifest file but got '{manifestPath}'.");
        }

        var extractDirectory = Path.GetDirectoryName(manifestPath);
        if (string.IsNullOrWhiteSpace(extractDirectory))
        {
            return Error(
                "mfme.extract.manifest.directory",
                $"Unable to resolve extract directory for manifest '{manifestPath}'.");
        }

        return Success(CreateExtractData(extractDirectory, manifestPath), []);
    }

    private static MfmeLegacyExtractData CreateExtractData(string extractDirectory, string manifestPath)
    {
        return new MfmeLegacyExtractData
        {
            ExtractRootPath = extractDirectory,
            ManifestPath = manifestPath,
            LayoutName = Path.GetFileNameWithoutExtension(manifestPath)
        };
    }

    private static bool ContainsInvalidPathChars(string path)
    {
        return path.IndexOfAny(Path.GetInvalidPathChars()) >= 0;
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

    private static MfmeExtractReadResult Success(MfmeLegacyExtractData extract, IReadOnlyList<MfmeImportWarning> warnings)
    {
        return new MfmeExtractReadResult
        {
            Extract = extract,
            Warnings = warnings,
            Errors = []
        };
    }
}
