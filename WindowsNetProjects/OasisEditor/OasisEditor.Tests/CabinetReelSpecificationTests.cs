using OasisEditor.Features.CabinetEditor.Models;
using OasisEditor.Features.CabinetEditor.Services;
using System.Windows.Media.Media3D;
using Xunit;

namespace OasisEditor.Tests;

public sealed class CabinetReelSpecificationTests
{
    [Fact]
    public void ReflectionDefinitionRoundTripsExplicitPlaneAndSettings()
    {
        var reflection = new CabinetReflectionDefinition("side", "CabinetSide", 1, [new CabinetReflectionSource("lowerGlass", new CabinetReflectionPlane(new(1, 2, 3), new(1, 0, 0), new(0, 1, 0), 2.5, 1.25))],
            CabinetReflectionSettings.PolishedChrome, "masks/side.png");
        var source = CabinetDocument.FromModelPath("cabinet.glb") with { Reflections = [reflection] };

        Assert.True(CabinetDocumentStorage.TryRead(CabinetDocumentStorage.Serialize(source), out var parsed));
        Assert.Equal(reflection, Assert.Single(parsed.Reflections!));
    }

    [Fact]
    public void ReflectionPresetsAreExplicitAndEditedValuesBecomeCustom()
    {
        Assert.Equal(CabinetReflectionPreset.RoughPlastic, CabinetReflectionPreset.Detect(CabinetReflectionSettings.RoughPlastic));
        Assert.Equal(CabinetReflectionPreset.PolishedChrome, CabinetReflectionPreset.Detect(CabinetReflectionSettings.PolishedChrome));
        Assert.Equal(CabinetReflectionPreset.Custom, CabinetReflectionPreset.Detect(CabinetReflectionSettings.RoughPlastic with { Strength = .31 }));
    }

    [Fact]
    public void ManualReflectionPlaneValidationRejectsDegenerateValues()
    {
        Assert.True(CabinetReflectionPlaneValidation.TryValidate(new(new(0, 0, 0), new(1, 0, 0), new(0, 1, 0), 1, 1), out _));
        Assert.False(CabinetReflectionPlaneValidation.TryValidate(new(new(0, 0, 0), new(0, 0, 0), new(0, 1, 0), 1, 1), out _));
        Assert.False(CabinetReflectionPlaneValidation.TryValidate(new(new(0, 0, 0), new(1, 0, 0), new(1, 0, 0), 1, 1), out _));
    }

    [Fact]
    public void ReflectionPlaneDerivesFromOrderedFaceTargetGeometry()
    {
        var target = new CabinetFaceTarget("glass", "OasisFace_glass", "Glass", new[] { new Point3D(2, 3, 4), new Point3D(6, 3, 4), new Point3D(6, 5, 4), new Point3D(2, 5, 4) }, new Vector3D(0, 0, 1), new Point3D(4, 4, 4), true, null);
        Assert.True(CabinetReflectionPlaneDeriver.TryDerive(target, out var plane, out var error), error);
        Assert.Equal(new CabinetReflectionVector(2, 3, 4), plane.Origin); Assert.Equal(new CabinetReflectionVector(1, 0, 0), plane.Right); Assert.Equal(new CabinetReflectionVector(0, 1, 0), plane.Up); Assert.Equal(4, plane.Width); Assert.Equal(2, plane.Height);
    }

    [Fact]
    public void CabinetSerialization_RoundTripsReelSpecificationsAndDefault()
    {
        var cabinet = new CabinetDocument(
            4,
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
            4,
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
            cabinetDocumentJson: CabinetDocumentStorage.Serialize(new CabinetDocument(4, new CabinetModelReference("cabinet.glb", 1, "Y"), [], CabinetPreviewSettings.Default, [], null)));

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
            File.WriteAllText(cabinetPath, CabinetDocumentStorage.Serialize(new CabinetDocument(4, new CabinetModelReference("cabinet.glb", 1, "Y"), [], CabinetPreviewSettings.Default, [new CabinetReelSpecification("standard", "Standard", 210, 50)], "standard")));
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
