using System.Globalization;
using System.IO;
using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Windows.Media.Media3D;
using OasisEditor.Features.CabinetEditor.Models;

namespace OasisEditor.Features.CabinetEditor.Services;

public sealed class GlbCabinetFaceTargetDetector : ICabinetFaceTargetDetector
{
    private const string TargetPrefix = "OasisFace_";

    public IReadOnlyList<CabinetFaceTarget> DetectTargets(string modelPath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(modelPath)) return Array.Empty<CabinetFaceTarget>();

        var glb = ReadGlb(modelPath);
        using var document = JsonDocument.Parse(glb.Json);
        var root = document.RootElement;
        if (!root.TryGetProperty("nodes", out var nodes) || nodes.ValueKind != JsonValueKind.Array) return Array.Empty<CabinetFaceTarget>();

        var sceneNodeIndexes = GetSceneNodeIndexes(root);
        var transforms = new Dictionary<int, Matrix4x4>();
        foreach (var nodeIndex in sceneNodeIndexes)
        {
            WalkNodeTransforms(nodes, nodeIndex, Matrix4x4.Identity, transforms, cancellationToken);
        }

        var targets = new List<CabinetFaceTarget>();
        foreach (var (nodeIndex, transform) in transforms.OrderBy(x => x.Key))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var node = nodes[nodeIndex];
            var nodeName = node.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : null;
            var meshIndex = node.TryGetProperty("mesh", out var meshElement) ? meshElement.GetInt32() : -1;
            var meshName = TryGetMeshName(root, meshIndex);
            var sourceName = IsTargetName(nodeName) ? nodeName! : IsTargetName(meshName) ? meshName! : null;
            if (sourceName is null) continue;

            targets.Add(ExtractTarget(root, glb.BinaryChunk, meshIndex, transform, sourceName));
        }

        return targets;
    }

    private static CabinetFaceTarget ExtractTarget(JsonElement root, byte[] binaryChunk, int meshIndex, Matrix4x4 transform, string sourceName)
    {
        var id = CreateStableId(sourceName);
        var displayName = CreateDisplayName(sourceName);
        try
        {
            if (meshIndex < 0 || !TryGetArrayItem(root, "meshes", meshIndex, out var mesh))
            {
                return Invalid(id, sourceName, displayName, "Target node does not reference a mesh.");
            }

            var points = new List<Point3D>();
            if (mesh.TryGetProperty("primitives", out var primitives) && primitives.ValueKind == JsonValueKind.Array)
            {
                foreach (var primitive in primitives.EnumerateArray())
                {
                    if (!primitive.TryGetProperty("attributes", out var attributes)
                        || !attributes.TryGetProperty("POSITION", out var positionAccessorElement)) continue;

                    var localPositions = ReadVector3Accessor(root, binaryChunk, positionAccessorElement.GetInt32());
                    var indexes = primitive.TryGetProperty("indices", out var indicesElement)
                        ? ReadIndexAccessor(root, binaryChunk, indicesElement.GetInt32())
                        : Enumerable.Range(0, localPositions.Count).ToArray();

                    foreach (var index in indexes)
                    {
                        if (index < 0 || index >= localPositions.Count) continue;
                        var transformed = Vector3.Transform(localPositions[index], transform);
                        AddUnique(points, new Point3D(transformed.X, transformed.Y, transformed.Z));
                    }
                }
            }

            if (points.Count != 4)
            {
                return Invalid(id, sourceName, displayName, $"Expected 4 unique quad corners, found {points.Count}.");
            }

            var center = new Point3D(points.Average(p => p.X), points.Average(p => p.Y), points.Average(p => p.Z));
            var ordered = OrderCorners(points, center);
            var normal = Vector3D.CrossProduct(ordered[1] - ordered[0], ordered[2] - ordered[1]);
            if (normal.LengthSquared <= 1e-12)
            {
                return Invalid(id, sourceName, displayName, "Quad corners are degenerate.");
            }

            normal.Normalize();
            if (!IsPlanar(ordered, normal, center))
            {
                return Invalid(id, sourceName, displayName, "Quad corners are not planar.");
            }

            return new CabinetFaceTarget(id, sourceName, displayName, ordered, normal, center, true, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Invalid(id, sourceName, displayName, $"Target geometry could not be read: {ex.Message}");
        }
    }

    private static IReadOnlyList<Point3D> OrderCorners(IReadOnlyList<Point3D> points, Point3D center)
    {
        var normal = Vector3D.CrossProduct(points[1] - points[0], points[2] - points[0]);
        if (normal.LengthSquared <= 1e-12) normal = new Vector3D(0, 0, 1);
        normal.Normalize();
        var right = points.OrderByDescending(p => (p - center).LengthSquared).First() - center;
        if (right.LengthSquared <= 1e-12) right = new Vector3D(1, 0, 0);
        right.Normalize();
        var up = Vector3D.CrossProduct(normal, right);
        if (up.LengthSquared <= 1e-12) up = new Vector3D(0, 1, 0);
        up.Normalize();

        return points
            .Select(p => new { Point = p, Angle = Math.Atan2(Vector3D.DotProduct(p - center, up), Vector3D.DotProduct(p - center, right)) })
            .OrderByDescending(x => x.Angle)
            .Select(x => x.Point)
            .ToArray();
    }

    private static bool IsPlanar(IEnumerable<Point3D> points, Vector3D normal, Point3D center)
        => points.All(p => Math.Abs(Vector3D.DotProduct(p - center, normal)) <= 0.001d);

    private static void AddUnique(ICollection<Point3D> points, Point3D point)
    {
        if (!points.Any(existing => (existing - point).LengthSquared <= 1e-10)) points.Add(point);
    }

    private static List<Vector3> ReadVector3Accessor(JsonElement root, byte[] binaryChunk, int accessorIndex)
    {
        var accessor = GetAccessor(root, accessorIndex, out var bufferView, out var byteOffset, out var stride);
        if (accessor.GetProperty("type").GetString() != "VEC3" || accessor.GetProperty("componentType").GetInt32() != 5126)
        {
            throw new InvalidDataException("POSITION accessor must be float VEC3.");
        }

        var count = accessor.GetProperty("count").GetInt32();
        var result = new List<Vector3>(count);
        for (var i = 0; i < count; i++)
        {
            var offset = byteOffset + i * stride;
            result.Add(new Vector3(ReadSingle(binaryChunk, offset), ReadSingle(binaryChunk, offset + 4), ReadSingle(binaryChunk, offset + 8)));
        }

        return result;
    }

    private static int[] ReadIndexAccessor(JsonElement root, byte[] binaryChunk, int accessorIndex)
    {
        var accessor = GetAccessor(root, accessorIndex, out _, out var byteOffset, out var stride);
        var count = accessor.GetProperty("count").GetInt32();
        var componentType = accessor.GetProperty("componentType").GetInt32();
        var result = new int[count];
        for (var i = 0; i < count; i++)
        {
            var offset = byteOffset + i * stride;
            result[i] = componentType switch
            {
                5121 => binaryChunk[offset],
                5123 => BitConverter.ToUInt16(binaryChunk, offset),
                5125 => unchecked((int)BitConverter.ToUInt32(binaryChunk, offset)),
                _ => throw new InvalidDataException("Unsupported index component type.")
            };
        }

        return result;
    }

    private static JsonElement GetAccessor(JsonElement root, int accessorIndex, out JsonElement bufferView, out int byteOffset, out int stride)
    {
        if (!TryGetArrayItem(root, "accessors", accessorIndex, out var accessor)) throw new InvalidDataException("Accessor missing.");
        var bufferViewIndex = accessor.GetProperty("bufferView").GetInt32();
        if (!TryGetArrayItem(root, "bufferViews", bufferViewIndex, out bufferView)) throw new InvalidDataException("Buffer view missing.");
        byteOffset = (accessor.TryGetProperty("byteOffset", out var ao) ? ao.GetInt32() : 0)
            + (bufferView.TryGetProperty("byteOffset", out var vo) ? vo.GetInt32() : 0);
        stride = bufferView.TryGetProperty("byteStride", out var strideElement) ? strideElement.GetInt32() : GetElementSize(accessor);
        return accessor;
    }

    private static int GetElementSize(JsonElement accessor)
    {
        var componentSize = accessor.GetProperty("componentType").GetInt32() switch { 5121 => 1, 5123 => 2, 5125 or 5126 => 4, _ => 4 };
        var componentCount = accessor.GetProperty("type").GetString() switch { "SCALAR" => 1, "VEC2" => 2, "VEC3" => 3, "VEC4" => 4, _ => 1 };
        return componentSize * componentCount;
    }

    private static void WalkNodeTransforms(JsonElement nodes, int nodeIndex, Matrix4x4 parentTransform, IDictionary<int, Matrix4x4> transforms, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (nodeIndex < 0 || nodeIndex >= nodes.GetArrayLength()) return;
        var node = nodes[nodeIndex];
        var world = ReadLocalTransform(node) * parentTransform;
        transforms[nodeIndex] = world;
        if (!node.TryGetProperty("children", out var children) || children.ValueKind != JsonValueKind.Array) return;
        foreach (var child in children.EnumerateArray()) WalkNodeTransforms(nodes, child.GetInt32(), world, transforms, cancellationToken);
    }

    private static Matrix4x4 ReadLocalTransform(JsonElement node)
    {
        if (node.TryGetProperty("matrix", out var matrix) && matrix.ValueKind == JsonValueKind.Array && matrix.GetArrayLength() == 16)
        {
            var m = matrix.EnumerateArray().Select(e => e.GetSingle()).ToArray();
            return new Matrix4x4(m[0], m[1], m[2], m[3], m[4], m[5], m[6], m[7], m[8], m[9], m[10], m[11], m[12], m[13], m[14], m[15]);
        }

        var scale = ReadVector3(node, "scale", new Vector3(1, 1, 1));
        var rotation = ReadQuaternion(node, "rotation", Quaternion.Identity);
        var translation = ReadVector3(node, "translation", Vector3.Zero);
        return Matrix4x4.CreateScale(scale) * Matrix4x4.CreateFromQuaternion(rotation) * Matrix4x4.CreateTranslation(translation);
    }

    private static IEnumerable<int> GetSceneNodeIndexes(JsonElement root)
    {
        if (root.TryGetProperty("scene", out var sceneIndexElement) && TryGetArrayItem(root, "scenes", sceneIndexElement.GetInt32(), out var scene))
        {
            return ReadSceneNodes(scene);
        }

        if (TryGetArrayItem(root, "scenes", 0, out var firstScene)) return ReadSceneNodes(firstScene);
        return Enumerable.Empty<int>();
    }

    private static IEnumerable<int> ReadSceneNodes(JsonElement scene)
        => scene.TryGetProperty("nodes", out var sceneNodes) && sceneNodes.ValueKind == JsonValueKind.Array
            ? sceneNodes.EnumerateArray().Select(x => x.GetInt32()).ToArray()
            : Array.Empty<int>();

    private static string? TryGetMeshName(JsonElement root, int meshIndex)
        => TryGetArrayItem(root, "meshes", meshIndex, out var mesh) && mesh.TryGetProperty("name", out var name) ? name.GetString() : null;

    private static bool IsTargetName(string? name) => name?.StartsWith(TargetPrefix, StringComparison.Ordinal) == true;
    private static CabinetFaceTarget Invalid(string id, string sourceName, string displayName, string message) => new(id, sourceName, displayName, Array.Empty<Point3D>(), default, default, false, message);

    private static string CreateStableId(string sourceName)
    {
        var suffix = sourceName.StartsWith(TargetPrefix, StringComparison.Ordinal) ? sourceName[TargetPrefix.Length..] : sourceName;
        var chars = suffix.Where(char.IsLetterOrDigit).ToArray();
        if (chars.Length == 0) return "target";
        var text = new string(chars);
        return char.ToLowerInvariant(text[0]) + text[1..];
    }

    private static string CreateDisplayName(string sourceName)
    {
        var suffix = sourceName.StartsWith(TargetPrefix, StringComparison.Ordinal) ? sourceName[TargetPrefix.Length..] : sourceName;
        var builder = new StringBuilder();
        for (var i = 0; i < suffix.Length; i++)
        {
            var c = suffix[i] is '_' or '-' ? ' ' : suffix[i];
            if (i > 0 && char.IsUpper(c) && char.IsLower(suffix[i - 1])) builder.Append(' ');
            builder.Append(c);
        }
        return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(builder.ToString().Trim());
    }

    private static Vector3 ReadVector3(JsonElement node, string propertyName, Vector3 fallback)
    {
        if (!node.TryGetProperty(propertyName, out var array) || array.ValueKind != JsonValueKind.Array || array.GetArrayLength() < 3) return fallback;
        return new Vector3(array[0].GetSingle(), array[1].GetSingle(), array[2].GetSingle());
    }

    private static Quaternion ReadQuaternion(JsonElement node, string propertyName, Quaternion fallback)
    {
        if (!node.TryGetProperty(propertyName, out var array) || array.ValueKind != JsonValueKind.Array || array.GetArrayLength() < 4) return fallback;
        return new Quaternion(array[0].GetSingle(), array[1].GetSingle(), array[2].GetSingle(), array[3].GetSingle());
    }

    private static bool TryGetArrayItem(JsonElement root, string propertyName, int index, out JsonElement item)
    {
        item = default;
        if (!root.TryGetProperty(propertyName, out var array) || array.ValueKind != JsonValueKind.Array || index < 0 || index >= array.GetArrayLength()) return false;
        item = array[index];
        return true;
    }

    private static float ReadSingle(byte[] data, int offset) => BitConverter.ToSingle(data, offset);

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
