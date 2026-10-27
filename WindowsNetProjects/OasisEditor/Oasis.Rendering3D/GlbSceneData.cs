using System.Numerics;
using OasisEditor.Features.CabinetEditor.Models;
using OasisEditor.Features.CabinetEditor.Services;

namespace Oasis.Rendering3D;

public sealed record GlbSceneLoadResult(
    bool Succeeded,
    string? SceneName,
    IReadOnlyList<GlbMeshInstance> Meshes,
    IReadOnlyList<GlbMaterialData> Materials,
    BoundingBox3 Bounds,
    IReadOnlyList<CabinetFaceTarget> FaceTargets,
    IReadOnlyList<CabinetReflectionReceiverTarget> ReflectionTargets,
    IReadOnlyList<string> Diagnostics,
    string? ErrorMessage)
{
    public static GlbSceneLoadResult Failure(string error) => new(false, null, [], [], BoundingBox3.Empty, [], [], [], error);
}

public readonly record struct BoundingBox3(Vector3 Minimum, Vector3 Maximum, bool IsEmpty)
{
    public static BoundingBox3 Empty => new(default, default, true);

    public BoundingBox3 Include(Vector3 point) => IsEmpty
        ? new(point, point, false)
        : new(Vector3.Min(Minimum, point), Vector3.Max(Maximum, point), false);
}

public sealed record GlbMeshInstance(
    int NodeIndex,
    string NodeName,
    string NodePath,
    Matrix4x4 WorldTransform,
    CabinetSemanticGeometryKind SemanticKind,
    string? SemanticId,
    IReadOnlyList<GlbPrimitiveData> Primitives);

public sealed record GlbPrimitiveData(
    Vector3[] Positions,
    Vector3[] Normals,
    Vector2[] TexCoords0,
    int[] TriangleIndices,
    int MaterialIndex);

public enum GlbAlphaMode { Opaque, Mask, Blend }
public enum GlbTextureWrapMode { Repeat, ClampToEdge, MirroredRepeat }
public enum GlbTextureFilter { Default, Nearest, Linear, NearestMipmapNearest, LinearMipmapNearest, NearestMipmapLinear, LinearMipmapLinear }

public sealed record GlbTextureData(
    byte[] EncodedBytes,
    string MimeType,
    int TextureIndex,
    int ImageIndex,
    int TextureCoordinate,
    GlbTextureWrapMode WrapS,
    GlbTextureWrapMode WrapT,
    GlbTextureFilter MinFilter,
    GlbTextureFilter MagFilter);

public sealed record GlbMaterialData(
    int MaterialIndex,
    string Name,
    Vector4 BaseColorFactor,
    Vector3 EmissiveFactor,
    float MetallicFactor,
    float RoughnessFactor,
    GlbAlphaMode AlphaMode,
    float AlphaCutoff,
    bool DoubleSided,
    bool Unlit,
    GlbTextureData? BaseColorTexture,
    GlbTextureData? MetallicRoughnessTexture,
    GlbTextureData? NormalTexture,
    GlbTextureData? OcclusionTexture,
    GlbTextureData? EmissiveTexture);
