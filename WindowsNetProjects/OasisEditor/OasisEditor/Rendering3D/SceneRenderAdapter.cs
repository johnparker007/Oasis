using System.Numerics;
using Oasis.Rendering3D;
using OasisEditor.Features.CabinetEditor.Services;

namespace OasisEditor.Avalonia.Rendering3D;

public sealed record ScenePalette(Vector4 Grid, Vector4 Selection, Vector4 Collider, Vector4 Trigger);

public static class SceneRenderAdapter
{
    public static SceneRenderData FromGlb(GlbSceneLoadResult source, Matrix4x4 placement, ScenePalette palette, string owner = "cabinet")
    {
        if (!source.Succeeded) throw new InvalidDataException(source.ErrorMessage);
        var meshes = ConvertMeshes(source.Meshes, source.Materials, placement, palette, owner);
        var min = new Vector3(float.PositiveInfinity); var max = new Vector3(float.NegativeInfinity);
        foreach (var mesh in meshes) for (var i = 0; i < mesh.Vertices.Length; i += 8)
        { var p = Vector3.Transform(new Vector3(mesh.Vertices[i], mesh.Vertices[i + 1], mesh.Vertices[i + 2]), mesh.Transform); min = Vector3.Min(min, p); max = Vector3.Max(max, p); }
        if (meshes.Count == 0) min = max = Vector3.Zero;
        return new(meshes, min, max, source.Diagnostics);
    }
    public static List<SceneRenderMesh> ConvertMeshes(IReadOnlyList<GlbMeshInstance> source,
        IReadOnlyList<GlbMaterialData> materials, Matrix4x4 placement, ScenePalette palette, string owner)
    {
        var result = new List<SceneRenderMesh>();
        var textures = new Dictionary<GlbTextureData, SceneTexture>(ReferenceEqualityComparer.Instance);
        var converted = materials.ToDictionary(m => m.MaterialIndex, Material);
        foreach (var mesh in source)
        {
            var kind = mesh.SemanticKind switch { CabinetSemanticGeometryKind.Collider => "collider", CabinetSemanticGeometryKind.Trigger => "trigger", _ => "visual" };
            for (var index = 0; index < mesh.Primitives.Count; index++)
            {
                var p = mesh.Primitives[index];
                if (p.TriangleIndices.Length == 0) continue;
                var hasNormals = p.Normals.Length == p.Positions.Length;
                var vertices = new float[(hasNormals ? p.Positions.Length : p.TriangleIndices.Length) * 8];
                var indices = hasNormals ? p.TriangleIndices : Enumerable.Range(0, p.TriangleIndices.Length).ToArray();
                if (hasNormals)
                    for (var i = 0; i < p.Positions.Length; i++) SetVertex(i, i, p.Normals[i]);
                else
                    for (var i = 0; i < p.TriangleIndices.Length; i += 3)
                    {
                        var a = p.Positions[p.TriangleIndices[i]]; var b = p.Positions[p.TriangleIndices[i + 1]]; var c = p.Positions[p.TriangleIndices[i + 2]];
                        var n = Vector3.Cross(b - a, c - a); n = n.LengthSquared() > 1e-16 ? Vector3.Normalize(n) : Vector3.UnitY;
                        for (var j = 0; j < 3; j++) SetVertex(i + j, p.TriangleIndices[i + j], n);
                    }
                var material = kind == "visual" ? converted.GetValueOrDefault(p.MaterialIndex) ?? new(Vector4.One, Vector3.Zero)
                    : new SceneMaterial(kind == "collider" ? palette.Collider : palette.Trigger, Vector3.Zero, AlphaMode: "BLEND", DoubleSided: true, Unlit: true);
                result.Add(new(owner + "/" + mesh.NodeIndex + "/" + index, mesh.NodeName, vertices, indices, material, mesh.WorldTransform * placement, kind));
                void SetVertex(int destination, int sourceIndex, Vector3 normal)
                {
                    var point = p.Positions[sourceIndex]; var uv = p.TexCoords0.Length == p.Positions.Length ? p.TexCoords0[sourceIndex] : Vector2.Zero;
                    var offset = destination * 8; vertices[offset] = point.X; vertices[offset + 1] = point.Y; vertices[offset + 2] = point.Z;
                    vertices[offset + 3] = normal.X; vertices[offset + 4] = normal.Y; vertices[offset + 5] = normal.Z;
                    vertices[offset + 6] = uv.X; vertices[offset + 7] = uv.Y;
                }
            }
        }
        return result;
        SceneMaterial Material(GlbMaterialData m) => new(m.BaseColorFactor, m.EmissiveFactor, m.MetallicFactor, m.RoughnessFactor,
            m.AlphaMode.ToString().ToUpperInvariant(), m.AlphaCutoff, m.DoubleSided, Texture(m.BaseColorTexture), Texture(m.EmissiveTexture),
            Texture(m.MetallicRoughnessTexture), Texture(m.NormalTexture), Texture(m.OcclusionTexture), m.Unlit);
        SceneTexture? Texture(GlbTextureData? t)
        {
            if (t is null || t.TextureCoordinate != 0) return null;
            if (textures.TryGetValue(t, out var existing)) return existing;
            return textures[t] = new(t.EncodedBytes, Wrap(t.WrapS), Wrap(t.WrapT), Filter(t.MinFilter), Filter(t.MagFilter));
        }
    }
    private static int Wrap(GlbTextureWrapMode mode) => mode switch { GlbTextureWrapMode.ClampToEdge => 33071, GlbTextureWrapMode.MirroredRepeat => 33648, _ => 10497 };
    private static int Filter(GlbTextureFilter mode) => mode switch
    { GlbTextureFilter.Nearest => 9728, GlbTextureFilter.NearestMipmapNearest => 9984, GlbTextureFilter.LinearMipmapNearest => 9985,
        GlbTextureFilter.NearestMipmapLinear => 9986, GlbTextureFilter.LinearMipmapLinear => 9987, _ => 9729 };
    public static SceneRenderMesh Grid(Vector3 minimum, Vector3 maximum, Vector4 color)
    {
        var size = Math.Max(.1f, Math.Max((maximum - minimum).Length(), 1)); var step = size / 10;
        var vertices = new List<float>(); var indices = new List<int>();
        for (var i = -10; i <= 10; i++) { Line(new(-size, 0, i * step), new(size, 0, i * step)); Line(new(i * step, 0, -size), new(i * step, 0, size)); }
        return new("grid", "World grid", vertices.ToArray(), indices.ToArray(), new(color, Vector3.Zero, Unlit: true), Matrix4x4.Identity, "grid", true);
        void Line(Vector3 a, Vector3 b)
        { var index = vertices.Count / 8; vertices.AddRange([a.X,a.Y,a.Z,0,1,0,0,0,b.X,b.Y,b.Z,0,1,0,0,0]); indices.Add(index); indices.Add(index + 1); }
    }
    public static SceneRenderMesh Bounds(string id, Vector3 minimum, Vector3 maximum, Vector4 color, Matrix4x4? transform = null)
    {
        var points = new[] { new Vector3(minimum.X,minimum.Y,minimum.Z), new Vector3(maximum.X,minimum.Y,minimum.Z),
            new Vector3(maximum.X,maximum.Y,minimum.Z),new Vector3(minimum.X,maximum.Y,minimum.Z),new Vector3(minimum.X,minimum.Y,maximum.Z),
            new Vector3(maximum.X,minimum.Y,maximum.Z),new Vector3(maximum.X,maximum.Y,maximum.Z),new Vector3(minimum.X,maximum.Y,maximum.Z) };
        return new(id,id,points.SelectMany(p => new[] { p.X,p.Y,p.Z,0f,1f,0f,0f,0f }).ToArray(),
            [0,1,1,2,2,3,3,0,4,5,5,6,6,7,7,4,0,4,1,5,2,6,3,7], new(color,Vector3.Zero,Unlit:true), transform ?? Matrix4x4.Identity,"selection",true);
    }
}
