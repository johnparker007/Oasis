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
        // Calibration mathematics is unchanged. Cached direct access can select an adjacent
        // premultiplied byte instead of reproducing Skia's extra straight-colour round trip.
        AssertPremultipliedPixelsEqual(expected,actual,1,0);
    }

    [Fact]
    public void Perspective_MatchesReferenceWithinPremultipliedByteRounding()
    {
        using var source=Gradient();var quad=new FacePointModel[]{new(){X=0,Y=0},new(){X=source.Width,Y=.4},new(){X=source.Width-.5,Y=source.Height},new(){X=.3,Y=source.Height}};
        using var expected=LegacyPerspectiveRasterizer.Rectify(source,quad,source.Width,source.Height);
        using var actual=PerspectiveRasterizer.Rectify(source,quad,source.Width,source.Height);
        // Straight RGB magnifies a one-byte stored difference at low alpha. Premultiplied bytes
        // are the rendered representation and therefore the meaningful compatibility metric.
        AssertPremultipliedPixelsEqual(expected,actual,1,1);
    }

    private static SKBitmap Gradient(){var bitmap=new SKBitmap(17,11,SKColorType.Bgra8888,SKAlphaType.Premul);for(var y=0;y<bitmap.Height;y++)for(var x=0;x<bitmap.Width;x++)bitmap.SetPixel(x,y,new SKColor((byte)(10+x*11),(byte)(20+y*17),(byte)(30+(x+y)*5),(byte)((x*13+y*7)%256)));return bitmap;}
    private static void AssertPixelsEqual(SKBitmap expected,SKBitmap actual,int tolerance){for(var y=0;y<expected.Height;y++)for(var x=0;x<expected.Width;x++){var e=expected.GetPixel(x,y);var a=actual.GetPixel(x,y);Assert.InRange(Math.Abs(e.Red-a.Red),0,tolerance);Assert.InRange(Math.Abs(e.Green-a.Green),0,tolerance);Assert.InRange(Math.Abs(e.Blue-a.Blue),0,tolerance);Assert.InRange(Math.Abs(e.Alpha-a.Alpha),0,tolerance);}}
    private static void AssertPremultipliedPixelsEqual(SKBitmap expected,SKBitmap actual,int colorTolerance,int alphaTolerance)
    {
        var expectedPixels=new BitmapPixelBuffer(expected);var actualPixels=new BitmapPixelBuffer(actual);
        for(var y=0;y<expected.Height;y++)for(var x=0;x<expected.Width;x++)
        {
            expectedPixels.ReadPremultiplied(x,y,out var er,out var eg,out var eb,out var ea);
            actualPixels.ReadPremultiplied(x,y,out var ar,out var ag,out var ab,out var aa);
            var expectedStraight=expected.GetPixel(x,y);var actualStraight=actual.GetPixel(x,y);
            var detail=$"Pixel ({x},{y}); straight expected {expectedStraight}, actual {actualStraight}; premultiplied expected ({er},{eg},{eb},{ea}), actual ({ar},{ag},{ab},{aa})";
            Assert.True(Math.Abs(er-ar)<=colorTolerance,detail);Assert.True(Math.Abs(eg-ag)<=colorTolerance,detail);
            Assert.True(Math.Abs(eb-ab)<=colorTolerance,detail);Assert.True(Math.Abs(ea-aa)<=alphaTolerance,detail);
        }
    }
}
