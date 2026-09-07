using OasisEditor.Features.CabinetEditor.Models;
using OasisEditor.Features.CabinetEditor.Services;
using System.Windows.Media.Media3D;
using NumericsVector2 = System.Numerics.Vector2;
using Xunit;

namespace OasisEditor.Tests;

public sealed class CabinetReelSpecificationTests
{
    [Fact]
    public void CabinetSerialization_RoundTripsFaceAndLogicalReelAssignments()
    {
        var cabinet = CabinetDocument.FromModelPath("cabinet.glb") with
        {
            FaceAssignments = [new("top-glass", "Assets/Faces/Top Glass/asset.face")],
            ReelSpecifications = [new("small", "Small", 230, 70)],
            ReelAssignments = [new(MachineObjectReference.Reel(3), "small")]
        };

        Assert.True(CabinetDocumentStorage.TryRead(CabinetDocumentStorage.Serialize(cabinet), out var parsed));
        Assert.Equal(new CabinetFaceAssignment("top-glass", "Assets/Faces/Top Glass/asset.face"), Assert.Single(parsed.FaceAssignments!));
        Assert.Equal(new CabinetReelAssignment(MachineObjectReference.Reel(3), "small"), Assert.Single(parsed.ReelAssignments!));
    }

    [Fact]
    public void CabinetAssignmentCommandsSupportUndo()
    {
        var document = new DocumentTabViewModel(EditorDocument.CreateCabinet3DStub("Cabinet"), cabinetDocumentJson: CabinetDocumentStorage.Serialize(CabinetDocument.FromModelPath("cabinet.glb") with { ReelSpecifications = [new("standard", "Standard", 290, 70)] }));
        var faceCommand = CabinetMutationCommands.CreateSetFaceAssignmentCommand(document.DocumentId, document, "top", "Assets/Faces/Top/asset.face");
        faceCommand.Execute();
        Assert.Single(document.GetCabinetDocument().FaceAssignments!);
        faceCommand.Undo();
        Assert.Empty(document.GetCabinetDocument().FaceAssignments!);

        var reelCommand = CabinetMutationCommands.CreateSetReelAssignmentCommand(document.DocumentId, document, MachineObjectReference.Reel(0), "standard");
        reelCommand.Execute();
        Assert.Single(document.GetCabinetDocument().ReelAssignments!);
        reelCommand.Undo();
        Assert.Empty(document.GetCabinetDocument().ReelAssignments!);
    }

    [Fact]
    public void FaceSerializationContainsNoCabinetOwnershipOrPhysicalSpecification()
    {
        var json = FaceDocumentStorage.Serialize(new FaceDocumentModel { Elements = [new FaceReelDisplayElement { ObjectId = "reel", LinkedMachineObjectReference = MachineObjectReference.Reel(0) }] });
        Assert.DoesNotContain("assignedCabinet", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("reelSpecificationId", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("linkedMachineObjectReference", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReflectionDefinitionRoundTripsExplicitPlaneAndSettings()
    {
        var reflection = new CabinetReflectionDefinition("side", "CabinetSide", 1, [new CabinetReflectionSource("lowerGlass", new CabinetReflectionPlane(new(1, 2, 3), new(1, 0, 0), new(0, 1, 0), 2.5, 1.25))],
            CabinetReflectionSettings.PolishedChrome, "masks/side.png");
        var source = CabinetDocument.FromModelPath("cabinet.glb") with { Reflections = [reflection] };

        Assert.True(CabinetDocumentStorage.TryRead(CabinetDocumentStorage.Serialize(source), out var parsed));
        var parsedReflection = Assert.Single(parsed.Reflections!);
        Assert.Equal(reflection with { Sources = parsedReflection.Sources }, parsedReflection);
        Assert.Equal(reflection.Sources, parsedReflection.Sources);
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
        var target = new CabinetFaceTarget("glass", "OasisFace_glass", "Glass", new[] { new Point3D(2, 3, 4), new Point3D(6, 3, 4), new Point3D(6, 5, 4), new Point3D(2, 5, 4) }, new Vector3D(0, 0, 1), new Point3D(4, 4, 4), true, null, new Point3D(2, 3, 4), new Vector3D(4, 0, 0), new Vector3D(0, 2, 0));
        Assert.True(CabinetReflectionPlaneDeriver.TryDerive(target, out var plane, out var error), error);
        Assert.Equal(new CabinetReflectionVector(2, 3, 4), plane.Origin); Assert.Equal(new CabinetReflectionVector(1, 0, 0), plane.Right); Assert.Equal(new CabinetReflectionVector(0, 1, 0), plane.Up); Assert.Equal(4, plane.Width); Assert.Equal(2, plane.Height);
    }

    [Theory]
    [MemberData(nameof(UvMappings))]
    public void ReflectionBasisComesFromTextureCoordinatesInsteadOfGeometricOrder(
        NumericsVector2 uv0, NumericsVector2 uv1, NumericsVector2 uv2, NumericsVector2 uv3,
        Point3D expectedOrigin, Vector3D expectedRight, Vector3D expectedUp)
    {
        var positions = new[] { new Point3D(2, 3, 4), new Point3D(6, 3, 4), new Point3D(6, 5, 4), new Point3D(2, 5, 4) };
        var samples = new[] { (positions[2], uv2), (positions[0], uv0), (positions[3], uv3), (positions[1], uv1), (positions[2], uv2), (positions[0], uv0) };
        Assert.True(GlbCabinetFaceTargetDetector.TryDeriveUvBasis(samples, out var origin, out var right, out var up, out var error), error);
        Assert.Equal(expectedOrigin, origin); Assert.Equal(expectedRight, right); Assert.Equal(expectedUp, up);
    }

    public static TheoryData<NumericsVector2, NumericsVector2, NumericsVector2, NumericsVector2, Point3D, Vector3D, Vector3D> UvMappings => new()
    {
        { new(0, 0), new(1, 0), new(1, 1), new(0, 1), new(2, 3, 4), new(4, 0, 0), new(0, 2, 0) },
        { new(0, 1), new(0, 0), new(1, 0), new(1, 1), new(6, 3, 4), new(0, 2, 0), new(-4, 0, 0) },
        { new(1, 0), new(0, 0), new(0, 1), new(1, 1), new(6, 3, 4), new(-4, 0, 0), new(0, 2, 0) },
        { new(0, 1), new(1, 1), new(1, 0), new(0, 0), new(2, 5, 4), new(4, 0, 0), new(0, -2, 0) }
    };

    [Fact]
    public void CabinetSerialization_RoundTripsReelSpecificationsAndDefault()
    {
        var cabinet = new CabinetDocument(
            6,
            new CabinetModelReference("source.glb", 1.0, "Y"),
            [],
            CabinetPreviewSettings.Default,
            [new CabinetReelSpecification("jpm-standard", "JPM Standard Reel", 210, 50)],
            "jpm-standard");

        var json = CabinetDocumentStorage.Serialize(cabinet);

        Assert.True(CabinetDocumentStorage.TryRead(json, out var parsed));
        Assert.Equal(6, parsed.Version);
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
                new FaceReelDisplayElement { ObjectId = "reel", Name = "Reel",}
            ]
        };
        var cabinet = new CabinetDocument(
            6,
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
            cabinetDocumentJson: CabinetDocumentStorage.Serialize(new CabinetDocument(6, new CabinetModelReference("cabinet.glb", 1, "Y"), [], CabinetPreviewSettings.Default, [], null)));

        var addCommand = CabinetMutationCommands.CreateAddReelSpecificationCommand(document.DocumentId, document);
        addCommand.Execute();
        var added = Assert.Single(document.GetCabinetDocument().ReelSpecifications);

        var renamed = added with { Name = "Renamed Reel", DiameterMm = 180, WidthMm = 45 };
        CabinetMutationCommands.CreateUpdateReelSpecificationCommand(document.DocumentId, document, renamed).Execute();
        var updated = Assert.Single(document.GetCabinetDocument().ReelSpecifications);
        Assert.Equal(added.Id, updated.Id);
        Assert.Equal("Renamed Reel", updated.Name);

        CabinetMutationCommands.CreateDeleteReelSpecificationCommand(document.DocumentId, document, added.Id).Execute();
        Assert.Empty(document.GetCabinetDocument().ReelSpecifications);
    }

    [Fact]
    public void FaceSerialization_UsesCurrentSchemaWithoutCabinetOwnership()
    {
        var face = new FaceDocumentModel { Title = "Face",};

        var json = FaceDocumentStorage.Serialize(face);

        Assert.True(FaceDocumentStorage.TryReadValidated(json, out var file, out var error), error);
        Assert.Equal(FaceDocumentStorage.CurrentSchemaVersion, file.SchemaVersion);
    }

}
