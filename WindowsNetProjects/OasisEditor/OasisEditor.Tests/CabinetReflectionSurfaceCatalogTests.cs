using System.Windows.Media.Media3D;
using OasisEditor.Features.CabinetEditor.Models;
using OasisEditor.Features.CabinetEditor.Services;
using Xunit;

namespace OasisEditor.Tests;

public sealed class CabinetReflectionSurfaceCatalogTests
{
    [Fact]
    public void CatalogUsesDetectedGlbTargets_NotSparseAuthoredSettings()
    {
        var detected = new[]
        {
            Target("OasisFace_TopGlass", true),
            Target("OasisFace_Invalid", false)
        };

        var choices = CabinetReflectionSurfaceCatalog.FromDetectedTargets(detected);

        var choice = Assert.Single(choices);
        Assert.Equal("OasisFace_TopGlass", choice.SourceSurfaceTargetId);
        Assert.Equal(choice.SourceSurfaceTargetId, choice.CabinetTargetId);
    }

    [Fact]
    public void NoDetectedTargetMeansNoChoice_EvenWhenSettingsExist()
    {
        var cabinet = CabinetDocument.FromModelPath("cabinet.glb") with
        {
            SurfaceTargetSettings = [CabinetSurfaceTargetSettings.Default("OasisFace_NotInGlb")]
        };

        Assert.NotEmpty(cabinet.SurfaceTargetSettings);
        Assert.Empty(CabinetReflectionSurfaceCatalog.FromDetectedTargets([]));
    }

    private static CabinetFaceTarget Target(string id, bool valid) =>
        new(id, id, id, [], new Vector3D(), new Point3D(), valid, valid ? null : "invalid");
}
