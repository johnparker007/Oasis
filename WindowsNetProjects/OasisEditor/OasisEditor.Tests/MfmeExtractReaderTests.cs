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

    [Fact]
    public void Read_WithSupportedMfmeComponents_ParsesLegacyDtos()
    {
        var extractDirectory = CreateTempDirectory();
        var manifestPath = Path.Combine(extractDirectory, "layout.json");
        File.WriteAllText(manifestPath, CreateSupportedComponentsManifestJson());

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
            var components = Assert.IsAssignableFrom<IReadOnlyList<MfmeLegacyComponentBase>>(result.Extract!.Components);
            Assert.Equal(7, components.Count);

            var background = Assert.IsType<MfmeLegacyBackgroundComponent>(components[0]);
            Assert.Equal("bg.png", background.BmpImageFilename);

            var lamp = Assert.IsType<MfmeLegacyLampComponent>(components[1]);
            Assert.Equal(12, lamp.FirstLampElement!.Number);
            Assert.Equal("lamp.png", lamp.FirstLampElement.BmpImageFilename);

            var reel = Assert.IsType<MfmeLegacyReelComponent>(components[2]);
            Assert.Equal(3, reel.Number);
            Assert.Equal("band.png", reel.BandBmpImageFilename);

            var sevenSegment = Assert.IsType<MfmeLegacySevenSegmentComponent>(components[3]);
            Assert.Equal(8, sevenSegment.Number);

            var alpha = Assert.IsType<MfmeLegacyAlphaComponent>(components[4]);
            Assert.Equal("ExtractComponentAlpha", alpha.SourceType);

            var alphaNew = Assert.IsType<MfmeLegacyAlphaComponent>(components[5]);
            Assert.Equal("ExtractComponentAlphaNew", alphaNew.SourceType);

            var matrixAlpha = Assert.IsType<MfmeLegacyAlphaComponent>(components[6]);
            Assert.Equal("ExtractComponentMatrixAlpha", matrixAlpha.SourceType);
        }
        finally
        {
            Directory.Delete(extractDirectory, recursive: true);
        }
    }

    [Fact]
    public void Read_WithUnsupportedComponent_AddsWarningAndSkipsComponent()
    {
        var extractDirectory = CreateTempDirectory();
        var manifestPath = Path.Combine(extractDirectory, "layout.json");
        File.WriteAllText(manifestPath, """
        {
          "ASName": "UnsupportedExample",
          "Components": [
            {
              "$type": "Oasis.MfmeTools.Shared.ExtractComponents.ExtractComponentBackground, MfmeTools",
              "Position": { "X": 0, "Y": 0 },
              "Size": { "X": 1, "Y": 1 }
            },
            {
              "$type": "Oasis.MfmeTools.Shared.ExtractComponents.ExtractComponentButton, MfmeTools",
              "Position": { "X": 2, "Y": 3 },
              "Size": { "X": 4, "Y": 5 }
            }
          ]
        }
        """);

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
            Assert.Single(result.Warnings);
            Assert.Contains("Unsupported legacy component type", result.Warnings[0].Message, StringComparison.OrdinalIgnoreCase);
            Assert.Single(result.Extract!.Components);
            Assert.IsType<MfmeLegacyBackgroundComponent>(result.Extract.Components[0]);
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

    private static string CreateSupportedComponentsManifestJson()
    {
        return """
        {
          "ASName": "SampleLayout",
          "Components": [
            {
              "$type": "Oasis.MfmeTools.Shared.ExtractComponents.ExtractComponentBackground, MfmeTools",
              "Position": { "X": 0, "Y": 0 },
              "Size": { "X": 1280, "Y": 720 },
              "BmpImageFilename": "bg.png",
              "Color": { "R": 1.0, "G": 0.5, "B": 0.25, "A": 1.0 }
            },
            {
              "$type": "Oasis.MfmeTools.Shared.ExtractComponents.ExtractComponentLamp, MfmeTools",
              "Position": { "X": 10, "Y": 20 },
              "Size": { "X": 30, "Y": 40 },
              "TextBoxText": "Hold",
              "TextBoxFontName": "Arial",
              "TextBoxFontStyle": "Bold",
              "TextBoxFontSize": "12",
              "NoOutline": true,
              "TextColor": { "R": 1, "G": 1, "B": 1, "A": 1 },
              "OffImageColor": { "R": 0, "G": 0, "B": 0, "A": 1 },
              "LampElements": [
                {
                  "NumberAsText": "12",
                  "OnColor": { "R": 1, "G": 0, "B": 0, "A": 1 },
                  "BmpImageFilename": "lamp.png"
                }
              ]
            },
            {
              "$type": "Oasis.MfmeTools.Shared.ExtractComponents.ExtractComponentReel, MfmeTools",
              "Position": { "X": 100, "Y": 150 },
              "Size": { "X": 80, "Y": 120 },
              "Number": 3,
              "Stops": 24,
              "Reversed": true,
              "Height": 150,
              "HasOverlay": true,
              "BandBmpImageFilename": "band.png",
              "OverlayBmpImageFilename": "overlay.png"
            },
            {
              "$type": "Oasis.MfmeTools.Shared.ExtractComponents.ExtractComponentSevenSegment, MfmeTools",
              "Position": { "X": 12, "Y": 34 },
              "Size": { "X": 56, "Y": 78 },
              "Number": 8,
              "SegmentOnColor": { "R": 0, "G": 1, "B": 0, "A": 1 }
            },
            {
              "$type": "Oasis.MfmeTools.Shared.ExtractComponents.ExtractComponentAlpha, MfmeTools",
              "Position": { "X": 1, "Y": 2 },
              "Size": { "X": 3, "Y": 4 },
              "Reversed": true
            },
            {
              "$type": "Oasis.MfmeTools.Shared.ExtractComponents.ExtractComponentAlphaNew, MfmeTools",
              "Position": { "X": 5, "Y": 6 },
              "Size": { "X": 7, "Y": 8 },
              "Reversed": false
            },
            {
              "$type": "Oasis.MfmeTools.Shared.ExtractComponents.ExtractComponentMatrixAlpha, MfmeTools",
              "Position": { "X": 9, "Y": 10 },
              "Size": { "X": 11, "Y": 12 }
            }
          ]
        }
        """;
    }
}
