using Xunit;

namespace OasisEditor.Tests;

public sealed class FaceComponentAuthoringTests
{
    [Fact]
    public void EveryConcreteElementType_HasExplicitSubsystemClassification()
    {
        Assert.Equal(FaceElementCategory.Artwork, FaceElementClassification.GetCategory(new FaceArtworkElement()));
        Assert.Equal(FaceElementCategory.Illumination, FaceElementClassification.GetCategory(new FaceLampWindowElement()));
        Assert.Equal(FaceElementCategory.Illumination, FaceElementClassification.GetCategory(new FaceLampEmitterElement()));
        Assert.Equal(FaceElementCategory.Component, FaceElementClassification.GetCategory(new FaceReelDisplayElement()));
        Assert.Equal(FaceElementCategory.Component, FaceElementClassification.GetCategory(new FaceButtonElement()));
        Assert.Equal(FaceElementCategory.Component, FaceElementClassification.GetCategory(new FaceSevenSegmentDisplayElement()));
        Assert.Equal(FaceElementCategory.Component, FaceElementClassification.GetCategory(new FaceAlphaDisplayElement()));
    }

    [Fact]
    public void NativeComponents_AreUndoableAndRoundTripWithAuthoredProvenance()
    {
        var document=new DocumentTabViewModel(EditorDocument.CreateFaceStub("Native"));
        foreach(var kind in Enum.GetValues<FaceComponentKind>())
            document.CommandService.Execute(FaceMutationCommands.CreateAddComponentCommand(document.DocumentId,document,FaceComponentFactory.Create(kind,10,20)));

        Assert.Equal(4,document.GetFaceElements().Count);
        Assert.Equal(FaceSubsystemOrigin.Authored,document.GetFaceDocument().Provenance.Components.Origin);
        Assert.False(document.GetFaceDocument().Provenance.Components.IsLocallyModified);
        Assert.True(FaceDocumentStorage.TryRead(document.GetFaceDocumentJson(),out var file));
        var reopened=FaceDocumentStorage.ToModel(file);
        Assert.Contains(reopened.Elements,e=>e is FaceReelDisplayElement);
        Assert.Contains(reopened.Elements,e=>e is FaceButtonElement);
        Assert.Contains(reopened.Elements,e=>e is FaceSevenSegmentDisplayElement);
        Assert.Contains(reopened.Elements,e=>e is FaceAlphaDisplayElement);

        Assert.True(document.CommandService.TryUndo());
        Assert.Equal(3,document.GetFaceElements().Count);
        Assert.True(document.CommandService.TryRedo());
        Assert.Equal(4,document.GetFaceElements().Count);
    }

    [Fact]
    public void EditingDerivedComponent_MarksOnlyComponentsLocallyModified_AndUndoRestoresProvenance()
    {
        var document=new DocumentTabViewModel(EditorDocument.CreateFaceStub("Derived"));
        var derived=new FaceSubsystemProvenanceModel { Origin=FaceSubsystemOrigin.Derived,SourceDocumentPath="main.panel2d" };
        var face=FaceDocumentCopy.WithElementsAndComponents(document.GetFaceDocument(),[new FaceReelDisplayElement { ObjectId="reel",Name="Reel",Width=50,Height=80 }],derived);
        document.SetFaceDocument(face);
        var original=Assert.IsType<FaceReelDisplayElement>(document.GetFaceElements()[0]);
        var moved=FaceElementModelCloner.Clone(original,x:25);
        document.CommandService.Execute(FaceMutationCommands.CreateUpdateElementCommand(document.DocumentId,document,"reel",moved,"Move reel"));

        Assert.True(document.GetFaceDocument().Provenance.Components.IsLocallyModified);
        Assert.False(document.GetFaceDocument().Provenance.Artwork.IsLocallyModified);
        Assert.False(document.GetFaceDocument().Provenance.Illumination.IsLocallyModified);
        Assert.True(document.CommandService.TryUndo());
        Assert.Equal(0,document.GetFaceElements()[0].X);
        Assert.False(document.GetFaceDocument().Provenance.Components.IsLocallyModified);
    }

    [Fact]
    public void ComponentsOnlyRebuild_PreservesArtworkAndIllumination_AndIsUndoable()
    {
        var document=new DocumentTabViewModel(EditorDocument.CreateFaceStub("Derived"));
        var artwork=new FaceArtworkElement{ObjectId="art",Name="Image artwork",Width=800,Height=600};
        var lamp=new FaceLampWindowElement{ObjectId="lamp",Name="Lamp",Width=20,Height=20};
        var local=new FaceButtonElement{ObjectId="local",Name="Local",Width=30,Height=20};
        var provenance=new FaceSubsystemProvenanceModel{Origin=FaceSubsystemOrigin.Derived,SourceDocumentPath="main.panel2d",IsLocallyModified=true};
        document.SetFaceDocument(FaceDocumentCopy.WithElementsAndComponents(document.GetFaceDocument(),[artwork,lamp,local],provenance));

        document.CommandService.Execute(FaceMutationCommands.CreateRebuildComponentsCommand(document.DocumentId,document,
            [new FaceReelDisplayElement{ObjectId="derived",Name="Derived Reel",Width=50,Height=80}],"main.panel2d"));

        Assert.Contains(document.GetFaceElements(),e=>e.ObjectId=="art");
        Assert.Contains(document.GetFaceElements(),e=>e.ObjectId=="lamp");
        Assert.DoesNotContain(document.GetFaceElements(),e=>e.ObjectId=="local");
        Assert.Contains(document.GetFaceElements(),e=>e.ObjectId=="derived");
        Assert.False(document.GetFaceDocument().Provenance.Components.IsLocallyModified);
        Assert.True(document.CommandService.TryUndo());
        Assert.Contains(document.GetFaceElements(),e=>e.ObjectId=="local");
        Assert.True(document.GetFaceDocument().Provenance.Components.IsLocallyModified);
    }
}
