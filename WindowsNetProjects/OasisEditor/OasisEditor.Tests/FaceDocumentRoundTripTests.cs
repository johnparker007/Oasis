using OasisEditor.Automation;
using Xunit;

namespace OasisEditor.Tests;

public sealed class FaceDocumentRoundTripTests
{
    [Fact]
    public void Serialize_AndOpen_RoundTripsFaceDocument()
    {
        const string path = "C:/Repo/Assets/sample.face";
        var source = new FaceDocumentModel
        {
            Id = "face-1",
            Title = "Front Face",
            AssetName = "Front Face Package",
            Summary = "Physical face summary",
            MaskLayer = new FaceMaskLayerModel
            {
                Id = "face-mask-layer",
                Name = "Face Mask",
                AssetPath = "Generated/Faces/face-1-mask.png",
                SourcePanel2DDocumentId = "panel-doc-1",
                SourceRegion = new FaceSourceRegionModel
                {
                    X = 100,
                    Y = 200,
                    Width = 320,
                    Height = 240
                },
                ExtractionThreshold = 24,
                GeneratedUtc = new DateTime(2026, 6, 7, 1, 0, 0, DateTimeKind.Utc),
                Width = 320,
                Height = 240,
                Contributions =
                [
                    new FaceMaskContributionModel
                    {
                        SourcePanel2DElementId = "panel-lamp-17",
                        LinkedMachineObjectReference = MachineObjectReference.Lamp(17),
                        Bounds = new FaceSourceRegionModel
                        {
                            X = 10,
                            Y = 20,
                            Width = 30,
                            Height = 40
                        },
                        PixelCount = 42
                    }
                ]
            },
            Layers =
            [
                new FaceLayerModel
                {
                    Id = "lamps",
                    Name = "Lamp Windows",
                    IsVisible = true
                }
            ],
            Elements =
            [
                new FaceArtworkElement
                {
                    ObjectId = "artwork-1",
                    Name = "Glass Artwork",
                    X = 0,
                    Y = 0,
                    Width = 320,
                    Height = 240,
                    IsVisible = true,
                    AssetPath = "Assets/Panel2D/glass.png",
                    SourcePanel2DDocumentId = "panel-doc-1",
                    SourceRegion = new FaceSourceRegionModel
                    {
                        X = 100,
                        Y = 200,
                        Width = 320,
                        Height = 240
                    },
                    Provenance = new FaceArtworkProvenanceModel
                    {
                        Generator = "Create Face from Face Source Shape",
                        GeneratedAtUtc = new DateTime(2026, 6, 7, 0, 0, 0, DateTimeKind.Utc),
                        SourcePanel2DElementId = "background-1",
                        SourcePanel2DElementKind = "background",
                        SourceAssetPath = "Assets/Panel2D/glass.png",
                        SourceElementBounds = new FaceSourceRegionModel
                        {
                            X = 0,
                            Y = 0,
                            Width = 640,
                            Height = 480
                        }
                    }
                },
                new FaceLampWindowElement
                {
                    ObjectId = "lamp-window-17",
                    Name = "Lamp 17 Window",
                    X = 10,
                    Y = 20,
                    Width = 30,
                    Height = 40,
                    IsVisible = true,
                    IsLocked = true,
                    LinkedMachineObjectReference = MachineObjectReference.Lamp(17),
                    LinkedPanel2DElementId = "panel-lamp-17"
                }
            ]
        };

        var sourceJson = FaceDocumentStorage.Serialize(source);
        var openData = DocumentWorkspaceViewModel.BuildOpenDocumentData(path, sourceJson);
        var document = new DocumentTabViewModel(
            EditorDocument.CreateFromFile(path, openData.Summary, openData.PanelTitle),
            faceDocumentJson: openData.FaceDocumentJson);

        var savedContent = DocumentWorkspaceViewModel.BuildDocumentContent(document);

        Assert.True(FaceDocumentStorage.TryRead(savedContent, out var savedDocument));
        Assert.Equal("Front Face Package", savedDocument.AssetName);
        Assert.Equal(FaceDocumentStorage.CurrentSchemaVersion, savedDocument.SchemaVersion);
        Assert.Equal("face-1", savedDocument.Id);
        Assert.Equal("Front Face", savedDocument.Title);
        Assert.Equal("Physical face summary", savedDocument.Summary);
        Assert.Equal("Generated/Faces/face-1-mask.png", savedDocument.MaskLayer!.AssetPath);
        Assert.Equal(24, savedDocument.MaskLayer.ExtractionThreshold);
        Assert.Equal(320, savedDocument.MaskLayer.Width);
        var maskContribution = Assert.Single(savedDocument.MaskLayer.Contributions!);
        Assert.Equal("panel-lamp-17", maskContribution.SourcePanel2DElementId);
        Assert.Equal("lamp:17", maskContribution.LinkedMachineObjectReference);
        Assert.Equal(42, maskContribution.PixelCount);
        var layer = Assert.Single(savedDocument.Layers!);
        Assert.Equal("lamps", layer.Id);
        Assert.Equal("Lamp Windows", layer.Name);
        Assert.Equal(2, savedDocument.Elements!.Count);
        var artwork = savedDocument.Elements![0];
        Assert.Equal("artwork-1", artwork.ObjectId);
        Assert.Equal("artwork", artwork.Kind);
        Assert.Equal("Assets/Panel2D/glass.png", artwork.AssetPath);
        Assert.Equal("panel-doc-1", artwork.SourcePanel2DDocumentId);
        Assert.Equal(100d, artwork.SourceRegion!.X);
        Assert.Equal("background-1", artwork.ArtworkProvenance!.SourcePanel2DElementId);

        var element = savedDocument.Elements![1];
        Assert.Equal("lamp-window-17", element.ObjectId);
        Assert.Equal("lampWindow", element.Kind);
        Assert.Equal("lamp:17", element.LinkedMachineObjectReference);
        Assert.Equal("panel-lamp-17", element.LinkedPanel2DElementId);
        Assert.True(element.IsLocked);
    }

    [Fact]
    public void CreateFromFile_WithFaceExtension_DetectsFaceDocumentType()
    {
        var document = EditorDocument.CreateFromFile("C:/Repo/Assets/front.face", "summary");

        Assert.Equal(EditorDocumentType.Face, document.DocumentType);
    }

    [Fact]
    public void CreateFaceStubDocument_ProvidesFaceDocumentWithStorage()
    {
        var service = new FaceDocumentCreationService();

        var document = service.CreateFaceStubDocument("Face 1", 1);

        Assert.Equal(EditorDocumentType.Face, document.Document.DocumentType);
        Assert.NotNull(document.FaceDocumentJson);
        Assert.True(FaceDocumentStorage.TryRead(document.FaceDocumentJson, out var faceDocument));
        Assert.Equal("Face 1", faceDocument.Title);
    }

    [Fact]
    public void SaveDocument_WritesFaceDocumentContent()
    {
        var service = new DocumentSaveService();
        var tempPath = Path.Combine(Path.GetTempPath(), $"oasis-save-{Guid.NewGuid():N}.face");

        try
        {
            var current = new DocumentTabViewModel(
                EditorDocument.CreateFaceStub("Face Save").MarkDirty(),
                faceDocumentJson: FaceDocumentStorage.Serialize(FaceDocumentStorage.CreateEmpty("Face Save")));

            var saved = service.SaveDocument(current, tempPath);

            Assert.False(saved.IsDirty);
            Assert.Equal(EditorDocumentType.Face, saved.Document.DocumentType);
            Assert.True(File.Exists(tempPath));
            Assert.True(FaceDocumentStorage.TryRead(File.ReadAllText(tempPath), out var faceDocument));
            Assert.Equal("Face Save", faceDocument.Title);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    [Fact]
    public void Serialize_AndRead_RoundTripsSevenSegmentDisplayElement()
    {
        var source = new FaceDocumentModel
        {
            Id = "face-seven",
            Title = "Displays",
            Elements =
            [
                new FaceSevenSegmentDisplayElement
                {
                    ObjectId = "face-seven-3",
                    Name = "Credit Digit",
                    X = 10,
                    Y = 20,
                    Width = 30,
                    Height = 40,
                    IsVisible = true,
                    IsLocked = true,
                    LinkedMachineObjectReference = MachineObjectReference.SevenSegmentDisplay(3),
                    LinkedPanel2DElementId = "panel-seven-3",
                    OnColorHex = "#FF2020",
                    OffColorHex = "#220404",
                    ShowDecimalPoint = true
                }
            ]
        };

        var json = FaceDocumentStorage.Serialize(source);
        Assert.True(FaceDocumentStorage.TryReadValidated(json, out var file, out var error), error);
        var saved = Assert.Single(file.Elements!);
        Assert.Equal("sevenSegmentDisplay", saved.Kind);
        Assert.Equal("sevenSegment:3", saved.LinkedMachineObjectReference);
        Assert.Equal("panel-seven-3", saved.LinkedPanel2DElementId);
        Assert.Equal("#FF2020", saved.OnColorHex);
        Assert.Equal("#220404", saved.OffColorHex);
        Assert.True(saved.ShowDecimalPoint);

        var model = FaceDocumentStorage.ToModel(file);
        var element = Assert.IsType<FaceSevenSegmentDisplayElement>(Assert.Single(model.Elements));
        Assert.Equal("sevenSegment:3", element.LinkedMachineObjectReference?.ToString());
        Assert.Equal("panel-seven-3", element.LinkedPanel2DElementId);
        Assert.Equal("#FF2020", element.OnColorHex);
        Assert.Equal("#220404", element.OffColorHex);
        Assert.True(element.ShowDecimalPoint);
    }

    [Fact]
    public void Serialize_AndRead_RoundTripsAlphaDisplayElement()
    {
        var source = new FaceDocumentModel
        {
            Id = "face-alpha-doc",
            Title = "Alpha Face",
            Elements =
            [
                new FaceAlphaDisplayElement
                {
                    ObjectId = "face-alpha-4",
                    Name = "Alpha 4",
                    X = 1,
                    Y = 2,
                    Width = 160,
                    Height = 32,
                    LinkedMachineObjectReference = MachineObjectReference.AlphaDisplay(4),
                    LinkedPanel2DElementId = "panel-alpha-4",
                    SegmentDisplayType = "bfm16seg",
                    OnColorHex = "#FF4040",
                    OffColorHex = "#200808",
                    ShowDecimalPoint = true,
                    ShowCommaTail = true,
                    IsReversed = true
                }
            ]
        };

        var json = FaceDocumentStorage.Serialize(source);
        Assert.True(FaceDocumentStorage.TryReadValidated(json, out var file, out var error), error);
        var stored = Assert.Single(file.Elements!);
        Assert.Equal("alphaDisplay", stored.Kind);
        Assert.Equal("alpha:4", stored.LinkedMachineObjectReference);
        Assert.Equal("panel-alpha-4", stored.LinkedPanel2DElementId);
        Assert.Equal("bfm16seg", stored.SegmentDisplayType);
        Assert.True(stored.ShowCommaTail);
        Assert.True(stored.IsReversed);

        var model = FaceDocumentStorage.ToModel(file);
        var alpha = Assert.IsType<FaceAlphaDisplayElement>(Assert.Single(model.Elements));
        Assert.Equal("alpha:4", alpha.LinkedMachineObjectReference?.ToString());
        Assert.Equal("panel-alpha-4", alpha.LinkedPanel2DElementId);
        Assert.Equal("bfm16seg", alpha.SegmentDisplayType);
        Assert.Equal("#FF4040", alpha.OnColorHex);
        Assert.Equal("#200808", alpha.OffColorHex);
        Assert.True(alpha.ShowDecimalPoint);
        Assert.True(alpha.ShowCommaTail);
        Assert.True(alpha.IsReversed);
    }

}
