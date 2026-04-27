using System;
using System.IO;
using OasisEditor.Features.MfmeImport;
using Xunit;

namespace OasisEditor.Tests;

public sealed class MfmeExtractReaderTests
{
    [Fact]
    public void Read_WhenExtractPathMissing_ReturnsError()
    {
        using var temp = new TempPaths();
        var reader = new MfmeExtractReader();

        var result = reader.Read(new MfmeImportContext
        {
            SourceExtractPath = Path.Combine(temp.Root, "missing.extract"),
            ProjectRootPath = temp.ProjectRoot,
            AssetsRootPath = temp.AssetsRoot,
            CopyAssetsToProject = true
        });

        Assert.True(result.HasErrors);
        Assert.Contains(result.Errors, error => error.Contains("does not exist", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Read_WhenManifestMissing_ReturnsError()
    {
        using var temp = new TempPaths();
        Directory.CreateDirectory(temp.ExtractRoot);

        var reader = new MfmeExtractReader();
        var result = reader.Read(temp.CreateContext());

        Assert.True(result.HasErrors);
        Assert.Contains(result.Errors, error => error.Contains("manifest", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Read_WhenComponentsMissing_ReturnsWarningWithoutErrors()
    {
        using var temp = new TempPaths();
        Directory.CreateDirectory(temp.ExtractRoot);
        File.WriteAllText(Path.Combine(temp.ExtractRoot, "layout.json"), """
            {
              "ASName": "Demo Layout"
            }
            """);

        var reader = new MfmeExtractReader();
        var result = reader.Read(temp.CreateContext());

        Assert.False(result.HasErrors);
        Assert.NotNull(result.ExtractDocument);
        Assert.Equal("Demo Layout", result.ExtractDocument!.LayoutName);
        Assert.Contains(result.Warnings, warning => warning.Code == "missing-components");
    }

    [Fact]
    public void Read_ParsesBackgroundLampReelSevenSegmentAndAlphaComponents()
    {
        using var temp = new TempPaths();
        Directory.CreateDirectory(temp.ExtractRoot);
        File.WriteAllText(Path.Combine(temp.ExtractRoot, "layout.json"), """
            {
              "ASName": "Demo Layout",
              "Components": [
                {
                  "$type": "MfmeTools.Shared.ExtractComponentBackground, MfmeTools",
                  "Position": { "X": 0, "Y": 0 },
                  "Size": { "X": 1280, "Y": 720 },
                  "BmpImageFilename": "bg.png",
                  "Color": "#FF00FF"
                },
                {
                  "$type": "MfmeTools.Shared.ExtractComponentLamp, MfmeTools",
                  "Position": { "X": 10, "Y": 20 },
                  "Size": { "X": 30, "Y": 40 },
                  "OffImageColor": "#111111",
                  "TextColor": "#222222",
                  "LampElements": [
                    {
                      "Number": 7,
                      "OnColor": "#00FF00",
                      "BmpImageFilename": "lamp.png"
                    }
                  ]
                },
                {
                  "$type": "MfmeTools.Shared.ExtractComponentReel, MfmeTools",
                  "Position": { "X": 100, "Y": 110 },
                  "Size": { "X": 120, "Y": 130 },
                  "Number": 2,
                  "Stops": 20,
                  "Reversed": true,
                  "BandBmpImageFilename": "reel.png"
                },
                {
                  "$type": "MfmeTools.Shared.ExtractComponentSevenSegment, MfmeTools",
                  "Position": { "X": 200, "Y": 210 },
                  "Size": { "X": 70, "Y": 80 },
                  "Number": 3,
                  "SegmentOnColor": "#ABCDEF"
                },
                {
                  "$type": "MfmeTools.Shared.ExtractComponentMatrixAlpha, MfmeTools",
                  "Position": { "X": 300, "Y": 310 },
                  "Size": { "X": 90, "Y": 100 },
                  "Number": 9,
                  "OnColor": "#334455"
                }
              ]
            }
            """);

        var reader = new MfmeExtractReader();
        var result = reader.Read(temp.CreateContext());

        Assert.False(result.HasErrors);
        Assert.Equal(5, result.ImportedElements.Count);

        Assert.IsType<MfmeBackgroundComponentData>(result.ImportedElements[0]);
        var lamp = Assert.IsType<MfmeLampComponentData>(result.ImportedElements[1]);
        Assert.Equal(7, lamp.Number);
        var reel = Assert.IsType<MfmeReelComponentData>(result.ImportedElements[2]);
        Assert.Equal(20, reel.Stops);
        Assert.True(reel.Reversed);
        Assert.IsType<MfmeSevenSegmentComponentData>(result.ImportedElements[3]);
        var alpha = Assert.IsType<MfmeAlphaComponentData>(result.ImportedElements[4]);
        Assert.Equal("MatrixAlpha", alpha.AlphaVariant);
    }

    [Fact]
    public void Read_WhenOptionalImagesMissing_AddsWarningsAndStillImportsSupportedComponents()
    {
        using var temp = new TempPaths();
        Directory.CreateDirectory(temp.ExtractRoot);
        File.WriteAllText(Path.Combine(temp.ExtractRoot, "layout.json"), """
            {
              "ASName": "Demo Layout",
              "Components": [
                {
                  "$type": "ExtractComponentBackground",
                  "Size": { "X": 10, "Y": 10 }
                },
                {
                  "$type": "ExtractComponentLamp",
                  "LampElements": [
                    {
                      "NumberAsText": "12"
                    }
                  ]
                },
                {
                  "$type": "ExtractComponentReel",
                  "Stops": 12
                },
                {
                  "$type": "ExtractComponentAlpha"
                }
              ]
            }
            """);

        var reader = new MfmeExtractReader();
        var result = reader.Read(temp.CreateContext());

        Assert.False(result.HasErrors);
        Assert.Equal(4, result.ImportedElements.Count);
        Assert.True(result.Warnings.Count >= 4);
        Assert.All(result.ImportedElements, component => Assert.NotEqual(MfmeComponentKind.Unknown, component.Kind));
    }

    [Fact]
    public void Read_WhenComponentHasUnsupportedType_SkipsWithWarning()
    {
        using var temp = new TempPaths();
        Directory.CreateDirectory(temp.ExtractRoot);
        File.WriteAllText(Path.Combine(temp.ExtractRoot, "layout.json"), """
            {
              "ASName": "Demo Layout",
              "Components": [
                {
                  "$type": "UnknownComponentType",
                  "Position": { "X": 10, "Y": 20 },
                  "Size": { "X": 100, "Y": 40 }
                }
              ]
            }
            """);

        var reader = new MfmeExtractReader();
        var result = reader.Read(temp.CreateContext());

        Assert.False(result.HasErrors);
        Assert.Empty(result.ImportedElements);
        Assert.Single(result.SkippedComponents);
        Assert.Equal(MfmeComponentKind.Unknown, result.SkippedComponents[0].Kind);
        Assert.Contains(result.Warnings, warning => warning.Code == "unsupported-component");
    }

    private sealed class TempPaths : IDisposable
    {
        public TempPaths()
        {
            Root = Path.Combine(Path.GetTempPath(), $"oasis-mfme-tests-{Guid.NewGuid():N}");
            ProjectRoot = Path.Combine(Root, "Project");
            AssetsRoot = Path.Combine(ProjectRoot, "Assets");
            ExtractRoot = Path.Combine(Root, "sample.extract");

            Directory.CreateDirectory(ProjectRoot);
            Directory.CreateDirectory(AssetsRoot);
        }

        public string Root { get; }
        public string ProjectRoot { get; }
        public string AssetsRoot { get; }
        public string ExtractRoot { get; }

        public MfmeImportContext CreateContext()
        {
            return new MfmeImportContext
            {
                SourceExtractPath = ExtractRoot,
                ProjectRootPath = ProjectRoot,
                AssetsRootPath = AssetsRoot,
                CopyAssetsToProject = true,
                LayoutDisplayName = null
            };
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }
}
