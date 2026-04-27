using System;
using System.IO;
using OasisEditor.Features.MfmeImport;
using Xunit;

namespace OasisEditor.Tests;

public sealed class MfmeImportAssetCopyServiceTests
{
    [Fact]
    public void CopyAssets_CopiesToExpectedProjectRelativePaths()
    {
        using var temp = new TempPaths();
        Directory.CreateDirectory(Path.Combine(temp.ExtractRoot, "background"));
        File.WriteAllText(Path.Combine(temp.ExtractRoot, "background", "bg.png"), "bg");

        var service = new MfmeImportAssetCopyService();
        var result = service.CopyAssets(temp.CreateContext(), "Demo Layout", [
            new PanelElementModel
            {
                ObjectId = "bg1",
                Kind = PanelElementKind.Background,
                Name = "Background",
                Width = 10,
                Height = 10,
                AssetPath = "background/bg.png"
            }
        ]);

        var mapped = Assert.Single(result.Elements);
        Assert.Equal("Assets/MfmeImport/Demo Layout/Background/bg.png", mapped.AssetPath);
        Assert.Single(result.CopiedAssets);
        Assert.Empty(result.Warnings);
        Assert.True(File.Exists(Path.Combine(temp.ProjectRoot, mapped.AssetPath!.Replace('/', Path.DirectorySeparatorChar))));
    }

    [Fact]
    public void CopyAssets_MissingSourceImage_AddsWarningAndKeepsElement()
    {
        using var temp = new TempPaths();

        var service = new MfmeImportAssetCopyService();
        var result = service.CopyAssets(temp.CreateContext(), "Demo", [
            new PanelElementModel
            {
                ObjectId = "lamp1",
                Kind = PanelElementKind.Lamp,
                Name = "Lamp",
                Width = 10,
                Height = 10,
                AssetPath = "lamps/missing.png"
            }
        ]);

        var element = Assert.Single(result.Elements);
        Assert.Null(element.AssetPath);
        Assert.Empty(result.CopiedAssets);
        var warning = Assert.Single(result.Warnings);
        Assert.Equal("missing-asset", warning.Code);
    }

    [Fact]
    public void CopyAssets_PreventsPathTraversal()
    {
        using var temp = new TempPaths();

        var service = new MfmeImportAssetCopyService();
        var result = service.CopyAssets(temp.CreateContext(), "Demo", [
            new PanelElementModel
            {
                ObjectId = "evil",
                Kind = PanelElementKind.Image,
                Name = "Evil",
                Width = 10,
                Height = 10,
                AssetPath = "../../outside.png"
            }
        ]);

        Assert.Empty(result.CopiedAssets);
        var warning = Assert.Single(result.Warnings);
        Assert.Equal("invalid-asset-path", warning.Code);
    }

    [Fact]
    public void CopyAssets_DuplicateDestinationNames_UsesDeterministicSuffix()
    {
        using var temp = new TempPaths();
        Directory.CreateDirectory(Path.Combine(temp.ExtractRoot, "lamps"));
        Directory.CreateDirectory(Path.Combine(temp.ExtractRoot, "reels"));
        File.WriteAllText(Path.Combine(temp.ExtractRoot, "lamps", "shared.png"), "lamp");
        File.WriteAllText(Path.Combine(temp.ExtractRoot, "reels", "shared.png"), "reel");

        var service = new MfmeImportAssetCopyService();
        var result = service.CopyAssets(temp.CreateContext(), "Demo", [
            new PanelElementModel
            {
                ObjectId = "one",
                Kind = PanelElementKind.Lamp,
                Name = "Lamp",
                Width = 10,
                Height = 10,
                AssetPath = "lamps/shared.png"
            },
            new PanelElementModel
            {
                ObjectId = "two",
                Kind = PanelElementKind.Lamp,
                Name = "Reel",
                Width = 10,
                Height = 10,
                AssetPath = "lamps/shared.png"
            }
        ]);

        Assert.Equal(1, result.CopiedAssets.Count);
        Assert.Equal(result.Elements[0].AssetPath, result.Elements[1].AssetPath);
    }

    private sealed class TempPaths : IDisposable
    {
        public TempPaths()
        {
            Root = Path.Combine(Path.GetTempPath(), $"oasis-mfme-copy-tests-{Guid.NewGuid():N}");
            ProjectRoot = Path.Combine(Root, "Project");
            AssetsRoot = Path.Combine(ProjectRoot, "Assets");
            ExtractRoot = Path.Combine(Root, "sample.extract");

            Directory.CreateDirectory(ProjectRoot);
            Directory.CreateDirectory(AssetsRoot);
            Directory.CreateDirectory(ExtractRoot);
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
                CopyAssetsToProject = true
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
