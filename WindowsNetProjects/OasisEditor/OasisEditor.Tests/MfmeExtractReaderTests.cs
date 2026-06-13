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
        Directory.CreateDirectory(Path.Combine(extractDirectory, "background"));
        Directory.CreateDirectory(Path.Combine(extractDirectory, "lamps"));
        Directory.CreateDirectory(Path.Combine(extractDirectory, "reels"));
        File.WriteAllText(Path.Combine(extractDirectory, "background", "bg.png"), "placeholder");
        File.WriteAllText(Path.Combine(extractDirectory, "lamps", "lamp.png"), "placeholder");
        File.WriteAllText(Path.Combine(extractDirectory, "lamps", "lamp-mask.png"), "placeholder");
        File.WriteAllText(Path.Combine(extractDirectory, "reels", "band.png"), "placeholder");
        File.WriteAllText(Path.Combine(extractDirectory, "reels", "overlay.png"), "placeholder");
        File.WriteAllText(Path.Combine(extractDirectory, "reels", "alpha-overlay.bmp"), "placeholder");
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
            Assert.Equal("lamp-mask.png", lamp.FirstLampElement.BmpMaskImageFilename);
            Assert.True(lamp.FirstLampElement.Graphic);
            Assert.True(lamp.HasButtonInput);
            Assert.Equal("12", lamp.ButtonNumberAsString);
            Assert.True(lamp.Inverted);
            Assert.Equal("SPACE", lamp.Shortcut1);
            Assert.Equal("S", lamp.Shortcut2);

            var reel = Assert.IsType<MfmeLegacyReelComponent>(components[2]);
            Assert.Equal(3, reel.Number);
            Assert.Equal("band.png", reel.BandBmpImageFilename);

            var sevenSegment = Assert.IsType<MfmeLegacySevenSegmentComponent>(components[3]);
            Assert.Equal(8, sevenSegment.Number);

            var alpha = Assert.IsType<MfmeLegacyAlphaComponent>(components[4]);
            Assert.Equal("ExtractComponentAlpha", alpha.SourceType);
            Assert.NotNull(alpha.SegmentOnColor);
            Assert.Equal(0.1f, alpha.SegmentOnColor?.R);
            Assert.True(alpha.HasOverlay);
            Assert.Equal("alpha-overlay.bmp", alpha.OverlayBmpImageFilename);

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
    public void Read_WithMissingOptionalImages_AddsWarningsAndKeepsSupportedComponents()
    {
        var extractDirectory = CreateTempDirectory();
        var manifestPath = Path.Combine(extractDirectory, "layout.json");
        Directory.CreateDirectory(Path.Combine(extractDirectory, "background"));
        Directory.CreateDirectory(Path.Combine(extractDirectory, "lamps"));
        Directory.CreateDirectory(Path.Combine(extractDirectory, "reels"));
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
            Assert.Equal(6, result.Warnings.Count);
            Assert.All(result.Warnings, warning => Assert.Equal("mfme.extract.asset.missing", warning.Code));
            Assert.Contains(result.Warnings, warning => warning.Message.Contains("Alpha overlay", StringComparison.OrdinalIgnoreCase));
            Assert.Equal(7, result.Extract!.Components.Count);
        }
        finally
        {
            Directory.Delete(extractDirectory, recursive: true);
        }
    }

    [Fact]
    public void Read_WithUnsupportedComponentType_AddsWarningAndSkipsComponent()
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
              "$type": "Oasis.MfmeTools.Shared.ExtractComponents.ExtractComponentCheckbox, MfmeTools",
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


    [Fact]
    public void Read_WithMultipleLampElements_KeepsAllValidAndIgnoresPlaceholders()
    {
        var extractDirectory = CreateTempDirectory();
        var manifestPath = Path.Combine(extractDirectory, "layout.json");
        Directory.CreateDirectory(Path.Combine(extractDirectory, "lamps"));
        File.WriteAllText(Path.Combine(extractDirectory, "lamps", "lamp-147.png"), "placeholder");
        File.WriteAllText(Path.Combine(extractDirectory, "lamps", "lamp-164.png"), "placeholder");
        File.WriteAllText(manifestPath, """
        {
          "ASName": "MultiLampLayout",
          "Components": [
            {
              "$type": "Oasis.MfmeTools.Shared.ExtractComponents.ExtractComponentLamp, MfmeTools",
              "Position": { "X": 10, "Y": 20 },
              "Size": { "X": 300, "Y": 80 },
              "Graphic": true,
              "LampElements": [
                { "NumberAsText": "147", "BmpImageFilename": "lamp-147.png" },
                { "NumberAsText": "", "BmpImageFilename": "", "BmpMaskImageFilename": "" },
                { "NumberAsText": "164", "BmpImageFilename": "lamp-164.png" }
              ]
            }
          ]
        }
        """);

        try
        {
            var result = new FileSystemMfmeExtractReader().Read(new MfmeImportContext
            {
                SourceExtractPath = extractDirectory,
                ProjectRootPath = "C:/Project",
                ProjectAssetsPath = "C:/Project/Assets",
                CopyAssets = true
            });

            Assert.True(result.Succeeded);
            Assert.Empty(result.Errors);
            Assert.Empty(result.Warnings);
            var lamp = Assert.IsType<MfmeLegacyLampComponent>(Assert.Single(result.Extract!.Components));
            Assert.Equal(2, lamp.LampElements!.Count);
            Assert.Equal(147, lamp.LampElements[0].Number);
            Assert.Equal(0, lamp.LampElements[0].SourceElementIndex);
            Assert.Equal("lamp-147.png", lamp.LampElements[0].BmpImageFilename);
            Assert.Equal(164, lamp.LampElements[1].Number);
            Assert.Equal(2, lamp.LampElements[1].SourceElementIndex);
            Assert.Equal(0, lamp.SourceComponentIndex);
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
              "Graphic": true,
              "ButtonNumberAsString": "12",
              "Inverted": true,
              "Shortcut1": "SPACE",
              "Shortcut2": "S",
              "LampElements": [
                {
                  "NumberAsText": "12",
                  "OnColor": { "R": 1, "G": 0, "B": 0, "A": 1 },
                  "BmpImageFilename": "lamp.png",
                  "BmpMaskImageFilename": "lamp-mask.png"
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
              "Reversed": true,
              "OnColor": { "R": 0.1, "G": 0.2, "B": 0.3, "A": 1.0 },
              "HasOverlay": true,
              "OverlayBmpImageFilename": "alpha-overlay.bmp"
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
