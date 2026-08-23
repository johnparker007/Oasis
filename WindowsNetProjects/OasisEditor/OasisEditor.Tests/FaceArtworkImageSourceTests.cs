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
        Assert.Equal(3200, size.Width);
        // The registered side is diagonal in source-pixel space: ceil(sqrt(200^2 + 2400^2)).
        Assert.Equal(2409, size.Height);
        Assert.True(size.Width <= 4000);
        Assert.True(size.Height <= 3000);
    }

    [Fact]
    public void InvalidBowTieIsRejectedWithoutSortingCorners()
    {
        Assert.False(new FacePerspectiveRegistrationModel { TopLeft=P(0,0),TopRight=P(1,1),BottomRight=P(1,0),BottomLeft=P(0,1) }.IsValid());
    }

    [Fact]
    public void RegistrationCommand_UndoRedoRestoresModelAndRaisesRefreshSignal()
    {
        var initial = Registration(.1, .1, .9, .1, .9, .9, .1, .9);
        var changed = Registration(.2, .15, .85, .1, .9, .85, .1, .9);
        var (document, workspace) = CreateImageWorkspace(initial);
        var notifications = new List<string?>();
        workspace.PropertyChanged += (_, args) => notifications.Add(args.PropertyName);

        workspace.CommitRegistration(changed);
        AssertRegistration(document, changed);

        notifications.Clear();
        Assert.True(document.CommandService.TryUndo());
        AssertRegistration(document, initial);
        Assert.Contains(nameof(FaceWorkspaceViewModel.ArtworkRegistration), notifications);

        notifications.Clear();
        Assert.True(document.CommandService.TryRedo());
        AssertRegistration(document, changed);
        Assert.Contains(nameof(FaceWorkspaceViewModel.ArtworkRegistration), notifications);
    }

    [Fact]
    public void ResetRegistration_UndoRestoresCustomRegistration()
    {
        var custom = Registration(.1, .2, .9, .15, .85, .9, .05, .8);
        var (document, workspace) = CreateImageWorkspace(custom);

        workspace.ResetRegistration();
        AssertRegistration(document, FacePerspectiveRegistrationModel.FullImage);

        Assert.True(document.CommandService.TryUndo());
        AssertRegistration(document, custom);
    }

    private static (DocumentTabViewModel Document, FaceWorkspaceViewModel Workspace) CreateImageWorkspace(FacePerspectiveRegistrationModel registration)
    {
        var model = new FaceDocumentModel
        {
            Title = "Upper Glass",
            Artwork = new FaceArtworkModel
            {
                Source = new FaceArtworkSourceModel { Kind = FaceArtworkSourceKind.Image, AssetPath = "Assets/source.png", PixelWidth = 1000, PixelHeight = 1000 },
                Geometry = new FaceArtworkGeometryModel { PerspectiveRegistration = registration }
            }
        };
        var document = new DocumentTabViewModel(EditorDocument.CreateFaceStub("Upper Glass"), faceDocumentJson: FaceDocumentStorage.Serialize(model));
        return (document, Assert.IsType<FaceWorkspaceViewModel>(document.FaceWorkspace));
    }

    private static FacePerspectiveRegistrationModel Registration(double tlx, double tly, double trx, double try_, double brx, double bry, double blx, double bly) => new()
    {
        TopLeft = P(tlx, tly), TopRight = P(trx, try_), BottomRight = P(brx, bry), BottomLeft = P(blx, bly)
    };

    private static void AssertRegistration(DocumentTabViewModel document, FacePerspectiveRegistrationModel expected)
    {
        var actual = document.GetFaceDocument().Artwork!.Geometry.PerspectiveRegistration;
        AssertPoint(actual.TopLeft, expected.TopLeft.X, expected.TopLeft.Y);
        AssertPoint(actual.TopRight, expected.TopRight.X, expected.TopRight.Y);
        AssertPoint(actual.BottomRight, expected.BottomRight.X, expected.BottomRight.Y);
        AssertPoint(actual.BottomLeft, expected.BottomLeft.X, expected.BottomLeft.Y);
    }

    private static NormalizedFacePointModel P(double x,double y)=>new(){X=x,Y=y};
    private static void AssertPoint(NormalizedFacePointModel p,double x,double y){Assert.Equal(x,p.X,6);Assert.Equal(y,p.Y,6);}
}
