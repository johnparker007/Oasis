using SkiaSharp;

namespace OasisEditor.Tests;

/// <summary>Evaluates authored operations in order. Calibration samples always observe the operation input.</summary>
internal sealed class LegacyFaceArtworkProcessingPipeline
{
    private const double Epsilon = 1e-6, MinimumReferenceRange = 1d / 255d;
    public SKBitmap Evaluate(SKBitmap input, ImageProcessingPipelineModel pipeline, int? operationCount = null)
    {
        var result = input.Copy();
        foreach (var operation in pipeline.Operations.Take(Math.Clamp(operationCount ?? pipeline.Operations.Count, 0, pipeline.Operations.Count)))
        {
            if (!operation.Enabled) continue;
            var next = operation switch { ArtworkCalibrationOperationModel calibration => ApplyCalibration(result, calibration.Normalize()), _ => throw new InvalidOperationException($"Unsupported image processing operation '{operation.GetType().Name}'.") };
            result.Dispose(); result = next;
        }
        return result;
    }

    private static SKBitmap ApplyCalibration(SKBitmap input, ArtworkCalibrationOperationModel operation)
    {
        if (operation.Strength <= 0) return input.Copy();
        using var spatial = ApplySpatialCorrection(input, operation);
        using var balanced = operation.NeutralizeWhite ? ApplyWhiteNeutralization(spatial, operation.WhiteReference) : spatial.Copy();
        using var calibrated = operation.NormalizeBlackWhite ? ApplyTonalNormalization(balanced, operation) : balanced.Copy();
        return Blend(input, calibrated, operation.Strength / 100d);
    }

    internal static SKBitmap ApplySpatialCorrection(SKBitmap input, ArtworkCalibrationOperationModel operation)
    {
        if (!operation.CorrectSpatialBrightness && !operation.CorrectSpatialColor) return input.Copy();
        var groups = operation.SameColorGroups.Where(g => g.Samples.Count >= 2).Select(g => g.Samples).ToList();
        if (operation.WhiteReference.Samples.Count >= 2) groups.Add(operation.WhiteReference.Samples);
        var observations = groups.Select((samples, group) => samples.Select(s => TryMeasureSample(input, s, out var c) ? new Observation(group, s.X, s.Y, c) : (Observation?)null)).SelectMany(x => x).Where(x => x.HasValue).Select(x => x!.Value).ToArray();
        if (observations.Length < 6) return input.Copy();
        var fields = Enumerable.Range(0, 3).Select(channel => FitField(observations, groups.Count, channel)).ToArray();
        if (fields.Any(f => f is null)) return input.Copy();
        var output = NewBitmap(input);
        for (var y = 0; y < input.Height; y++) for (var x = 0; x < input.Width; x++)
        {
            var p = input.GetPixel(x, y); var linear = new[] { ToLinear(p.Red), ToLinear(p.Green), ToLinear(p.Blue) };
            var fx = input.Width <= 1 ? 0 : (double)x / (input.Width - 1);
            var fy = input.Height <= 1 ? 0 : (double)y / (input.Height - 1);
            var evaluated = fields.Select(f => EvaluateField(f!, fx, fy)).ToArray();
            if (!operation.CorrectSpatialColor) { var mean = evaluated.Average(); evaluated = [mean, mean, mean]; }
            if (!operation.CorrectSpatialBrightness) { var mean = evaluated.Average(); evaluated = evaluated.Select(v => v - mean).ToArray(); }
            output.SetPixel(x, y, new SKColor(ToByte(linear[0] * BoundedExp(-evaluated[0])), ToByte(linear[1] * BoundedExp(-evaluated[1])), ToByte(linear[2] * BoundedExp(-evaluated[2])), p.Alpha));
        }
        return output;
    }

    private static double[]? FitField(Observation[] observations, int groupCount, int channel)
    {
        var valid = observations.Where(o => o.Color[channel] > Epsilon).ToArray(); var n = groupCount + 5;
        if (valid.Length < n) return null;
        var ata = new double[n, n]; var atb = new double[n];
        foreach (var o in valid) { var row = new double[n]; row[o.Group] = 1; row[groupCount] = o.X; row[groupCount + 1] = o.Y; row[groupCount + 2] = o.X * o.X; row[groupCount + 3] = o.X * o.Y; row[groupCount + 4] = o.Y * o.Y; var b = Math.Log(o.Color[channel]); for (var i=0;i<n;i++) { atb[i]+=row[i]*b; for(var j=0;j<n;j++) ata[i,j]+=row[i]*row[j]; } }
        var solved = Solve(ata, atb); if (solved is null) return null;
        var f = solved.Skip(groupCount).ToArray();
        // Integral mean over unit square: E[x]=E[y]=1/2, E[x²]=E[y²]=1/3, E[xy]=1/4.
        var mean = .5*f[0]+.5*f[1]+f[2]/3+f[3]/4+f[4]/3;
        return [f[0], f[1], f[2], f[3], f[4], mean];
    }

    private static double[]? Solve(double[,] a, double[] b)
    {
        var n=b.Length;
        for(var k=0;k<n;k++){ var pivot=k; for(var i=k+1;i<n;i++) if(Math.Abs(a[i,k])>Math.Abs(a[pivot,k])) pivot=i; if(Math.Abs(a[pivot,k])<1e-10)return null; for(var j=k;j<n;j++) (a[k,j],a[pivot,j])=(a[pivot,j],a[k,j]); (b[k],b[pivot])=(b[pivot],b[k]); for(var i=k+1;i<n;i++){var q=a[i,k]/a[k,k]; for(var j=k;j<n;j++)a[i,j]-=q*a[k,j]; b[i]-=q*b[k];}}
        var x=new double[n]; for(var i=n-1;i>=0;i--){var v=b[i];for(var j=i+1;j<n;j++)v-=a[i,j]*x[j];x[i]=v/a[i,i];if(!double.IsFinite(x[i]))return null;} return x;
    }

    private static SKBitmap ApplyWhiteNeutralization(SKBitmap input, CalibrationReferenceModel reference)
    {
        if (!TryResolveReference(input, reference, out var white)) return input.Copy();
        var luminance=Luminance(white[0],white[1],white[2]); if(luminance<Epsilon)return input.Copy();
        var multipliers=white.Select(c=>Math.Clamp(luminance/Math.Max(c,Epsilon),.25,4)).ToArray(); return Transform(input,(c,ch)=>c*multipliers[ch]);
    }

    private static SKBitmap ApplyTonalNormalization(SKBitmap input, ArtworkCalibrationOperationModel operation)
    {
        if (!TryResolveReference(input, operation.BlackReference, out var black)||!TryResolveReference(input,operation.WhiteReference,out var white))return input.Copy();
        var lo=Luminance(black[0],black[1],black[2]);
        var hi=Luminance(white[0],white[1],white[2]);
        if(hi-lo<MinimumReferenceRange)return input.Copy();
        return Transform(input,(c,ch,all)=>{var l=Luminance(all[0],all[1],all[2]);var desired=Math.Clamp((l-lo)/(hi-lo),0,1);return l>Epsilon?c*desired/l:0;});
    }

    internal static bool TryMeasureSample(SKBitmap image, CalibrationSampleModel sample, out double[] color)
    {
        color=[]; if(image.Width==0||image.Height==0||sample.X<0||sample.X>1||sample.Y<0||sample.Y>1)return false;
        var cx=(int)Math.Round(sample.X*(image.Width-1));
        var cy=(int)Math.Round(sample.Y*(image.Height-1));
        if(sample.SamplingMode==CalibrationSamplingMode.Pixel){var p=image.GetPixel(cx,cy);if(p.Alpha==0)return false;color=[ToLinear(p.Red),ToLinear(p.Green),ToLinear(p.Blue)];return true;}
        var radius=Math.Max(0,sample.RadiusPixels(image.Width,image.Height)); var values=new[]{new List<double>(),new List<double>(),new List<double>()}; var r=(int)Math.Ceiling(radius);
        for(var y=Math.Max(0,cy-r);y<=Math.Min(image.Height-1,cy+r);y++)for(var x=Math.Max(0,cx-r);x<=Math.Min(image.Width-1,cx+r);x++){if((x-cx)*(x-cx)+(y-cy)*(y-cy)>radius*radius)continue;var p=image.GetPixel(x,y);if(p.Alpha==0)continue;values[0].Add(ToLinear(p.Red));values[1].Add(ToLinear(p.Green));values[2].Add(ToLinear(p.Blue));}
        if(values[0].Count==0)return false;color=values.Select(v=>{v.Sort();var m=v.Count/2;return v.Count%2==1?v[m]:(v[m-1]+v[m])/2;}).ToArray();return true;
    }

    internal static string? MeasureSampleHex(SKBitmap image, CalibrationSampleModel sample)=>TryMeasureSample(image,sample,out var c)?$"#FF{ToByte(c[0]):X2}{ToByte(c[1]):X2}{ToByte(c[2]):X2}":null;
    internal static bool TryResolveReferenceColors(SKBitmap image, ArtworkCalibrationOperationModel operation,out string? black,out string? white){var br=TryResolveReference(image,operation.BlackReference,out var b);var wr=TryResolveReference(image,operation.WhiteReference,out var w);black=br?Hex(b):null;white=wr?Hex(w):null;return br&&wr;}
    private static bool TryResolveReference(SKBitmap image,CalibrationReferenceModel reference,out double[] color){if(reference.ManualEnabled)return TryParse(reference.ManualColor,out color);var values=reference.Samples.Select(s=>TryMeasureSample(image,s,out var c)?c:null).Where(c=>c is not null).ToArray();if(values.Length==0){color=[];return false;}color=Enumerable.Range(0,3).Select(ch=>values.Average(c=>c![ch])).ToArray();return true;}
    private static bool TryParse(string text,out double[] c){var v=text.Trim().TrimStart('#');if(v.Length==8)v=v[2..];if(v.Length!=6||!uint.TryParse(v,System.Globalization.NumberStyles.HexNumber,null,out var rgb)){c=[];return false;}c=[ToLinear((byte)(rgb>>16)),ToLinear((byte)(rgb>>8)),ToLinear((byte)rgb)];return true;}
    private static SKBitmap Blend(SKBitmap a,SKBitmap b,double amount)=>Transform(a,(c,ch,all,x,y)=>c+(new[]{ToLinear(b.GetPixel(x,y).Red),ToLinear(b.GetPixel(x,y).Green),ToLinear(b.GetPixel(x,y).Blue)}[ch]-c)*amount);
    private static SKBitmap Transform(SKBitmap input,Func<double,int,double> f)=>Transform(input,(c,ch,all,x,y)=>f(c,ch));
    private static SKBitmap Transform(SKBitmap input,Func<double,int,double[],double> f)=>Transform(input,(c,ch,all,x,y)=>f(c,ch,all));
    private static SKBitmap Transform(SKBitmap input,Func<double,int,double[],int,int,double> f){var o=NewBitmap(input);for(var y=0;y<input.Height;y++)for(var x=0;x<input.Width;x++){var p=input.GetPixel(x,y);var c=new[]{ToLinear(p.Red),ToLinear(p.Green),ToLinear(p.Blue)};o.SetPixel(x,y,new SKColor(ToByte(f(c[0],0,c,x,y)),ToByte(f(c[1],1,c,x,y)),ToByte(f(c[2],2,c,x,y)),p.Alpha));}return o;}
    private static SKBitmap NewBitmap(SKBitmap i)=>new(i.Width,i.Height,SKColorType.Rgba8888,SKAlphaType.Premul);
    private static double EvaluateField(double[] f,double x,double y)=>f[0]*x+f[1]*y+f[2]*x*x+f[3]*x*y+f[4]*y*y-f[5];
    private static double BoundedExp(double x)=>Math.Exp(Math.Clamp(x,Math.Log(.25),Math.Log(4)));
    private static double ToLinear(byte v){var x=v/255d;return x<=.04045?x/12.92:Math.Pow((x+.055)/1.055,2.4);}
    private static byte ToByte(double v){v=Math.Clamp(v,0,1);var s=v<=.0031308?v*12.92:1.055*Math.Pow(v,1/2.4)-.055;return(byte)Math.Round(Math.Clamp(s,0,1)*255);}
    private static double Luminance(double r,double g,double b)=>.2126*r+.7152*g+.0722*b;
    private static string Hex(double[] c)=>$"#FF{ToByte(c[0]):X2}{ToByte(c[1]):X2}{ToByte(c[2]):X2}";
    private readonly record struct Observation(int Group,double X,double Y,double[] Color);
}
