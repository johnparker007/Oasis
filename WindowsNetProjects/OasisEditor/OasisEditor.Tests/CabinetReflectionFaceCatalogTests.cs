using OasisEditor.Features.CabinetEditor.Models;
using OasisEditor.Features.CabinetEditor.Services;
using Xunit;

namespace OasisEditor.Tests;

public sealed class CabinetReflectionFaceCatalogTests
{
    [Fact]
    public void Discover_ReturnsReusableCabinetTargetIds_NotInstalledFaces()
    {
        var cabinet = CabinetDocument.FromModelPath("cabinet.glb") with
        {
            TargetOverrides = [CabinetTargetOverride.Default("OasisFace_TopGlass"), CabinetTargetOverride.Default("OasisFace_BottomGlass")]
        };
        var choices = CabinetReflectionFaceCatalog.Discover("ignored", cabinet);
        Assert.Equal(2, choices.Count);
        Assert.Contains(choices, item => item.FaceId == "OasisFace_TopGlass");
        Assert.All(choices, item => Assert.Equal(item.FaceId, item.CabinetTargetId));
    }
}
