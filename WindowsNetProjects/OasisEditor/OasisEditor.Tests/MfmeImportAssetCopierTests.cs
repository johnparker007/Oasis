using OasisEditor.Features.MfmeImport;
using Xunit;

namespace OasisEditor.Tests;

public sealed class MfmeImportAssetCopierTests
{
    [Fact]
    public void CopyAssets_CopiesKnownFoldersAndRewritesToProjectRelativePaths()
    {
        var extractRoot = CreateTempDirectory();
        var projectRoot = CreateTempDirectory();

        try
        {
            Directory.CreateDirectory(Path.Combine(extractRoot, "background"));
            Directory.CreateDirectory(Path.Combine(extractRoot, "lamps"));
            Directory.CreateDirectory(Path.Combine(extractRoot, "reels"));
            File.WriteAllText(Path.Combine(extractRoot, "background", "bg.png"), "bg");
            File.WriteAllText(Path.Combine(extractRoot, "lamps", "lamp.png"), "lamp");
            File.WriteAllText(Path.Combine(extractRoot, "reels", "band.png"), "band");

            var context = new MfmeImportContext
            {
                SourceExtractPath = extractRoot,
                ProjectRootPath = projectRoot,
                ProjectAssetsPath = Path.Combine(projectRoot, "Assets"),
                CopyAssets = true
            };

            var extract = new MfmeLegacyExtractData
            {
                ExtractRootPath = extractRoot,
                ManifestPath = Path.Combine(extractRoot, "layout.json"),
                LayoutName = "My Layout",
                Components = []
            };

            var elements = new[]
            {
                new PanelElementModel
                {
                    ObjectId = "bg-1",
                    Name = "Background",
                    Kind = PanelElementKind.Background,
                    Width = 1,
                    Height = 1,
                    AssetPath = "background/bg.png"
                },
                new PanelElementModel
                {
                    ObjectId = "lamp-1",
                    Name = "Lamp",
                    Kind = PanelElementKind.Lamp,
                    Width = 1,
                    Height = 1,
                    AssetPath = "lamps/lamp.png",
                    TextBoxFontName = "Lithograph",
                    TextBoxFontStyle = "Bold",
                    TextBoxFontSize = "12"
                },
                new PanelElementModel
                {
                    ObjectId = "reel-1",
                    Name = "Reel",
                    Kind = PanelElementKind.Reel,
                    Width = 1,
                    Height = 1,
                    AssetPath = "reels/band.png"
                }
            };

            var copier = new MfmeImportAssetCopier();
            var result = copier.CopyAssets(context, extract, elements);

            Assert.True(result.Succeeded);
            Assert.Empty(result.Warnings);
            Assert.Equal(3, result.CopiedAssetRelativePaths.Count);
            Assert.All(result.CopiedAssetRelativePaths, path => Assert.StartsWith("Assets/MfmeImport/My Layout/", path));

            Assert.Equal("Assets/MfmeImport/My Layout/Background/bg.png", result.Elements[0].AssetPath);
            Assert.Equal("Assets/MfmeImport/My Layout/Lamps/lamp.png", result.Elements[1].AssetPath);
            Assert.Equal("Assets/MfmeImport/My Layout/Reels/band.png", result.Elements[2].AssetPath);
            Assert.Equal("Lithograph", result.Elements[1].TextBoxFontName);
            Assert.Equal("Bold", result.Elements[1].TextBoxFontStyle);
            Assert.Equal("12", result.Elements[1].TextBoxFontSize);
        }
        finally
        {
            Directory.Delete(extractRoot, recursive: true);
            Directory.Delete(projectRoot, recursive: true);
        }
    }

    [Fact]
    public void CopyAssets_WithMissingFile_AddsWarningAndKeepsElementEditable()
    {
        var extractRoot = CreateTempDirectory();
        var projectRoot = CreateTempDirectory();

        try
        {
            Directory.CreateDirectory(Path.Combine(extractRoot, "background"));

            var context = new MfmeImportContext
            {
                SourceExtractPath = extractRoot,
                ProjectRootPath = projectRoot,
                ProjectAssetsPath = Path.Combine(projectRoot, "Assets"),
                CopyAssets = true
            };

            var extract = new MfmeLegacyExtractData
            {
                ExtractRootPath = extractRoot,
                ManifestPath = Path.Combine(extractRoot, "layout.json"),
                LayoutName = "layout",
                Components = []
            };

            var element = new PanelElementModel
            {
                ObjectId = "bg-1",
                Name = "Background",
                Kind = PanelElementKind.Background,
                Width = 1,
                Height = 1,
                AssetPath = "background/missing.png"
            };

            var copier = new MfmeImportAssetCopier();
            var result = copier.CopyAssets(context, extract, [element]);

            Assert.True(result.Succeeded);
            var warning = Assert.Single(result.Warnings);
            Assert.Equal("mfme.import.asset.missing", warning.Code);
            Assert.Null(result.Elements[0].AssetPath);
        }
        finally
        {
            Directory.Delete(extractRoot, recursive: true);
            Directory.Delete(projectRoot, recursive: true);
        }
    }

    [Fact]
    public void CopyAssets_WithNameCollision_UsesDeterministicSuffix()
    {
        var extractRoot = CreateTempDirectory();
        var projectRoot = CreateTempDirectory();

        try
        {
            Directory.CreateDirectory(Path.Combine(extractRoot, "lamps"));
            File.WriteAllText(Path.Combine(extractRoot, "lamps", "lamp.png"), "source");

            var context = new MfmeImportContext
            {
                SourceExtractPath = extractRoot,
                ProjectRootPath = projectRoot,
                ProjectAssetsPath = Path.Combine(projectRoot, "Assets"),
                CopyAssets = true
            };

            var extract = new MfmeLegacyExtractData
            {
                ExtractRootPath = extractRoot,
                ManifestPath = Path.Combine(extractRoot, "layout.json"),
                LayoutName = "layout",
                Components = []
            };

            var existingFolder = Path.Combine(projectRoot, "Assets", "MfmeImport", "layout", "Lamps");
            Directory.CreateDirectory(existingFolder);
            File.WriteAllText(Path.Combine(existingFolder, "lamp.png"), "existing");

            var element = new PanelElementModel
            {
                ObjectId = "lamp-1",
                Name = "Lamp",
                Kind = PanelElementKind.Lamp,
                Width = 1,
                Height = 1,
                AssetPath = "lamps/lamp.png"
            };

            var copier = new MfmeImportAssetCopier();
            var result = copier.CopyAssets(context, extract, [element]);

            Assert.True(result.Succeeded);
            Assert.Equal("Assets/MfmeImport/layout/Lamps/lamp_2.png", result.Elements[0].AssetPath);
            Assert.Contains("Assets/MfmeImport/layout/Lamps/lamp_2.png", result.CopiedAssetRelativePaths);
        }
        finally
        {
            Directory.Delete(extractRoot, recursive: true);
            Directory.Delete(projectRoot, recursive: true);
        }
    }

    [Fact]
    public void CopyAssets_WithEscapingRelativePath_IsRejectedByContainmentGuard()
    {
        var extractRoot = CreateTempDirectory();
        var projectRoot = CreateTempDirectory();

        try
        {
            var context = new MfmeImportContext
            {
                SourceExtractPath = extractRoot,
                ProjectRootPath = projectRoot,
                ProjectAssetsPath = Path.Combine(projectRoot, "Assets"),
                CopyAssets = true
            };

            var extract = new MfmeLegacyExtractData
            {
                ExtractRootPath = extractRoot,
                ManifestPath = Path.Combine(extractRoot, "layout.json"),
                LayoutName = "layout",
                Components = []
            };

            var element = new PanelElementModel
            {
                ObjectId = "x",
                Name = "Escape",
                Kind = PanelElementKind.Image,
                Width = 1,
                Height = 1,
                AssetPath = "background/../outside.png"
            };

            var copier = new MfmeImportAssetCopier();
            var result = copier.CopyAssets(context, extract, [element]);

            Assert.True(result.Succeeded);
            var warning = Assert.Single(result.Warnings);
            Assert.Equal("mfme.import.asset.path.invalid", warning.Code);
            Assert.Null(result.Elements[0].AssetPath);
        }
        finally
        {
            Directory.Delete(extractRoot, recursive: true);
            Directory.Delete(projectRoot, recursive: true);
        }
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"oasis-mfme-copy-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
