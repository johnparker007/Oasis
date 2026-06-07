using System.Windows;
using Xunit;

namespace OasisEditor.Tests;

public sealed class FaceRegenerationServiceTests
{
    [Fact]
    public void Regenerate_UpdatesGeneratedElementsAndPreservesRuntimeReferencesAndManualElements()
    {
        var sourcePanel = new Panel2DDocumentModel
        {
            Elements =
            [
                new PanelElementModel
                {
                    ObjectId = "lamp-1",
                    Name = "Start Lamp Updated",
                    Kind = PanelElementKind.Lamp,
                    X = 130,
                    Y = 240,
                    Width = 35,
                    Height = 45,
                    DisplayNumber = 1,
                    IsVisible = true
                },
                new PanelElementModel
                {
                    ObjectId = "seven-2",
                    Name = "Credits",
                    Kind = PanelElementKind.SevenSegment,
                    X = 170,
                    Y = 250,
                    Width = 42,
                    Height = 24,
                    DisplayNumber = 2,
                    OnColorHex = "#FF0000",
                    IsVisible = true
                },
                new PanelElementModel
                {
                    ObjectId = "alpha-3",
                    Name = "Message",
                    Kind = PanelElementKind.Alpha,
                    X = 220,
                    Y = 250,
                    Width = 100,
                    Height = 28,
                    DisplayNumber = 3,
                    SegmentDisplayType = "bfm16seg",
                    IsVisible = true
                },
                new PanelElementModel
                {
                    ObjectId = "reel-4",
                    Name = "Reel Four",
                    Kind = PanelElementKind.Reel,
                    X = 120,
                    Y = 300,
                    Width = 50,
                    Height = 120,
                    DisplayNumber = 4,
                    AssetPath = "Assets/Reels/updated.png",
                    Stops = 16,
                    IsVisible = true
                }
            ]
        };

        var existingFace = new FaceDocumentModel
        {
            Id = "face-1",
            Title = "Face",
            SourcePanel2DDocumentId = "panel-doc",
            SourceRegion = FaceSourceRegionModel.FromRect(new Rect(100, 200, 300, 300)),
            Elements =
            [
                new FaceLampWindowElement
                {
                    ObjectId = "custom-face-lamp-id",
                    Name = "Old Start Lamp",
                    X = 10,
                    Y = 20,
                    Width = 30,
                    Height = 40,
                    LinkedMachineObjectReference = MachineObjectReference.Lamp(99),
                    LinkedPanel2DElementId = "lamp-1"
                },
                new FaceSevenSegmentDisplayElement
                {
                    ObjectId = "custom-seven-id",
                    LinkedMachineObjectReference = MachineObjectReference.SevenSegmentDisplay(77),
                    LinkedPanel2DElementId = "seven-2"
                },
                new FaceAlphaDisplayElement
                {
                    ObjectId = "custom-alpha-id",
                    LinkedMachineObjectReference = MachineObjectReference.AlphaDisplay(88),
                    LinkedPanel2DElementId = "alpha-3"
                },
                new FaceReelDisplayElement
                {
                    ObjectId = "custom-reel-id",
                    LinkedMachineObjectReference = MachineObjectReference.Reel(66),
                    LinkedPanel2DElementId = "reel-4"
                },
                new FaceLampWindowElement
                {
                    ObjectId = "manual-lamp-window",
                    Name = "Manual Face Lamp",
                    X = 1,
                    Y = 2,
                    Width = 3,
                    Height = 4,
                    LinkedMachineObjectReference = MachineObjectReference.Lamp(5)
                },
                new FaceLampWindowElement
                {
                    ObjectId = "stale-generated-lamp",
                    LinkedPanel2DElementId = "lamp-removed",
                    LinkedMachineObjectReference = MachineObjectReference.Lamp(6)
                }
            ]
        };

        var result = new FaceRegenerationService().Regenerate(existingFace, sourcePanel);

        Assert.Equal(4, result.UpdatedElementCount);
        Assert.Equal(1, result.AddedElementCount); // source-region artwork placeholder
        Assert.Equal(1, result.RemovedGeneratedElementCount);
        Assert.Equal(1, result.PreservedManualElementCount);

        var lamp = Assert.IsType<FaceLampWindowElement>(result.Document.Elements.Single(element => element.ObjectId == "custom-face-lamp-id"));
        Assert.Equal("Start Lamp Updated", lamp.Name);
        Assert.Equal(30d, lamp.X);
        Assert.Equal(40d, lamp.Y);
        Assert.Equal(35d, lamp.Width);
        Assert.Equal(45d, lamp.Height);
        Assert.Equal("lamp:99", lamp.LinkedMachineObjectReference?.ToString());
        Assert.Equal("lamp-1", lamp.LinkedPanel2DElementId);

        var sevenSegment = Assert.IsType<FaceSevenSegmentDisplayElement>(result.Document.Elements.Single(element => element.ObjectId == "custom-seven-id"));
        Assert.Equal("sevenSegment:77", sevenSegment.LinkedMachineObjectReference?.ToString());
        Assert.Equal("#FF0000", sevenSegment.OnColorHex);

        var alpha = Assert.IsType<FaceAlphaDisplayElement>(result.Document.Elements.Single(element => element.ObjectId == "custom-alpha-id"));
        Assert.Equal("alpha:88", alpha.LinkedMachineObjectReference?.ToString());
        Assert.Equal("bfm16seg", alpha.SegmentDisplayType);

        var reel = Assert.IsType<FaceReelDisplayElement>(result.Document.Elements.Single(element => element.ObjectId == "custom-reel-id"));
        Assert.Equal("reel:66", reel.LinkedMachineObjectReference?.ToString());
        Assert.Equal("Assets/Reels/updated.png", reel.AssetPath);

        Assert.Contains(result.Document.Elements, element => element.ObjectId == "manual-lamp-window");
        Assert.DoesNotContain(result.Document.Elements, element => element.ObjectId == "stale-generated-lamp");
        Assert.NotNull(result.Document.LastRegeneratedAtUtc);
    }

    [Fact]
    public void Regenerate_UpdatesButtonsAndPreservesInputReference()
    {
        var buttonGuid = Guid.NewGuid();
        var panel = new Panel2DDocumentModel
        {
            Elements =
            [
                new PanelElementModel
                {
                    ObjectId = buttonGuid.ToString("N"),
                    Name = "Start Button Visual",
                    Kind = PanelElementKind.Lamp,
                    X = 125,
                    Y = 240,
                    Width = 40,
                    Height = 30,
                    DisplayNumber = 7,
                    IsVisible = true
                }
            ]
        };
        var inputDefinitions = new[]
        {
            new InputDefinitionModel
            {
                Id = "start",
                Name = "Start Button Updated",
                LinkedVisualElementId = buttonGuid
            }
        };
        var existingFace = new FaceDocumentModel
        {
            Id = "face-buttons",
            Title = "Face Buttons",
            SourcePanel2DDocumentId = "panel-doc",
            SourceRegion = FaceSourceRegionModel.FromRect(new Rect(100, 200, 100, 100)),
            Elements =
            [
                new FaceButtonElement
                {
                    ObjectId = "custom-button-id",
                    Name = "Old Button",
                    LinkedPanel2DElementId = buttonGuid.ToString("N"),
                    LinkedMachineObjectReference = MachineObjectReference.Input("custom-start"),
                    LinkedInputReference = new MachineInputReference(MachineObjectReference.Input("custom-start"))
                }
            ]
        };

        var result = new FaceRegenerationService().Regenerate(existingFace, panel, inputDefinitions);

        var button = Assert.IsType<FaceButtonElement>(result.Document.Elements.Single(element => element.ObjectId == "custom-button-id"));
        Assert.Equal("Start Button Updated", button.Name);
        Assert.Equal(25d, button.X);
        Assert.Equal(40d, button.Y);
        Assert.Equal("input:custom-start", button.LinkedMachineObjectReference?.ToString());
        Assert.Equal("input:custom-start", button.LinkedInputReference?.ToString());
    }
}
