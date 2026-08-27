using SkiaSharp;
using Xunit;

namespace OasisEditor.Tests;

public sealed class FaceArtworkOptimizationCompatibilityTests
{
    [Fact]
    public void Sharpening_MatchesPreOptimizationReferenceOnTransparentGradient()
    {
        using var source=Gradient();var settings=new FaceGenerationSettingsModel{PostWarpSharpeningEnabled=true,PostWarpSharpeningAmount=.65,PostWarpSharpeningRadiusPixels=.75,PostWarpSharpeningThreshold=2};
        using var expected=LegacyFaceArtworkSharpeningService.Apply(source,settings);
        using var actual=FaceArtworkSharpeningService.Apply(source,settings);
        AssertPixelsEqual(expected,actual,0);
    }

    [Fact]
    public void Calibration_MatchesPreOptimizationReference()
    {
        using var source=Gradient();var pipeline=new ImageProcessingPipelineModel{Operations=[new ArtworkCalibrationOperationModel{CorrectSpatialBrightness=false,CorrectSpatialColor=false,BlackReference=new(){ManualEnabled=true,ManualColor="#FF101010"},WhiteReference=new(){ManualEnabled=true,ManualColor="#FFE0D8C0"}}]};
        using var expected=new LegacyFaceArtworkProcessingPipeline().Evaluate(source,pipeline);
        using var actual=new FaceArtworkProcessingPipeline().Evaluate(source,pipeline);
        AssertPixelsEqual(expected,actual,0);
    }

    [Fact]
    public void Perspective_MatchesReferenceWithinPremultipliedByteRounding()
    {
        using var source=Gradient();var quad=new FacePointModel[]{new(){X=0,Y=0},new(){X=source.Width,Y=.4},new(){X=source.Width-.5,Y=source.Height},new(){X=.3,Y=source.Height}};
        using var expected=LegacyPerspectiveRasterizer.Rectify(source,quad,source.Width,source.Height);
        using var actual=PerspectiveRasterizer.Rectify(source,quad,source.Width,source.Height);
        AssertPixelsEqual(expected,actual,1);
    }

    private static SKBitmap Gradient(){var bitmap=new SKBitmap(17,11,SKColorType.Bgra8888,SKAlphaType.Premul);for(var y=0;y<bitmap.Height;y++)for(var x=0;x<bitmap.Width;x++)bitmap.SetPixel(x,y,new SKColor((byte)(10+x*11),(byte)(20+y*17),(byte)(30+(x+y)*5),(byte)((x*13+y*7)%256)));return bitmap;}
    private static void AssertPixelsEqual(SKBitmap expected,SKBitmap actual,int tolerance){for(var y=0;y<expected.Height;y++)for(var x=0;x<expected.Width;x++){var e=expected.GetPixel(x,y);var a=actual.GetPixel(x,y);Assert.InRange(Math.Abs(e.Red-a.Red),0,tolerance);Assert.InRange(Math.Abs(e.Green-a.Green),0,tolerance);Assert.InRange(Math.Abs(e.Blue-a.Blue),0,tolerance);Assert.InRange(Math.Abs(e.Alpha-a.Alpha),0,tolerance);}}
}
