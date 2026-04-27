using System;
using System.IO;
using OasisEditor.Features.MfmeImport;
using Xunit;

namespace OasisEditor.Tests;

public sealed class MfmeImportPreparationServiceTests
{
    [Fact]
    public void Prepare_MapsAndCopiesAssets_WhenCopyEnabled()
    {
        using var temp = new TempPaths();
        WriteManifest(temp.ExtractRoot, "DemoLayout");

        Directory.CreateDirectory(Path.Combine(temp.ExtractRoot, "background"));
        File.WriteAllText(Path.Combine(temp.ExtractRoot, "background", "bg.png"), "background-bytes");

        var service = CreateService();
        var result = service.Prepare(temp.CreateContext(copyAssets: true));

        Assert.False(result.HasErrors);
        Assert.Equal("DemoLayout", result.LayoutName);
        var mapped = Assert.Single(result.Elements);
        Assert.Equal(PanelElementKind.Background, mapped.Kind);
        Assert.Equal("Assets/MfmeImport/DemoLayout/Background/bg.png", mapped.AssetPath);
        Assert.Single(result.CopiedAssets);
        Assert.Empty(result.SkippedComponents);
    }

    [Fact]
    public void Prepare_KeepsExtractRelativeAssetPath_WhenCopyDisabled()
    {
        using var temp = new TempPaths();
        WriteManifest(temp.ExtractRoot, "DemoLayout");

        var service = CreateService();
        var result = service.Prepare(temp.CreateContext(copyAssets: false));

        Assert.False(result.HasErrors);
        var mapped = Assert.Single(result.Elements);
        Assert.Equal("background/bg.png", mapped.AssetPath);
        Assert.Empty(result.CopiedAssets);
    }

    [Fact]
    public void Prepare_ReturnsReaderErrors_ForMissingExtractPath()
    {
        using var temp = new TempPaths();
        Directory.Delete(temp.ExtractRoot, recursive: true);

        var service = CreateService();
        var result = service.Prepare(temp.CreateContext(copyAssets: true));

        Assert.True(result.HasErrors);
        Assert.Empty(result.Elements);
        Assert.Contains(result.Errors, message => message.Contains("does not exist", StringComparison.OrdinalIgnoreCase));
    }

    private static MfmeImportPreparationService CreateService()
    {
        return new MfmeImportPreparationService(
            new MfmeExtractReader(),
            new MfmeComponentMapper(),
            new MfmeImportAssetCopyService());
    }

    private static void WriteManifest(string extractRoot, string layoutName)
    {
        var manifestPath = Path.Combine(extractRoot, layoutName + ".json");
        var manifest =
            """
            {
              "ASName": "DemoLayout",
              "Components": [
                {
                  "$type": "MfmeTools.Shared.Extract.ExtractComponentBackground, MfmeTools",
                  "Position": { "X": 0, "Y": 0 },
                  "Size": { "Width": 640, "Height": 360 },
                  "BmpImageFilename": "bg.png",
                  "Color": "#112233"
                }
              ]
            }
            """;

        File.WriteAllText(manifestPath, manifest.Replace("DemoLayout", layoutName, StringComparison.Ordinal));
    }

    private sealed class TempPaths : IDisposable
    {
        public TempPaths()
        {
            Root = Path.Combine(Path.GetTempPath(), $"oasis-mfme-prepare-tests-{Guid.NewGuid():N}");
            ProjectRoot = Path.Combine(Root, "Project");
            AssetsRoot = Path.Combine(ProjectRoot, "Assets");
            ExtractRoot = Path.Combine(Root, "DemoLayout");

            Directory.CreateDirectory(ProjectRoot);
            Directory.CreateDirectory(AssetsRoot);
            Directory.CreateDirectory(ExtractRoot);
        }

        public string Root { get; }
        public string ProjectRoot { get; }
        public string AssetsRoot { get; }
        public string ExtractRoot { get; }

        public MfmeImportContext CreateContext(bool copyAssets)
        {
            return new MfmeImportContext
            {
                SourceExtractPath = ExtractRoot,
                ProjectRootPath = ProjectRoot,
                AssetsRootPath = AssetsRoot,
                CopyAssetsToProject = copyAssets
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
