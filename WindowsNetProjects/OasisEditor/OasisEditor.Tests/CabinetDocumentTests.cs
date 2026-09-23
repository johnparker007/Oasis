using OasisEditor.Features.CabinetEditor.Models;
using Xunit;

namespace OasisEditor.Tests;

public sealed class CabinetDocumentTests
{
    [Fact]
    public void Schema8_RoundTripsOnlyIntrinsicCabinetStateAndTemporaryPhysicalReels()
    {
        var plane = new CabinetReflectionPlane(new(0, 0, 0), new(1, 0, 0), new(0, 1, 0), 2, 1);
        var source = CabinetDocument.FromModelPath("cabinet.glb") with
        {
            Model = new("cabinet.glb", 0.01, "Z"),
            SurfaceTargetSettings = [new("OasisFace_Top", CabinetSurfaceTargetSettings.InvertedFrontSide, 90, true)],
            ReelSpecifications = [new("standard", "Standard physical reel", 210, 50)],
            DefaultReelSpecificationId = "standard",
            Reflections = [new("glass", "Cabinet/Glass", 0, [new("OasisFace_Top", plane)], CabinetReflectionSettings.RoughPlastic)]
        };

        var json = CabinetDocumentStorage.Serialize(source);
        Assert.True(CabinetDocumentStorage.TryRead(json, out var parsed));
        Assert.Equal(8, parsed.Version);
        Assert.Equal(source.Model, parsed.Model);
        Assert.Equal(source.SurfaceTargetSettings, parsed.SurfaceTargetSettings);
        Assert.Equal(source.ReelSpecifications, parsed.ReelSpecifications);
        Assert.Equal(source.DefaultReelSpecificationId, parsed.DefaultReelSpecificationId);
        Assert.Equal(source.Reflections, parsed.Reflections);
        Assert.DoesNotContain("preview", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("faceAssignments", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("reelAssignments", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("machine", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("runtime", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("input", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SparseSurfaceSettings_DefaultWithoutCreatingTargetRegistry()
    {
        var cabinet = CabinetDocument.FromModelPath("cabinet.glb");
        var defaults = cabinet.GetSurfaceTargetSettings("OasisFace_Unconfigured");
        Assert.Equal(CabinetSurfaceTargetSettings.NormalFrontSide, defaults.FrontSide);
        Assert.Equal(0, defaults.FaceRotation);
        Assert.False(defaults.FaceFlipHorizontal);
        Assert.Empty(cabinet.SurfaceTargetSettings);
    }

    [Theory]
    [InlineData(7)]
    [InlineData(9)]
    public void ReaderRejectsNonCurrentSchema(int version)
    {
        var json = CabinetDocumentStorage.Serialize(CabinetDocument.FromModelPath("cabinet.glb"));
        json = json.Replace("\"version\": 8", $"\"version\": {version}", StringComparison.Ordinal);
        Assert.False(CabinetDocumentStorage.TryRead(json, out _));
    }

    [Fact]
    public void ReaderRejectsSupersededPreviewState()
    {
        var json = CabinetDocumentStorage.Serialize(CabinetDocument.FromModelPath("cabinet.glb"));
        json = json.TrimEnd('}', '\r', '\n') + ",\n  \"preview\": { \"lampPreviewMode\": \"Live\" }\n}";
        Assert.False(CabinetDocumentStorage.TryRead(json, out _));
    }
}
