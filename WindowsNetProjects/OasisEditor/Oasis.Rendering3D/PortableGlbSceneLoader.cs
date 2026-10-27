using System.Numerics;
using System.Text.Json;
using OasisEditor.Features.CabinetEditor.Services;
using SharpGLTF.Schema2;

namespace Oasis.Rendering3D;

/// <summary>Loads cabinet and reusable Object3D GLB geometry without binding scene data to a UI renderer.</summary>
public sealed class PortableGlbSceneLoader
{
    private static readonly GlbMaterialData DefaultMaterial = new(
        -1, "Default", Vector4.One, Vector3.Zero, 1f, 1f, GlbAlphaMode.Opaque, 0.5f,
        true, false, null, null, null, null, null);

    private static readonly ICabinetFaceTargetDetector FaceTargetDetector = new GlbCabinetFaceTargetDetector();

    public Task<GlbSceneLoadResult> LoadAsync(string modelPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(modelPath))
            return Task.FromResult(GlbSceneLoadResult.Failure("Choose a .glb model to load."));
        if (!File.Exists(modelPath))
            return Task.FromResult(GlbSceneLoadResult.Failure($"GLB model file was not found: {modelPath}"));
        if (!string.Equals(Path.GetExtension(modelPath), ".glb", StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(GlbSceneLoadResult.Failure("Only .glb files are supported."));

        return Task.Run(() => Load(modelPath, cancellationToken), cancellationToken);
    }

    private static GlbSceneLoadResult Load(string modelPath, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            ValidateExternalResourcePaths(modelPath);
            var root = ModelRoot.Load(modelPath);
            var scene = root.DefaultScene ?? root.LogicalScenes.FirstOrDefault();
            if (scene is null) return GlbSceneLoadResult.Failure("The .glb loaded, but it did not contain a scene.");

            GlbCabinetSemanticGeometryValidator.Validate(scene, modelPath, cancellationToken);
            var meshes = new List<GlbMeshInstance>();
            var diagnostics = new SortedSet<string>(StringComparer.Ordinal);
            if (root.LogicalAnimations.Count > 0)
                diagnostics.Add("GLB animations are not evaluated in the static scene preview.");
            if (root.LogicalSkins.Count > 0)
                diagnostics.Add("GLB skinning is not evaluated in the static scene preview.");

            var bounds = BoundingBox3.Empty;
            foreach (var node in scene.VisualChildren)
                VisitNode(node, string.Empty, meshes, diagnostics, ref bounds, cancellationToken);

            var triangleCount = meshes.Sum(mesh => mesh.Primitives.Sum(primitive => primitive.TriangleIndices.Length / 3));
            if (triangleCount == 0)
                return GlbSceneLoadResult.Failure("The .glb loaded, but it did not contain displayable triangle geometry.");

            var materials = new List<GlbMaterialData> { DefaultMaterial };
            materials.AddRange(root.LogicalMaterials.Select(CreateMaterial));
            foreach (var material in materials.Where(material => material.MaterialIndex >= 0))
            {
                foreach (var texture in Textures(material).Where(texture => texture.TextureCoordinate != 0))
                    diagnostics.Add($"Material '{material.Name}' references TEXCOORD_{texture.TextureCoordinate}; only TEXCOORD_0 vertex data is exposed.");
            }
            var faceTargets = FaceTargetDetector.DetectTargets(modelPath, cancellationToken);
            var reflectionTargets = GlbCabinetReflectionReceiverDiscovery.Discover(scene);
            return new GlbSceneLoadResult(true, Path.GetFileNameWithoutExtension(modelPath), meshes, materials, bounds, faceTargets,
                reflectionTargets, diagnostics.ToArray(), null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return GlbSceneLoadResult.Failure($"GLB scene load failed ({ex.GetType().Name}): {ex.Message}");
        }
    }

    private static void VisitNode(Node node, string parentPath, List<GlbMeshInstance> meshes,
        SortedSet<string> diagnostics, ref BoundingBox3 bounds, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var nodeName = string.IsNullOrWhiteSpace(node.Name) ? $"Node{node.LogicalIndex}" : node.Name;
        var path = string.IsNullOrEmpty(parentPath) ? nodeName : parentPath + "/" + nodeName;
        if (node.Mesh is not null)
        {
            var semanticKind = CabinetSemanticGeometry.Classify(node.Name, node.Mesh.Name);
            if (semanticKind != CabinetSemanticGeometryKind.FaceTarget)
            {
                var primitives = new List<GlbPrimitiveData>();
                foreach (var primitive in node.Mesh.Primitives)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var positions = primitive.GetVertexAccessor("POSITION")?.AsVector3Array()
                        ?? throw new InvalidDataException($"Mesh '{node.Mesh.Name ?? nodeName}' has a primitive without POSITION.");
                    var normalValues = primitive.GetVertexAccessor("NORMAL")?.AsVector3Array();
                    var uvValues = primitive.GetVertexAccessor("TEXCOORD_0")?.AsVector2Array();
                    var positionArray = positions.ToArray();
                    var normals = normalValues is null ? Array.Empty<Vector3>() : normalValues.ToArray();
                    var uvs = uvValues is null ? Array.Empty<Vector2>() : uvValues.ToArray();
                    var indices = primitive.GetTriangleIndices().SelectMany(triangle => new[] { triangle.A, triangle.B, triangle.C }).ToArray();
                    if (indices.Length == 0) diagnostics.Add($"Node '{path}': primitive has no triangle faces and is omitted from the scene.");
                    foreach (var index in indices)
                        if (index < 0 || index >= positionArray.Length)
                            throw new InvalidDataException($"Mesh '{node.Mesh.Name ?? nodeName}' contains triangle index {index} outside its {positionArray.Length} POSITION values.");

                    if (HasUnsupportedAttributes(primitive)) diagnostics.Add($"Node '{path}': additional vertex attributes are not preserved; only POSITION, NORMAL and TEXCOORD_0 are exposed.");
                    if (uvValues is null && HasTextureUsingUv0(primitive.Material))
                        diagnostics.Add($"Node '{path}': material textures reference TEXCOORD_0, but the primitive has no TEXCOORD_0 attribute.");

                    var materialIndex = primitive.Material?.LogicalIndex ?? -1;
                    primitives.Add(new GlbPrimitiveData(positionArray, normals, uvs, indices, materialIndex));
                    if (semanticKind is CabinetSemanticGeometryKind.Visual or CabinetSemanticGeometryKind.Collider or CabinetSemanticGeometryKind.Trigger)
                    {
                        foreach (var position in positionArray)
                        {
                            var world = Vector3.Transform(position, node.WorldMatrix);
                            bounds = bounds.Include(world);
                        }
                    }
                }

                if (primitives.Count > 0)
                    meshes.Add(new GlbMeshInstance(node.LogicalIndex, nodeName, path, node.WorldMatrix,
                        semanticKind, CabinetSemanticGeometry.GetSemanticId(node.Name, node.Mesh.Name, semanticKind), primitives));
            }
        }

        foreach (var child in node.VisualChildren)
            VisitNode(child, path, meshes, diagnostics, ref bounds, cancellationToken);
    }

    private static bool HasUnsupportedAttributes(MeshPrimitive primitive)
    {
        // These channels need shader or vertex-layout support that the current previews do not have.
        return primitive.GetVertexAccessor("TANGENT") is not null
            || primitive.GetVertexAccessor("COLOR_0") is not null
            || primitive.GetVertexAccessor("TEXCOORD_1") is not null
            || primitive.GetVertexAccessor("JOINTS_0") is not null
            || primitive.GetVertexAccessor("WEIGHTS_0") is not null;
    }

    private static bool HasTextureUsingUv0(Material? material) => material is not null
        && material.Channels.Any(channel => channel.Texture is not null && channel.TextureCoordinate == 0);

    private static IEnumerable<GlbTextureData> Textures(GlbMaterialData material)
    {
        if (material.BaseColorTexture is { } baseColor) yield return baseColor;
        if (material.MetallicRoughnessTexture is { } metallicRoughness) yield return metallicRoughness;
        if (material.NormalTexture is { } normal) yield return normal;
        if (material.OcclusionTexture is { } occlusion) yield return occlusion;
        if (material.EmissiveTexture is { } emissive) yield return emissive;
    }

    private static GlbMaterialData CreateMaterial(Material material)
    {
        var baseColorChannel = material.FindChannel("BaseColor");
        var metallicRoughnessChannel = material.FindChannel("MetallicRoughness");
        var emissiveChannel = material.FindChannel("Emissive");
        var normalChannel = material.FindChannel("Normal");
        var occlusionChannel = material.FindChannel("Occlusion");
        var baseColor = baseColorChannel?.Parameter ?? Vector4.One;
        var metallicRoughness = metallicRoughnessChannel?.Parameter ?? new Vector4(1, 1, 1, 1);
        var emissive = emissiveChannel?.Parameter ?? Vector4.Zero;
        return new GlbMaterialData(
            material.LogicalIndex,
            string.IsNullOrWhiteSpace(material.Name) ? $"Material{material.LogicalIndex}" : material.Name,
            baseColor,
            new Vector3(emissive.X, emissive.Y, emissive.Z),
            metallicRoughness.X,
            metallicRoughness.Y,
            material.Alpha switch
            {
                AlphaMode.MASK => GlbAlphaMode.Mask,
                AlphaMode.BLEND => GlbAlphaMode.Blend,
                _ => GlbAlphaMode.Opaque
            },
            material.AlphaCutoff,
            material.DoubleSided,
            material.Unlit,
            CopyTexture(baseColorChannel),
            CopyTexture(metallicRoughnessChannel),
            CopyTexture(normalChannel),
            CopyTexture(occlusionChannel),
            CopyTexture(emissiveChannel));
    }

    private static GlbTextureData? CopyTexture(MaterialChannel? channel)
    {
        var texture = channel?.Texture;
        if (texture is null) return null;
        var image = texture.PrimaryImage;
        var content = image.Content;
        if (!content.IsValid || content.Content.IsEmpty) return null;
        var sampler = texture.Sampler;
        return new GlbTextureData(content.Content.ToArray(), content.MimeType ?? "application/octet-stream",
            texture.LogicalIndex, image.LogicalIndex, channel!.Value.TextureCoordinate,
            MapWrap(sampler?.WrapS), MapWrap(sampler?.WrapT), MapFilter(sampler?.MinFilter), MapFilter(sampler?.MagFilter));
    }

    private static GlbTextureWrapMode MapWrap(TextureWrapMode? value) => value switch
    {
        TextureWrapMode.CLAMP_TO_EDGE => GlbTextureWrapMode.ClampToEdge,
        TextureWrapMode.MIRRORED_REPEAT => GlbTextureWrapMode.MirroredRepeat,
        _ => GlbTextureWrapMode.Repeat
    };

    private static GlbTextureFilter MapFilter(object? value) => value?.ToString()?.ToUpperInvariant() switch
    {
        "NEAREST" => GlbTextureFilter.Nearest,
        "LINEAR" => GlbTextureFilter.Linear,
        "NEAREST_MIPMAP_NEAREST" => GlbTextureFilter.NearestMipmapNearest,
        "LINEAR_MIPMAP_NEAREST" => GlbTextureFilter.LinearMipmapNearest,
        "NEAREST_MIPMAP_LINEAR" => GlbTextureFilter.NearestMipmapLinear,
        "LINEAR_MIPMAP_LINEAR" => GlbTextureFilter.LinearMipmapLinear,
        _ => GlbTextureFilter.Default
    };

    private static void ValidateExternalResourcePaths(string modelPath)
    {
        using var stream = File.OpenRead(modelPath);
        using var reader = new BinaryReader(stream);
        if (reader.ReadUInt32() != 0x46546C67) throw new InvalidDataException("Not a GLB file.");
        if (reader.ReadUInt32() != 2) throw new InvalidDataException("Unsupported GLB version.");
        var declaredLength = reader.ReadUInt32();
        if (declaredLength != stream.Length) throw new InvalidDataException("GLB declared length does not match its file length.");

        JsonDocument? jsonDocument = null;
        while (stream.Position + 8 <= stream.Length)
        {
            var chunkLength = reader.ReadUInt32();
            var chunkType = reader.ReadUInt32();
            if (chunkLength > stream.Length - stream.Position) throw new InvalidDataException("GLB chunk extends beyond the file length.");
            if (chunkType == 0x4E4F534A && jsonDocument is null)
            {
                var bytes = reader.ReadBytes(checked((int)chunkLength));
                jsonDocument = JsonDocument.Parse(bytes);
            }
            else stream.Seek(chunkLength, SeekOrigin.Current);
        }

        using (jsonDocument)
        {
            if (jsonDocument is null) throw new InvalidDataException("GLB JSON chunk is missing.");
            var root = jsonDocument.RootElement;
            foreach (var collectionName in new[] { "buffers", "images" })
            {
                if (!root.TryGetProperty(collectionName, out var collection) || collection.ValueKind != JsonValueKind.Array) continue;
                foreach (var resource in collection.EnumerateArray())
                {
                    if (!resource.TryGetProperty("uri", out var uriElement) || uriElement.ValueKind != JsonValueKind.String) continue;
                    ValidateResourceUri(modelPath, collectionName, uriElement.GetString()!);
                }
            }
        }
    }

    private static void ValidateResourceUri(string modelPath, string resourceType, string uri)
    {
        if (uri.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) return;
        string decoded;
        try { decoded = Uri.UnescapeDataString(uri); }
        catch (UriFormatException ex) { throw new InvalidDataException($"GLB {resourceType} URI is invalid.", ex); }
        if (Uri.TryCreate(decoded, UriKind.Absolute, out _) || Path.IsPathRooted(decoded) || decoded.StartsWith('\\') || decoded.StartsWith('/'))
            throw new InvalidDataException($"GLB {resourceType} URI must be relative to the model folder: {uri}");

        var modelDirectory = Path.GetFullPath(Path.GetDirectoryName(modelPath)!);
        var resolved = Path.GetFullPath(Path.Combine(modelDirectory, decoded.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar)));
        var relative = Path.GetRelativePath(modelDirectory, resolved);
        if (relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) || Path.IsPathRooted(relative))
            throw new InvalidDataException($"GLB {resourceType} URI escapes the model folder: {uri}");
    }
}
