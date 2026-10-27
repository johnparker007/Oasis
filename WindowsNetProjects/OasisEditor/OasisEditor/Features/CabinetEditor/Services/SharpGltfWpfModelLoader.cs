using System.Diagnostics;
using System.IO;
using System.Numerics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using Oasis.Rendering3D;

namespace OasisEditor.Features.CabinetEditor.Services;

/// <summary>WPF projection adapter for the portable GLB scene data.</summary>
public sealed class SharpGltfWpfModelLoader : ICabinetModelLoader
{
    private readonly PortableGlbSceneLoader _sceneLoader = new();

    public async Task<CabinetModelLoadResult> LoadAsync(string modelPath, CancellationToken cancellationToken = default)
    {
        var scene = await _sceneLoader.LoadAsync(modelPath, cancellationToken).ConfigureAwait(false);
        if (!scene.Succeeded) return CabinetModelLoadResult.Failure(scene.ErrorMessage ?? "The GLB scene could not be loaded.");

        foreach (var diagnostic in scene.Diagnostics)
            Trace.TraceWarning($"Cabinet GLB preview: {diagnostic}");

        try
        {
            var materials = scene.Materials.ToDictionary(material => material.MaterialIndex, CreateMaterial);
            var visualMeshes = new Dictionary<int, MeshGeometry3D>();
            var colliderMesh = new MeshGeometry3D();
            var triggerMesh = new MeshGeometry3D();
            foreach (var node in scene.Meshes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                foreach (var primitive in node.Primitives)
                {
                    MeshGeometry3D target;
                    if (node.SemanticKind == CabinetSemanticGeometryKind.Collider)
                    {
                        target = colliderMesh;
                    }
                    else if (node.SemanticKind == CabinetSemanticGeometryKind.Trigger)
                    {
                        target = triggerMesh;
                    }
                    else if (visualMeshes.TryGetValue(primitive.MaterialIndex, out var visualMesh))
                    {
                        target = visualMesh;
                    }
                    else
                    {
                        target = new MeshGeometry3D();
                        visualMeshes.Add(primitive.MaterialIndex, target);
                    }
                    AppendPrimitive(target, primitive, node.WorldTransform);
                }
            }

            var visual = new Model3DGroup();
            foreach (var (materialIndex, mesh) in visualMeshes)
            {
                if (mesh.TriangleIndices.Count == 0) continue;
                mesh.Freeze();
                var material = materials.TryGetValue(materialIndex, out var found) ? found : CreateDefaultMaterial();
                visual.Children.Add(new GeometryModel3D
                {
                    Geometry = mesh,
                    Material = material.Value,
                    BackMaterial = material.DoubleSided ? material.Value : null
                });
            }
            visual.Freeze();

            var colliders = CreateDiagnosticGroup(colliderMesh, Color.FromArgb(105, 40, 170, 255));
            var triggers = CreateDiagnosticGroup(triggerMesh, Color.FromArgb(105, 255, 90, 185));
            return CabinetModelLoadResult.Success(visual, scene.FaceTargets, scene.ReflectionTargets, colliders, triggers);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Trace.TraceError($"Cabinet GLB WPF projection path='{modelPath}' exception={ex}");
            return CabinetModelLoadResult.Failure($"WPF GLB projection failed ({ex.GetType().Name}): {ex.Message}");
        }
    }

    private static void AppendPrimitive(MeshGeometry3D target, GlbPrimitiveData primitive, Matrix4x4 transform)
    {
        var normalTransform = Matrix4x4.Invert(transform, out var inverse) ? Matrix4x4.Transpose(inverse) : transform;
        for (var offset = 0; offset + 2 < primitive.TriangleIndices.Length; offset += 3)
        {
            var ia = primitive.TriangleIndices[offset];
            var ib = primitive.TriangleIndices[offset + 1];
            var ic = primitive.TriangleIndices[offset + 2];
            var a = Transform(primitive.Positions[ia], transform);
            var b = Transform(primitive.Positions[ib], transform);
            var c = Transform(primitive.Positions[ic], transform);
            var fallbackNormal = CalculateNormal(a, b, c);
            AppendVertex(target, ia, a, primitive, normalTransform, fallbackNormal);
            AppendVertex(target, ib, b, primitive, normalTransform, fallbackNormal);
            AppendVertex(target, ic, c, primitive, normalTransform, fallbackNormal);
        }
    }

    private static void AppendVertex(MeshGeometry3D mesh, int sourceIndex, Point3D position, GlbPrimitiveData primitive,
        Matrix4x4 normalTransform, Vector3D fallbackNormal)
    {
        var index = mesh.Positions.Count;
        mesh.Positions.Add(position);
        var normal = fallbackNormal;
        if (sourceIndex < primitive.Normals.Length && primitive.Normals[sourceIndex].LengthSquared() > float.Epsilon)
        {
            var transformed = Vector3.TransformNormal(primitive.Normals[sourceIndex], normalTransform);
            normal = new Vector3D(transformed.X, transformed.Y, transformed.Z);
            if (normal.LengthSquared > double.Epsilon) normal.Normalize();
        }
        mesh.Normals.Add(normal);
        var uv = sourceIndex < primitive.TexCoords0.Length ? primitive.TexCoords0[sourceIndex] : Vector2.Zero;
        mesh.TextureCoordinates.Add(new Point(uv.X, uv.Y));
        mesh.TriangleIndices.Add(index);
    }

    private static Point3D Transform(Vector3 value, Matrix4x4 transform)
    {
        var transformed = Vector3.Transform(value, transform);
        return new Point3D(transformed.X, transformed.Y, transformed.Z);
    }

    private static Vector3D CalculateNormal(Point3D a, Point3D b, Point3D c)
    {
        var normal = Vector3D.CrossProduct(b - a, c - a);
        if (normal.LengthSquared <= double.Epsilon) return new Vector3D(0, 1, 0);
        normal.Normalize();
        return normal;
    }

    private static Model3DGroup? CreateDiagnosticGroup(MeshGeometry3D mesh, Color color)
    {
        if (mesh.TriangleIndices.Count == 0) return null;
        mesh.Freeze();
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        var material = new DiffuseMaterial(brush);
        material.Freeze();
        var geometry = new GeometryModel3D(mesh, material) { BackMaterial = material };
        geometry.Freeze();
        var group = new Model3DGroup();
        group.Children.Add(geometry);
        group.Freeze();
        return group;
    }

    private static WpfMaterial CreateMaterial(GlbMaterialData info)
    {
        try
        {
            Brush brush = info.BaseColorTexture is null
                ? new SolidColorBrush(ToColor(info.BaseColorFactor))
                : new ImageBrush(LoadBitmap(info.BaseColorTexture.EncodedBytes))
                {
                    ViewportUnits = BrushMappingMode.Absolute,
                    TileMode = TileMode.None,
                    Stretch = Stretch.Fill
                };
            // Keep the existing WPF preview's transparency behavior. Alpha MASK cutoff is retained
            // in the portable material DTO for renderers that support cutout, but WPF has no cutout path.
            brush.Opacity = info.AlphaMode == GlbAlphaMode.Opaque ? 1d : Math.Clamp(info.BaseColorFactor.W, 0f, 1f);
            brush.Freeze();
            var material = new DiffuseMaterial(brush);
            material.Freeze();
            return new WpfMaterial(material, info.DoubleSided);
        }
        catch
        {
            var material = CreateDefaultMaterial();
            return new WpfMaterial(material.Value, info.DoubleSided);
        }
    }

    private static WpfMaterial CreateDefaultMaterial()
    {
        var brush = new SolidColorBrush(Color.FromRgb(192, 196, 204));
        brush.Freeze();
        var material = new DiffuseMaterial(brush);
        material.Freeze();
        return new WpfMaterial(material, true);
    }

    private static BitmapImage LoadBitmap(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes, writable: false);
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = stream;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    private static Color ToColor(Vector4 factor) => Color.FromArgb(ToByte(factor.W), ToByte(factor.X), ToByte(factor.Y), ToByte(factor.Z));
    private static byte ToByte(float value) => (byte)Math.Clamp((int)Math.Round(value * 255f), 0, 255);

    private sealed record WpfMaterial(DiffuseMaterial Value, bool DoubleSided);
}
