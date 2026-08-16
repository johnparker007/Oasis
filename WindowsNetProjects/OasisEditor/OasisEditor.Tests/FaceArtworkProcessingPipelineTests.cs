using SkiaSharp;
using OasisEditor;
using Xunit;
namespace OasisEditor.Tests;
public sealed class FaceArtworkProcessingPipelineTests
{
    [Fact] public void Calibration_RoundTripsSamplesGroupsAndOptions()
    {
        var sample=new CalibrationSampleModel{Id="sample",X=.2,Y=.7,SamplingMode=CalibrationSamplingMode.Area,RadiusNormalized=.08};
        var operation=new ArtworkCalibrationOperationModel{Id="cal",Strength=73,BlackReference=new(){Samples=[sample],ManualColor="#FF000000"},WhiteReference=new(){Samples=[new(){Id="pixel",X=.9,Y=.1}],ManualColor="#FFFFFFFF"},SameColorGroups=[new(){Id="group",Name="Grey Border",Samples=[sample]}],NeutralizeWhite=false};
        var face=new FaceDocumentModel{Artwork=new(){ProcessingPipeline=new(){Operations=[operation]}}};
        Assert.True(FaceDocumentStorage.TryRead(FaceDocumentStorage.Serialize(face),out var file));var saved=Assert.IsType<ArtworkCalibrationOperationModel>(Assert.Single(FaceDocumentStorage.ToModel(file).Artwork!.ProcessingPipeline.Operations));
        Assert.Equal("sample",saved.BlackReference.Samples[0].Id);Assert.Equal(CalibrationSamplingMode.Area,saved.BlackReference.Samples[0].SamplingMode);Assert.Equal(.08,saved.BlackReference.Samples[0].RadiusNormalized);Assert.Equal("Grey Border",Assert.Single(saved.SameColorGroups).Name);Assert.False(saved.NeutralizeWhite);
    }
    [Fact] public void RadiusPixels_UsesShortDimensionAndSurvivesResolutionChange(){var s=new CalibrationSampleModel{RadiusNormalized=.1};Assert.Equal(10,s.RadiusPixels(100,200));Assert.Equal(20,s.RadiusPixels(400,200));Assert.Equal(.1,s.WithRadiusPixels(10,100,200).RadiusNormalized);}
    [Fact] public void PixelSample_IsExactAndUnaffectedByNeighbours(){using var b=Bitmap(3,3,SKColors.Red);b.SetPixel(1,1,SKColors.Blue);var s=new CalibrationSampleModel{X=.5,Y=.5};Assert.Equal("#FF0000FF",FaceArtworkProcessingPipeline.MeasureSampleHex(b,s));}
    [Fact] public void AreaSample_IsCircularIgnoresTransparentAndRejectsOutlier(){using var b=Bitmap(5,5,new SKColor(100,100,100));b.SetPixel(2,2,SKColors.Red);b.SetPixel(0,0,SKColors.Transparent);var s=new CalibrationSampleModel{X=.5,Y=.5,SamplingMode=CalibrationSamplingMode.Area,RadiusNormalized=.4};var hex=FaceArtworkProcessingPipeline.MeasureSampleHex(b,s);Assert.Equal("#FF646464",hex);}
    [Fact] public void InsufficientSpatialData_IsSafeNoOpAndPreservesAlpha(){using var b=Bitmap(4,4,new SKColor(30,60,90,77));using var o=new FaceArtworkProcessingPipeline().Evaluate(b,new(){Operations=[new ArtworkCalibrationOperationModel{NormalizeBlackWhite=false,NeutralizeWhite=false}]});Assert.Equal(b.GetPixel(2,2),o.GetPixel(2,2));}
    [Fact] public void ManualLevels_PreserveSaturatedChromaticityAndAlpha(){using var b=Bitmap(2,1,new SKColor(100,20,10,99));var op=new ArtworkCalibrationOperationModel{CorrectSpatialBrightness=false,CorrectSpatialColor=false,NeutralizeWhite=false,BlackReference=new(){ManualEnabled=true,ManualColor="#FF101010"},WhiteReference=new(){ManualEnabled=true,ManualColor="#FF808080"}};using var o=new FaceArtworkProcessingPipeline().Evaluate(b,new(){Operations=[op]});var p=o.GetPixel(0,0);Assert.True(p.Red>p.Green);Assert.True(p.Green>=p.Blue);Assert.Equal(99,p.Alpha);}
    [Fact] public void StrengthZero_IsExactNoOp(){using var b=Bitmap(2,2,SKColors.CornflowerBlue);using var o=new FaceArtworkProcessingPipeline().Evaluate(b,new(){Operations=[new ArtworkCalibrationOperationModel{Strength=0}]});Assert.Equal(b.GetPixel(0,0),o.GetPixel(0,0));}

    [Fact]
    public void ApplyProcessing_UsesCanonicalOriginalAndLeavesItUnchanged()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var generatedPath = Path.Combine(directory, "artwork.png");
            var originalPath = FaceArtworkRebuildService.GetOriginalArtworkPath(generatedPath);
            WriteBitmap(originalPath, new SKColor(80, 30, 10));
            WriteBitmap(generatedPath, new SKColor(10, 200, 200));
            var originalBytes = File.ReadAllBytes(originalPath);
            var processedBefore = File.ReadAllBytes(generatedPath);
            var result = new FaceArtworkRebuildService().ApplyProcessing(CalibratedArtwork(generatedPath), directory);

            Assert.True(result.Succeeded, result.ErrorMessage);
            Assert.Equal(originalBytes, File.ReadAllBytes(originalPath));
            Assert.False(processedBefore.SequenceEqual(File.ReadAllBytes(generatedPath)));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public void ApplyProcessing_MissingCanonicalOriginalFailsWithoutUsingProcessedArtwork()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var generatedPath = Path.Combine(directory, "artwork.png");
            WriteBitmap(generatedPath, new SKColor(10, 20, 30));
            var before = File.ReadAllBytes(generatedPath);

            var result = new FaceArtworkRebuildService().ApplyProcessing(CalibratedArtwork(generatedPath), directory);

            Assert.False(result.Succeeded);
            Assert.Contains("Canonical original artwork was not found", result.ErrorMessage);
            Assert.Equal(before, File.ReadAllBytes(generatedPath));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public void ApplyProcessingCommand_MissingCanonicalOriginalReportsSpecificFailure()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var generatedPath = Path.Combine(directory, "artwork.png");
            WriteBitmap(generatedPath, new SKColor(10, 20, 30));
            var face = new FaceDocumentModel { Artwork = CalibratedArtwork(generatedPath) };
            var document = new DocumentTabViewModel(EditorDocument.CreateFaceStub("Face"), faceDocumentJson: FaceDocumentStorage.Serialize(face));
            document.SetProjectAccessor(() => new EditorProject { ProjectDirectory = directory });
            var command = FaceMutationCommands.CreateApplyArtworkProcessingCommand(document.DocumentId, document);

            document.CommandService.Execute(command);

            Assert.False(Assert.IsAssignableFrom<Commands.IExecutionTrackedCommand>(command).WasExecuted);
            Assert.Contains("Canonical original artwork was not found", Assert.IsAssignableFrom<Commands.IExecutionFailureDiagnostic>(command).ExecutionFailureMessage);
            Assert.Empty(document.CommandService.History);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public void ApplyProcessingCommand_AppliesAndUndoRedoOnlyReplaceProcessedArtwork()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var generatedPath = Path.Combine(directory, "artwork.png");
            var originalPath = FaceArtworkRebuildService.GetOriginalArtworkPath(generatedPath);
            WriteBitmap(originalPath, new SKColor(90, 35, 15));
            WriteBitmap(generatedPath, new SKColor(25, 25, 25));
            var originalBytes = File.ReadAllBytes(originalPath);
            var previousProcessed = File.ReadAllBytes(generatedPath);
            var face = new FaceDocumentModel { Artwork = CalibratedArtwork(generatedPath) };
            var document = new DocumentTabViewModel(EditorDocument.CreateFaceStub("Face"), faceDocumentJson: FaceDocumentStorage.Serialize(face));
            document.SetProjectAccessor(() => new EditorProject { ProjectDirectory = directory });

            document.CommandService.Execute(FaceMutationCommands.CreateApplyArtworkProcessingCommand(document.DocumentId, document));
            var applied = File.ReadAllBytes(generatedPath);
            Assert.False(previousProcessed.SequenceEqual(applied));
            Assert.True(document.CommandService.TryUndo());
            Assert.Equal(previousProcessed, File.ReadAllBytes(generatedPath));
            Assert.Equal(originalBytes, File.ReadAllBytes(originalPath));
            Assert.True(document.CommandService.TryRedo());
            Assert.Equal(applied, File.ReadAllBytes(generatedPath));
            Assert.Equal(originalBytes, File.ReadAllBytes(originalPath));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private static FaceArtworkModel CalibratedArtwork(string generatedPath) => new()
    {
        GeneratedAssetPath = generatedPath,
        ProcessingPipeline = new ImageProcessingPipelineModel
        {
            Operations = [new ArtworkCalibrationOperationModel
            {
                CorrectSpatialBrightness = false,
                CorrectSpatialColor = false,
                NeutralizeWhite = false,
                BlackReference = new CalibrationReferenceModel { ManualEnabled = true, ManualColor = "#FF202020" },
                WhiteReference = new CalibrationReferenceModel { ManualEnabled = true, ManualColor = "#FF707070" }
            }]
        }
    };

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"oasis-face-calibration-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static void WriteBitmap(string path, SKColor color)
    {
        using var bitmap = Bitmap(4, 4, color);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.Create(path);
        data.SaveTo(stream);
    }

    private static SKBitmap Bitmap(int w,int h,SKColor c){var b=new SKBitmap(w,h,SKColorType.Rgba8888,SKAlphaType.Premul);b.Erase(c);return b;}
}
