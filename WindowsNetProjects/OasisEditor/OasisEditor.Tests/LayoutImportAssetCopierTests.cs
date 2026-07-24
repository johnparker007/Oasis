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


    [Fact]
    public void CopyAssetsFromStaging_PreservesReelLampEnabledAndSlotsWhileMappingAssets()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "OasisEditorTests", Guid.NewGuid().ToString("N"));
        var stagingRoot = Path.Combine(tempRoot, "staging");
        var projectRoot = Path.Combine(tempRoot, "project");
        var assetsRoot = Path.Combine(projectRoot, "Assets");
        Directory.CreateDirectory(Path.Combine(stagingRoot, "reels"));
        File.WriteAllBytes(Path.Combine(stagingRoot, "reels", "reel.png"), [1, 2, 3]);
        var elements = new[]
        {
            new PanelElementModel
            {
                ObjectId = "reel", Name = "Reel 2", Kind = PanelElementKind.Reel, Width = 10, Height = 10, AssetPath = "reels/reel.png",
                ReelLampsEnabled = true,
                ReelLamps =
                [
                    new ReelLampSlotModel { Position = ReelLampSlotPosition.Top, LampNumber = 5, LocalVerticalCenter = 0.12d, Radius = 0.21d, Intensity = 1.1d },
                    new ReelLampSlotModel { Position = ReelLampSlotPosition.Middle, LampNumber = 4, LocalVerticalCenter = 0.45d, Radius = 0.31d, Intensity = 1.2d },
                    new ReelLampSlotModel { Position = ReelLampSlotPosition.Bottom, LampNumber = 3, LocalVerticalCenter = 0.88d, Radius = 0.41d, Intensity = 1.3d }
                ]
            }
        };

        var result = new LayoutImportAssetCopier().CopyAssetsFromStaging(stagingRoot, "My Layout", assetsRoot, copyAssets: true, elements, FmlBackgroundMode.TransparentBackground);

        Assert.True(result.Succeeded);
        var reel = Assert.Single(result.Elements);
        Assert.Equal("Assets/FmlImport/My Layout/Reels/reel.png", reel.AssetPath);
        Assert.True(reel.ReelLampsEnabled);
        Assert.Equal([5, 4, 3], reel.ReelLamps.Select(lamp => lamp.LampNumber).ToArray());
    }

}
