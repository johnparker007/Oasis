using Xunit;
using OasisEditor.Features.MfmeImport;

namespace OasisEditor.Tests;

public sealed class MfmeExtractReaderTests
{
    [Fact]
    public void Read_WithEmptyPath_ReturnsRequiredPathError()
    {
        var reader = new FileSystemMfmeExtractReader();
        var context = new MfmeImportContext
        {
            SourceExtractPath = "   ",
            ProjectRootPath = "C:/Project",
            ProjectAssetsPath = "C:/Project/Assets",
            CopyAssets = true
        };

        var result = reader.Read(context);

        Assert.False(result.Succeeded);
        Assert.Null(result.Extract);
        var error = Assert.Single(result.Errors);
        Assert.Contains("required", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Read_WithMissingPath_ReturnsNotFoundError()
    {
        var reader = new FileSystemMfmeExtractReader();
        var context = new MfmeImportContext
        {
            SourceExtractPath = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}"),
            ProjectRootPath = "C:/Project",
            ProjectAssetsPath = "C:/Project/Assets",
            CopyAssets = true
        };

        var result = reader.Read(context);

        Assert.False(result.Succeeded);
        Assert.Null(result.Extract);
        var error = Assert.Single(result.Errors);
        Assert.Contains("not found", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Read_WithNonJsonManifestFile_ReturnsManifestExtensionError()
    {
        var extractDirectory = CreateTempDirectory();
        var manifestPath = Path.Combine(extractDirectory, "layout.txt");
        File.WriteAllText(manifestPath, "manifest");

        try
        {
            var reader = new FileSystemMfmeExtractReader();
            var context = new MfmeImportContext
            {
                SourceExtractPath = manifestPath,
                ProjectRootPath = "C:/Project",
                ProjectAssetsPath = "C:/Project/Assets",
                CopyAssets = true
            };

            var result = reader.Read(context);

            Assert.False(result.Succeeded);
            Assert.Null(result.Extract);
            var error = Assert.Single(result.Errors);
            Assert.Contains(".json", error, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(extractDirectory, recursive: true);
        }
    }

    [Fact]
    public void Read_WithExtractDirectoryAndSingleManifest_ReturnsExtractData()
    {
        var extractDirectory = CreateTempDirectory();
        var manifestPath = Path.Combine(extractDirectory, "sample-layout.json");
        File.WriteAllText(manifestPath, "{}");

        try
        {
            var reader = new FileSystemMfmeExtractReader();
            var context = new MfmeImportContext
            {
                SourceExtractPath = extractDirectory,
                ProjectRootPath = "C:/Project",
                ProjectAssetsPath = "C:/Project/Assets",
                CopyAssets = true
            };

            var result = reader.Read(context);

            Assert.True(result.Succeeded);
            Assert.Empty(result.Errors);
            Assert.Empty(result.Warnings);
            Assert.NotNull(result.Extract);
            Assert.Equal(extractDirectory, result.Extract.ExtractRootPath);
            Assert.Equal(manifestPath, result.Extract.ManifestPath);
            Assert.Equal("sample-layout", result.Extract.LayoutName);
        }
        finally
        {
            Directory.Delete(extractDirectory, recursive: true);
        }
    }

    [Fact]
    public void Read_WithExtractDirectoryAndMultipleManifests_AddsWarning()
    {
        var extractDirectory = CreateTempDirectory();
        File.WriteAllText(Path.Combine(extractDirectory, "b-layout.json"), "{}");
        var firstManifest = Path.Combine(extractDirectory, "a-layout.json");
        File.WriteAllText(firstManifest, "{}");

        try
        {
            var reader = new FileSystemMfmeExtractReader();
            var context = new MfmeImportContext
            {
                SourceExtractPath = extractDirectory,
                ProjectRootPath = "C:/Project",
                ProjectAssetsPath = "C:/Project/Assets",
                CopyAssets = false
            };

            var result = reader.Read(context);

            Assert.True(result.Succeeded);
            Assert.Empty(result.Errors);
            var warning = Assert.Single(result.Warnings);
            Assert.Equal("mfme.extract.manifest.multiple", warning.Code);
            Assert.Equal(firstManifest, result.Extract!.ManifestPath);
        }
        finally
        {
            Directory.Delete(extractDirectory, recursive: true);
        }
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"oasis-mfme-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
