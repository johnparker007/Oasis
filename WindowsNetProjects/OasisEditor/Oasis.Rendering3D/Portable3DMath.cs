using System.Numerics;
using OasisEditor;
using OasisEditor.Features.CabinetEditor.Models;

namespace Oasis.Rendering3D;

/// <summary>Portable equivalents of the WPF authoring transforms, using System.Numerics row-vector matrices.</summary>
public static class Portable3DTransform
{
    public static Matrix4x4 CreatePlacement(MachineObjectTransform placement) =>
        Matrix4x4.CreateScale(ToVector3(placement.Scale))
        * Rotation(placement.Rotation)
        * Matrix4x4.CreateTranslation(ToVector3(placement.Position));

    public static Matrix4x4 CreateNodeTransform(Object3DNodeDefinition node) => CreatePlacement(node.Transform);

    public static Matrix4x4 CreateIntrinsic(double scale, string upAxis) => Matrix4x4.CreateScale((float)scale) * AxisCorrection(upAxis);

    public static Matrix4x4 CreateIntrinsic(Object3DModelDefinition model) =>
        Matrix4x4.CreateScale((float)model.Scale) * AxisCorrection(model.UpAxis);

    public static Matrix4x4 CreateIntrinsic(CabinetModelReference model) =>
        Matrix4x4.CreateScale((float)model.Scale) * AxisCorrection(model.UpAxis);

    /// <summary>Applies authored Z-X-Y Euler placement after the model's intrinsic scale and up-axis correction.</summary>
    public static Matrix4x4 CreateModelPlacement(MachineObjectTransform placement, Object3DModelDefinition model) =>
        CreateIntrinsic(model) * CreatePlacement(placement);

    public static Matrix4x4 Rotation(MachineVector3 eulerDegrees)
    {
        var radians = MathF.PI / 180f;
        return Matrix4x4.CreateRotationZ((float)eulerDegrees.Z * radians)
            * Matrix4x4.CreateRotationX((float)eulerDegrees.X * radians)
            * Matrix4x4.CreateRotationY((float)eulerDegrees.Y * radians);
    }

    public static Vector3 ToVector3(MachineVector3 value) => new((float)value.X, (float)value.Y, (float)value.Z);

    public static Matrix4x4 AxisCorrection(string? upAxis) => upAxis switch
    {
        "Z" => Matrix4x4.CreateRotationX(-MathF.PI / 2f),
        "X" => Matrix4x4.CreateRotationZ(MathF.PI / 2f),
        _ => Matrix4x4.Identity
    };
}

/// <summary>Renderer-neutral, oriented quad geometry for one assigned Cabinet Face target.</summary>
public sealed record PortableFacePlaneGeometry(
    string OwnerId,
    string TargetId,
    string FaceAssetPath,
    CabinetFaceTarget SourceTarget,
    CabinetSurfaceTargetSettings Settings,
    Matrix4x4 IntrinsicTransform,
    Vector3[] Positions,
    Vector2[] TextureCoordinates,
    int[] TriangleIndices,
    Vector3 Normal);

/// <summary>Preserves the WPF face preview's corner order, UVs, and front-side triangle winding.</summary>
public static class PortableFacePlaneGeometryBuilder
{
    private static readonly Vector2[] Uvs = [new(0, 0), new(1, 0), new(1, 1), new(0, 1)];

    public static bool TryCreate(
        CabinetFaceTarget target,
        CabinetSurfaceTargetSettings settings,
        string faceAssetPath,
        Matrix4x4 intrinsicTransform,
        out PortableFacePlaneGeometry geometry,
        string ownerId = "cabinet")
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(faceAssetPath);
        geometry = default!;
        if (!target.IsValid || target.Corners.Count != 4) return false;

        var worldCorners = target.Corners.Select(point => Vector3.Transform(point, intrinsicTransform)).ToArray();
        var rotation = CabinetSurfaceTargetSettings.NormalizeFaceRotation(settings.FaceRotation);
        var cornerOrder = rotation switch
        {
            90 => new[] { 3, 0, 1, 2 },
            180 => new[] { 2, 3, 0, 1 },
            270 => new[] { 1, 2, 3, 0 },
            _ => new[] { 0, 1, 2, 3 }
        };
        if (settings.FaceFlipHorizontal)
            cornerOrder = [cornerOrder[1], cornerOrder[0], cornerOrder[3], cornerOrder[2]];

        var positions = cornerOrder.Select(index => worldCorners[index]).ToArray();
        var inverted = CabinetSurfaceTargetSettings.NormalizeFrontSide(settings.FrontSide) == CabinetSurfaceTargetSettings.InvertedFrontSide;
        var reverseWinding = settings.FaceFlipHorizontal ^ inverted;
        var triangles = reverseWinding ? new[] { 0, 2, 1, 0, 3, 2 } : new[] { 0, 1, 2, 0, 2, 3 };
        var triangleA = triangles[0];
        var triangleB = triangles[1];
        var triangleC = triangles[2];
        var normal = Vector3.Cross(positions[triangleB] - positions[triangleA], positions[triangleC] - positions[triangleA]);
        if (normal.LengthSquared() <= 1e-12f) return false;
        normal = Vector3.Normalize(normal);
        geometry = new PortableFacePlaneGeometry(ownerId, target.Id, faceAssetPath, target, settings.Normalized(), intrinsicTransform, positions, Uvs.ToArray(), triangles, normal);
        return true;
    }
}
