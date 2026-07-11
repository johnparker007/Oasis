using OasisEditor.Features.LayoutImport;
using Xunit;

namespace OasisEditor.Tests;

public sealed class LayoutImportAssetCopierTests
{
    [Fact]
    public void CopyAssetsFromStaging_CopiesAssetsIntoFmlImportFolderAndUpdatesElements()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "OasisEditorTests", Guid.NewGuid().ToString("N"));
        var stagingRoot = Path.Combine(tempRoot, "staging");
        var projectRoot = Path.Combine(tempRoot, "project");
        var assetsRoot = Path.Combine(projectRoot, "Assets");
        Directory.CreateDirectory(Path.Combine(stagingRoot, "background"));
        File.WriteAllBytes(Path.Combine(stagingRoot, "background", "bg.png"), [1, 2, 3]);

        var elements = new[]
        {
            new PanelElementModel { ObjectId = "bg", Name = "Background", Kind = PanelElementKind.Background, Width = 10, Height = 10, AssetPath = "background/bg.png" }
        };

        var result = new LayoutImportAssetCopier().CopyAssetsFromStaging(stagingRoot, "My Layout", assetsRoot, copyAssets: true, elements);

        Assert.True(result.Succeeded);
        Assert.Contains("Assets/FmlImport/My Layout/Background/bg.png", result.CopiedAssetRelativePaths);
        Assert.Contains(result.Elements, element => element.Kind == PanelElementKind.Background && element.AssetPath == "Assets/FmlImport/My Layout/Background/bg.png");
    }
}
