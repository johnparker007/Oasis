using System.IO;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using SharpGLTF.Geometry;
using SharpGLTF.Geometry.VertexTypes;
using SharpGLTF.Schema2;

namespace OasisEditor.Features.CabinetEditor.Services;

public sealed class SharpGltfCabinetModelLoader : ICabinetModelLoader
{
    private static readonly DiffuseMaterial DefaultMaterial = CreateDefaultMaterial();

    public Task<CabinetModelLoadResult> LoadAsync(string modelPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(modelPath))
        {
            return Task.FromResult(CabinetModelLoadResult.Failure("Choose a .glb cabinet model to load."));
        }

        if (!File.Exists(modelPath))
        {
            return Task.FromResult(CabinetModelLoadResult.Failure($"Cabinet model file was not found: {modelPath}"));
        }

        if (!string.Equals(Path.GetExtension(modelPath), ".glb", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(CabinetModelLoadResult.Failure("Cabinet Model Viewer currently supports .glb files only."));
        }

        return Task.Run(() => LoadModel(modelPath, cancellationToken), cancellationToken);
    }

    private static CabinetModelLoadResult LoadModel(string modelPath, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var modelRoot = ModelRoot.Load(modelPath);
            var scene = modelRoot.DefaultScene ?? modelRoot.LogicalScenes.FirstOrDefault();
            if (scene is null)
            {
                return CabinetModelLoadResult.Failure("The .glb loaded, but it did not contain a scene to display.");
            }

            var mesh = new MeshGeometry3D();
            var triangleCount = 0;
            foreach (var triangle in scene.EvaluateTriangles())
            {
                cancellationToken.ThrowIfCancellationRequested();

                var pointA = ToPoint3D(triangle.A);
                var pointB = ToPoint3D(triangle.B);
                var pointC = ToPoint3D(triangle.C);
                var normal = CalculateNormal(pointA, pointB, pointC);

                AddTriangleVertex(mesh, pointA, normal);
                AddTriangleVertex(mesh, pointB, normal);
                AddTriangleVertex(mesh, pointC, normal);

                triangleCount++;
            }

            if (triangleCount == 0)
            {
                return CabinetModelLoadResult.Failure("The .glb loaded, but it did not contain displayable triangle geometry.");
            }

            mesh.Freeze();

            var model = new GeometryModel3D
            {
                Geometry = mesh,
                Material = DefaultMaterial,
                BackMaterial = DefaultMaterial
            };
            model.Freeze();

            var group = new Model3DGroup();
            group.Children.Add(model);
            group.Freeze();

            return CabinetModelLoadResult.Success(group);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return CabinetModelLoadResult.Failure($"Unable to load cabinet model: {ex.Message}");
        }
    }

    private static Point3D ToPoint3D(IVertexBuilder vertex)
    {
        var position = vertex.GetGeometry().GetPosition();
        return new Point3D(position.X, position.Y, position.Z);
    }

    private static void AddTriangleVertex(MeshGeometry3D mesh, Point3D point, Vector3D normal)
    {
        var index = mesh.Positions.Count;
        mesh.Positions.Add(point);
        mesh.Normals.Add(normal);
        mesh.TriangleIndices.Add(index);
    }

    private static Vector3D CalculateNormal(Point3D a, Point3D b, Point3D c)
    {
        var normal = Vector3D.CrossProduct(b - a, c - a);
        if (normal.LengthSquared <= double.Epsilon)
        {
            return new Vector3D(0, 1, 0);
        }

        normal.Normalize();
        return normal;
    }

    private static DiffuseMaterial CreateDefaultMaterial()
    {
        var material = new DiffuseMaterial(new SolidColorBrush(Color.FromRgb(192, 196, 204)));
        material.Freeze();
        return material;
    }
}
