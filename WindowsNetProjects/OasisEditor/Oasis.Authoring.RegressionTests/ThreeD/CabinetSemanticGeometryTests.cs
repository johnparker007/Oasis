using OasisEditor.FacePipeline;
using System.Text;
using OasisEditor.Features.CabinetEditor.Services;
using Xunit;

namespace OasisEditor.Tests;

public sealed class CabinetSemanticGeometryTests
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "OasisCabinetSemanticTests", Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData("OasisFace_Screen", CabinetSemanticGeometryKind.FaceTarget)]
    [InlineData("OasisCollider_PlayingSurface", CabinetSemanticGeometryKind.Collider)]
    [InlineData("OasisTrigger_Pocket", CabinetSemanticGeometryKind.Trigger)]
    [InlineData("Cabinet", CabinetSemanticGeometryKind.Visual)]
    [InlineData("COL_Cushion", CabinetSemanticGeometryKind.Visual)]
    [InlineData("TRG_Pocket", CabinetSemanticGeometryKind.Visual)]
    public void ClassifiesOnlyCurrentOasisSemanticNames(string name, CabinetSemanticGeometryKind expected)
        => Assert.Equal(expected, CabinetSemanticGeometry.ClassifyName(name));

    [Fact]
    public void NodeSemanticNameTakesPrecedenceAndMeshNameIsUsedOtherwise()
    {
        Assert.Equal(CabinetSemanticGeometryKind.Collider, CabinetSemanticGeometry.Classify("OasisCollider_Node", "OasisTrigger_Mesh"));
        Assert.Equal(CabinetSemanticGeometryKind.Trigger, CabinetSemanticGeometry.Classify("OrdinaryNode", "OasisTrigger_Mesh"));
    }

}
