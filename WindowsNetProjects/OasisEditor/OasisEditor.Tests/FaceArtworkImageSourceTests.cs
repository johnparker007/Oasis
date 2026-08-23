using OasisEditor;
using Xunit;

namespace OasisEditor.Tests;

public sealed class FaceArtworkImageSourceTests
{
    [Fact]
    public void ImageSourceAndSemanticRegistrationRoundTrip()
    {
        var face=new FaceDocumentModel { Title="Upper Glass", Artwork=new FaceArtworkModel {
            Source=new FaceArtworkSourceModel { Kind=FaceArtworkSourceKind.Image, AssetPath="Assets/Faces/Upper Glass/ArtworkSource/photo.jpg", PixelWidth=4032, PixelHeight=3024 },
            Geometry=new FaceArtworkGeometryModel { PerspectiveRegistration=new FacePerspectiveRegistrationModel {
                TopLeft=P(.1,.2),TopRight=P(.9,.15),BottomRight=P(.85,.95),BottomLeft=P(.05,.8) } } } };

        Assert.True(FaceDocumentStorage.TryRead(FaceDocumentStorage.Serialize(face), out var file));
        var model=FaceDocumentStorage.ToModel(file);

        Assert.Equal(FaceArtworkSourceKind.Image,model.Artwork!.Source.Kind);
        Assert.Equal("Assets/Faces/Upper Glass/ArtworkSource/photo.jpg",model.Artwork.Source.AssetPath);
        Assert.Equal(4032,model.Artwork.Source.PixelWidth);
        AssertPoint(model.Artwork.Geometry.PerspectiveRegistration.TopLeft,.1,.2);
        AssertPoint(model.Artwork.Geometry.PerspectiveRegistration.TopRight,.9,.15);
        AssertPoint(model.Artwork.Geometry.PerspectiveRegistration.BottomRight,.85,.95);
        AssertPoint(model.Artwork.Geometry.PerspectiveRegistration.BottomLeft,.05,.8);
    }

    [Fact]
    public void DefaultRegistrationCoversFullImageInSemanticOrder()
    {
        var r=new FaceArtworkGeometryModel().PerspectiveRegistration;
        AssertPoint(r.TopLeft,0,0); AssertPoint(r.TopRight,1,0); AssertPoint(r.BottomRight,1,1); AssertPoint(r.BottomLeft,0,1);
        Assert.True(r.IsValid());
    }

    [Fact]
    public void NormalizeClampsWithoutReorderingSemanticCorners()
    {
        var r=new FacePerspectiveRegistrationModel { TopLeft=P(-2,.2),TopRight=P(2,.1),BottomRight=P(.8,3),BottomLeft=P(.1,-4) }.Normalize();
        AssertPoint(r.TopLeft,0,.2); AssertPoint(r.TopRight,1,.1); AssertPoint(r.BottomRight,.8,1); AssertPoint(r.BottomLeft,.1,0);
    }

    [Fact]
    public void OutputEstimateRetainsUsefulRegisteredSourceResolutionWithoutUpscaling()
    {
        var size=FaceSourceShapeTransformService.EstimateRegisteredImageOutputSize(4000,3000,new FacePerspectiveRegistrationModel {
            TopLeft=P(.1,.1),TopRight=P(.9,.1),BottomRight=P(.85,.9),BottomLeft=P(.15,.9) });
        Assert.InRange(size.Width,3199,3201); Assert.InRange(size.Height,2399,2401);
        Assert.True(size.Width<=4000);Assert.True(size.Height<=3000);
    }

    [Fact]
    public void InvalidBowTieIsRejectedWithoutSortingCorners()
    {
        Assert.False(new FacePerspectiveRegistrationModel { TopLeft=P(0,0),TopRight=P(1,1),BottomRight=P(1,0),BottomLeft=P(0,1) }.IsValid());
    }

    private static NormalizedFacePointModel P(double x,double y)=>new(){X=x,Y=y};
    private static void AssertPoint(NormalizedFacePointModel p,double x,double y){Assert.Equal(x,p.X,6);Assert.Equal(y,p.Y,6);}
}
