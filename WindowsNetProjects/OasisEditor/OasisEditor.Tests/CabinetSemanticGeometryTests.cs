using System.Text;
using OasisEditor.Features.CabinetEditor.Services;
using Xunit;

namespace OasisEditor.Tests;

public sealed class CabinetSemanticGeometryTests : IDisposable
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

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    public async Task ValidVisualTriangleLoadsWithOptionalUvAndMaterial(bool includeUv, bool includeMaterial)
    {
        var path = WriteGlb(("Cabinet", "CabinetMesh"), includeUv, includeMaterial);
        var result = await new SharpGltfWpfModelLoader().LoadAsync(path);

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.NotNull(result.Model);
        Assert.Null(result.ColliderModel);
        Assert.Null(result.TriggerModel);
    }

    [Fact]
    public async Task SemanticModelsAreSeparatedAndFaceTargetIsNotRenderedAsVisual()
    {
        var path = WriteGlb(
            [("Cabinet", "Visual"), ("OasisCollider_Rail", "Rail"), ("TriggerNode", "OasisTrigger_Pocket"), ("OasisFace_Display", "Display")],
            includeUv: true,
            includeMaterial: true);
        var result = await new SharpGltfWpfModelLoader().LoadAsync(path);

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.Single(result.Model!.Children);
        Assert.Single(result.ColliderModel!.Children);
        Assert.Single(result.TriggerModel!.Children);
        Assert.Single(result.FaceTargets);
    }

    private string WriteGlb((string Node, string Mesh) name, bool includeUv, bool includeMaterial)
        => WriteGlb([name], includeUv, includeMaterial);

    private string WriteGlb((string Node, string Mesh)[] names, bool includeUv, bool includeMaterial)
    {
        Directory.CreateDirectory(_root);
        var path = Path.Combine(_root, Guid.NewGuid() + ".glb");
        var positions = new float[] { 0, 0, 0, 1, 0, 0, 1, 1, 0, 0, 1, 0 };
        var normals = new float[] { 0, 0, 1, 0, 0, 1, 0, 0, 1, 0, 0, 1 };
        var uvs = new float[] { 0, 0, 1, 0, 1, 1, 0, 1 };
        var indices = new ushort[] { 0, 1, 2, 0, 2, 3 };
        var binary = new byte[includeUv ? 140 : 108];
        Buffer.BlockCopy(positions, 0, binary, 0, 48);
        Buffer.BlockCopy(normals, 0, binary, 48, 48);
        var indexOffset = includeUv ? 128 : 96;
        if (includeUv) Buffer.BlockCopy(uvs, 0, binary, 96, 32);
        Buffer.BlockCopy(indices, 0, binary, indexOffset, 12);
        var attributes = includeUv ? "\"POSITION\":0,\"NORMAL\":1,\"TEXCOORD_0\":2" : "\"POSITION\":0,\"NORMAL\":1";
        var indexAccessor = includeUv ? 3 : 2;
        var material = includeMaterial ? ",\"material\":0" : string.Empty;
        var nodes = string.Join(',', names.Select((name, index) => $"{{\"name\":\"{name.Node}\",\"mesh\":{index}}}"));
        var meshes = string.Join(',', names.Select(name => $"{{\"name\":\"{name.Mesh}\",\"primitives\":[{{\"attributes\":{{{attributes}}},\"indices\":{indexAccessor}{material}}}]}}"));
        var views = includeUv
            ? "{\"buffer\":0,\"byteOffset\":0,\"byteLength\":48},{\"buffer\":0,\"byteOffset\":48,\"byteLength\":48},{\"buffer\":0,\"byteOffset\":96,\"byteLength\":32},{\"buffer\":0,\"byteOffset\":128,\"byteLength\":12}"
            : "{\"buffer\":0,\"byteOffset\":0,\"byteLength\":48},{\"buffer\":0,\"byteOffset\":48,\"byteLength\":48},{\"buffer\":0,\"byteOffset\":96,\"byteLength\":12}";
        var accessors = includeUv
            ? "{\"bufferView\":0,\"componentType\":5126,\"count\":4,\"type\":\"VEC3\"},{\"bufferView\":1,\"componentType\":5126,\"count\":4,\"type\":\"VEC3\"},{\"bufferView\":2,\"componentType\":5126,\"count\":4,\"type\":\"VEC2\"},{\"bufferView\":3,\"componentType\":5123,\"count\":6,\"type\":\"SCALAR\"}"
            : "{\"bufferView\":0,\"componentType\":5126,\"count\":4,\"type\":\"VEC3\"},{\"bufferView\":1,\"componentType\":5126,\"count\":4,\"type\":\"VEC3\"},{\"bufferView\":2,\"componentType\":5123,\"count\":6,\"type\":\"SCALAR\"}";
        var sceneNodes = string.Join(',', Enumerable.Range(0, names.Length));
        var materials = includeMaterial ? ",\"materials\":[{\"pbrMetallicRoughness\":{}}]" : string.Empty;
        var json = $"{{\"asset\":{{\"version\":\"2.0\"}},\"scene\":0,\"scenes\":[{{\"nodes\":[{sceneNodes}]}}],\"nodes\":[{nodes}],\"meshes\":[{meshes}],\"buffers\":[{{\"byteLength\":{binary.Length}}}],\"bufferViews\":[{views}],\"accessors\":[{accessors}]{materials}}}";
        WriteGlbFile(path, json, binary);
        return path;
    }

    private static void WriteGlbFile(string path, string json, byte[] binary)
    {
        var jsonBytes = Encoding.UTF8.GetBytes(json);
        var paddedJsonLength = (jsonBytes.Length + 3) & ~3;
        using var writer = new BinaryWriter(File.Create(path));
        writer.Write(0x46546C67); writer.Write(2); writer.Write(12 + 8 + paddedJsonLength + 8 + binary.Length);
        writer.Write(paddedJsonLength); writer.Write(0x4E4F534A); writer.Write(jsonBytes); writer.Write(Enumerable.Repeat((byte)0x20, paddedJsonLength - jsonBytes.Length).ToArray());
        writer.Write(binary.Length); writer.Write(0x004E4942); writer.Write(binary);
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
