using OasisEditor;
using Xunit;

namespace OasisEditor.Tests;

public sealed class FaceRuntimeDisplayReferenceIndexTests
{
    [Fact]
    public void GetObjectIdsByReference_GroupsRuntimeDisplayElementsByMachineReference()
    {
        var document = CreateFaceDocument([
            new FaceReelDisplayElement { ObjectId = "face-reel-a", LinkedMachineObjectReference = MachineObjectReference.Reel(2) },
            new FaceReelDisplayElement { ObjectId = "face-reel-b", LinkedMachineObjectReference = MachineObjectReference.Reel(2) },
            new FaceReelDisplayElement { ObjectId = "face-reel-other", LinkedMachineObjectReference = MachineObjectReference.Reel(3) },
            new FaceSevenSegmentDisplayElement { ObjectId = "face-seven-2", LinkedMachineObjectReference = MachineObjectReference.SevenSegmentDisplay(2) }
        ]);

        var index = FaceRuntimeDisplayReferenceIndex.GetObjectIdsByReference<FaceReelDisplayElement>(document, MachineObjectKind.Reel);

        var objectIds = Assert.Contains(MachineObjectReference.Reel(2), index);
        Assert.Equal(["face-reel-a", "face-reel-b"], objectIds);
        Assert.DoesNotContain(MachineObjectReference.SevenSegmentDisplay(2), index.Keys);
    }

    [Fact]
    public void AddObjectIdsForReference_IgnoresProvenanceOnlyLinks()
    {
        var document = CreateFaceDocument([
            new FaceAlphaDisplayElement
            {
                ObjectId = "face-alpha-10",
                LinkedPanel2DElementId = "panel-alpha-10"
            }
        ]);
        var changedObjectIds = new HashSet<string>(StringComparer.Ordinal);

        FaceRuntimeDisplayReferenceIndex.AddObjectIdsForReference<FaceAlphaDisplayElement>(
            document,
            MachineObjectReference.AlphaDisplay(10),
            changedObjectIds);

        Assert.Empty(changedObjectIds);
    }

    private static DocumentTabViewModel CreateFaceDocument(IReadOnlyList<FaceElementModel> elements)
    {
        var faceDocument = EditorDocument.CreateFromFile("face.face", "face", "face");
        var tab = new DocumentTabViewModel(faceDocument);
        tab.SetFaceElements(elements);
        return tab;
    }
}
