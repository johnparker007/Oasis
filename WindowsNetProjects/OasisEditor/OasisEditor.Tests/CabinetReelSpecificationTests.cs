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
        Assert.Contains(diagnostics, diagnostic => diagnostic.Code == "Face.ReelSpecification.MissingSelection");
    }
}
