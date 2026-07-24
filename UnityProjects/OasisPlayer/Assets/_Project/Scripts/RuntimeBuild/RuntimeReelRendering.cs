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


    public static class RuntimeReelLampGeometry
    {
        public const float DefaultRadius = 0.15f;

        public static Vector4 DeriveVerticalCenters(int stops)
        {
            // Reel lamps are fixed in projected aperture space, not rotating band space.
            // For N stops, adjacent visible symbols are at +/- one stop angle around the reel;
            // projecting those positions onto the visible diameter gives 0.5 +/- 0.5*sin(2*pi/N).
            var safeStops = Mathf.Max(1, stops);
            var offset = 0.5f * Mathf.Sin((Mathf.PI * 2f) / safeStops);
            return new Vector4(0.5f + offset, 0.5f, 0.5f - offset, 0f);
        }

        public static float EvaluateField(Vector2 apertureUv, float verticalCenter, float radius, float physicalWidth, float physicalDiameter)
        {
            var delta = apertureUv - new Vector2(0.5f, verticalCenter);
            delta.x *= physicalWidth / Mathf.Max(physicalDiameter, 0.0001f);
            var d = delta.magnitude;
            return 1f - Mathf.SmoothStep(radius * 0.35f, Mathf.Max(radius, 0.0001f), d);
        }
    }

    public sealed class RuntimeReelRenderBinding : IDisposable
    {
        public RuntimeReelRenderBinding(GameObject gameObject, Material material, MeshRenderer renderer, FaceRuntimeReelManifestEntry reel)
        {
            GameObject = gameObject;
            Material = material;
            Renderer = renderer;
            PropertyBlock = new MaterialPropertyBlock();
            Configure(reel);
        }

        public GameObject GameObject { get; private set; }
        public Material Material { get; private set; }
        public MeshRenderer Renderer { get; private set; }
        public MaterialPropertyBlock PropertyBlock { get; private set; }
        private readonly int[] _lampIds = new int[3];
        private readonly float[] _brightness = new float[3];
        private int _lampStateVersion = -1;

        private void Configure(FaceRuntimeReelManifestEntry reel)
        {
            var lampsEnabled = reel == null || reel.reelLampsEnabled;
            var lamps = reel != null && reel.reelLamps != null ? reel.reelLamps : Array.Empty<FaceRuntimeReelLampManifestEntry>();
            for (var i = 0; i < _lampIds.Length; i++) _lampIds[i] = -1;
            var verticalCenters = RuntimeReelLampGeometry.DeriveVerticalCenters(reel != null ? reel.stops : 12);
            var radii = new Vector4(RuntimeReelLampGeometry.DefaultRadius, RuntimeReelLampGeometry.DefaultRadius, RuntimeReelLampGeometry.DefaultRadius, 0f);
            var intensities = new Vector4(1f, 1f, 1f, 0f);
            for (var i = 0; i < lamps.Length && i < 3; i++)
            {
                var lamp = lamps[i];
                // RuntimeLampState is one-based (1..255), so both the -1 unassigned sentinel and manifest value 0 stay off.
                _lampIds[i] = lampsEnabled && lamp != null && lamp.lampId > 0 ? lamp.lampId : -1;
                var radius = lamp != null && lamp.radius > 0f ? lamp.radius : RuntimeReelLampGeometry.DefaultRadius;
                var intensity = lamp != null ? lamp.intensity : 1f;
                if (i == 0) { radii.x = radius; intensities.x = intensity; }
                else if (i == 1) { radii.y = radius; intensities.y = intensity; }
                else { radii.z = radius; intensities.z = intensity; }
            }
            PropertyBlock.SetVector(RuntimeFaceShaderProperties.ReelLampVerticalCenters, verticalCenters);
            PropertyBlock.SetVector(RuntimeFaceShaderProperties.ReelLampRadii, radii);
            PropertyBlock.SetVector(RuntimeFaceShaderProperties.ReelLampIntensities, intensities);
            PropertyBlock.SetFloat(RuntimeFaceShaderProperties.ReelTransmissionMaskEnabled, reel != null && reel.TransmissionMaskTexture != null ? 1f : 0f);
            if (reel != null && reel.TransmissionMaskTexture != null) PropertyBlock.SetTexture(RuntimeFaceShaderProperties.ReelTransmissionMaskTexture, reel.TransmissionMaskTexture.Texture);
            PropertyBlock.SetVector(RuntimeFaceShaderProperties.ReelLampBrightness, Vector4.zero);
            if (Renderer != null) Renderer.SetPropertyBlock(PropertyBlock);
        }

        public void ConfigureAperture(Vector3 center, Vector3 right, Vector3 up, float physicalWidth, float physicalDiameter)
        {
            PropertyBlock.SetVector(RuntimeFaceShaderProperties.ReelApertureCenterWS, new Vector4(center.x, center.y, center.z, 0f));
            PropertyBlock.SetVector(RuntimeFaceShaderProperties.ReelApertureRightWS, new Vector4(right.x, right.y, right.z, 0f));
            PropertyBlock.SetVector(RuntimeFaceShaderProperties.ReelApertureUpWS, new Vector4(up.x, up.y, up.z, 0f));
            PropertyBlock.SetVector(RuntimeFaceShaderProperties.ReelApertureSize, new Vector4(physicalWidth, physicalDiameter, 0f, 0f));
            if (Renderer != null) Renderer.SetPropertyBlock(PropertyBlock);
        }

        public bool ApplyLampState(RuntimeLampState lampState)
        {
            if (lampState == null || lampState.Version == _lampStateVersion) return false;
            _lampStateVersion = lampState.Version;
            var changed = false;
            for (var i = 0; i < _brightness.Length; i++)
            {
                var next = _lampIds[i] > 0 ? lampState.GetBrightness(_lampIds[i]) : 0f;
                if (Mathf.Abs(_brightness[i] - next) > 0.0001f) { _brightness[i] = next; changed = true; }
            }
            if (!changed) return false;
            PropertyBlock.SetVector(RuntimeFaceShaderProperties.ReelLampBrightness, new Vector4(_brightness[0], _brightness[1], _brightness[2], 0f));
            if (Renderer != null) Renderer.SetPropertyBlock(PropertyBlock);
            return true;
        }

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
                    var material = CreateMaterial(machine, reel);
                    if (material == null)
                    {
                        if (Application.isPlaying) UnityEngine.Object.Destroy(go);
                        else UnityEngine.Object.DestroyImmediate(go);
                        continue;
                    }
                    renderer.sharedMaterial = material;
                    var binding = new RuntimeReelRenderBinding(go, material, renderer, reel);
                    binding.ConfigureAperture(surfacePoint, surface.HorizontalTangent, surface.VerticalTangent, physicalWidthMetres, physicalRadiusMetres * 2f);
                    binding.ApplyLampState(machine.LampState);
                    machine.AddWarning($"Reel lamp binding: reel={DisplayReelName(reel)}, enabled={reel.reelLampsEnabled.ToString().ToLowerInvariant()}, ids=[{FormatReelLampIds(reel)}], assigned={CountAssignedReelLamps(reel)}");
                    face.AddReelRenderBinding(binding);
                }
            }
        }

        private static string DisplayReelName(FaceRuntimeReelManifestEntry reel)
        {
            return reel == null || string.IsNullOrWhiteSpace(reel.name) ? reel != null ? reel.objectId : string.Empty : reel.name;
        }

        private static string FormatReelLampIds(FaceRuntimeReelManifestEntry reel)
        {
            var lamps = reel != null && reel.reelLamps != null ? reel.reelLamps : Array.Empty<FaceRuntimeReelLampManifestEntry>();
            var ids = new string[3];
            for (var i = 0; i < ids.Length; i++) ids[i] = i < lamps.Length ? lamps[i].lampId.ToString() : "-1";
            return string.Join(",", ids);
        }

        private static int CountAssignedReelLamps(FaceRuntimeReelManifestEntry reel)
        {
            var lamps = reel != null && reel.reelLamps != null ? reel.reelLamps : Array.Empty<FaceRuntimeReelLampManifestEntry>();
            var count = 0;
            for (var i = 0; i < lamps.Length && i < 3; i++) if (lamps[i].lampId > 0) count++;
            return count;
        }

        public static Material CreateMaterial(RuntimeMachine machine, FaceRuntimeReelManifestEntry reel)
        {
            var shader = Shader.Find("Oasis/ReelLamp");
            if (shader == null || !shader.isSupported)
            {
                if (machine != null) machine.AddWarning(shader == null ? "Runtime reel shader 'Oasis/ReelLamp' could not be found; using visible diagnostic fallback." : "Runtime reel shader 'Oasis/ReelLamp' is not supported by the active render pipeline; using visible diagnostic fallback.");
                shader = Shader.Find("Universal Render Pipeline/Lit");
            }
            if (shader == null)
            {
                if (machine != null) machine.AddWarning("No supported shader was available for runtime reel rendering; reel was skipped.");
                return null;
            }
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
            else if (!string.IsNullOrWhiteSpace(reel.transmissionMask) && (reel.TransmissionMaskTexture == null || reel.TransmissionMaskTexture.Texture == null)) warning = $"Runtime Face '{RuntimeFacePlacement.FaceId(face)}' reel '{reel.objectId}' declares a transmission mask but it was not loaded.";
            return string.IsNullOrEmpty(warning);
        }

    }
}
