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
    [Fact] public void Calibration_MultipleWorkersExactlyMatchesOneWorker()
    {
        using var b=Bitmap(47,35,new SKColor(100,40,15,143));
        var pipeline=new ImageProcessingPipelineModel{Operations=[new ArtworkCalibrationOperationModel{CorrectSpatialBrightness=false,CorrectSpatialColor=false,NeutralizeWhite=true,WhiteReference=new(){ManualEnabled=true,ManualColor="#FFB09070"},BlackReference=new(){ManualEnabled=true,ManualColor="#FF101010"}}]};
        using var serial=new FaceArtworkProcessingPipeline().Evaluate(b,pipeline,executionOptions:new ImageProcessingExecutionOptions(1));
        using var parallel=new FaceArtworkProcessingPipeline().Evaluate(b,pipeline,executionOptions:new ImageProcessingExecutionOptions(4));
        for(var y=0;y<b.Height;y++)for(var x=0;x<b.Width;x++)Assert.Equal(serial.GetPixel(x,y),parallel.GetPixel(x,y));
    }

    [Fact]
    public void FinalizeOutput_CopiesBaseExactlyAndDoesNotReprocessIt()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"oasis-face-finalize-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var basePath = Path.Combine(directory, "base.png");
            var outputPath = Path.Combine(directory, "artwork.png");
            using (var bitmap = Bitmap(4, 4, new SKColor(80, 30, 10)))
            using (var image = SKImage.FromBitmap(bitmap))
            using (var data = image.Encode(SKEncodedImageFormat.Png, 100))
            using (var stream = File.Create(basePath)) data.SaveTo(stream);
            var artwork = new FaceArtworkModel { BaseAssetPath = basePath, OutputAssetPath = outputPath };

            var result = new FaceArtworkRebuildService().FinalizeOutput(artwork, directory);

            Assert.True(result.Succeeded, result.ErrorMessage);
            Assert.Equal(File.ReadAllBytes(basePath), File.ReadAllBytes(outputPath));
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public void CalibrationSampling_ReusesGeneratedCorrectionInputWithoutSourceReconstruction()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"oasis-correction-input-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var inputPath = Path.Combine(directory, "correction-input.png");
            WriteBitmap(inputPath, new SKColor(40, 80, 120));
            var sample = new CalibrationSampleModel { X = .5, Y = .5 };
            var operation = new ArtworkCalibrationOperationModel
            {
                Id = "calibration", CorrectSpatialBrightness = false, CorrectSpatialColor = false,
                NeutralizeWhite = false, BlackReference = new() { ManualEnabled = true, ManualColor = "#FF202020", Samples = [sample] },
                WhiteReference = new() { ManualEnabled = true, ManualColor = "#FF808080" }
            };
            var state = FaceBuildStateFactory.CreateGeneratedState(true, false, false, false, false);
            var face = new FaceDocumentModel
            {
                BuildState = state,
                Artwork = new FaceArtworkModel
                {
                    CorrectionInputAssetPath = inputPath,
                    BaseAssetPath = Path.Combine(directory, "base.png"),
                    OutputAssetPath = Path.Combine(directory, "artwork.png"),
                    ProcessingPipeline = new() { Operations = [operation] }
                }
            };
            using var document = new DocumentTabViewModel(EditorDocument.CreateFaceStub("Face"),
                faceDocumentJson: FaceDocumentStorage.Serialize(face));
            document.SetProjectAccessor(() => new EditorProject
            {
                Name = "Test", ProjectDirectory = directory, ProjectFilePath = Path.Combine(directory, "test.oasisproj"),
                AssetsDirectory = Path.Combine(directory, "Assets"), MachinesDirectory = Path.Combine(directory, "Machines"),
                GeneratedDirectory = Path.Combine(directory, "Generated")
            });
            var measurements = document.GetArtworkCalibrationMeasurements(operation);
            Assert.Equal("#FF285078", measurements.SampleColors[sample.Id]);
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public void BuildArtwork_BuildsStaleBaseAndOutputWithoutRebuildingCurrentCorrectionInput()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"oasis-apply-processing-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var inputPath = Path.Combine(directory, "correction-input.png");
            var basePath = Path.Combine(directory, "base.png");
            var outputPath = Path.Combine(directory, "artwork.png");
            WriteBitmap(inputPath, new SKColor(80, 40, 20));
            WriteBitmap(basePath, SKColors.Black);
            WriteBitmap(outputPath, SKColors.Black);
            var inputBytes = File.ReadAllBytes(inputPath);
            var operation = new ArtworkCalibrationOperationModel
            {
                CorrectSpatialBrightness = false, CorrectSpatialColor = false, NeutralizeWhite = false,
                BlackReference = new() { ManualEnabled = true, ManualColor = "#FF202020" },
                WhiteReference = new() { ManualEnabled = true, ManualColor = "#FF808080" }
            };
            var state = FaceBuildStateFactory.CreateGeneratedState(true, false, false, false, false);
            state.Get(FaceGeneratedProduct.BaseArtwork).Status = FaceBuildStatus.Stale;
            state.Get(FaceGeneratedProduct.ArtworkOutput).Status = FaceBuildStatus.Stale;
            using var document = CreateDocument(directory, inputPath, basePath, outputPath, operation, state);
            var result = document.BuildArtwork();

            Assert.True(result.Succeeded);
            Assert.Equal(FaceBuildStatus.Current, document.GetFaceDocument().BuildState.Get(FaceGeneratedProduct.ArtworkCorrectionInput).Status);
            Assert.Equal(FaceBuildStatus.Current, document.GetFaceDocument().BuildState.Get(FaceGeneratedProduct.BaseArtwork).Status);
            Assert.Equal(FaceBuildStatus.Current, document.GetFaceDocument().BuildState.Get(FaceGeneratedProduct.ArtworkOutput).Status);
            Assert.Equal(inputBytes, File.ReadAllBytes(inputPath));
            Assert.Equal(File.ReadAllBytes(basePath), File.ReadAllBytes(outputPath));
            Assert.NotEqual(inputBytes, File.ReadAllBytes(basePath));
            Assert.Equal(0, document.CommandService.History.Count);
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public void BuildArtwork_BaseFailureDoesNotFinalizeStaleOutput()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"oasis-apply-failure-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var inputPath = Path.Combine(directory, "correction-input.png");
            var outputPath = Path.Combine(directory, "artwork.png");
            File.WriteAllText(inputPath, "not a png");
            WriteBitmap(outputPath, SKColors.Magenta);
            var outputBytes = File.ReadAllBytes(outputPath);
            var state = FaceBuildStateFactory.CreateGeneratedState(true, false, false, false, false);
            state.Get(FaceGeneratedProduct.BaseArtwork).Status = FaceBuildStatus.Stale;
            state.Get(FaceGeneratedProduct.ArtworkOutput).Status = FaceBuildStatus.Stale;
            using var document = CreateDocument(directory, inputPath, Path.Combine(directory, "base.png"), outputPath,
                new ArtworkCalibrationOperationModel(), state);
            var result = document.BuildArtwork();

            Assert.False(result.Succeeded);
            Assert.Equal(FaceBuildStatus.Error, document.GetFaceDocument().BuildState.Get(FaceGeneratedProduct.BaseArtwork).Status);
            Assert.Equal(FaceBuildStatus.Stale, document.GetFaceDocument().BuildState.Get(FaceGeneratedProduct.ArtworkOutput).Status);
            Assert.Equal(outputBytes, File.ReadAllBytes(outputPath));
        }
        finally { Directory.Delete(directory, true); }
    }

    private static DocumentTabViewModel CreateDocument(string directory, string inputPath, string basePath,
        string outputPath, ArtworkCalibrationOperationModel operation, FaceBuildStateModel state)
    {
        var face = new FaceDocumentModel
        {
            BuildState = state,
            Artwork = new FaceArtworkModel
            {
                CorrectionInputAssetPath = inputPath, BaseAssetPath = basePath, OutputAssetPath = outputPath,
                ProcessingPipeline = new() { Operations = [operation] }
            }
        };
        var document = new DocumentTabViewModel(EditorDocument.CreateFaceStub("Face"),
            faceDocumentJson: FaceDocumentStorage.Serialize(face));
        document.SetProjectAccessor(() => new EditorProject
        {
            Name = "Test", ProjectDirectory = directory, ProjectFilePath = Path.Combine(directory, "test.oasisproj"),
            AssetsDirectory = Path.Combine(directory, "Assets"), MachinesDirectory = Path.Combine(directory, "Machines"),
            GeneratedDirectory = Path.Combine(directory, "Generated")
        });
        return document;
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
