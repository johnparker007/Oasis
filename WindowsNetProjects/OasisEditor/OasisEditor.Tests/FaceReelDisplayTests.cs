using System.Windows;
using OasisEditor;
using Xunit;

namespace OasisEditor.Tests;

public sealed class FaceReelDisplayTests
{
    [Fact]
    public void GenerateFromPanelRegion_ConvertsContainedReelsWithMachineReferences()
    {
        var panel = new Panel2DDocumentModel
        {
            Elements =
            [
                new PanelElementModel
                {
                    ObjectId = "reel-2",
                    Name = "Reel 2",
                    Kind = PanelElementKind.Reel,
                    X = 120,
                    Y = 230,
                    Width = 48,
                    Height = 120,
                    DisplayNumber = 2,
                    AssetPath = "Assets/Reels/reel2.png",
                    Stops = 16,
                    VisibleScale = 0.33d,
                    BandOffset = 0.125d,
                    IsReversed = true,
                    IsVisible = true
                }
            ]
        };

        var result = new FaceGenerationService().GenerateFromPanelRegion(
            panel,
            FaceSourceRegionModel.FromRect(new Rect(100, 200, 200, 200)),
            "Generated Face",
            "panel-doc-1");

        Assert.Equal(1, result.ConvertedReelDisplayCount);
        var element = Assert.IsType<FaceReelDisplayElement>(result.Document.Elements[1]);
        Assert.Equal("face-reel-2", element.ObjectId);
        Assert.Equal("Reel 2", element.Name);
        Assert.Equal(20d, element.X);
        Assert.Equal(30d, element.Y);
        Assert.Equal("reel:2", element.LinkedMachineObjectReference?.ToString());
        Assert.Equal("reel-2", element.LinkedPanel2DElementId);
        Assert.Equal("Assets/Reels/reel2.png", element.AssetPath);
        Assert.Equal(16, element.Stops);
        Assert.Equal(0.33d, element.VisibleScale);
        Assert.Equal(0.125d, element.BandOffset);
        Assert.True(element.IsReversed);
    }

    [Fact]
    public void Serialize_AndRead_RoundTripsReelDisplayElement()
    {
        var source = new FaceDocumentModel
        {
            Id = "face-reel-doc",
            Title = "Reel Face",
            Elements =
            [
                new FaceReelDisplayElement
                {
                    ObjectId = "face-reel-2",
                    Name = "Reel 2",
                    X = 1,
                    Y = 2,
                    Width = 50,
                    Height = 120,
                    IsVisible = true,
                    IsLocked = true,
                    LinkedMachineObjectReference = MachineObjectReference.Reel(2),
                    LinkedPanel2DElementId = "panel-reel-2",
                    AssetPath = "Assets/Reels/reel2.png",
                    Stops = 16,
                    VisibleScale = 0.5d,
                    BandOffset = 0.25d,
                    IsReversed = true
                }
            ]
        };

        var json = FaceDocumentStorage.Serialize(source);
        Assert.True(FaceDocumentStorage.TryReadValidated(json, out var file, out var error), error);
        var saved = Assert.Single(file.Elements!);
        Assert.Equal("reelDisplay", saved.Kind);
        Assert.Equal("reel:2", saved.LinkedMachineObjectReference);
        Assert.Equal("panel-reel-2", saved.LinkedPanel2DElementId);
        Assert.Equal("Assets/Reels/reel2.png", saved.AssetPath);
        Assert.Equal(16, saved.Stops);
        Assert.Equal(0.5d, saved.VisibleScale);
        Assert.Equal(0.25d, saved.BandOffset);
        Assert.True(saved.IsReversed);

        var model = FaceDocumentStorage.ToModel(file);
        var element = Assert.IsType<FaceReelDisplayElement>(Assert.Single(model.Elements));
        Assert.Equal("reel:2", element.LinkedMachineObjectReference?.ToString());
        Assert.Equal("panel-reel-2", element.LinkedPanel2DElementId);
        Assert.Equal("Assets/Reels/reel2.png", element.AssetPath);
        Assert.Equal(16, element.Stops);
        Assert.Equal(0.5d, element.VisibleScale);
        Assert.Equal(0.25d, element.BandOffset);
        Assert.True(element.IsReversed);
    }

    [Fact]
    public void RuntimeResolver_UsesMachineReferenceAndIgnoresLinkedPanelElementId()
    {
        var runtimeState = new MachineRuntimeState();
        runtimeState.SetReelPositionIfChanged(MachineObjectReference.Reel(2), 83d);
        runtimeState.SetReelPositionIfChanged("panel-reel-2", 12d);
        var reel = new FaceReelDisplayElement
        {
            ObjectId = "face-reel-2",
            LinkedMachineObjectReference = MachineObjectReference.Reel(2),
            LinkedPanel2DElementId = "panel-reel-2"
        };

        var position = FaceRuntimeStateResolver.Instance.GetReelPosition(reel, runtimeState);

        Assert.Equal(83d, position);
    }

    [Fact]
    public void RuntimeResolver_ReturnsZeroWhenOnlyLinkedPanelElementIdHasState()
    {
        var runtimeState = new MachineRuntimeState();
        runtimeState.SetReelPositionIfChanged("panel-reel-2", 12d);
        var reel = new FaceReelDisplayElement
        {
            ObjectId = "face-reel-2",
            LinkedPanel2DElementId = "panel-reel-2"
        };

        var position = FaceRuntimeStateResolver.Instance.GetReelPosition(reel, runtimeState);

        Assert.Equal(0d, position);
    }
}
