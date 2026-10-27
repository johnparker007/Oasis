using System.Numerics;
using System.Text;
using Oasis.Rendering3D;
using OasisEditor.Features.CabinetEditor.Services;
using Xunit;

namespace Oasis.Rendering3D.Tests;

public sealed class PortableGlbSceneLoaderTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "OasisPortableGlbTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task LoadsNestedTransformsSemanticMeshesAndPbrTextureMetadata()
    {
        Directory.CreateDirectory(_root);
        var path = Path.Combine(_root, "machine.glb");
        var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jGQAAAABJRU5ErkJggg==");
        WriteFixture(path, png);

        var result = await new PortableGlbSceneLoader().LoadAsync(path);

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.Equal("machine", result.SceneName);
        Assert.Equal(2, result.Meshes.Count);
        var visual = Assert.Single(result.Meshes, mesh => mesh.SemanticKind == CabinetSemanticGeometryKind.Visual);
        var trigger = Assert.Single(result.Meshes, mesh => mesh.SemanticKind == CabinetSemanticGeometryKind.Trigger);
        Assert.Equal("Cabinet/Visual", visual.NodePath);
        Assert.Equal(1, visual.NodeIndex);
        Assert.Equal("Test", trigger.SemanticId);
        var geometry = Assert.Single(visual.Primitives);
        Assert.Equal(new Vector3(1, 0, 0), geometry.Positions[1]);
        Assert.Equal(new Vector3(0, 0, 1), geometry.Normals[0]);
        Assert.Equal(new Vector2(1, 0), geometry.TexCoords0[1]);
        Assert.Equal(new[] { 0, 1, 2, 0, 2, 3 }, geometry.TriangleIndices);
        Assert.Equal(new Vector3(2, 3, 0), Vector3.Transform(Vector3.Zero, visual.WorldTransform));
        Assert.Equal(new Vector3(2, 0, 4), Vector3.Transform(Vector3.Zero, trigger.WorldTransform));
        Assert.Equal(new Vector3(2, 0, 0), result.Bounds.Minimum);
        Assert.Equal(new Vector3(3, 4, 4), result.Bounds.Maximum);

        var material = Assert.Single(result.Materials, item => item.MaterialIndex == 0);
        Assert.Equal(new Vector4(.25f, .5f, .75f, .5f), material.BaseColorFactor);
        Assert.Equal(new Vector3(.1f, .2f, .3f), material.EmissiveFactor);
        Assert.Equal(.6f, material.MetallicFactor, 3);
        Assert.Equal(.35f, material.RoughnessFactor, 3);
        Assert.Equal(GlbAlphaMode.Mask, material.AlphaMode);
        Assert.Equal(.3f, material.AlphaCutoff, 3);
        Assert.True(material.DoubleSided);
        var texture = Assert.IsType<GlbTextureData>(material.BaseColorTexture);
        Assert.Equal("image/png", texture.MimeType);
        Assert.Equal(png, texture.EncodedBytes);
        Assert.Equal(GlbTextureWrapMode.MirroredRepeat, texture.WrapS);
        Assert.Equal(GlbTextureWrapMode.ClampToEdge, texture.WrapT);
        Assert.Equal(GlbTextureFilter.LinearMipmapLinear, texture.MinFilter);
        Assert.Equal(GlbTextureFilter.Nearest, texture.MagFilter);
    }

    [Fact]
    public async Task LoadsExternalTextureWhoseRelativePathStaysInsideModelFolder()
    {
        Directory.CreateDirectory(_root);
        var path = Path.Combine(_root, "machine.glb");
        var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jGQAAAABJRU5ErkJggg==");
        WriteFixture(path, png, externalImage: true);

        var result = await new PortableGlbSceneLoader().LoadAsync(path);

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.Equal(png, Assert.Single(result.Materials, item => item.MaterialIndex == 0).BaseColorTexture!.EncodedBytes);
    }

    [Fact]
    public async Task RejectsExternalResourceThatEscapesModelFolder()
    {
        Directory.CreateDirectory(_root);
        var path = Path.Combine(_root, "unsafe.glb");
        WriteGlb(path, "{\"asset\":{\"version\":\"2.0\"},\"buffers\":[{\"uri\":\"../outside.bin\",\"byteLength\":1}],\"scenes\":[{}]}", []);

        var result = await new PortableGlbSceneLoader().LoadAsync(path);

        Assert.False(result.Succeeded);
        Assert.Contains("escapes the model folder", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    private static void WriteFixture(string path, byte[] png, bool externalImage = false)
    {
        if (externalImage)
        {
            var textureDirectory = Path.Combine(Path.GetDirectoryName(path)!, "textures");
            Directory.CreateDirectory(textureDirectory);
            File.WriteAllBytes(Path.Combine(textureDirectory, "face.png"), png);
        }
        var positions = new float[] { 0, 0, 0, 1, 0, 0, 1, 1, 0, 0, 1, 0 };
        var normals = new float[] { 0, 0, 1, 0, 0, 1, 0, 0, 1, 0, 0, 1 };
        var uvs = new float[] { 0, 0, 1, 0, 1, 1, 0, 1 };
        var indices = new ushort[] { 0, 1, 2, 0, 2, 3 };
        var binary = new byte[140 + png.Length];
        Buffer.BlockCopy(positions, 0, binary, 0, 48);
        Buffer.BlockCopy(normals, 0, binary, 48, 48);
        Buffer.BlockCopy(uvs, 0, binary, 96, 32);
        Buffer.BlockCopy(indices, 0, binary, 128, 12);
        Buffer.BlockCopy(png, 0, binary, 140, png.Length);
        var json = """
        {
          "asset":{"version":"2.0"},"scene":0,
          "scenes":[{"nodes":[0]}],
          "nodes":[
            {"name":"Cabinet","translation":[2,0,0],"children":[1,2]},
            {"name":"Visual","mesh":0,"translation":[0,3,0]},
            {"name":"Trigger","mesh":1,"translation":[0,0,4]}
          ],
          "meshes":[
            {"name":"CabinetMesh","primitives":[{"attributes":{"POSITION":0,"NORMAL":1,"TEXCOORD_0":2},"indices":3,"material":0}]},
            {"name":"OasisTrigger_Test","primitives":[{"attributes":{"POSITION":0,"NORMAL":1,"TEXCOORD_0":2},"indices":3,"material":0}]}
          ],
          "materials":[{"name":"Glass","alphaMode":"MASK","alphaCutoff":0.3,"doubleSided":true,"emissiveFactor":[0.1,0.2,0.3],"pbrMetallicRoughness":{"baseColorFactor":[0.25,0.5,0.75,0.5],"metallicFactor":0.6,"roughnessFactor":0.35,"baseColorTexture":{"index":0}}}],
          "textures":[{"sampler":0,"source":0}],
          "samplers":[{"wrapS":33648,"wrapT":33071,"minFilter":9987,"magFilter":9728}],
          "images":[{"bufferView":4,"mimeType":"image/png"}],
          "buffers":[{"byteLength":__BINARY_LENGTH__}],
          "bufferViews":[{"buffer":0,"byteOffset":0,"byteLength":48},{"buffer":0,"byteOffset":48,"byteLength":48},{"buffer":0,"byteOffset":96,"byteLength":32},{"buffer":0,"byteOffset":128,"byteLength":12},{"buffer":0,"byteOffset":140,"byteLength":__PNG_LENGTH__}],
          "accessors":[{"bufferView":0,"componentType":5126,"count":4,"type":"VEC3"},{"bufferView":1,"componentType":5126,"count":4,"type":"VEC3"},{"bufferView":2,"componentType":5126,"count":4,"type":"VEC2"},{"bufferView":3,"componentType":5123,"count":6,"type":"SCALAR"}]
        }
        """.Replace("__BINARY_LENGTH__", binary.Length.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("__PNG_LENGTH__", png.Length.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);
        if (externalImage) json = json.Replace("\"images\":[{\"bufferView\":4,\"mimeType\":\"image/png\"}]", "\"images\":[{\"uri\":\"textures/face.png\"}]", StringComparison.Ordinal);
        WriteGlb(path, json, binary);
    }

    private static void WriteGlb(string path, string json, byte[] binary)
    {
        var jsonBytes = Encoding.UTF8.GetBytes(json);
        var paddedJsonLength = (jsonBytes.Length + 3) & ~3;
        var paddedBinaryLength = (binary.Length + 3) & ~3;
        using var writer = new BinaryWriter(File.Create(path));
        writer.Write(0x46546C67);
        writer.Write(2);
        writer.Write(12 + 8 + paddedJsonLength + (binary.Length == 0 ? 0 : 8 + paddedBinaryLength));
        writer.Write(paddedJsonLength);
        writer.Write(0x4E4F534A);
        writer.Write(jsonBytes);
        writer.Write(Enumerable.Repeat((byte)0x20, paddedJsonLength - jsonBytes.Length).ToArray());
        if (binary.Length == 0) return;
        writer.Write(paddedBinaryLength);
        writer.Write(0x004E4942);
        writer.Write(binary);
        writer.Write(new byte[paddedBinaryLength - binary.Length]);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
