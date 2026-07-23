using System;
using System.Collections.Generic;
using UnityEngine;

namespace OasisPlayer.RuntimeBuild
{
    public static class RuntimeReelPositionConverter
    {
        public const float PositionsPerRevolution = 96f;
        public const float DefaultBaselineDegrees = 180f;
        public const float DefaultDirectionSign = -1f;

        public static Quaternion ToLocalRotation(float effectivePosition, bool isReversed, float bandOffset, float baselineDegrees, float directionSign)
        {
            var adjusted = effectivePosition;
            if (isReversed)
            {
                var wrappedInput = PositiveModulo(adjusted, PositionsPerRevolution);
                adjusted = wrappedInput == 0f ? 0f : PositionsPerRevolution - wrappedInput;
            }

            adjusted += bandOffset * PositionsPerRevolution;
            var wrapped = PositiveModulo(adjusted, PositionsPerRevolution);
            var normalized = wrapped / PositionsPerRevolution;
            var angle = baselineDegrees + directionSign * normalized * 360f;
            return Quaternion.AngleAxis(angle, Vector3.right);
        }

        public static float PositiveModulo(float value, float divisor)
        {
            if (divisor == 0f) return 0f;
            var result = value % divisor;
            return result < 0f ? result + divisor : result;
        }
    }

    public static class RuntimeReelUnits
    {
        public const float MillimetresPerMetre = 1000f;

        public static float MillimetresToMetres(float millimetres)
        {
            return millimetres / MillimetresPerMetre;
        }
    }

    public sealed class RuntimeReelMeshFactory
    {
        private readonly Dictionary<string, Mesh> _cache = new Dictionary<string, Mesh>(StringComparer.Ordinal);

        public Mesh Get(float width, float radius, int radialSegments)
        {
            width = Mathf.Max(0.001f, width);
            radius = Mathf.Max(0.001f, radius);
            radialSegments = Mathf.Max(3, radialSegments);
            var key = width.ToString("R") + ":" + radius.ToString("R") + ":" + radialSegments;
            Mesh mesh;
            if (_cache.TryGetValue(key, out mesh) && mesh != null) return mesh;
            mesh = Create(width, radius, radialSegments);
            _cache[key] = mesh;
            return mesh;
        }

        public static Mesh Create(float width, float radius, int radialSegments)
        {
            width = Mathf.Max(0.001f, width);
            radius = Mathf.Max(0.001f, radius);
            radialSegments = Mathf.Max(3, radialSegments);
            var vertices = new Vector3[(radialSegments + 1) * 2];
            var normals = new Vector3[vertices.Length];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[radialSegments * 6];
            var halfWidth = width * 0.5f;
            for (var i = 0; i <= radialSegments; i++)
            {
                var v = i / (float)radialSegments;
                var angle = Mathf.PI + v * Mathf.PI * 2f;
                var normal = new Vector3(0f, Mathf.Cos(angle), Mathf.Sin(angle));
                var left = i * 2;
                vertices[left] = new Vector3(-halfWidth, normal.y * radius, normal.z * radius);
                vertices[left + 1] = new Vector3(halfWidth, normal.y * radius, normal.z * radius);
                normals[left] = normal;
                normals[left + 1] = normal;
                uv[left] = new Vector2(0f, v);
                uv[left + 1] = new Vector2(1f, v);
            }

            var t = 0;
            for (var i = 0; i < radialSegments; i++)
            {
                var a = i * 2;
                var b = a + 1;
                var c = a + 2;
                var d = a + 3;
                triangles[t++] = a; triangles[t++] = c; triangles[t++] = b;
                triangles[t++] = b; triangles[t++] = c; triangles[t++] = d;
            }

            var mesh = new Mesh();
            mesh.name = "OasisRuntimeReelCylinder";
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }
    }

    public sealed class RuntimeReelRenderBinding : IDisposable
    {
        public RuntimeReelRenderBinding(GameObject gameObject, Material material)
        {
            GameObject = gameObject;
            Material = material;
        }

        public GameObject GameObject { get; private set; }
        public Material Material { get; private set; }

        public void Dispose()
        {
            if (GameObject != null)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(GameObject);
                else UnityEngine.Object.DestroyImmediate(GameObject);
                GameObject = null;
            }
            if (Material != null)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(Material);
                else UnityEngine.Object.DestroyImmediate(Material);
                Material = null;
            }
        }
    }

    public sealed class RuntimeReelRenderer
    {
        private readonly RuntimeReelMeshFactory _meshFactory = new RuntimeReelMeshFactory();

        public void RenderReels(RuntimeMachine machine)
        {
            if (machine == null) throw new ArgumentNullException(nameof(machine));
            foreach (var face in machine.Faces)
            {
                var reels = face.Manifest != null && face.Manifest.reels != null ? face.Manifest.reels : Array.Empty<FaceRuntimeReelManifestEntry>();
                if (reels.Length == 0) continue;
                if (!RuntimeFacePlacement.TryResolve(face, out var surface, out var surfaceWarning))
                {
                    machine.AddWarning(surfaceWarning);
                    continue;
                }

                foreach (var reel in reels)
                {
                    if (reel == null) continue;
                    if (!Validate(face, reel, out var warning)) { machine.AddWarning(warning); continue; }
                    if (!RuntimeFacePlacement.ValidateComponent(face, reel, out warning)) { machine.AddWarning(warning); continue; }
                    if (reel.BandTexture == null || reel.BandTexture.Texture == null) { machine.AddWarning($"Runtime Face '{RuntimeFacePlacement.FaceId(face)}' reel '{reel.objectId}' has no loaded reel-band texture."); continue; }

                    var surfacePoint = surface.FaceRectCenterToWorld(reel, face.Manifest);
                    var physicalWidthMetres = RuntimeReelUnits.MillimetresToMetres(reel.physicalWidth);
                    var physicalRadiusMetres = RuntimeReelUnits.MillimetresToMetres(reel.physicalRadius);
                    var position = surfacePoint - (surface.VisibleNormal * (physicalRadiusMetres + RuntimeFacePlacement.DefaultSurfaceClearanceMetres));
                    if (float.IsNaN(position.x) || float.IsInfinity(position.x) || float.IsNaN(position.y) || float.IsInfinity(position.y) || float.IsNaN(position.z) || float.IsInfinity(position.z))
                    {
                        machine.AddWarning($"Runtime Face '{RuntimeFacePlacement.FaceId(face)}' reel '{reel.objectId}' produced a non-finite placement position.");
                        continue;
                    }

                    var go = new GameObject("OasisRuntimeReel_" + reel.objectId);
                    go.transform.position = position;
                    var baseRotation = surface.AlignLocalReelAxesToSurface();
                    var positionRotation = RuntimeReelPositionConverter.ToLocalRotation(0f, reel.isReversed, reel.bandOffset, RuntimeReelPositionConverter.DefaultBaselineDegrees, RuntimeReelPositionConverter.DefaultDirectionSign);
                    go.transform.rotation = baseRotation * positionRotation;
                    go.transform.localScale = Vector3.one;
                    var filter = go.AddComponent<MeshFilter>();
                    filter.sharedMesh = _meshFactory.Get(physicalWidthMetres, physicalRadiusMetres, 96);
                    var renderer = go.AddComponent<MeshRenderer>();
                    var material = CreateMaterial(reel);
                    renderer.sharedMaterial = material;
                    face.AddReelRenderBinding(new RuntimeReelRenderBinding(go, material));
                }
            }
        }

        private static Material CreateMaterial(FaceRuntimeReelManifestEntry reel)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            var material = new Material(shader);
            material.name = "RuntimeReel_" + reel.objectId;
            material.mainTexture = reel.BandTexture.Texture;
            return material;
        }

        private static bool Validate(RuntimeFace face, FaceRuntimeReelManifestEntry reel, out string warning)
        {
            warning = string.Empty;
            if (string.IsNullOrWhiteSpace(reel.objectId)) warning = $"Runtime Face '{RuntimeFacePlacement.FaceId(face)}' has a reel entry with an empty objectId.";
            else if (string.IsNullOrWhiteSpace(reel.machineReference)) warning = $"Runtime Face '{RuntimeFacePlacement.FaceId(face)}' reel '{reel.objectId}' has an empty machineReference.";
            else if (string.IsNullOrWhiteSpace(reel.reelBand)) warning = $"Runtime Face '{RuntimeFacePlacement.FaceId(face)}' reel '{reel.objectId}' has an empty reelBand path.";
            else if (reel.stops <= 0) warning = $"Runtime Face '{RuntimeFacePlacement.FaceId(face)}' reel '{reel.objectId}' has invalid stop count '{reel.stops}'.";
            else if (reel.physicalWidth <= 0f || reel.physicalRadius <= 0f) warning = $"Runtime Face '{RuntimeFacePlacement.FaceId(face)}' reel '{reel.objectId}' has invalid physical dimensions.";
            return string.IsNullOrEmpty(warning);
        }

    }
}
