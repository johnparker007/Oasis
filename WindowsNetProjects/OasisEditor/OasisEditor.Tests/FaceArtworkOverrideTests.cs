using OasisEditor;
using SkiaSharp;
using Xunit;

namespace OasisEditor.Tests;

public sealed class FaceArtworkOverrideTests : IDisposable
{
    private readonly string _root=Path.Combine(Path.GetTempPath(),$"oasis-override-{Guid.NewGuid():N}");

    [Fact]
    public void Serialization_RoundTripsOverrideRecipe()
    {
        var expected=new FaceArtworkOverrideModel{Enabled=false,AssetPath="Assets/Faces/Glass/ArtworkOverride/override.png",PixelWidth=4000,PixelHeight=6000,X=-.01,Y=.005,Width=1.025,Height=.995,ContentRevision=7};
        Assert.True(FaceDocumentStorage.TryRead(FaceDocumentStorage.Serialize(new FaceDocumentModel{Title="Glass",Artwork=new FaceArtworkModel{Override=expected}}),out var file));
        var model=FaceDocumentStorage.ToModel(file);
        var actual=Assert.IsType<FaceArtworkOverrideModel>(model.Artwork!.Override);
        Assert.Equal(expected.Enabled,actual.Enabled);Assert.Equal(expected.AssetPath,actual.AssetPath);Assert.Equal(expected.PixelWidth,actual.PixelWidth);Assert.Equal(expected.PixelHeight,actual.PixelHeight);
        Assert.Equal(expected.X,actual.X);Assert.Equal(expected.Y,actual.Y);Assert.Equal(expected.Width,actual.Width);Assert.Equal(expected.Height,actual.Height);Assert.Equal(7,actual.ContentRevision);
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
    public void AssetService_CreateFromBaseAndReloadAfterUpscale_PreservesAlignment()
    {
        var project=Project();var basePath=Path.Combine(project.GeneratedDirectory,"base.png");Directory.CreateDirectory(project.GeneratedDirectory);Write(basePath,2,3,(_,_)=>SKColors.Green);
        var recipe=new FaceArtworkModel{BaseAssetPath=Path.GetRelativePath(_root,basePath)};var created=FaceArtworkOverrideAssetService.CreateFromBase(recipe,project,"Top Glass");
        Assert.StartsWith("Assets/Faces/Top Glass/ArtworkOverride/",created.AssetPath);Assert.Equal((0d,0d,1d,1d),(created.X,created.Y,created.Width,created.Height));
        var authored=Path.Combine(_root,created.AssetPath.Replace('/',Path.DirectorySeparatorChar));Write(authored,8,12,(_,_)=>SKColors.Green);
        var reloaded=FaceArtworkOverrideAssetService.Reload(new FaceArtworkOverrideModel{Enabled=true,AssetPath=created.AssetPath,PixelWidth=2,PixelHeight=3,X=-.1,Y=.2,Width=1.1,Height=.9},project);
        Assert.Equal((8,12),(reloaded.PixelWidth,reloaded.PixelHeight));Assert.Equal((-.1,.2,1.1,.9),(reloaded.X,reloaded.Y,reloaded.Width,reloaded.Height));Assert.True(reloaded.ContentRevision>0);
    }

    private EditorProject Project(){var assets=Path.Combine(_root,"Assets");var generated=Path.Combine(_root,"Generated");Directory.CreateDirectory(assets);return new EditorProject{Name="Test",ProjectDirectory=_root,ProjectFilePath=Path.Combine(_root,"test.oasisproj"),AssetsDirectory=assets,GeneratedDirectory=generated,MachinesDirectory=Path.Combine(_root,"Machines")};}
    private static void Write(string path,int width,int height,Func<int,int,SKColor> pixel){Directory.CreateDirectory(Path.GetDirectoryName(path)!);using var bitmap=new SKBitmap(width,height);for(var y=0;y<height;y++)for(var x=0;x<width;x++)bitmap.SetPixel(x,y,pixel(x,y));using var image=SKImage.FromBitmap(bitmap);using var data=image.Encode(SKEncodedImageFormat.Png,100);using var stream=File.Create(path);data.SaveTo(stream);}
    public void Dispose(){if(Directory.Exists(_root))Directory.Delete(_root,true);}
}
