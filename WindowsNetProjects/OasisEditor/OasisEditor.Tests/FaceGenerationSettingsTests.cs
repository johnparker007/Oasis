using System.Windows;
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
                ClampTrayBoundsToLampWindow = true
            }
        };

        var json = FaceDocumentStorage.Serialize(source);

        Assert.True(FaceDocumentStorage.TryReadValidated(json, out var file, out var error), error);
        Assert.NotNull(file.GenerationSettings);
        Assert.Equal(17, file.GenerationSettings!.MaskExtractionThreshold);
        Assert.Equal(22.5, file.GenerationSettings.TrayBoundsInflationPercent);
        Assert.Equal(6.25, file.GenerationSettings.TrayBoundsPaddingPixels);
        Assert.True(file.GenerationSettings.ClampTrayBoundsToLampWindow);

        var model = FaceDocumentStorage.ToModel(file);
        Assert.Equal(17, model.GenerationSettings.MaskExtractionThreshold);
        Assert.Equal(22.5, model.GenerationSettings.TrayBoundsInflationPercent);
        Assert.Equal(6.25, model.GenerationSettings.TrayBoundsPaddingPixels);
        Assert.True(model.GenerationSettings.ClampTrayBoundsToLampWindow);
    }


    [Fact]
    public void BuildDocumentContent_PreservesFaceGenerationSettingsOnSave()
    {
        var source = new FaceDocumentModel
        {
            Id = "face-save-settings",
            Title = "Settings Face",
            GenerationSettings = new FaceGenerationSettingsModel
            {
                MaskExtractionThreshold = 19,
                TrayBoundsInflationPercent = 7.5,
                TrayBoundsPaddingPixels = 2.5,
                ClampTrayBoundsToLampWindow = true
            }
        };
        var document = new DocumentTabViewModel(
            EditorDocument.CreateFaceStub("Settings Face"),
            faceDocumentJson: FaceDocumentStorage.Serialize(source));

        var json = DocumentWorkspaceViewModel.BuildDocumentContent(document);

        Assert.True(FaceDocumentStorage.TryReadValidated(json, out var file, out var error), error);
        var saved = FaceDocumentStorage.ToModel(file);
        Assert.Equal(19, saved.GenerationSettings.MaskExtractionThreshold);
        Assert.Equal(7.5, saved.GenerationSettings.TrayBoundsInflationPercent);
        Assert.Equal(2.5, saved.GenerationSettings.TrayBoundsPaddingPixels);
        Assert.True(saved.GenerationSettings.ClampTrayBoundsToLampWindow);
    }

    [Fact]
    public void GenerateFromPanelRegion_CopiesProvidedDefaultsIntoNewFaceAndMaskLayer()
    {
        var settings = new FaceGenerationSettingsModel
        {
            MaskExtractionThreshold = 9,
            TrayBoundsInflationPercent = 0,
            TrayBoundsPaddingPixels = 0,
            ClampTrayBoundsToLampWindow = false
        };

        var result = new FaceGenerationService().GenerateFromPanelRegion(
            new Panel2DDocumentModel(),
            FaceSourceRegionModel.FromRect(new Rect(0, 0, 100, 100)),
            "Face",
            "panel-doc",
            generationSettings: settings);

        Assert.Equal(9, result.Document.GenerationSettings.MaskExtractionThreshold);
        Assert.Equal(9, result.Document.MaskLayer!.ExtractionThreshold);
        Assert.Equal(0, result.Document.GenerationSettings.TrayBoundsInflationPercent);
        Assert.Equal(0, result.Document.GenerationSettings.TrayBoundsPaddingPixels);
    }

    [Fact]
    public void Regenerate_UsesExistingFaceSettingsWhenNoOverrideIsProvided()
    {
        var existingFace = new FaceDocumentModel
        {
            Id = "face-1",
            Title = "Face",
            SourcePanel2DDocumentId = "panel-doc",
            SourceRegion = FaceSourceRegionModel.FromRect(new Rect(0, 0, 100, 100)),
            GenerationSettings = new FaceGenerationSettingsModel
            {
                MaskExtractionThreshold = 13,
                TrayBoundsInflationPercent = 0,
                TrayBoundsPaddingPixels = 0
            }
        };

        var result = new FaceRegenerationService().Regenerate(existingFace, new Panel2DDocumentModel());

        Assert.Equal(13, result.Document.GenerationSettings.MaskExtractionThreshold);
        Assert.Equal(13, result.Document.MaskLayer!.ExtractionThreshold);
    }

    [Fact]
    public void Regenerate_UsesOverrideSettingsWhenProvided()
    {
        var existingFace = new FaceDocumentModel
        {
            Id = "face-1",
            Title = "Face",
            SourcePanel2DDocumentId = "panel-doc",
            SourceRegion = FaceSourceRegionModel.FromRect(new Rect(0, 0, 100, 100)),
            GenerationSettings = new FaceGenerationSettingsModel { MaskExtractionThreshold = 13 }
        };

        var result = new FaceRegenerationService().Regenerate(
            existingFace,
            new Panel2DDocumentModel(),
            generationSettings: new FaceGenerationSettingsModel
            {
                MaskExtractionThreshold = 31,
                TrayBoundsInflationPercent = 0,
                TrayBoundsPaddingPixels = 0
            });

        Assert.Equal(31, result.Document.GenerationSettings.MaskExtractionThreshold);
        Assert.Equal(31, result.Document.MaskLayer!.ExtractionThreshold);
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
                        Bounds = FaceSourceRegionModel.FromRect(new Rect(10, 20, 30, 40)),
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
    public void ViewModel_InitializesFromSettingsAndCreatesEditedSettings()
    {
        var viewModel = new FaceGenerationSettingsViewModel(new FaceGenerationSettingsModel
        {
            MaskExtractionThreshold = 18,
            TrayBoundsInflationPercent = 12.5,
            TrayBoundsPaddingPixels = 3.5,
            ClampTrayBoundsToLampWindow = true
        });

        Assert.Equal("18", viewModel.MaskExtractionThresholdText);
        Assert.Equal("12.5", viewModel.TrayBoundsInflationPercentText);
        Assert.Equal("3.5", viewModel.TrayBoundsPaddingPixelsText);
        Assert.True(viewModel.ClampTrayBoundsToLampWindow);

        viewModel.MaskExtractionThresholdText = "7";
        viewModel.TrayBoundsInflationPercentText = "20";
        viewModel.TrayBoundsPaddingPixelsText = "5";
        viewModel.ClampTrayBoundsToLampWindow = false;

        Assert.True(viewModel.TryCreateSettings(out var settings));
        Assert.Equal(7, settings.MaskExtractionThreshold);
        Assert.Equal(20, settings.TrayBoundsInflationPercent);
        Assert.Equal(5, settings.TrayBoundsPaddingPixels);
        Assert.False(settings.ClampTrayBoundsToLampWindow);
    }
}
