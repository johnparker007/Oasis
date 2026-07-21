using OasisEditor.Features.CabinetEditor.Models;
using Xunit;

namespace OasisEditor.Tests;

public sealed class CabinetReelSpecificationTests
{
    [Fact]
    public void CabinetSerialization_RoundTripsReelSpecificationsAndDefault()
    {
        var cabinet = new CabinetDocument(
            2,
            new CabinetModelReference("source.glb", 1.0, "Y"),
            [],
            CabinetPreviewSettings.Default,
            [new CabinetReelSpecification("jpm-standard", "JPM Standard Reel", 210, 50)],
            "jpm-standard");

        var json = CabinetDocumentStorage.Serialize(cabinet);

        Assert.True(CabinetDocumentStorage.TryRead(json, out var parsed));
        Assert.Equal(2, parsed.Version);
        Assert.Equal("jpm-standard", parsed.DefaultReelSpecificationId);
        var specification = Assert.Single(parsed.ReelSpecifications);
        Assert.Equal("jpm-standard", specification.Id);
        Assert.Equal("JPM Standard Reel", specification.Name);
        Assert.Equal(210, specification.DiameterMm);
        Assert.Equal(50, specification.WidthMm);
    }

    [Fact]
    public void Validation_ReportsCabinetReelSpecificationProblems()
    {
        var face = new FaceDocumentModel
        {
            Elements =
            [
                new FaceReelDisplayElement { ObjectId = "reel", Name = "Reel", ReelSpecificationId = "missing" }
            ]
        };
        var cabinet = new CabinetDocument(
            2,
            new CabinetModelReference("source.glb", 1.0, "Y"),
            [],
            CabinetPreviewSettings.Default,
            [
                new CabinetReelSpecification("dup", "Duplicate A", 210, 50),
                new CabinetReelSpecification("dup", "Duplicate B", 210, 50),
                new CabinetReelSpecification("bad", "Bad", 0, 50)
            ],
            "default-missing");

        var diagnostics = new FaceValidationService().Validate(face, null, [], cabinet);

        Assert.Contains(diagnostics, diagnostic => diagnostic.Code == "Cabinet.ReelSpecification.DefaultMissing");
        Assert.Contains(diagnostics, diagnostic => diagnostic.Code == "Cabinet.ReelSpecification.DuplicateId");
        Assert.Contains(diagnostics, diagnostic => diagnostic.Code == "Cabinet.ReelSpecification.InvalidDimensions");
        Assert.Contains(diagnostics, diagnostic => diagnostic.Code == "Face.ReelSpecification.UnknownSelection");
    }

    [Fact]
    public void CabinetReelSpecificationCommands_AddDeleteDefaultAndPreserveIdsOnRename()
    {
        var document = new DocumentTabViewModel(
            EditorDocument.CreateCabinet3DStub("Cabinet"),
            cabinetDocumentJson: CabinetDocumentStorage.Serialize(new CabinetDocument(2, new CabinetModelReference("cabinet.glb", 1, "Y"), [], CabinetPreviewSettings.Default, [], null)));

        var addCommand = CabinetMutationCommands.CreateAddReelSpecificationCommand(document.DocumentId, document);
        addCommand.Execute();
        var added = Assert.Single(document.GetCabinetDocument().ReelSpecifications);
        Assert.Equal(added.Id, document.GetCabinetDocument().DefaultReelSpecificationId);

        var renamed = added with { Name = "Renamed Reel", DiameterMm = 180, WidthMm = 45 };
        CabinetMutationCommands.CreateUpdateReelSpecificationCommand(document.DocumentId, document, renamed).Execute();
        var updated = Assert.Single(document.GetCabinetDocument().ReelSpecifications);
        Assert.Equal(added.Id, updated.Id);
        Assert.Equal("Renamed Reel", updated.Name);

        CabinetMutationCommands.CreateDeleteReelSpecificationCommand(document.DocumentId, document, added.Id).Execute();
        Assert.Empty(document.GetCabinetDocument().ReelSpecifications);
        Assert.Null(document.GetCabinetDocument().DefaultReelSpecificationId);
    }

    [Fact]
    public void CabinetContextResolver_ResolvesAssignedCabinetAssetPath()
    {
        var root = Path.Combine(Path.GetTempPath(), $"oasis-cabinet-context-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var project = new EditorProject { Name = "Test", ProjectDirectory = root, ProjectFilePath = Path.Combine(root, "test.oasis"), AssetsDirectory = Path.Combine(root, "Assets"), MachinesDirectory = Path.Combine(root, "Machines"), GeneratedDirectory = Path.Combine(root, "Generated") };
            var cabinetPath = Path.Combine(root, "Assets", "Cabinets", "main.cabinet3d");
            Directory.CreateDirectory(Path.GetDirectoryName(cabinetPath)!);
            File.WriteAllText(cabinetPath, CabinetDocumentStorage.Serialize(new CabinetDocument(2, new CabinetModelReference("cabinet.glb", 1, "Y"), [], CabinetPreviewSettings.Default, [new CabinetReelSpecification("standard", "Standard", 210, 50)], "standard")));
            var face = new FaceDocumentModel { AssignedCabinetAssetPath = "Assets/Cabinets/main.cabinet3d" };

            var context = new FaceCabinetContextResolver().ResolveForFace(project, [], face);

            Assert.True(context.HasCabinet);
            Assert.Equal("standard", context.CabinetDocument!.DefaultReelSpecificationId);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void FaceSerialization_RoundTripsAssignedCabinetAssetPathWithSchemaVersion7()
    {
        var face = new FaceDocumentModel { Title = "Face", AssignedCabinetAssetPath = "Assets\\Cabinets\\main.cabinet3d" };

        var json = FaceDocumentStorage.Serialize(face);

        Assert.True(FaceDocumentStorage.TryReadValidated(json, out var file, out var error), error);
        Assert.Equal(7, file.SchemaVersion);
        Assert.Equal("Assets/Cabinets/main.cabinet3d", file.AssignedCabinetAssetPath);
    }

}
