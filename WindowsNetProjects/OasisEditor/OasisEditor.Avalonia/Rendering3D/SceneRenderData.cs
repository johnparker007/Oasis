using System.Numerics;

namespace OasisEditor.Avalonia.Rendering3D;

public sealed record SceneTexture(byte[] EncodedImage, int WrapS = 10497, int WrapT = 10497, int MinFilter = 9729, int MagFilter = 9729,
    byte[]? RgbaPixels = null, int Width = 0, int Height = 0);
public sealed record SceneMaterial(Vector4 Color, Vector3 Emissive, float Metallic = 0, float Roughness = .7f,
    string AlphaMode = "OPAQUE", float AlphaCutoff = .5f, bool DoubleSided = false,
    SceneTexture? BaseTexture = null, SceneTexture? EmissiveTexture = null,
    SceneTexture? MetallicRoughnessTexture = null, SceneTexture? NormalTexture = null, SceneTexture? OcclusionTexture = null,
    bool Unlit = false);
public sealed record SceneReflectionSource(Vector3 Origin, Vector3 Right, Vector3 Up, Vector3 VisibleNormal, float Width, float Height,
    string SourceSurfaceTargetId, SceneTexture Texture, SceneTexture? LampOverlayTexture = null,
    int UvQuarterTurns = 0, bool FlipHorizontal = false);
public sealed record SceneReflectionBinding(bool Enabled, float Strength, float FresnelPower, float FresnelStrength,
    float UnlitArtworkStrength, float LitLampStrength, float Roughness, float Distortion, float EdgeFade,
    IReadOnlyList<SceneReflectionSource> Sources, SceneTexture? VisibilityMask = null);
public sealed record SceneRenderMesh(string Id, string Name, float[] Vertices, int[] Indices, SceneMaterial Material,
    Matrix4x4 Transform, string Kind = "visual", bool Lines = false)
{
    public SceneReflectionBinding? Reflection { get; init; }
    public Vector3 Center
    {
        get
        {
            if (Vertices.Length < 8) return Vector3.Transform(Vector3.Zero, Transform);
            var min = new Vector3(float.PositiveInfinity); var max = new Vector3(float.NegativeInfinity);
            for (var i = 0; i + 7 < Vertices.Length; i += 8)
            { var p = new Vector3(Vertices[i], Vertices[i + 1], Vertices[i + 2]); min = Vector3.Min(min, p); max = Vector3.Max(max, p); }
            return Vector3.Transform((min + max) * .5f, Transform);
        }
    }
}
public sealed record SceneRenderData(IReadOnlyList<SceneRenderMesh> Meshes, Vector3 Minimum, Vector3 Maximum, IReadOnlyList<string> Diagnostics)
{
    public static SceneRenderData Empty => new([], Vector3.Zero, Vector3.Zero, []);
}
public sealed record SceneFrame(int Width, int Height, byte[] BgraPixels, string Device, int DrawCalls, int Triangles);
