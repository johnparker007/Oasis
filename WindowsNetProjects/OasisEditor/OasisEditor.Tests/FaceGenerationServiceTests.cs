using System.Windows;
using Xunit;

namespace OasisEditor.Tests;

public sealed class FaceGenerationServiceTests
{






    [Fact]
    public void GenerateFromPanelFaceSourceShape_TransformsContainedLampsIntoFaceSpace()
    {
        var panel = new Panel2DDocumentModel
        {
            Elements =
            [
                new PanelElementModel
                {
                    ObjectId = "lamp-17",
                    Name = "Start Lamp",
                    Kind = PanelElementKind.Lamp,
                    X = 110,
                    Y = 220,
                    Width = 30,
                    Height = 40,
                    DisplayNumber = 17,
                    SecondaryAssetPath = "Assets/Masks/lamp-17.png",
                    SourceComponentIndex = 2,
                    SharedSourceSetId = "set-a",
                    SharedSourceSetCount = 3,
                    SourceBlend = true,
                    IsVisible = true
                },
                new PanelElementModel
                {
                    ObjectId = "lamp-outside",
                    Name = "Outside Lamp",
                    Kind = PanelElementKind.Lamp,
                    X = 10,
                    Y = 20,
                    Width = 30,
                    Height = 40,
                    DisplayNumber = 99,
                    IsVisible = true
                }
            ]
        };
        var sourceShape = new PanelFaceSourceShapeModel
        {
            Id = "shape-1",
            Name = "Glass",
            TopLeft = new FacePointModel { X = 100, Y = 200 },
            TopRight = new FacePointModel { X = 300, Y = 200 },
            BottomRight = new FacePointModel { X = 300, Y = 400 },
            BottomLeft = new FacePointModel { X = 100, Y = 400 }
        };

        var result = new FaceGenerationService().GenerateFromPanelFaceSourceShape(panel, sourceShape, "Face", "panel-doc-1");

        Assert.Equal(1, result.ConvertedLampCount);
        Assert.Equal("shape-1", result.Document.SourceFaceShapeId);
        var lamp = Assert.IsType<FaceLampWindowElement>(Assert.Single(result.Document.Elements.OfType<FaceLampWindowElement>()));
        Assert.Equal("face-lamp-17", lamp.ObjectId);
        Assert.Equal("Start Lamp", lamp.Name);
        Assert.Equal(10d, lamp.X);
        Assert.Equal(20d, lamp.Y);
        Assert.Equal(30d, lamp.Width);
        Assert.Equal(40d, lamp.Height);
        Assert.Equal("lamp:17", lamp.LinkedMachineObjectReference?.ToString());
        Assert.Equal("lamp-17", lamp.LinkedPanel2DElementId);
        Assert.Null(lamp.BulbMaskAssetPath);
        Assert.Equal(2, lamp.SourceComponentIndex);
        Assert.Equal("set-a", lamp.SharedSourceSetId);
        Assert.Equal(3, lamp.SharedSourceSetCount);
        Assert.True(lamp.SourceBlend);
    }

}
