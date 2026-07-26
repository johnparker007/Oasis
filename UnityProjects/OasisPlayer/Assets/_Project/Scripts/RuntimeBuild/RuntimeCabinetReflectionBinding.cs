using System;
using UnityEngine;

namespace OasisPlayer.RuntimeBuild
{
    /// <summary>Owns one converted analytic material and its per-slot reflection data.</summary>
    public sealed class RuntimeCabinetReflectionBinding : IDisposable
    {
        private readonly Renderer _renderer;
        private readonly int _materialIndex;
        private readonly MaterialPropertyBlock _properties = new MaterialPropertyBlock();
        private readonly MaterialPropertyBlock _originalProperties = new MaterialPropertyBlock();
        private readonly Material _originalMaterial;
        private readonly Material _ownedMaterial;
        private bool _disposed;

        private RuntimeCabinetReflectionBinding(Renderer renderer, int materialIndex, Material originalMaterial, Material ownedMaterial) { _renderer = renderer; _materialIndex = materialIndex; _originalMaterial = originalMaterial; _ownedMaterial = ownedMaterial; }
        public Material RuntimeMaterial { get { return _ownedMaterial; } }

        public static bool TryCreate(RuntimeMachine machine, string faceId, Renderer renderer, int materialIndex, RuntimeFaceReflectionPlane plane, out RuntimeCabinetReflectionBinding binding, out string warning, RuntimeCabinetReflectionSettings settings = null, Texture visibilityMask = null)
        {
            binding = null; warning = string.Empty;
            if (machine == null || renderer == null) { warning = "Cabinet reflection requires a runtime machine and target renderer."; return false; }
            if (!plane.IsValid) { warning = "Cabinet reflection Face plane is invalid."; return false; }
            if (string.IsNullOrWhiteSpace(faceId)) { warning = "Cabinet reflection source Face ID is empty."; return false; }
            RuntimeFace face = null; var matches = 0;
            foreach (var candidate in machine.Faces)
                if (candidate.Reference != null && string.Equals(candidate.Reference.faceId, faceId.Trim(), StringComparison.Ordinal)) { face = candidate; matches++; }
            if (matches != 1) { warning = matches == 0 ? $"Cabinet reflection source Face '{faceId}' was not found." : $"Cabinet reflection source Face '{faceId}' is ambiguous ({matches} matches)."; return false; }
            if (face.Artwork?.Texture == null || face.Mask?.Texture == null || face.LampIds0?.Texture == null || face.LampWeights0?.Texture == null) { warning = $"Cabinet reflection source Face '{faceId}' is missing required artwork or lamp lookup textures."; return false; }
            var materials = renderer.sharedMaterials;
            if (materialIndex < 0 || materialIndex >= materials.Length || materials[materialIndex] == null) { warning = $"Cabinet reflection material slot {materialIndex} is invalid."; return false; }
            var shader = Shader.Find(RuntimeCabinetReflectionShaderProperties.ShaderName);
            if (shader == null) { warning = $"Cabinet reflection shader '{RuntimeCabinetReflectionShaderProperties.ShaderName}' was not found."; return false; }
            var originalMaterial = materials[materialIndex];
            Material ownedMaterial = null;
            try
            {
                ownedMaterial = new Material(shader) { name = originalMaterial.name + " (Oasis Analytic Reflection)" };
                CopyCabinetProperties(originalMaterial, ownedMaterial);
            }
            catch (Exception ex) { DestroyOwned(ownedMaterial); warning = "Cabinet reflection material conversion failed: " + ex.Message; return false; }

            binding = new RuntimeCabinetReflectionBinding(renderer, materialIndex, originalMaterial, ownedMaterial);
            renderer.GetPropertyBlock(binding._originalProperties, materialIndex);
            renderer.GetPropertyBlock(binding._properties, materialIndex);
            var replacement = (Material[])materials.Clone(); replacement[materialIndex] = ownedMaterial; renderer.sharedMaterials = replacement;
            binding._properties.SetTexture(RuntimeFaceShaderProperties.ArtworkTexture, face.Artwork.Texture);
            binding._properties.SetTexture(RuntimeFaceShaderProperties.MaskTexture, face.Mask.Texture);
            binding._properties.SetTexture(RuntimeFaceShaderProperties.LampIds0Texture, face.LampIds0.Texture);
            binding._properties.SetTexture(RuntimeFaceShaderProperties.LampWeights0Texture, face.LampWeights0.Texture);
            binding._properties.SetTexture(RuntimeFaceShaderProperties.LampStateTexture, machine.LampStateTexture.Texture);
            if (visibilityMask != null) binding._properties.SetTexture(RuntimeCabinetReflectionShaderProperties.VisibilityMask, visibilityMask);
            if (face.RenderBinding?.RuntimeMaterial != null)
            {
                binding._properties.SetFloat(RuntimeFaceShaderProperties.LampExposureStops, face.RenderBinding.RuntimeMaterial.GetFloat(RuntimeFaceShaderProperties.LampExposureStops));
                binding._properties.SetFloat(RuntimeFaceShaderProperties.MaskStrength, face.RenderBinding.RuntimeMaterial.GetFloat(RuntimeFaceShaderProperties.MaskStrength));
            }
            var orientation = RuntimeFaceTextureOrientation.FromReference(face.Reference);
            binding._properties.SetFloat(RuntimeFaceShaderProperties.FaceRotationQuarterTurns, orientation.UnityUvQuarterTurns);
            binding._properties.SetFloat(RuntimeFaceShaderProperties.FaceFlipHorizontal, orientation.FlipHorizontal ? 1f : 0f);
            if (settings != null)
            {
                binding._properties.SetFloat(RuntimeCabinetReflectionShaderProperties.Enabled, settings.enabled ? 1f : 0f);
                binding._properties.SetFloat(RuntimeCabinetReflectionShaderProperties.Strength, Mathf.Clamp(settings.strength, 0f, 2f));
                binding._properties.SetFloat(RuntimeCabinetReflectionShaderProperties.UnlitArtworkStrength, Mathf.Clamp(settings.unlitArtworkStrength, 0f, 2f));
                binding._properties.SetFloat(RuntimeCabinetReflectionShaderProperties.LitLampStrength, Mathf.Clamp(settings.litLampStrength, 0f, 4f));
                binding._properties.SetFloat(RuntimeCabinetReflectionShaderProperties.FresnelPower, Mathf.Clamp(settings.fresnelPower, .1f, 10f));
                binding._properties.SetFloat(RuntimeCabinetReflectionShaderProperties.FresnelStrength, Mathf.Clamp(settings.fresnelStrength, 0f, 2f));
                binding._properties.SetFloat(RuntimeCabinetReflectionShaderProperties.Roughness, Mathf.Clamp01(settings.roughness));
                binding._properties.SetFloat(RuntimeCabinetReflectionShaderProperties.Distortion, Mathf.Clamp(settings.distortion, 0f, .05f));
                binding._properties.SetFloat(RuntimeCabinetReflectionShaderProperties.EdgeFade, Mathf.Clamp(settings.edgeFade, 0f, .25f));
            }
            binding.SetPlane(plane);
            if (settings == null) binding._properties.SetFloat(RuntimeCabinetReflectionShaderProperties.Enabled, 1f);
            renderer.SetPropertyBlock(binding._properties, materialIndex);
            return true;
        }

        private static void CopyCabinetProperties(Material source, Material destination)
        {
            if (source.HasProperty("_BaseMap")) CopyTexture(source, destination, "_BaseMap", "_BaseMap"); else CopyTexture(source, destination, "_MainTex", "_BaseMap");
            if (source.HasProperty("_BumpMap")) CopyTexture(source, destination, "_BumpMap", "_BumpMap"); else CopyTexture(source, destination, "_NormalMap", "_BumpMap");
            CopyColor(source, destination, "_BaseColor", "_BaseColor");
            if (!source.HasProperty("_BaseColor")) CopyColor(source, destination, "_Color", "_BaseColor");
            CopyFloat(source, destination, "_BumpScale", "_BumpScale");
            CopyFloat(source, destination, "_Metallic", "_Metallic");
            if (source.HasProperty("_Smoothness")) CopyFloat(source, destination, "_Smoothness", "_Smoothness"); else CopyFloat(source, destination, "_Glossiness", "_Smoothness");
            CopyFloat(source, destination, "_Cutoff", "_Cutoff"); CopyFloat(source, destination, "_Cull", "_Cull");
        }

        private static void CopyTexture(Material source, Material destination, string sourceName, string destinationName)
        {
            if (!source.HasProperty(sourceName) || !destination.HasProperty(destinationName)) return;
            destination.SetTexture(destinationName, source.GetTexture(sourceName)); destination.SetTextureScale(destinationName, source.GetTextureScale(sourceName)); destination.SetTextureOffset(destinationName, source.GetTextureOffset(sourceName));
        }
        private static void CopyColor(Material source, Material destination, string sourceName, string destinationName) { if (source.HasProperty(sourceName) && destination.HasProperty(destinationName)) destination.SetColor(destinationName, source.GetColor(sourceName)); }
        private static void CopyFloat(Material source, Material destination, string sourceName, string destinationName) { if (source.HasProperty(sourceName) && destination.HasProperty(destinationName)) destination.SetFloat(destinationName, source.GetFloat(sourceName)); }
        private static void DestroyOwned(UnityEngine.Object value) { if (value == null) return; if (Application.isPlaying) UnityEngine.Object.Destroy(value); else UnityEngine.Object.DestroyImmediate(value); }

        public void SetPlane(RuntimeFaceReflectionPlane plane)
        {
            if (_disposed) return;
            _properties.SetVector(RuntimeCabinetReflectionShaderProperties.FaceOrigin, plane.OriginWS);
            _properties.SetVector(RuntimeCabinetReflectionShaderProperties.FaceRight, plane.RightWS);
            _properties.SetVector(RuntimeCabinetReflectionShaderProperties.FaceUp, plane.UpWS);
            _properties.SetVector(RuntimeCabinetReflectionShaderProperties.FaceNormal, plane.NormalWS);
            _properties.SetVector(RuntimeCabinetReflectionShaderProperties.FaceSize, new Vector4(plane.Width, plane.Height, 0f, 0f));
            if (_renderer != null) _renderer.SetPropertyBlock(_properties, _materialIndex);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_renderer == null) return;
            _renderer.SetPropertyBlock(_originalProperties, _materialIndex);
            var materials = _renderer.sharedMaterials;
            if (_materialIndex >= 0 && _materialIndex < materials.Length && materials[_materialIndex] == _ownedMaterial) { var restored = (Material[])materials.Clone(); restored[_materialIndex] = _originalMaterial; _renderer.sharedMaterials = restored; }
            DestroyOwned(_ownedMaterial);
        }
    }
}
