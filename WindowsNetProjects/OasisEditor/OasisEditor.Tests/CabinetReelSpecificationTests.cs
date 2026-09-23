using OasisEditor.Features.CabinetEditor.Models;
using Xunit;

namespace OasisEditor.Tests;

public sealed class CabinetReelSpecificationTests
{
    [Fact]
    public void CabinetV8_RetainsPhysicalSpecificationsButNotCompositionAssignments()
    {
        var cabinet = CabinetDocument.FromModelPath("cabinet.glb") with
        {
            ReelSpecifications = [new CabinetReelSpecification("standard", "Standard", 210, 50)],
            DefaultReelSpecificationId = "standard"
        };
        var json = CabinetDocumentStorage.Serialize(cabinet);
        Assert.True(CabinetDocumentStorage.TryRead(json, out var parsed));
        Assert.Equal(8, parsed.Version);
        Assert.Single(parsed.ReelSpecifications);
        Assert.DoesNotContain("faceAssignments", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("reelAssignments", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CabinetV7_IsRejectedRatherThanMigrated()
    {
        var json = CabinetDocumentStorage.Serialize(CabinetDocument.FromModelPath("cabinet.glb"));
        var old = json.Replace("\"version\": 8", "\"version\": 7", StringComparison.Ordinal);
        Assert.False(CabinetDocumentStorage.TryRead(old, out _));
    }

    [Fact]
    public void ReflectionSourceSerializesStableSurfaceTargetIdentity()
    {
        var plane = new CabinetReflectionPlane(new(0,0,0), new(1,0,0), new(0,1,0), 1, 1);
        var cabinet = CabinetDocument.FromModelPath("cabinet.glb") with { Reflections = [new("r", "receiver", 0, [new("OasisFace_TopGlass", plane)], CabinetReflectionSettings.RoughPlastic)] };
        var json = CabinetDocumentStorage.Serialize(cabinet);
        Assert.Contains("sourceSurfaceTargetId", json);
        Assert.DoesNotContain("\"faceId\"", json);
    }
}
