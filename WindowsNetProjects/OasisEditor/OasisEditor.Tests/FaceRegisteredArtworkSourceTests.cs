using SkiaSharp;
using Xunit;
using EditorCommands = OasisEditor.Commands;

namespace OasisEditor.Tests;

public sealed class FaceRegisteredArtworkSourceTests
{
    [Fact]
    public void Registered_source_round_trips_semantic_normalized_corners_and_latest_schema()
    {
        var source = new FaceArtworkSourceModel
        {
            Kind = FaceArtworkSourceKind.RegisteredImage, AssetPath = "Assets/FaceSources/photo.jpg",
            RegistrationQuad = new FaceArtworkRegistrationQuadModel
            {
                TopLeft = new() { X = .12, Y = .08 }, TopRight = new() { X = .91, Y = .11 },
                BottomRight = new() { X = .87, Y = .94 }, BottomLeft = new() { X = .09, Y = .9 }
            }
        };
        var json = FaceDocumentStorage.Serialize(new FaceDocumentModel { Artwork = new FaceArtworkModel { Source = source } });
        Assert.Contains($"\"SchemaVersion\": {FaceDocumentStorage.CurrentSchemaVersion}", json);
        Assert.True(FaceDocumentStorage.TryRead(json, out var file));
        var restored = FaceDocumentStorage.ToModel(file).Artwork!.Source;
        Assert.Equal(FaceArtworkSourceKind.RegisteredImage, restored.Kind);
        Assert.Equal(.12, restored.RegistrationQuad.TopLeft.X);
        Assert.Equal(.11, restored.RegistrationQuad.TopRight.Y);
        Assert.Equal(.87, restored.RegistrationQuad.BottomRight.X);
        Assert.Equal(.9, restored.RegistrationQuad.BottomLeft.Y);
    }

    [Fact]
    public void Full_image_quad_rectifies_without_distortion()
    {
        using var source = CreateCornerBitmap(41, 61);
        var quad = new[] { P(0, 0), P(40, 0), P(40, 60), P(0, 60) };
        using var result = PerspectiveRectificationService.Rectify(source, quad, 41, 61);
        Assert.Equal(source.GetPixel(0, 0), result.GetPixel(0, 0));
        Assert.Equal(source.GetPixel(40, 60), result.GetPixel(40, 60));
        Assert.Equal(source.GetPixel(20, 30), result.GetPixel(20, 30));
    }

    [Fact]
    public void Perspective_quad_maps_known_corner_and_centre_content()
    {
        using var source = CreateCornerBitmap(101, 101);
        var quad = new[] { P(10, 5), P(90, 10), P(80, 95), P(20, 90) };
        foreach (var (point, color) in new[] { (quad[0], SKColors.Red), (quad[1], SKColors.Green), (quad[2], SKColors.Blue), (quad[3], SKColors.Yellow) })
            source.SetPixel((int)point.X, (int)point.Y, color);
        using var result = PerspectiveRectificationService.Rectify(source, quad, 81, 86);
        Assert.Equal(SKColors.Red, result.GetPixel(0, 0));
        Assert.Equal(SKColors.Green, result.GetPixel(80, 0));
        Assert.Equal(SKColors.Blue, result.GetPixel(80, 85));
        Assert.Equal(SKColors.Yellow, result.GetPixel(0, 85));
    }

    [Fact]
    public void Registered_rebuild_preserves_source_resolution_and_writes_only_outputs_to_generated()
    {
        var root = Path.Combine(Path.GetTempPath(), $"oasis-registered-{Guid.NewGuid():N}");
        var sourcePath = Path.Combine(root, "Assets", "FaceSources", "photo.png");
        var outputPath = Path.Combine(root, "Generated", "Faces", "Test", "Artwork", "artwork.png");
        Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
        using (var source = CreateCornerBitmap(1200, 1800)) Save(source, sourcePath);
        try
        {
            var artwork = new FaceArtworkModel { Source = new FaceArtworkSourceModel { Kind = FaceArtworkSourceKind.RegisteredImage, AssetPath = "Assets/FaceSources/photo.png" } };
            var result = new FaceArtworkRebuildService().RebuildRegisteredImage(artwork, root, outputPath, null, out var size);
            Assert.True(result.Succeeded, result.ErrorMessage);
            Assert.Equal(1200, size.Width); Assert.Equal(1800, size.Height);
            Assert.True(File.Exists(sourcePath)); Assert.True(File.Exists(outputPath));
            Assert.True(File.Exists(Path.Combine(Path.GetDirectoryName(outputPath)!, "original.png")));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void Registration_normalization_clamps_points_without_reordering_semantics()
    {
        var normalized = new FaceArtworkRegistrationQuadModel
        {
            TopLeft = new() { X = 2, Y = -.5 }, TopRight = new() { X = -.2, Y = 2 },
            BottomRight = new() { X = .4, Y = .6 }, BottomLeft = new() { X = .7, Y = .8 }
        }.Normalize();
        Assert.Equal((1d, 0d), (normalized.TopLeft.X, normalized.TopLeft.Y));
        Assert.Equal((0d, 1d), (normalized.TopRight.X, normalized.TopRight.Y));
        Assert.Equal((.4, .6), (normalized.BottomRight.X, normalized.BottomRight.Y));
    }

    [Fact]
    public void Registration_edit_command_undoes_and_redoes_as_one_authored_change()
    {
        var model = new FaceDocumentModel { Artwork = new FaceArtworkModel { Source = new FaceArtworkSourceModel { Kind = FaceArtworkSourceKind.RegisteredImage, AssetPath = "Assets/a.png" } } };
        var document = new DocumentTabViewModel(EditorDocument.CreateFaceStub("Face"), faceDocumentJson: FaceDocumentStorage.Serialize(model));
        var edited = new FaceArtworkSourceModel { Kind = FaceArtworkSourceKind.RegisteredImage, AssetPath = "Assets/a.png",
            RegistrationQuad = new FaceArtworkRegistrationQuadModel { TopLeft = new() { X = .2, Y = .3 }, TopRight = new() { X = 1 }, BottomRight = new() { X = 1, Y = 1 }, BottomLeft = new() { Y = 1 } } };
        var command = FaceMutationCommands.CreateSetArtworkSourceCommand(document.DocumentId, document, edited, "Move registration corner");
        command.Execute();
        Assert.Equal(.2, document.GetFaceDocument().Artwork!.Source.RegistrationQuad.TopLeft.X);
        command.Undo();
        Assert.Equal(0, document.GetFaceDocument().Artwork!.Source.RegistrationQuad.TopLeft.X);
        command.Execute();
        Assert.Equal(.2, document.GetFaceDocument().Artwork!.Source.RegistrationQuad.TopLeft.X);
    }

    [Fact]
    public void Artwork_inspector_exposes_registered_source_workflow_and_switch_preserves_calibration()
    {
        var calibration=new ArtworkCalibrationOperationModel{BlackReference=new CalibrationReferenceModel{Samples=[new CalibrationSampleModel{Id="black",X=.2,Y=.3}]},SameColorGroups=[new SameColorCalibrationGroupModel{Id="group",Name="Gold",Samples=[new CalibrationSampleModel{Id="gold",X=.7,Y=.8}]}]};
        var model=new FaceDocumentModel{Artwork=new FaceArtworkModel{Source=new FaceArtworkSourceModel(),ProcessingPipeline=new ImageProcessingPipelineModel{Operations=[calibration]}},Elements=[new FaceArtworkElement{ObjectId="art",Name="Artwork",Width=100,Height=200}]};
        var document=new DocumentTabViewModel(EditorDocument.CreateFaceStub("Face"),faceDocumentJson:FaceDocumentStorage.Serialize(model));document.SelectionState.Replace(new EditorSelectionItem(EditorSelectionDomain.FaceElement,"art"));
        var context=new ActiveDocumentContextService();context.SetActiveDocument(document);context.SetPanelSelection(document.DocumentId,new PanelSelectionInfo("art","artwork",0,0,100,200));
        var inspector=new InspectorViewModel(()=>null,()=>document,()=>null,context,Execute,(d,s)=>d);inspector.NotifyContextChanged();
        var choice=Assert.IsType<InspectorChoicePropertyViewModel>(inspector.InspectorPropertyRows.Single(row=>row.DisplayName=="Source Type"));
        Assert.Contains("Registered Image",choice.Choices);choice.Value="Registered Image";
        Assert.Equal(FaceArtworkSourceKind.RegisteredImage,document.GetFaceDocument().Artwork!.Source.Kind);
        Assert.Equal(0,document.GetFaceDocument().Artwork.Source.RegistrationQuad.TopLeft.X);Assert.Equal(1,document.GetFaceDocument().Artwork.Source.RegistrationQuad.BottomRight.Y);
        var retained=Assert.IsType<ArtworkCalibrationOperationModel>(Assert.Single(document.GetFaceDocument().Artwork.ProcessingPipeline.Operations));
        Assert.Equal("black",Assert.Single(retained.BlackReference.Samples).Id);Assert.Equal("group",Assert.Single(retained.SameColorGroups).Id);
        inspector.NotifyContextChanged();Assert.Contains(inspector.InspectorPropertyRows,row=>row.DisplayName=="Choose Image...");Assert.Contains(inspector.InspectorPropertyRows,row=>row.DisplayName=="Edit / Finish Registration");Assert.Contains(inspector.InspectorPropertyRows,row=>row.DisplayName=="Apply Registration");
        static bool Execute(Guid _,EditorCommands.ICommand command){command.Execute();return command is not EditorCommands.IExecutionTrackedCommand tracked||tracked.WasExecuted;}
    }

    [Fact]
    public void Apply_registration_updates_texture_dimensions_without_changing_face_geometry_or_calibration()
    {
        var root=Path.Combine(Path.GetTempPath(),$"oasis-apply-{Guid.NewGuid():N}");var sourcePath=Path.Combine(root,"Assets","FaceSources","photo.png");Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);using(var image=CreateCornerBitmap(120,180))Save(image,sourcePath);
        try
        {
            var sample=new CalibrationSampleModel{Id="sample",X=.25,Y=.75,SamplingMode=CalibrationSamplingMode.Area,RadiusNormalized=.03};var calibration=new ArtworkCalibrationOperationModel{Id="calibration",BlackReference=new CalibrationReferenceModel{Samples=[sample]}};
            var element=new FaceArtworkElement{ObjectId="art",Name="Artwork",X=11,Y=22,Width=300,Height=450,AssetPath="Generated/Faces/Test/Artwork/artwork.png"};
            var model=new FaceDocumentModel{SourceRegion=new FaceSourceRegionModel{X=0,Y=0,Width=2,Height=3},Artwork=new FaceArtworkModel{GeneratedAssetPath=element.AssetPath,Source=new FaceArtworkSourceModel{Kind=FaceArtworkSourceKind.RegisteredImage,AssetPath="Assets/FaceSources/photo.png"},ProcessingPipeline=new ImageProcessingPipelineModel{Operations=[calibration]}},Elements=[element]};
            var document=new DocumentTabViewModel(EditorDocument.CreateFaceStub("Face"),faceDocumentJson:FaceDocumentStorage.Serialize(model));document.SetProjectAccessor(()=>new EditorProject{Name="Test",ProjectFilePath=Path.Combine(root,"test.oasis"),ProjectDirectory=root,AssetsDirectory=Path.Combine(root,"Assets"),MachinesDirectory=Path.Combine(root,"Machines"),GeneratedDirectory=Path.Combine(root,"Generated")});
            Assert.True(document.TryApplyArtworkRegistration(out var error),error);var updated=document.GetFaceDocument();
            Assert.Equal((11d,22d,300d,450d),(updated.Elements[0].X,updated.Elements[0].Y,updated.Elements[0].Width,updated.Elements[0].Height));Assert.True(updated.Artwork!.OutputWidth>100);Assert.True(updated.Artwork.OutputHeight>150);
            var retained=Assert.IsType<ArtworkCalibrationOperationModel>(Assert.Single(updated.Artwork.ProcessingPipeline.Operations));Assert.Equal("sample",Assert.Single(retained.BlackReference.Samples).Id);
            Assert.True(File.Exists(Path.Combine(root,"Generated","Faces","Test","Artwork","original.png")));Assert.True(File.Exists(Path.Combine(root,"Generated","Faces","Test","Artwork","artwork.png")));
        }
        finally{Directory.Delete(root,true);}
    }

    private static FacePointModel P(double x, double y) => new() { X = x, Y = y };
    private static SKBitmap CreateCornerBitmap(int width, int height)
    {
        var bitmap = new SKBitmap(width, height); bitmap.Erase(SKColors.White);
        bitmap.SetPixel(0, 0, SKColors.Red); bitmap.SetPixel(width - 1, 0, SKColors.Green);
        bitmap.SetPixel(width - 1, height - 1, SKColors.Blue); bitmap.SetPixel(0, height - 1, SKColors.Yellow);
        bitmap.SetPixel(width / 2, height / 2, SKColors.Magenta); return bitmap;
    }
    private static void Save(SKBitmap bitmap, string path)
    { using var image = SKImage.FromBitmap(bitmap); using var data = image.Encode(SKEncodedImageFormat.Png, 100); using var stream = File.Create(path); data.SaveTo(stream); }
}
