using Xunit;

namespace OasisEditor.Tests;

public sealed class FaceIlluminationAuthoringTests
{
    [Fact]
    public void NativeLamp_IsUndoableAndRoundTripsAsAuthoredIllumination()
    {
        var document=new DocumentTabViewModel(EditorDocument.CreateFaceStub("Native"));
        var lamp=FaceElementFactory.CreateLampWindow(new System.Windows.Point(12,34));
        document.CommandService.Execute(FaceMutationCommands.CreateAddLampWindowCommand(document.DocumentId,document,lamp));
        Assert.Contains(document.GetFaceElements(),element=>element.ObjectId==lamp.ObjectId);
        Assert.Equal(FaceSubsystemOrigin.Authored,document.GetFaceDocument().Provenance.Illumination.Origin);
        Assert.True(FaceDocumentStorage.TryRead(document.GetFaceDocumentJson(),out var file));
        Assert.Contains(FaceDocumentStorage.ToModel(file).Elements,element=>element is FaceLampWindowElement);
        Assert.True(document.CommandService.TryUndo());
        Assert.DoesNotContain(document.GetFaceElements(),element=>element.ObjectId==lamp.ObjectId);
        Assert.True(document.CommandService.TryRedo());
    }

    [Fact]
    public void EditingDerivedLamp_MarksOnlyIlluminationLocallyModified_AndUndoRestoresIt()
    {
        var document=new DocumentTabViewModel(EditorDocument.CreateFaceStub("Derived"));
        var face=document.GetFaceDocument();
        var provenance=new FaceSubsystemProvenanceModel{Origin=FaceSubsystemOrigin.Derived,SourceDocumentPath="main.panel2d"};
        face=FaceDocumentCopy.WithIllumination(face,[new FaceLampWindowElement{ObjectId="lamp",Name="Lamp",Width=20,Height=20}],face.MaskLayer,face.Trays,face.LampEmitters,provenance);
        document.SetFaceDocument(face);
        var moved=FaceElementModelCloner.Clone(document.GetFaceElements()[0],x:25);
        document.CommandService.Execute(FaceMutationCommands.CreateUpdateElementCommand(document.DocumentId,document,"lamp",moved,"Move lamp"));
        Assert.True(document.GetFaceDocument().Provenance.Illumination.IsLocallyModified);
        Assert.False(document.GetFaceDocument().Provenance.Artwork.IsLocallyModified);
        Assert.False(document.GetFaceDocument().Provenance.Components.IsLocallyModified);
        Assert.True(document.CommandService.TryUndo());
        Assert.False(document.GetFaceDocument().Provenance.Illumination.IsLocallyModified);
    }

    [Fact]
    public void AuthoredMaskSource_RoundTripsSeparatelyFromGeneratedPath()
    {
        var mask=new FaceMaskLayerModel{AssetPath="Generated/Faces/Top/Illumination/lamp-mask.png",SourceKind=FaceLampMaskSourceKind.AuthoredImage,AuthoredAssetPath="Assets/Faces/Top/Illumination/lamp-mask.png",Width=100,Height=200};
        var model=FaceDocumentStorage.ToModel(FaceDocumentStorage.ToFile(new FaceDocumentModel{Title="Top",MaskLayer=mask}));
        Assert.Equal(FaceLampMaskSourceKind.AuthoredImage,model.MaskLayer!.SourceKind);
        Assert.Equal("Assets/Faces/Top/Illumination/lamp-mask.png",model.MaskLayer.AuthoredAssetPath);
        Assert.Equal("Generated/Faces/Top/Illumination/lamp-mask.png",model.MaskLayer.AssetPath);
    }

    [Fact]
    public void FocusedIlluminationInvalidations_DoNotStaleArtworkOrComponents()
    {
        var state=FaceBuildStateFactory.CreateGeneratedState(true,true,true,true,true);
        new FaceBuildService().Invalidate(state,FaceBuildInput.LampMaskSource);
        Assert.Equal(FaceBuildStatus.Stale,state.Get(FaceGeneratedProduct.LampMask).Status);
        Assert.Equal(FaceBuildStatus.Stale,state.Get(FaceGeneratedProduct.Trays).Status);
        Assert.Equal(FaceBuildStatus.Stale,state.Get(FaceGeneratedProduct.RuntimeAssets).Status);
        Assert.Equal(FaceBuildStatus.Current,state.Get(FaceGeneratedProduct.ArtworkOutput).Status);
    }
}
