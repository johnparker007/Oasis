using System.Windows;
using Xunit;

namespace OasisEditor.Tests;

public sealed class FaceValidationServiceTests
{
    [Fact]
    public void Validate_ReportsMissingSourceAssetsAndMachineReferences()
    {
        var face = new FaceDocumentModel
        {
            Id = "face-validation",
            Title = "Face Validation",
            SourcePanel2DDocumentId = "missing-panel",
            SourceRegion = FaceSourceRegionModel.FromRect(new Rect(0, 0, 100, 100)),
            Elements =
            [
                new FaceArtworkElement
                {
                    ObjectId = "artwork-1",
                    Name = "Artwork",
                    AssetPath = "Assets/Missing/artwork.png"
                },
                new FaceLampWindowElement
                {
                    ObjectId = "lamp-1",
                    Name = "Lamp"
                },
                new FaceReelDisplayElement
                {
                    ObjectId = "reel-1",
                    Name = "Reel",
                    LinkedMachineObjectReference = MachineObjectReference.Lamp(1)
                }
            ]
        };
        var project = new EditorProject
        {
            Name = "Test",
            ProjectFilePath = "/tmp/project.oasis",
            ProjectDirectory = "/tmp",
            AssetsDirectory = "/tmp/Assets",
            MachinesDirectory = "/tmp/Machines",
            GeneratedDirectory = "/tmp/Generated"
        };

        var diagnostics = new FaceValidationService().Validate(face, project, []);

        Assert.Contains(diagnostics, diagnostic => diagnostic.Code == "Face.SourcePanel2D.NotOpen");
        Assert.Contains(diagnostics, diagnostic => diagnostic.Code == "Face.ArtworkAsset.Missing");
        Assert.Contains(diagnostics, diagnostic => diagnostic.Code == "Face.MachineReference.Missing");
        Assert.Contains(diagnostics, diagnostic => diagnostic.Code == "Face.MachineReference.KindMismatch");
    }
}
