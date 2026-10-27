using OasisEditor.FacePipeline;
using Xunit;

namespace OasisEditor.Tests;

public sealed class FaceGenerationSettingsTests
{
    [Fact]
    public void Serialize_AndRead_RoundTripsGenerationSettings()
    {
        var source = new FaceDocumentModel
        {
            Id = "face-settings",
            Title = "Settings Face",
            GenerationSettings = new FaceGenerationSettingsModel
            {
                MaskExtractionThreshold = 17,
                TrayBoundsInflationPercent = 22.5,
                TrayBoundsPaddingPixels = 6.25,
                ClampTrayBoundsToLampWindow = true,
                PostWarpSharpeningEnabled = false,
                PostWarpSharpeningAmount = 1.2,
                PostWarpSharpeningRadiusPixels = 1.5,
                PostWarpSharpeningThreshold = 8
            }
        };

        var json = FaceDocumentStorage.Serialize(source);

        Assert.True(FaceDocumentStorage.TryReadValidated(json, out var file, out var error), error);
        Assert.Equal(FaceDocumentStorage.CurrentSchemaVersion, file.SchemaVersion);
        Assert.NotNull(file.GenerationSettings);
        Assert.Equal(17, file.GenerationSettings!.MaskExtractionThreshold);
        Assert.Equal(22.5, file.GenerationSettings.TrayBoundsInflationPercent);
        Assert.Equal(6.25, file.GenerationSettings.TrayBoundsPaddingPixels);
        Assert.True(file.GenerationSettings.ClampTrayBoundsToLampWindow);
        Assert.False(file.GenerationSettings.PostWarpSharpeningEnabled);
        Assert.Equal(1.2, file.GenerationSettings.PostWarpSharpeningAmount);
        Assert.Equal(1.5, file.GenerationSettings.PostWarpSharpeningRadiusPixels);
        Assert.Equal(8, file.GenerationSettings.PostWarpSharpeningThreshold);

        var model = FaceDocumentStorage.ToModel(file);
        Assert.Equal(17, model.GenerationSettings.MaskExtractionThreshold);
        Assert.Equal(22.5, model.GenerationSettings.TrayBoundsInflationPercent);
        Assert.Equal(6.25, model.GenerationSettings.TrayBoundsPaddingPixels);
        Assert.True(model.GenerationSettings.ClampTrayBoundsToLampWindow);
        Assert.False(model.GenerationSettings.PostWarpSharpeningEnabled);
        Assert.Equal(1.2, model.GenerationSettings.PostWarpSharpeningAmount);
    }


    [Fact]
    public void AutoAuthor_AppliesTrayPaddingAndInflationToContributionBounds()
    {
        var result = new FaceTrayAutoAuthoringService().AutoAuthor(new FaceDocumentModel
        {
            GenerationSettings = new FaceGenerationSettingsModel
            {
                MaskExtractionThreshold = 24,
                TrayBoundsPaddingPixels = 2,
                TrayBoundsInflationPercent = 10,
                ClampTrayBoundsToLampWindow = false
            },
            MaskLayer = new FaceMaskLayerModel
            {
                Contributions =
                [
                    new FaceMaskContributionModel
                    {
                        SourcePanel2DElementId = "lamp-1",
                        Bounds = FaceSourceRegionModel.FromBounds(10, 20, 30, 40),
                        PixelCount = 100
                    }
                ]
            },
            Elements =
            [
                new FaceLampWindowElement
                {
                    ObjectId = "face-lamp-1",
                    LinkedPanel2DElementId = "lamp-1",
                    X = 0,
                    Y = 0,
                    Width = 100,
                    Height = 100,
                    IsVisible = true
                }
            ]
        });

        var tray = Assert.Single(result.Trays);
        Assert.Equal(6.3d, tray.Bounds!.X, 3);
        Assert.Equal(15.8d, tray.Bounds.Y, 3);
        Assert.Equal(37.4d, tray.Bounds.Width, 3);
        Assert.Equal(48.4d, tray.Bounds.Height, 3);
    }

    [Fact]
    public void DefaultsAndNormalize_UseBoundedSharpeningValues()
    {
        var defaults = FaceGenerationSettingsModel.Default;
        Assert.True(defaults.PostWarpSharpeningEnabled);
        Assert.Equal(0.65, defaults.PostWarpSharpeningAmount);
        Assert.Equal(0.75, defaults.PostWarpSharpeningRadiusPixels);
        Assert.Equal(2, defaults.PostWarpSharpeningThreshold);

        var normalized = new FaceGenerationSettingsModel
        {
            PostWarpSharpeningAmount = double.NaN,
            PostWarpSharpeningRadiusPixels = double.PositiveInfinity,
            PostWarpSharpeningThreshold = 999
        }.Normalize();
        Assert.Equal(0.65, normalized.PostWarpSharpeningAmount);
        Assert.Equal(0.75, normalized.PostWarpSharpeningRadiusPixels);
        Assert.Equal(255, normalized.PostWarpSharpeningThreshold);
        Assert.Equal(0, new FaceGenerationSettingsModel { PostWarpSharpeningAmount = -1 }.Normalize().PostWarpSharpeningAmount);
        Assert.Equal(2, new FaceGenerationSettingsModel { PostWarpSharpeningAmount = 9 }.Normalize().PostWarpSharpeningAmount);
        Assert.Equal(0.1, new FaceGenerationSettingsModel { PostWarpSharpeningRadiusPixels = 0 }.Normalize().PostWarpSharpeningRadiusPixels);
        Assert.Equal(3, new FaceGenerationSettingsModel { PostWarpSharpeningRadiusPixels = 9 }.Normalize().PostWarpSharpeningRadiusPixels);
    }

    private static Panel2DDocumentModel CreatePanelWithFaceSourceShape()
    {
        return new Panel2DDocumentModel
        {
            FaceSourceShapes =
            [
                new PanelFaceSourceShapeModel
                {
                    Id = "shape-1",
                    Name = "Main Glass Source",
                    TopLeft = new FacePointModel { X = 0, Y = 0 },
                    TopRight = new FacePointModel { X = 100, Y = 0 },
                    BottomRight = new FacePointModel { X = 100, Y = 100 },
                    BottomLeft = new FacePointModel { X = 0, Y = 100 }
                }
            ]
        };
    }
}
