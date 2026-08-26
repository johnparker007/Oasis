using OasisEditor;
using SkiaSharp;
using System.Windows.Media;
using Xunit;

namespace OasisEditor.Tests;

public sealed class FaceArtworkOverrideTests : IDisposable
{
    private readonly string _root=Path.Combine(Path.GetTempPath(),$"oasis-override-{Guid.NewGuid():N}");

    [Fact]
    public void Serialization_RoundTripsOverrideRecipe()
    {
        var expected=new FaceArtworkOverrideModel{Enabled=false,AssetPath="Assets/Faces/Glass/ArtworkOverride/override.png",PixelWidth=4000,PixelHeight=6000,
            PerspectiveRegistration=new FacePerspectiveRegistrationModel{TopLeft=P(.1,.2),TopRight=P(.9,.1),BottomRight=P(.8,.9),BottomLeft=P(.2,.8)},X=-.01,Y=.005,Width=1.025,Height=.995,ContentRevision=7};
        Assert.True(FaceDocumentStorage.TryRead(FaceDocumentStorage.Serialize(new FaceDocumentModel{Title="Glass",Artwork=new FaceArtworkModel{Override=expected}}),out var file));
        var model=FaceDocumentStorage.ToModel(file);
        var actual=Assert.IsType<FaceArtworkOverrideModel>(model.Artwork!.Override);
        Assert.Equal(expected.Enabled,actual.Enabled);Assert.Equal(expected.AssetPath,actual.AssetPath);Assert.Equal(expected.PixelWidth,actual.PixelWidth);Assert.Equal(expected.PixelHeight,actual.PixelHeight);
        Assert.Equal(expected.X,actual.X);Assert.Equal(expected.Y,actual.Y);Assert.Equal(expected.Width,actual.Width);Assert.Equal(expected.Height,actual.Height);Assert.Equal(7,actual.ContentRevision);
        Assert.Equal((.1,.2),(actual.PerspectiveRegistration.TopLeft.X,actual.PerspectiveRegistration.TopLeft.Y));
        Assert.Equal((.9,.1),(actual.PerspectiveRegistration.TopRight.X,actual.PerspectiveRegistration.TopRight.Y));
        Assert.Equal((.8,.9),(actual.PerspectiveRegistration.BottomRight.X,actual.PerspectiveRegistration.BottomRight.Y));
        Assert.Equal((.2,.8),(actual.PerspectiveRegistration.BottomLeft.X,actual.PerspectiveRegistration.BottomLeft.Y));
        Assert.Equal(21,FaceDocumentStorage.CurrentSchemaVersion);
    }

    [Fact]
    public void Override_DefaultsToFullImageAndRejectsCrossedRegistration()
    {
        var value=new FaceArtworkOverrideModel{AssetPath="override.png",PixelWidth=10,PixelHeight=10};
        Assert.Equal((0d,0d),(value.PerspectiveRegistration.TopLeft.X,value.PerspectiveRegistration.TopLeft.Y));
        Assert.True(value.IsValid());
        var invalid=new FaceArtworkOverrideModel{AssetPath="override.png",PixelWidth=10,PixelHeight=10,
            PerspectiveRegistration=new FacePerspectiveRegistrationModel{TopLeft=P(0,0),TopRight=P(1,1),BottomRight=P(1,0),BottomLeft=P(0,1)}};
        Assert.False(invalid.IsValid());
    }

    [Fact]
    public void Storage_RejectsPreviousSchema()
    {
        var json=FaceDocumentStorage.Serialize(new FaceDocumentModel()).Replace("\"SchemaVersion\": 21","\"SchemaVersion\": 20");
        Assert.False(FaceDocumentStorage.TryRead(json,out _));
    }

    [Fact]
    public void OverrideInvalidation_LeavesBaseCurrentAndStalesOutputAndRuntime()
    {
        var state=FaceBuildStateFactory.CreateGeneratedState(true,true,true,true,true);
        new FaceBuildService().Invalidate(state,FaceBuildInput.ArtworkOverride);
        Assert.Equal(FaceBuildStatus.Current,state.Get(FaceGeneratedProduct.ArtworkCorrectionInput).Status);
        Assert.Equal(FaceBuildStatus.Current,state.Get(FaceGeneratedProduct.BaseArtwork).Status);
        Assert.Equal(FaceBuildStatus.Stale,state.Get(FaceGeneratedProduct.ArtworkOutput).Status);
        Assert.Equal(FaceBuildStatus.Stale,state.Get(FaceGeneratedProduct.RuntimeAssets).Status);
        Assert.Equal(FaceBuildStatus.Current,state.Get(FaceGeneratedProduct.LampMask).Status);
        Assert.Equal(FaceBuildStatus.Current,state.Get(FaceGeneratedProduct.Trays).Status);
    }

    [Fact]
    public void FinalizeOutput_AlphaCompositesAlignedOverrideAndPreservesUsefulResolution()
    {
        Directory.CreateDirectory(_root);var basePath=Path.Combine(_root,"base.png");var overridePath=Path.Combine(_root,"repair.png");var outputPath=Path.Combine(_root,"artwork.png");
        Write(basePath,2,2,(x,y)=>SKColors.Blue);Write(overridePath,8,8,(x,y)=>x<4&&y<4?SKColors.Red:SKColors.Transparent);
        var recipe=new FaceArtworkModel{BaseAssetPath=basePath,OutputAssetPath=outputPath,OutputWidth=2,OutputHeight=2,Override=new FaceArtworkOverrideModel{AssetPath=overridePath,PixelWidth=8,PixelHeight=8,Width=1,Height=1}};
        Assert.True(new FaceArtworkRebuildService().FinalizeOutput(recipe,_root).Succeeded);
        using var output=SKBitmap.Decode(outputPath);Assert.Equal(8,output.Width);Assert.Equal(8,output.Height);Assert.Equal(SKColors.Red,output.GetPixel(1,1));Assert.Equal(SKColors.Blue,output.GetPixel(7,7));
        using var unchanged=SKBitmap.Decode(basePath);Assert.Equal(2,unchanged.Width);Assert.Equal(2,unchanged.Height);
    }

    [Fact]
    public void FinalizeOutput_RectifiesSelectedPhotographRegionBeforeCompositing()
    {
        Directory.CreateDirectory(_root);var basePath=Path.Combine(_root,"base.png");var sourcePath=Path.Combine(_root,"photo.png");var outputPath=Path.Combine(_root,"artwork.png");
        Write(basePath,2,2,(_,_)=>SKColors.Black);
        Write(sourcePath,12,10,(x,y)=>x>=2&&x<=9&&y>=2&&y<=7?SKColors.Red:SKColors.Lime);
        var registration=new FacePerspectiveRegistrationModel{TopLeft=P(2d/12,2d/10),TopRight=P(10d/12,2d/10),BottomRight=P(10d/12,8d/10),BottomLeft=P(2d/12,8d/10)};
        var recipe=new FaceArtworkModel{BaseAssetPath=basePath,OutputAssetPath=outputPath,Override=new FaceArtworkOverrideModel{AssetPath=sourcePath,PixelWidth=12,PixelHeight=10,PerspectiveRegistration=registration}};
        Assert.True(new FaceArtworkRebuildService().FinalizeOutput(recipe,_root).Succeeded);
        using var output=SKBitmap.Decode(outputPath);
        Assert.Equal(8,output.Width);Assert.Equal(6,output.Height);Assert.Equal(SKColors.Red,output.GetPixel(output.Width/2,output.Height/2));
        Assert.NotEqual(SKColors.Lime,output.GetPixel(0,0));
    }

    [Fact]
    public void AssetService_CreateFromBaseAndReloadAfterUpscale_PreservesAlignment()
    {
        var project=Project();var basePath=Path.Combine(project.GeneratedDirectory,"base.png");Directory.CreateDirectory(project.GeneratedDirectory);Write(basePath,2,3,(_,_)=>SKColors.Green);
        var recipe=new FaceArtworkModel{BaseAssetPath=Path.GetRelativePath(_root,basePath)};var created=FaceArtworkOverrideAssetService.CreateFromBase(recipe,project,"Top Glass");
        Assert.StartsWith("Assets/Faces/Top Glass/ArtworkOverride/",created.AssetPath);Assert.Equal((0d,0d,1d,1d),(created.X,created.Y,created.Width,created.Height));
        var authored=Path.Combine(_root,created.AssetPath.Replace('/',Path.DirectorySeparatorChar));Write(authored,8,12,(_,_)=>SKColors.Green);
        var retained=new FacePerspectiveRegistrationModel{TopLeft=P(.1,.1),TopRight=P(.9,.1),BottomRight=P(.9,.9),BottomLeft=P(.1,.9)};
        var reloaded=FaceArtworkOverrideAssetService.Reload(new FaceArtworkOverrideModel{Enabled=true,AssetPath=created.AssetPath,PixelWidth=2,PixelHeight=3,PerspectiveRegistration=retained,X=-.1,Y=.2,Width=1.1,Height=.9,ContentRevision=8},project);
        Assert.Equal((8,12),(reloaded.PixelWidth,reloaded.PixelHeight));Assert.Equal((-.1,.2,1.1,.9),(reloaded.X,reloaded.Y,reloaded.Width,reloaded.Height));Assert.Equal(9,reloaded.ContentRevision);Assert.Same(retained,reloaded.PerspectiveRegistration);
        var state=FaceBuildStateFactory.CreateGeneratedState(true,true,true,true,true);new FaceBuildService().Invalidate(state,FaceBuildInput.ArtworkOverride);
        Assert.Equal(FaceBuildStatus.Stale,state.Get(FaceGeneratedProduct.ArtworkOutput).Status);
    }

    [Fact]
    public void PreviewLoader_ReadsCurrentPixelsWhenSamePathIsOverwritten()
    {
        var path=Path.Combine(_root,"mutable.png");Write(path,2,2,(_,_)=>SKColors.Red);
        var first=Assert.IsType<System.Windows.Media.Imaging.BitmapImage>(ReloadableBitmapImageLoader.Load(path));
        Assert.Equal((byte)255,Red(first));
        Write(path,2,2,(_,_)=>SKColors.Blue);
        var second=Assert.IsType<System.Windows.Media.Imaging.BitmapImage>(ReloadableBitmapImageLoader.Load(path));
        Assert.NotSame(first,second);Assert.Equal((byte)0,Red(second));
    }

    [Fact]
    public void OutputBuilder_ReadsCurrentOverrideBytesAfterSamePathReload()
    {
        var project=Project();var basePath=Path.Combine(project.GeneratedDirectory,"Artwork","base.png");Directory.CreateDirectory(Path.GetDirectoryName(basePath)!);Write(basePath,2,2,(_,_)=>SKColors.Green);
        var artwork=new FaceArtworkModel{BaseAssetPath=Path.GetRelativePath(_root,basePath),OutputAssetPath="Generated/Artwork/artwork.png"};
        var created=FaceArtworkOverrideAssetService.CreateFromBase(artwork,project,"Glass");var overridePath=Path.Combine(_root,created.AssetPath.Replace('/',Path.DirectorySeparatorChar));
        Write(overridePath,2,2,(_,_)=>SKColors.Red);var reloaded=FaceArtworkOverrideAssetService.Reload(created,project);
        var result=new FaceArtworkRebuildService().FinalizeOutput(FaceDocumentCopy.WithOverride(artwork,reloaded),_root);Assert.True(result.Succeeded);
        using var output=SKBitmap.Decode(Path.Combine(_root,"Generated","Artwork","artwork.png"));Assert.Equal(SKColors.Red,output.GetPixel(0,0));
    }

    [Fact]
    public void PreviewLoader_ReadsRebuiltBaseAtStablePath()
    {
        var path=Path.Combine(_root,"base.png");Write(path,1,1,(_,_)=>SKColors.Red);var first=ReloadableBitmapImageLoader.Load(path)!;Assert.Equal((byte)255,Red(first));
        Write(path,1,1,(_,_)=>SKColors.Blue);var rebuilt=ReloadableBitmapImageLoader.Load(path)!;Assert.Equal((byte)0,Red(rebuilt));
    }

    private EditorProject Project(){var assets=Path.Combine(_root,"Assets");var generated=Path.Combine(_root,"Generated");Directory.CreateDirectory(assets);return new EditorProject{Name="Test",ProjectDirectory=_root,ProjectFilePath=Path.Combine(_root,"test.oasisproj"),AssetsDirectory=assets,GeneratedDirectory=generated,MachinesDirectory=Path.Combine(_root,"Machines")};}
    private static void Write(string path,int width,int height,Func<int,int,SKColor> pixel){Directory.CreateDirectory(Path.GetDirectoryName(path)!);using var bitmap=new SKBitmap(width,height);for(var y=0;y<height;y++)for(var x=0;x<width;x++)bitmap.SetPixel(x,y,pixel(x,y));using var image=SKImage.FromBitmap(bitmap);using var data=image.Encode(SKEncodedImageFormat.Png,100);using var stream=File.Create(path);data.SaveTo(stream);}
    private static byte Red(System.Windows.Media.Imaging.BitmapSource source){var converted=new System.Windows.Media.Imaging.FormatConvertedBitmap(source,PixelFormats.Bgra32,null,0);var pixels=new byte[4];converted.CopyPixels(new System.Windows.Int32Rect(0,0,1,1),pixels,4,0);return pixels[2];}
    private static NormalizedFacePointModel P(double x,double y)=>new(){X=x,Y=y};
    public void Dispose(){if(Directory.Exists(_root))Directory.Delete(_root,true);}
}
