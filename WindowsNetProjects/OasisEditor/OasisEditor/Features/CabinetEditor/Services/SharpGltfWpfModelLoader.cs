using System.IO;
using System.Diagnostics;
using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using SharpGLTF.Schema2;

namespace OasisEditor.Features.CabinetEditor.Services;

public sealed class SharpGltfWpfModelLoader : ICabinetModelLoader
{
    private static readonly DiffuseMaterial DefaultMaterial = CreateDefaultMaterial();
    private static readonly ICabinetFaceTargetDetector FaceTargetDetector = new GlbCabinetFaceTargetDetector();

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

            var materialInfos = GlbMaterialInfoCollection.ReadFrom(modelPath);
            var modelRoot = ModelRoot.Load(modelPath);
            var scene = modelRoot.DefaultScene ?? modelRoot.LogicalScenes.FirstOrDefault();
            if (scene is null)
            {
                return CabinetModelLoadResult.Failure("The .glb loaded, but it did not contain a scene to display.");
            }

            var faceTargets = FaceTargetDetector.DetectTargets(modelPath, cancellationToken);
            var reflectionTargets = GlbCabinetReflectionReceiverDiscovery.Discover(modelPath);
            var visualMeshes = new Dictionary<int, MeshGeometry3D>();
            var colliderMesh = new MeshGeometry3D();
            var triggerMesh = new MeshGeometry3D();
            var triangleCount = 0;
            foreach (var node in scene.VisualChildren)
            {
                AddNodeGeometry(node, visualMeshes, colliderMesh, triggerMesh, ref triangleCount, cancellationToken);
            }

            if (triangleCount == 0)
            {
                return CabinetModelLoadResult.Failure("The .glb loaded, but it did not contain displayable triangle geometry.");
            }

            var group = new Model3DGroup();
            foreach (var (materialIndex, mesh) in visualMeshes)
            {
                mesh.Freeze();
                var materialInfo = materialInfos.GetMaterialInfo(materialIndex);
                var wpfMaterial = CreateMaterial(materialInfo);
                var model = new GeometryModel3D
                {
                    Geometry = mesh,
                    Material = wpfMaterial,
                    BackMaterial = materialInfo.DoubleSided ? wpfMaterial : null
                };
                model.Freeze();
                group.Children.Add(model);
            }

            var colliderGroup = CreateDiagnosticGroup(colliderMesh, Color.FromArgb(105, 40, 170, 255));
            var triggerGroup = CreateDiagnosticGroup(triggerMesh, Color.FromArgb(105, 255, 90, 185));
            group.Freeze();
            return CabinetModelLoadResult.Success(group, faceTargets, reflectionTargets, colliderGroup, triggerGroup);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Trace.TraceError($"Cabinet GLB loader stage=SharpGLTF path='{modelPath}' exception={ex}");
            return CabinetModelLoadResult.Failure($"SharpGLTF fallback failed ({ex.GetType().Name}): {ex.Message}");
        }
    }

    private static void AddNodeGeometry(Node node, Dictionary<int, MeshGeometry3D> visualMeshes, MeshGeometry3D colliderMesh, MeshGeometry3D triggerMesh, ref int triangleCount, CancellationToken cancellationToken)
    {
        if (node.Mesh is not null)
        {
            var kind = CabinetSemanticGeometry.Classify(node.Name, node.Mesh.Name);
            if (kind != CabinetSemanticGeometryKind.FaceTarget)
            {
                foreach (var primitive in node.Mesh.Primitives)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var positions = primitive.GetVertexAccessor("POSITION")?.AsVector3Array()
                        ?? throw new InvalidDataException($"Mesh '{node.Mesh.Name ?? node.Name}' has a primitive without POSITION.");
                    var normals = primitive.GetVertexAccessor("NORMAL")?.AsVector3Array();
                    var textureCoordinates = primitive.GetVertexAccessor("TEXCOORD_0")?.AsVector2Array();
                    var target = kind switch
                    {
                        CabinetSemanticGeometryKind.Collider => colliderMesh,
                        CabinetSemanticGeometryKind.Trigger => triggerMesh,
                        _ => GetMeshForMaterial(visualMeshes, GetLogicalIndex(primitive.Material))
                    };
                    foreach (var (a, b, c) in primitive.GetTriangleIndices())
                    {
                        ValidateTriangleIndex(a, positions.Count, node);
                        ValidateTriangleIndex(b, positions.Count, node);
                        ValidateTriangleIndex(c, positions.Count, node);
                        var pointA = TransformPoint(positions[a], node.WorldMatrix);
                        var pointB = TransformPoint(positions[b], node.WorldMatrix);
                        var pointC = TransformPoint(positions[c], node.WorldMatrix);
                        var fallbackNormal = CalculateNormal(pointA, pointB, pointC);
                        AddTriangleVertex(target, a, positions, normals, textureCoordinates, node.WorldMatrix, fallbackNormal);
                        AddTriangleVertex(target, b, positions, normals, textureCoordinates, node.WorldMatrix, fallbackNormal);
                        AddTriangleVertex(target, c, positions, normals, textureCoordinates, node.WorldMatrix, fallbackNormal);
                        triangleCount++;
                    }
                }
            }
        }
        foreach (var child in node.VisualChildren) AddNodeGeometry(child, visualMeshes, colliderMesh, triggerMesh, ref triangleCount, cancellationToken);
    }

    private static void ValidateTriangleIndex(int index, int count, Node node)
    {
        if (index < 0 || index >= count) throw new InvalidDataException($"Mesh '{node.Mesh?.Name ?? node.Name}' contains triangle index {index} outside its {count} POSITION values.");
    }

    private static MeshGeometry3D GetMeshForMaterial(Dictionary<int, MeshGeometry3D> meshes, int materialIndex)
    {
        if (!meshes.TryGetValue(materialIndex, out var mesh))
        {
            mesh = new MeshGeometry3D();
            meshes.Add(materialIndex, mesh);
        }

        return mesh;
    }

    private static Point3D TransformPoint(Vector3 position, Matrix4x4 transform)
    {
        var transformed = Vector3.Transform(position, transform);
        return new Point3D(transformed.X, transformed.Y, transformed.Z);
    }

    private static void AddTriangleVertex(MeshGeometry3D mesh, int sourceIndex, IList<Vector3> positions, IList<Vector3>? normals, IList<Vector2>? textureCoordinates, Matrix4x4 transform, Vector3D fallbackNormal)
    {
        var index = mesh.Positions.Count;
        mesh.Positions.Add(TransformPoint(positions[sourceIndex], transform));
        mesh.Normals.Add(TryGetNormal(normals, sourceIndex, transform, out var normal) ? normal : fallbackNormal);
        var uv = textureCoordinates is not null && sourceIndex < textureCoordinates.Count ? textureCoordinates[sourceIndex] : Vector2.Zero;
        mesh.TextureCoordinates.Add(new Point(uv.X, uv.Y));
        mesh.TriangleIndices.Add(index);
    }

    private static bool TryGetNormal(IList<Vector3>? normals, int index, Matrix4x4 transform, out Vector3D normal)
    {
        if (normals is not null && index < normals.Count && normals[index].LengthSquared() > float.Epsilon)
        {
            var normalTransform = Matrix4x4.Invert(transform, out var inverse)
                ? Matrix4x4.Transpose(inverse)
                : transform;
            var sourceNormal = Vector3.TransformNormal(normals[index], normalTransform);
            normal = new Vector3D(sourceNormal.X, sourceNormal.Y, sourceNormal.Z);
            normal.Normalize();
            return true;
        }

        normal = default;
        return false;
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

    private static int GetLogicalIndex(SharpGLTF.Schema2.Material? material)
    {
        if (material is null) return -1;
        try { return material.LogicalIndex; }
        catch { return -1; }
    }

    private static System.Windows.Media.Media3D.Material CreateMaterial(GlbMaterialInfo info)
    {
        try
        {
            Brush brush = info.BaseColorTexture is null
                ? new SolidColorBrush(info.BaseColor)
                : new ImageBrush(info.BaseColorTexture) { ViewportUnits = BrushMappingMode.Absolute, TileMode = TileMode.None, Stretch = Stretch.Fill };
            brush.Opacity = info.AlphaMode == GlbAlphaMode.Opaque ? 1d : info.BaseColor.A / 255d;
            brush.Freeze();
            var material = new DiffuseMaterial(brush);
            material.Freeze();
            return material;
        }
        catch
        {
            return DefaultMaterial;
        }
    }

    private static DiffuseMaterial CreateDefaultMaterial()
    {
        var material = new DiffuseMaterial(new SolidColorBrush(Color.FromRgb(192, 196, 204)));
        material.Freeze();
        return material;
    }

    private sealed record GlbMaterialInfo(Color BaseColor, ImageSource? BaseColorTexture, GlbAlphaMode AlphaMode, bool DoubleSided)
    {
        public static GlbMaterialInfo Default { get; } = new(Color.FromRgb(192, 196, 204), null, GlbAlphaMode.Opaque, true);
    }

    private enum GlbAlphaMode { Opaque, Mask, Blend }

    private sealed class GlbMaterialInfoCollection
    {
        private readonly Dictionary<int, GlbMaterialInfo> _materials;
        public GlbMaterialInfoCollection(Dictionary<int, GlbMaterialInfo> materials) => _materials = materials;
        public GlbMaterialInfo GetMaterialInfo(int index) => _materials.TryGetValue(index, out var info) ? info : GlbMaterialInfo.Default;

        public static GlbMaterialInfoCollection ReadFrom(string modelPath)
        {
            try
            {
                var glb = ReadGlb(modelPath);
                using var document = JsonDocument.Parse(glb.Json);
                var root = document.RootElement;
                var result = new Dictionary<int, GlbMaterialInfo>();
                if (!root.TryGetProperty("materials", out var materials) || materials.ValueKind != JsonValueKind.Array)
                {
                    return new GlbMaterialInfoCollection(result);
                }

                for (var i = 0; i < materials.GetArrayLength(); i++)
                {
                    var material = materials[i];
                    var baseColor = ReadBaseColor(material);
                    var texture = ReadBaseColorTexture(root, glb.BinaryChunk, material);
                    var alphaMode = ReadAlphaMode(material);
                    var doubleSided = material.TryGetProperty("doubleSided", out var doubleSidedElement) && doubleSidedElement.GetBoolean();
                    result[i] = new GlbMaterialInfo(baseColor, texture, alphaMode, doubleSided);
                }

                return new GlbMaterialInfoCollection(result);
            }
            catch
            {
                return new GlbMaterialInfoCollection(new Dictionary<int, GlbMaterialInfo>());
            }
        }

        private static Color ReadBaseColor(JsonElement material)
        {
            if (material.TryGetProperty("pbrMetallicRoughness", out var pbr)
                && pbr.TryGetProperty("baseColorFactor", out var factor)
                && factor.ValueKind == JsonValueKind.Array
                && factor.GetArrayLength() >= 4)
            {
                return Color.FromArgb(ToByte(factor[3]), ToByte(factor[0]), ToByte(factor[1]), ToByte(factor[2]));
            }

            return GlbMaterialInfo.Default.BaseColor;
        }

        private static GlbAlphaMode ReadAlphaMode(JsonElement material)
        {
            if (!material.TryGetProperty("alphaMode", out var alphaMode)) return GlbAlphaMode.Opaque;
            return alphaMode.GetString() switch
            {
                "MASK" => GlbAlphaMode.Mask,
                "BLEND" => GlbAlphaMode.Blend,
                _ => GlbAlphaMode.Opaque
            };
        }

        private static BitmapImage? ReadBaseColorTexture(JsonElement root, byte[] binaryChunk, JsonElement material)
        {
            if (!material.TryGetProperty("pbrMetallicRoughness", out var pbr)
                || !pbr.TryGetProperty("baseColorTexture", out var baseColorTexture)
                || !baseColorTexture.TryGetProperty("index", out var textureIndexElement)) return null;

            var textureIndex = textureIndexElement.GetInt32();
            if (!TryGetArrayItem(root, "textures", textureIndex, out var texture)
                || !texture.TryGetProperty("source", out var sourceIndexElement)) return null;

            var sourceIndex = sourceIndexElement.GetInt32();
            if (!TryGetArrayItem(root, "images", sourceIndex, out var image)
                || !image.TryGetProperty("bufferView", out var bufferViewIndexElement)) return null;

            var bufferViewIndex = bufferViewIndexElement.GetInt32();
            if (!TryGetArrayItem(root, "bufferViews", bufferViewIndex, out var bufferView)) return null;

            var offset = bufferView.TryGetProperty("byteOffset", out var offsetElement) ? offsetElement.GetInt32() : 0;
            var length = bufferView.GetProperty("byteLength").GetInt32();
            if (offset < 0 || length <= 0 || offset + length > binaryChunk.Length) return null;

            using var stream = new MemoryStream(binaryChunk, offset, length, writable: false);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }

        private static bool TryGetArrayItem(JsonElement root, string propertyName, int index, out JsonElement item)
        {
            item = default;
            if (!root.TryGetProperty(propertyName, out var array) || array.ValueKind != JsonValueKind.Array || index < 0 || index >= array.GetArrayLength()) return false;
            item = array[index];
            return true;
        }

        private static byte ToByte(JsonElement value) => (byte)Math.Clamp((int)Math.Round(value.GetSingle() * 255f), 0, 255);

        private static (string Json, byte[] BinaryChunk) ReadGlb(string modelPath)
        {
            using var reader = new BinaryReader(File.OpenRead(modelPath));
            if (reader.ReadUInt32() != 0x46546C67) throw new InvalidDataException("Not a GLB file.");
            reader.ReadUInt32();
            reader.ReadUInt32();

            var jsonLength = reader.ReadInt32();
            if (reader.ReadUInt32() != 0x4E4F534A) throw new InvalidDataException("GLB JSON chunk missing.");
            var json = Encoding.UTF8.GetString(reader.ReadBytes(jsonLength));

            if (reader.BaseStream.Position >= reader.BaseStream.Length) return (json, Array.Empty<byte>());
            var binaryLength = reader.ReadInt32();
            var chunkType = reader.ReadUInt32();
            var binary = chunkType == 0x004E4942 ? reader.ReadBytes(binaryLength) : Array.Empty<byte>();
            return (json, binary);
        }
    }
}
